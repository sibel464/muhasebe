using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Guvenlik;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Modeller.Ortak;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Doviz;
using Uygulama.Dto.Muhasebe;

namespace Altyapi.Servisler;

public class RevaluasyonServisi : IRevaluasyonServisi
{
    private readonly IAktifFirmaBaglami _aktifFirma;
    private readonly MuhasebeBaglami _baglam;
    private readonly IKurServisi _kurServisi;
    private readonly IYevmiyeServisi _yevmiyeServisi;
    private readonly IMuhasebeBaglantiServisi _baglantiServisi;
    private readonly IKullaniciServisi _kullaniciServisi;

    public RevaluasyonServisi(
        MuhasebeBaglami baglam,
        IKurServisi kurServisi,
        IYevmiyeServisi yevmiyeServisi,
        IMuhasebeBaglantiServisi baglantiServisi,
        IKullaniciServisi kullaniciServisi, IAktifFirmaBaglami aktifFirma)
    {
        _aktifFirma = aktifFirma;
        _baglam = baglam;
        _kurServisi = kurServisi;
        _yevmiyeServisi = yevmiyeServisi;
        _baglantiServisi = baglantiServisi;
        _kullaniciServisi = kullaniciServisi;
    }

    private Guid FirmaDogrula(Guid sirketId)
    {
        if (_kullaniciServisi.AktifKullanici == null || sirketId == Guid.Empty || sirketId != _aktifFirma.SirketId)
            throw new YetkisizErisimException("Revalüasyon firma/oturum doğrulaması");
        return sirketId;
    }

    // Kaynağın (cari/banka/kasa) bu değerleme tarihine kadar onaylanmış revalüasyonlarla deftere eklenen kur farkı.
    // Defter değeri = hareketlerin TL karşılığı + bu toplam; böylece ikinci değerlemede yalnız yeni fark kaydedilir.
    private async Task<decimal> OncekiKurFarklariAsync(Guid sirketId, string kaynakTuru, int kaynakId, string currency, DateTime date) =>
        await _baglam.RevaluasyonKalemleri.AsNoTracking()
            .Where(rk => rk.KaynakTuru == kaynakTuru && rk.KaynakId == kaynakId && !rk.Silindi && rk.RevaluasyonKaydi != null
                && rk.RevaluasyonKaydi.SirketId == sirketId && !rk.RevaluasyonKaydi.Silindi
                && rk.RevaluasyonKaydi.Durum == RevaluasyonDurumu.Onaylandi && rk.RevaluasyonKaydi.DegerlemeTarihi <= date
                && rk.RevaluasyonKaydi.ParaBirimiKodu == currency)
            .SumAsync(rk => (decimal?)rk.KurFarki) ?? 0m;

    public async Task<RevaluasyonOnizleDto> RevaluasyonOnizleAsync(Guid sirketId, string paraBirimiKodu, DateTime? degerlemeTarihi = null)
    {
        var targetSirketId = FirmaDogrula(sirketId);
        _kullaniciServisi.YetkiKontrolEt(y => y.MuhasebeFisGoruntule, "Dövizli bakiye ve revalüasyon görüntüleme");
        var date = (degerlemeTarihi ?? DateTime.UtcNow).Date;
        var currency = string.IsNullOrWhiteSpace(paraBirimiKodu) ? "USD" : paraBirimiKodu.ToUpperInvariant();

        if (currency == "TRY")
            throw new InvalidOperationException("TRY (Yerel Para Birimi) revalüasyona tabi değildir.");

        var guncelKurDto = await _kurServisi.GetirGecerliKurAsync(currency, date);
        if (guncelKurDto == null)
            throw new InvalidOperationException($"[{currency}] para birimi için {date:dd.MM.yyyy} tarihli kur bulunamadı.");

        decimal yeniKur = guncelKurDto.DovizAlis > 0 ? guncelKurDto.DovizAlis : guncelKurDto.DovizSatis;

        var result = new RevaluasyonOnizleDto
        {
            SirketId = targetSirketId,
            DegerlemeTarihi = date,
            ParaBirimiKodu = currency,
            YeniDegerlemeKuru = yeniKur
        };

        // 1. Scan Open FX Cari Accounts
        var dovizliCariler = await _baglam.CariHareketler
            .AsNoTracking()
            .Where(h => h.CariKart != null && h.CariKart.SirketId == targetSirketId && !h.Silindi && h.ParaBirimi != null && h.ParaBirimi.Kod == currency && h.Tarih < date.AddDays(1))
            .GroupBy(h => new { h.CariKartId, h.CariKart!.Kod, h.CariKart.Unvan })
            .Select(g => new
            {
                g.Key.CariKartId,
                g.Key.Kod,
                g.Key.Unvan,
                DovizBorc = g.Sum(x => x.DovizBorc),
                DovizAlacak = g.Sum(x => x.DovizAlacak),
                YerelBorc = g.Sum(x => x.Borc),
                YerelAlacak = g.Sum(x => x.Alacak)
            })
            .ToListAsync();

        foreach (var c in dovizliCariler)
        {
            decimal dovizBakiye = c.DovizBorc - c.DovizAlacak;
            if (Math.Abs(dovizBakiye) > 0.001m)
            {
                // Revalüasyon farkı cari harekete yazılmaz (yalnız yevmiyeye); önceki farklar eklenmezse
                // her yeni değerleme eski farkı yeniden kaydederdi.
                decimal eskiDefterDegeri = c.YerelBorc - c.YerelAlacak
                    + await OncekiKurFarklariAsync(targetSirketId, "Cari", c.CariKartId, currency, date);
                decimal oncekiKur = dovizBakiye != 0 ? Math.Abs(eskiDefterDegeri / dovizBakiye) : yeniKur;
                decimal yeniYerelDeger = dovizBakiye * yeniKur;
                decimal kurFarki = yeniYerelDeger - eskiDefterDegeri;

                if (Math.Abs(kurFarki) > 0.01m)
                {
                    result.Kalemler.Add(new RevaluasyonKalemiDto
                    {
                        KaynakTuru = "Cari",
                        KaynakId = c.CariKartId,
                        KaynakKodu = c.Kod,
                        KaynakAciklama = c.Unvan,
                        HesapKodu = dovizBakiye > 0 ? "120" : "320",
                        DovizBakiye = dovizBakiye,
                        EskiDefterDegeri = eskiDefterDegeri,
                        OncekiDegerlemeKuru = Math.Round(oncekiKur, 4),
                        YeniDegerlemeKuru = yeniKur,
                        YeniYerelDeger = yeniYerelDeger,
                        KurFarki = kurFarki
                    });
                }
            }
        }

        // 2-3. Dövizli banka hesapları ve kasalar: döviz bakiyesi hareketlerin döviz tutarlarından, defter değeri TL
        // tutarlarından (+ önceki onaylı değerleme farkları; fark yalnız yevmiyeye yazılır). Döviz bakiyesi sıfır olsa da
        // defter değeri kalmışsa (döviz farklı kurdan girip çıktıysa) o fark da kapatılır. Kart üzerindeki Bakiye alanı tutulmaz.
        var bankalar = await _baglam.BankaHesaplari.AsNoTracking()
            .Where(b => b.SirketId == targetSirketId && b.ParaBirimi.ToUpper() == currency && b.AktifMi)
            .ToListAsync();
        var bankaIdleri = bankalar.Select(b => b.Id).ToList();
        var bankaOzet = await _baglam.BankaHareketleri.AsNoTracking()
            .Where(h => h.BankaHesabiId != null && bankaIdleri.Contains(h.BankaHesabiId.Value) && h.IslemDurumu == FinansIslemDurumu.Tamamlandi && h.Tarih < date.AddDays(1))
            .GroupBy(h => h.BankaHesabiId!.Value)
            .Select(g => new { Id = g.Key, Doviz = g.Sum(h => h.Tur == HareketTuru.Gelir ? h.DovizTutar : -h.DovizTutar), Tl = g.Sum(h => h.Tur == HareketTuru.Gelir ? h.Tutar : -h.Tutar) })
            .ToDictionaryAsync(x => x.Id);
        foreach (var b in bankalar)
        {
            bankaOzet.TryGetValue(b.Id, out var o);
            var hesapKodu = string.IsNullOrWhiteSpace(b.MuhasebeKodu) ? await _baglantiServisi.GetirHesapKoduForKategoriAsync("BANKA", targetSirketId) : b.MuhasebeKodu;
            await HesapKalemiEkleAsync(result, "Banka", b.Id, b.HesapNo, b.Ad, hesapKodu, o?.Doviz ?? 0m, o?.Tl ?? 0m, targetSirketId, currency, date, yeniKur);
        }

        var kasalar = await _baglam.Kasalar.AsNoTracking()
            .Where(k => k.SirketId == targetSirketId && k.ParaBirimi.ToUpper() == currency && k.AktifMi)
            .ToListAsync();
        var kasaIdleri = kasalar.Select(k => k.Id).ToList();
        var kasaOzet = await _baglam.KasaHareketleri.AsNoTracking()
            .Where(h => h.KasaId != null && kasaIdleri.Contains(h.KasaId.Value) && h.IslemDurumu == FinansIslemDurumu.Tamamlandi && h.Tarih < date.AddDays(1))
            .GroupBy(h => h.KasaId!.Value)
            .Select(g => new { Id = g.Key, Doviz = g.Sum(h => h.Tur == HareketTuru.Gelir ? h.DovizTutar : -h.DovizTutar), Tl = g.Sum(h => h.Tur == HareketTuru.Gelir ? h.Tutar : -h.Tutar) })
            .ToDictionaryAsync(x => x.Id);
        foreach (var k in kasalar)
        {
            kasaOzet.TryGetValue(k.Id, out var o);
            var hesapKodu = string.IsNullOrWhiteSpace(k.MuhasebeKodu) ? await _baglantiServisi.GetirHesapKoduForKategoriAsync("KASA", targetSirketId) : k.MuhasebeKodu;
            await HesapKalemiEkleAsync(result, "Kasa", k.Id, k.Kod, k.Ad, hesapKodu, o?.Doviz ?? 0m, o?.Tl ?? 0m, targetSirketId, currency, date, yeniKur);
        }

        result.ToplamEskiYerel = result.Kalemler.Sum(k => k.EskiDefterDegeri);
        result.ToplamYeniYerel = result.Kalemler.Sum(k => k.YeniYerelDeger);
        result.ToplamKurFarki = result.Kalemler.Sum(k => k.KurFarki);

        return result;
    }

    private async Task HesapKalemiEkleAsync(RevaluasyonOnizleDto result, string kaynakTuru, int kaynakId, string kaynakKodu, string kaynakAciklama,
        string hesapKodu, decimal dovizBakiye, decimal tlDegeri, Guid sirketId, string currency, DateTime date, decimal yeniKur)
    {
        var eskiDefterDegeri = tlDegeri + await OncekiKurFarklariAsync(sirketId, kaynakTuru, kaynakId, currency, date);
        var yeniYerelDeger = Math.Round(dovizBakiye * yeniKur, 2, MidpointRounding.AwayFromZero);
        var kurFarki = yeniYerelDeger - eskiDefterDegeri;
        if (Math.Abs(kurFarki) < 0.01m)
            return;

        var oncekiKalem = await _baglam.RevaluasyonKalemleri.AsNoTracking()
            .Where(rk => rk.KaynakTuru == kaynakTuru && rk.KaynakId == kaynakId && rk.RevaluasyonKaydi != null && rk.RevaluasyonKaydi.Durum == RevaluasyonDurumu.Onaylandi
                && rk.RevaluasyonKaydi.DegerlemeTarihi <= date && rk.RevaluasyonKaydi.ParaBirimiKodu == currency)
            .OrderByDescending(rk => rk.Id).Select(rk => (decimal?)rk.YeniDegerlemeKuru).FirstOrDefaultAsync();
        var oncekiKur = oncekiKalem is > 0 ? oncekiKalem.Value : dovizBakiye != 0 ? Math.Abs(eskiDefterDegeri / dovizBakiye) : yeniKur;

        result.Kalemler.Add(new RevaluasyonKalemiDto
        {
            KaynakTuru = kaynakTuru,
            KaynakId = kaynakId,
            KaynakKodu = kaynakKodu,
            KaynakAciklama = kaynakAciklama,
            HesapKodu = hesapKodu,
            DovizBakiye = dovizBakiye,
            EskiDefterDegeri = eskiDefterDegeri,
            OncekiDegerlemeKuru = Math.Round(oncekiKur, 4),
            YeniDegerlemeKuru = yeniKur,
            YeniYerelDeger = yeniYerelDeger,
            KurFarki = kurFarki
        });
    }

    public async Task<RevaluasyonKaydiDto> RevaluasyonUygulaAsync(RevaluasyonOnizleDto dto, int kullaniciId = 0)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.AyarlariYonet || y.MuhasebeFisEkle, "Dönem Sonu Revalüasyon");

        var targetSirketId = FirmaDogrula(dto.SirketId);
        await using var transaction = _baglam.Database.CurrentTransaction == null
            ? await _baglam.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
        var date = dto.DegerlemeTarihi.Date;
        var currency = dto.ParaBirimiKodu.ToUpperInvariant();

        // Closed Period Guard (Rule 15)
        var kapaliDonem = await _baglam.MaliDonemler.AnyAsync(m => m.SirketId == targetSirketId && m.KilitliMi && date >= m.Baslangic && date <= m.Bitis);
        if (kapaliDonem)
            throw new InvalidOperationException($"[{date:dd.MM.yyyy}] tarihi kapalı bir mali döneme denk geldiği için revalüasyon uygulanamaz.");

        // Idempotency Guard (Rule 20)
        var varMi = await _baglam.RevaluasyonKayitlari
            .AnyAsync(r => r.SirketId == targetSirketId && r.DegerlemeTarihi.Date == date && r.ParaBirimiKodu == currency && r.Durum == RevaluasyonDurumu.Onaylandi && !r.Silindi);

        if (varMi)
            throw new InvalidOperationException($"[{currency}] için [{date:dd.MM.yyyy}] tarihinde zaten aktif bir revalüasyon kaydı mevcuttur. Yeniden değerleme için öncelikle mevcut değerlemeyi İPTAL etmelisiniz.");

        // Finansal tutar ve hesaplar istemciden kabul edilmez; aynı transaction içinde yeniden hesaplanır.
        var maliDonemId = dto.MaliDonemId;
        dto = await RevaluasyonOnizleAsync(targetSirketId, currency, date);
        dto.MaliDonemId = maliDonemId;

        if (dto.Kalemler.Count == 0)
            throw new InvalidOperationException("Değerlenecek herhangi bir açık döviz bakiyesi veya kur farkı bulunamadı.");

        var kullanici = _kullaniciServisi.AktifKullanici;
        var uId = kullanici!.Id;
        var uAdi = kullanici?.KullaniciAdi ?? "Sistem";

        // Get FX Connection Codes (Rule 13 / Rule 15)
        var gelirHesapKodu = await _baglantiServisi.GetirHesapKoduForKategoriAsync("KUR_FARKI_GELIRI", targetSirketId);
        var giderHesapKodu = await _baglantiServisi.GetirHesapKoduForKategoriAsync("KUR_FARKI_GIDERI", targetSirketId);

        var gelirHesap = await _baglam.HesapPlanlari.FirstOrDefaultAsync(h => h.SirketId == targetSirketId && h.HesapKodu == gelirHesapKodu);
        var giderHesap = await _baglam.HesapPlanlari.FirstOrDefaultAsync(h => h.SirketId == targetSirketId && h.HesapKodu == giderHesapKodu);

        if (gelirHesap == null || giderHesap == null)
            throw new InvalidOperationException("Kur farkı gelir (646) veya gider (656) muhasebe bağlantı hesabı tanımlı değildir.");

        // Create Revaluation Header
        var kayit = new RevaluasyonKaydi
        {
            SirketId = targetSirketId,
            MaliDonemId = dto.MaliDonemId,
            DegerlemeTarihi = DateTime.SpecifyKind(date, DateTimeKind.Utc),
            ParaBirimiKodu = currency,
            Kur = dto.YeniDegerlemeKuru,
            ToplamEskiYerel = dto.ToplamEskiYerel,
            ToplamYeniYerel = dto.ToplamYeniYerel,
            ToplamKurFarki = dto.ToplamKurFarki,
            Durum = RevaluasyonDurumu.Onaylandi,
            KullaniciId = uId,
            OlusturanKullaniciAdi = uAdi,
            OlusturmaTarihi = DateTime.UtcNow,
            Aciklama = $"Dönem Sonu [{currency}] Revalüasyon İşlemi ({date:dd.MM.yyyy})"
        };

        foreach (var k in dto.Kalemler)
        {
            kayit.Kalemler.Add(new RevaluasyonKalemi
            {
                KaynakTuru = k.KaynakTuru,
                KaynakId = k.KaynakId,
                KaynakKodu = k.KaynakKodu,
                KaynakAciklama = k.KaynakAciklama,
                HesapId = k.HesapId,
                HesapKodu = k.HesapKodu,
                DovizBakiye = k.DovizBakiye,
                EskiDefterDegeri = k.EskiDefterDegeri,
                OncekiDegerlemeKuru = k.OncekiDegerlemeKuru,
                YeniDegerlemeKuru = k.YeniDegerlemeKuru,
                YeniYerelDeger = k.YeniYerelDeger,
                KurFarki = k.KurFarki
            });
        }

        _baglam.RevaluasyonKayitlari.Add(kayit);
        await _baglam.SaveChangesAsync();

        // Create Accounting Voucher (KRF-YYYY-XXXXXX)
        var yevmiyeSatirlari = new List<YevmiyeSatiriDto>();
        foreach (var k in kayit.Kalemler)
        {
            var anaHesap = await _baglam.HesapPlanlari.FirstOrDefaultAsync(h => h.SirketId == targetSirketId && h.HesapKodu == k.HesapKodu) ?? throw new InvalidOperationException("Değerleme hesabı hesap planında bulunamadı.");

            if (k.KurFarki > 0)
            {
                // Unearned FX Gain: Debit Main Account, Credit FX Gain Account (646)
                yevmiyeSatirlari.Add(new YevmiyeSatiriDto { HesapId = anaHesap.Id, Borc = k.KurFarki, Alacak = 0m, Aciklama = $"Kur Farkı Geliri: {k.KaynakAciklama}" });
                yevmiyeSatirlari.Add(new YevmiyeSatiriDto { HesapId = gelirHesap.Id, Borc = 0m, Alacak = k.KurFarki, Aciklama = $"Kambiyo Karı ({currency}): {k.KaynakKodu}" });
            }
            else if (k.KurFarki < 0)
            {
                // Unearned FX Loss: Debit FX Loss Account (656), Credit Main Account
                decimal mutlakFark = Math.Abs(k.KurFarki);
                yevmiyeSatirlari.Add(new YevmiyeSatiriDto { HesapId = giderHesap.Id, Borc = mutlakFark, Alacak = 0m, Aciklama = $"Kambiyo Zararı ({currency}): {k.KaynakKodu}" });
                yevmiyeSatirlari.Add(new YevmiyeSatiriDto { HesapId = anaHesap.Id, Borc = 0m, Alacak = mutlakFark, Aciklama = $"Kur Farkı Gideri: {k.KaynakAciklama}" });
            }
        }

        if (yevmiyeSatirlari.Count > 0)
        {
            var fis = await _yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
            {
                SirketId = targetSirketId,
                FisTuru = FisTuru.Mahsup,
                Tarih = date,
                Aciklama = $"Revalüasyon Fişi [{currency}] - {date:dd.MM.yyyy}",
                BelgeTipi = "REVALUASYON",
                BelgeId = kayit.Id,
                Durum = YevmiyeDurumu.Onaylandi,
                Satirlar = yevmiyeSatirlari
            }, targetSirketId, uId);

            kayit.YevmiyeFisId = fis.Id;
            await _baglam.SaveChangesAsync();
        }

        if (transaction != null) await transaction.CommitAsync();
        return MapToDto(kayit);
    }

    public async Task<RevaluasyonKaydiDto> RevaluasyonIptalEtAsync(int revaluasyonKaydiId, string iptalNedeni, Guid sirketId = default)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.AyarlariYonet || y.MuhasebeFisIptal, "Revalüasyon İptali");

        FirmaDogrula(sirketId);
        if (string.IsNullOrWhiteSpace(iptalNedeni))
            throw new ArgumentException("İptal nedeni gerekiyor.", nameof(iptalNedeni));
        await using var transaction = _baglam.Database.CurrentTransaction == null
            ? await _baglam.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
        var kayit = await _baglam.RevaluasyonKayitlari
            .Include(r => r.Kalemler)
            .FirstOrDefaultAsync(r => r.Id == revaluasyonKaydiId && r.SirketId == sirketId && !r.Silindi);

        if (kayit == null)
            throw new KeyNotFoundException("İptal edilecek revalüasyon kaydı bulunamadı.");

        if (kayit.Durum == RevaluasyonDurumu.Iptal)
            throw new InvalidOperationException("Bu revalüasyon kaydı daha önce iptal edilmiştir.");

        // Closed Period Guard (Rule 15)
        var kapaliDonem = await _baglam.MaliDonemler.AnyAsync(m => m.SirketId == kayit.SirketId && m.KilitliMi && kayit.DegerlemeTarihi >= m.Baslangic && kayit.DegerlemeTarihi <= m.Bitis);
        if (kapaliDonem)
            throw new InvalidOperationException("Kapalı mali döneme ait revalüasyon kaydı iptal edilemez.");

        // Storno Cancellation (Rule 21): Mark as cancelled and reverse accounting voucher
        kayit.Durum = RevaluasyonDurumu.Iptal;

        if (kayit.YevmiyeFisId.HasValue)
        {
            await _yevmiyeServisi.FisIptalEtAsync(kayit.YevmiyeFisId.Value, $"Revalüasyon İptali: {iptalNedeni}", kayit.SirketId);
        }

        // Record Audit
        var uAdi = _kullaniciServisi.AktifKullanici?.KullaniciAdi ?? "Sistem";
        _baglam.AuditLoglar.Add(new AuditLog
        {
            TabloAdi = "RevaluasyonKaydi",
            KayitId = kayit.Id.ToString(),
            IslemTuru = AuditIslemTuru.Guncelleme,
            Tarih = DateTime.UtcNow,
            KullaniciAdi = uAdi,
            YeniDegerJson = System.Text.Json.JsonSerializer.Serialize(new { Durum = "Iptal", Neden = iptalNedeni })
        });

        await _baglam.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
        return MapToDto(kayit);
    }

    public async Task<SayfaliSonuc<RevaluasyonKaydiDto>> GetirRevaluasyonGecmisiAsync(Guid sirketId, int sayfa = 1, int sayfaBoyutu = 20)
    {
        var targetSirketId = FirmaDogrula(sirketId);
        _kullaniciServisi.YetkiKontrolEt(y => y.MuhasebeFisGoruntule, "Dövizli bakiye ve revalüasyon görüntüleme");
        var query = _baglam.RevaluasyonKayitlari
            .AsNoTracking()
            .Include(r => r.Kalemler)
            .Include(r => r.YevmiyeFis)
            .Where(r => r.SirketId == targetSirketId && !r.Silindi);

        var toplam = await query.CountAsync();
        var pg = Math.Max(1, sayfa);
        var sz = Math.Clamp(sayfaBoyutu, 10, 500);

        var list = await query
            .OrderByDescending(r => r.DegerlemeTarihi)
            .ThenByDescending(r => r.Id)
            .Skip((pg - 1) * sz)
            .Take(sz)
            .ToListAsync();

        return new SayfaliSonuc<RevaluasyonKaydiDto>
        {
            Veri = list.Select(MapToDto).ToList(),
            ToplamKayit = toplam,
            MevcutSayfa = pg,
            SayfaBoyutu = sz
        };
    }

    public async Task<List<DovizliBakiyeOzetDto>> GetirDovizliBakiyeOzetleriAsync(Guid sirketId)
    {
        var targetSirketId = FirmaDogrula(sirketId);
        _kullaniciServisi.YetkiKontrolEt(y => y.MuhasebeFisGoruntule, "Dövizli bakiye ve revalüasyon görüntüleme");
        var list = new List<DovizliBakiyeOzetDto>();

        foreach (var currency in new[] { "USD", "EUR", "GBP" })
        {
            var kurDto = await _kurServisi.GetirGecerliKurAsync(currency);
            decimal guncelKur = kurDto?.DovizAlis ?? 1m;

            // Döviz bakiyesi hareketlerin döviz tutarlarından; defter değeri TL tutarlarından (önceki değerleme farkları hariç).
            var bankaOzet = await _baglam.BankaHareketleri.AsNoTracking()
                .Where(h => h.BankaHesabi!.SirketId == targetSirketId && h.BankaHesabi.ParaBirimi.ToUpper() == currency && h.BankaHesabi.AktifMi && h.IslemDurumu == FinansIslemDurumu.Tamamlandi)
                .GroupBy(h => 1)
                .Select(g => new { Doviz = g.Sum(h => h.Tur == HareketTuru.Gelir ? h.DovizTutar : -h.DovizTutar), Tl = g.Sum(h => h.Tur == HareketTuru.Gelir ? h.Tutar : -h.Tutar) })
                .FirstOrDefaultAsync();
            var kasaOzet = await _baglam.KasaHareketleri.AsNoTracking()
                .Where(h => h.Kasa!.SirketId == targetSirketId && h.Kasa.ParaBirimi.ToUpper() == currency && h.Kasa.AktifMi && h.IslemDurumu == FinansIslemDurumu.Tamamlandi)
                .GroupBy(h => 1)
                .Select(g => new { Doviz = g.Sum(h => h.Tur == HareketTuru.Gelir ? h.DovizTutar : -h.DovizTutar), Tl = g.Sum(h => h.Tur == HareketTuru.Gelir ? h.Tutar : -h.Tutar) })
                .FirstOrDefaultAsync();
            var bankaBakiye = bankaOzet?.Doviz ?? 0m;
            var kasaBakiye = kasaOzet?.Doviz ?? 0m;
            var defterDegeri = (bankaOzet?.Tl ?? 0m) + (kasaOzet?.Tl ?? 0m);

            decimal toplamDoviz = bankaBakiye + kasaBakiye;
            decimal guncelDegerTRY = toplamDoviz * guncelKur;

            list.Add(new DovizliBakiyeOzetDto
            {
                ParaBirimiKodu = currency,
                ToplamDovizBakiye = toplamDoviz,
                ToplamDefterDegeriTRY = defterDegeri,
                GuncelKur = guncelKur,
                GuncelDegerTRY = guncelDegerTRY
            });
        }

        return list;
    }

    private static RevaluasyonKaydiDto MapToDto(RevaluasyonKaydi entity)
    {
        return new RevaluasyonKaydiDto
        {
            Id = entity.Id,
            SirketId = entity.SirketId,
            MaliDonemId = entity.MaliDonemId,
            DegerlemeTarihi = entity.DegerlemeTarihi,
            ParaBirimiKodu = entity.ParaBirimiKodu,
            Kur = entity.Kur,
            ToplamEskiYerel = entity.ToplamEskiYerel,
            ToplamYeniYerel = entity.ToplamYeniYerel,
            ToplamKurFarki = entity.ToplamKurFarki,
            YevmiyeFisId = entity.YevmiyeFisId,
            FisNo = entity.YevmiyeFis?.FisNo ?? "",
            DurumStr = entity.Durum == RevaluasyonDurumu.Onaylandi ? "Onaylı" : "İptal",
            KullaniciId = entity.KullaniciId,
            OlusturanKullaniciAdi = entity.OlusturanKullaniciAdi,
            OlusturmaTarihi = entity.OlusturmaTarihi,
            Aciklama = entity.Aciklama,
            Kalemler = entity.Kalemler.Select(k => new RevaluasyonKalemiDto
            {
                Id = k.Id,
                KaynakTuru = k.KaynakTuru,
                KaynakId = k.KaynakId,
                KaynakKodu = k.KaynakKodu,
                KaynakAciklama = k.KaynakAciklama,
                HesapId = k.HesapId,
                HesapKodu = k.HesapKodu,
                DovizBakiye = k.DovizBakiye,
                EskiDefterDegeri = k.EskiDefterDegeri,
                OncekiDegerlemeKuru = k.OncekiDegerlemeKuru,
                YeniDegerlemeKuru = k.YeniDegerlemeKuru,
                YeniYerelDeger = k.YeniYerelDeger,
                KurFarki = k.KurFarki
            }).ToList()
        };
    }
}
