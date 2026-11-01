using Altyapi.Veri;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using static Altyapi.Servisler.FinansMuhasebeKaydi;

namespace Altyapi.Servisler;

// Gider pusulası (VUK 234) ve müstahsil makbuzu (VUK 235): vergi mükellefi olmayan kişilerden alım.
// Kayıt: gider hesabı borç (brüt) / 360 stopaj + 336 net alacak; ödeme kasa/bankadan 336'yı kapatır.
// Kasa/banka hareketi "GIDER_PUSULASI" özel koduyla yalnız bu ekrandan iptal edilir. İptal: ters fişler, belge İptal olarak kalır.
public class GiderPusulasiServisi : IGiderPusulasiServisi
{
    public const string BelgeTipi = "GIDER_PUSULASI";
    public const string OzelKod = "GIDER_PUSULASI";
    public const string Kasa = "Kasa";
    public const string Banka = "Banka";
    public const string SaticiHesabi = "336";
    public const string StopajHesabi = "360";

    public static readonly IReadOnlyList<StopajSecenegi> StopajSecenekleri =
    [
        new(0, "%0 - Stopaj yok"),
        new(2, "%2 - Muaf esnaf: hurda mal (GVK 94/13-a)"),
        new(5, "%5 - Muaf esnaf: diğer mal (GVK 94/13-a)"),
        new(10, "%10 - Muaf esnaf: hizmet (GVK 94/13-a)"),
        new(1, "%1 - Çiftçi: hayvan ve hayvan ürünleri (GVK 94/11)"),
        new(2, "%2 - Çiftçi: diğer zirai ürün ve hizmet (GVK 94/11)")
    ];

    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public GiderPusulasiServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    private IKullaniciServisi Oturum => _kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama");
    private void Goruntule() => _kullaniciServisi?.YetkiKontrolEt(y => y.FaturaGoruntule, "Gider pusulası görüntüleme");

    public static string TurAdi(GiderPusulasiTuru tur) => tur == GiderPusulasiTuru.MustahsilMakbuzu ? "Müstahsil Makbuzu" : "Gider Pusulası";

    public async Task<List<GiderPusulasiListeDto>> ListeAsync(DateTime baslangic, DateTime bitis, bool iptallerDahil = true, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        var bas = DateTime.SpecifyKind(baslangic.Date, DateTimeKind.Utc);
        var son = DateTime.SpecifyKind(bitis.Date.AddDays(1), DateTimeKind.Utc);
        var belgeler = await _baglam.GiderPusulalari.AsNoTracking()
            .Where(g => g.SirketId == sirketId && g.Tarih >= bas && g.Tarih < son && (iptallerDahil || !g.IptalEdildi))
            .OrderByDescending(g => g.Tarih).ThenByDescending(g => g.Id).ToListAsync();
        var kasaIdleri = belgeler.Where(g => g.KasaHareketiId != null).Select(g => g.KasaHareketiId!.Value).ToList();
        var bankaIdleri = belgeler.Where(g => g.BankaHareketiId != null).Select(g => g.BankaHareketiId!.Value).ToList();
        var kasaAdlari = await _baglam.KasaHareketleri.AsNoTracking().Where(k => kasaIdleri.Contains(k.Id))
            .Select(k => new { k.Id, Ad = k.Kasa!.Ad }).ToDictionaryAsync(k => k.Id, k => k.Ad);
        var bankaAdlari = await _baglam.BankaHareketleri.AsNoTracking().Where(b => bankaIdleri.Contains(b.Id))
            .Select(b => new { b.Id, Ad = b.BankaHesabi!.Ad }).ToDictionaryAsync(b => b.Id, b => b.Ad);
        return belgeler.Select(g => new GiderPusulasiListeDto
        {
            Id = g.Id, Tur = g.Tur, BelgeNo = g.BelgeNo, Tarih = g.Tarih, SaticiAdSoyad = g.SaticiAdSoyad, SaticiTcKimlikNo = g.SaticiTcKimlikNo,
            GiderHesapKodu = g.GiderHesapKodu, BrutTutar = g.BrutTutar, StopajOrani = g.StopajOrani, StopajTutari = g.StopajTutari, NetTutar = g.NetTutar,
            IptalEdildi = g.IptalEdildi, Aciklama = g.Aciklama,
            OdemeHesabi = g.KasaHareketiId is int k ? $"Kasa: {kasaAdlari.GetValueOrDefault(k)}" : g.BankaHareketiId is int b ? $"Banka: {bankaAdlari.GetValueOrDefault(b)}" : ""
        }).ToList();
    }

    public async Task<GiderPusulasi?> GetirAsync(int id, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        return await _baglam.GiderPusulalari.AsNoTracking().Include(g => g.Kalemler)
            .FirstOrDefaultAsync(g => g.Id == id && g.SirketId == sirketId);
    }

    public async Task<List<OdemeHesabiDto>> OdemeHesaplariAsync(Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        var liste = new List<OdemeHesabiDto>();
        foreach (var k in (await _baglam.Kasalar.AsNoTracking().Where(k => k.AktifMi).OrderBy(k => k.Ad).ToListAsync())
                     .Where(k => _baglam.SirketIdCoz(k.SirketId) == sirketId && !FinansKurallari.DovizliMi(k.ParaBirimi)))
            liste.Add(new OdemeHesabiDto { Kaynak = Kasa, Id = k.Id, Ad = k.Ad, Bakiye = await FinansKurallari.KasaBakiyesiAsync(_baglam, k.Id) });
        foreach (var b in (await _baglam.BankaHesaplari.AsNoTracking().Where(b => b.AktifMi && b.HesapTuru != BankaHesapTuru.POSHesabi).OrderBy(b => b.Ad).ToListAsync())
                     .Where(b => _baglam.SirketIdCoz(b.SirketId) == sirketId && !FinansKurallari.DovizliMi(b.ParaBirimi)))
            liste.Add(new OdemeHesabiDto { Kaynak = Banka, Id = b.Id, Ad = b.Ad, Bakiye = await FinansKurallari.BankaBakiyesiAsync(_baglam, b.Id) });
        return liste;
    }

    public static decimal StopajHesapla(decimal brut, decimal oran) => decimal.Round(brut * oran / 100m, 2, MidpointRounding.AwayFromZero);

    private static string? Bos(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public async Task<GiderPusulasi> KaydetAsync(GiderPusulasiGirisDto giris, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Oturum.YetkiKontrolEt(y => y.FaturaOlustur && y.OdemeYap, "Gider pusulası düzenleme");

        var turAdi = TurAdi(giris.Tur);
        if (!Enum.IsDefined(giris.Tur)) throw new InvalidOperationException("Belge türü geçersiz.");
        var satici = (giris.SaticiAdSoyad ?? "").Trim();
        if (satici.Length == 0) throw new InvalidOperationException("Satıcının adı soyadı zorunludur.");
        if (satici.Length > 200) throw new InvalidOperationException("Satıcı adı en fazla 200 karakter olabilir.");
        var tc = Bos(giris.SaticiTcKimlikNo);
        if (tc != null && !PersonelServisi.TcKimlikGecerli(tc))
            throw new InvalidOperationException("T.C. kimlik numarası geçersiz (11 hane ve sağlama tutmalı).");
        var adres = Bos(giris.SaticiAdres);
        if (adres?.Length > 500) throw new InvalidOperationException("Adres en fazla 500 karakter olabilir.");
        var giderKodu = (giris.GiderHesapKodu ?? "").Trim();
        if (giderKodu.Length == 0) throw new InvalidOperationException("Gider hesabı seçilmelidir.");
        if (giris.StopajOrani < 0 || giris.StopajOrani > 50 || decimal.Round(giris.StopajOrani, 2) != giris.StopajOrani)
            throw new InvalidOperationException("Stopaj oranı %0 ile %50 arasında olmalıdır.");
        if (giris.OdemeKaynagi is not (Kasa or Banka)) throw new InvalidOperationException("Ödeme kasadan ya da bankadan yapılmalıdır.");

        var kalemler = giris.Kalemler.Where(k => !string.IsNullOrWhiteSpace(k.Cinsi) || k.BirimFiyat != 0).ToList();
        if (kalemler.Count == 0) throw new InvalidOperationException("En az bir kalem (malın / hizmetin cinsi ve tutarı) girilmelidir.");
        foreach (var k in kalemler)
        {
            if (string.IsNullOrWhiteSpace(k.Cinsi)) throw new InvalidOperationException("Her kalemde malın / hizmetin cinsi yazılmalıdır.");
            if (k.Cinsi.Trim().Length > 300) throw new InvalidOperationException("Kalem açıklaması en fazla 300 karakter olabilir.");
            if (k.Miktar <= 0 || decimal.Round(k.Miktar, 3) != k.Miktar) throw new InvalidOperationException($"'{k.Cinsi.Trim()}': miktar sıfırdan büyük, en fazla 3 ondalıklı olmalıdır.");
            if (k.BirimFiyat <= 0 || decimal.Round(k.BirimFiyat, 4) != k.BirimFiyat) throw new InvalidOperationException($"'{k.Cinsi.Trim()}': birim fiyat sıfırdan büyük olmalıdır.");
            if (k.Tutar <= 0) throw new InvalidOperationException($"'{k.Cinsi.Trim()}': tutar sıfır çıkıyor.");
        }
        var brut = kalemler.Sum(k => k.Tutar);
        FinansKurallari.TutarDogrula(brut, turAdi);
        var stopaj = StopajHesapla(brut, giris.StopajOrani);
        var net = brut - stopaj;
        if (net <= 0) throw new InvalidOperationException("Ödenecek net tutar sıfırdan büyük olmalıdır.");

        var tarih = FinansKurallari.TarihNormalizeEt(giris.Tarih.Date);
        if (tarih.Date > DateTime.Today) throw new InvalidOperationException("İleri tarihli belge düzenlenemez.");
        await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, sirketId);
        if (await _baglam.YevmiyeFisleri.AnyAsync(f => f.SirketId == sirketId && f.BelgeTipi == $"YILSONU_{tarih.Year}_KAPANIS" && f.TersFisId == null))
            throw new InvalidOperationException($"{tarih.Year} yılı kapatılmış. Önce Yıl Sonu Kapanışı ekranından kapanışı geri alın.");
        var aciklama = Bos(giris.Aciklama) ?? "";
        if (aciklama.Length > 500) throw new InvalidOperationException("Açıklama en fazla 500 karakter olabilir.");

        await using var islem = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var belge = await KaydetIcAsync(giris, sirketId, turAdi, satici, tc, adres, giderKodu, kalemler, brut, stopaj, net, tarih, aciklama);
            await islem.CommitAsync();
            return belge;
        }
        catch
        {
            // Ekran aynı bağlamı sonraki işlemlerde de kullanır; geri alınan kayıtların izleri kalmasın.
            _baglam.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<GiderPusulasi> KaydetIcAsync(GiderPusulasiGirisDto giris, Guid sirketId, string turAdi, string satici, string? tc, string? adres,
        string giderKodu, List<GiderPusulasiKalemDto> kalemler, decimal brut, decimal stopaj, decimal net, DateTime tarih, string aciklama)
    {
        await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", $"giderpusulasi:{sirketId}");
        // Ödeme hesabı ve bakiye belge yazılmadan önce denetlenir.
        await OdemeYapilabilirAsync(sirketId, giris.OdemeKaynagi, giris.OdemeHesabiId, net);

        var onEk = giris.Tur == GiderPusulasiTuru.MustahsilMakbuzu ? "MM" : "GP";
        var yilOnEki = $"{onEk}-{tarih.Year}-";
        var sonNolar = await _baglam.GiderPusulalari.IgnoreQueryFilters()
            .Where(g => g.SirketId == sirketId && g.BelgeNo.StartsWith(yilOnEki)).Select(g => g.BelgeNo).ToListAsync();
        var sira = sonNolar.Select(n => int.TryParse(n[yilOnEki.Length..], out var x) ? x : 0).DefaultIfEmpty(0).Max() + 1;

        var belge = new GiderPusulasi
        {
            SirketId = sirketId, Tur = giris.Tur, BelgeNo = $"{yilOnEki}{sira:00000}", Tarih = tarih, SaticiAdSoyad = satici, SaticiTcKimlikNo = tc,
            SaticiAdres = adres, GiderHesapKodu = giderKodu, BrutTutar = brut, StopajOrani = giris.StopajOrani, StopajTutari = stopaj, NetTutar = net,
            Aciklama = aciklama,
            Kalemler = kalemler.Select(k => new GiderPusulasiKalemi
            {
                Cinsi = k.Cinsi.Trim(), Miktar = k.Miktar, Birim = string.IsNullOrWhiteSpace(k.Birim) ? "Adet" : k.Birim.Trim(), BirimFiyat = k.BirimFiyat, Tutar = k.Tutar
            }).ToList()
        };
        _baglam.GiderPusulalari.Add(belge);
        await _baglam.SaveChangesAsync();

        var fisAciklama = $"{turAdi} {belge.BelgeNo} - {satici}";
        var satirlar = new List<Satir> { new(giderKodu, brut, 0), new(SaticiHesabi, 0, net) };
        if (stopaj > 0) satirlar.Add(new Satir(StopajHesabi, 0, stopaj));
        await FisEkleAsync(_baglam, sirketId, BelgeTipi, belge.Id, tarih, fisAciklama, belge.BelgeNo, satirlar.ToArray());
        var fis = await _baglam.YevmiyeFisleri.SingleAsync(f => f.SirketId == sirketId && f.BelgeTipi == BelgeTipi && f.BelgeId == belge.Id);
        belge.YevmiyeFisId = fis.Id;

        var (kasa, banka) = await CikisEkleAsync(sirketId, giris.OdemeKaynagi, giris.OdemeHesabiId, net, tarih, $"{turAdi} ödemesi {belge.BelgeNo} - {satici}", belge);
        belge.KasaHareketiId = kasa?.Id;
        belge.BankaHareketiId = banka?.Id;
        await _baglam.SaveChangesAsync();
        return belge;
    }

    private async Task<(EtkiAlani.Modeller.Finans.Kasa? Kasa, BankaHesabi? Banka)> OdemeYapilabilirAsync(Guid sirketId, string kaynak, int hesapId, decimal tutar)
    {
        if (kaynak == Kasa)
        {
            var k = await _baglam.Kasalar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == hesapId);
            if (k == null || _baglam.SirketIdCoz(k.SirketId) != sirketId) throw new KeyNotFoundException("Kasa bulunamadı.");
            if (!k.AktifMi) throw new InvalidOperationException($"'{k.Ad}' kasası pasif durumda.");
            if (FinansKurallari.DovizliMi(k.ParaBirimi)) throw new InvalidOperationException("Ödeme TL kasa veya bankadan yapılır.");
            await FinansKurallari.KasaKilitleAsync(_baglam, hesapId);
            await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, k, tutar);
            return (k, null);
        }
        var b = await _baglam.BankaHesaplari.AsNoTracking().FirstOrDefaultAsync(x => x.Id == hesapId);
        if (b == null || _baglam.SirketIdCoz(b.SirketId) != sirketId) throw new KeyNotFoundException("Banka hesabı bulunamadı.");
        if (!b.AktifMi) throw new InvalidOperationException($"'{b.Ad}' banka hesabı pasif durumda.");
        if (FinansKurallari.DovizliMi(b.ParaBirimi) || b.HesapTuru == BankaHesapTuru.POSHesabi)
            throw new InvalidOperationException("Ödeme TL vadesiz hesaptan yapılır.");
        await FinansKurallari.BankaHesabiKilitleAsync(_baglam, hesapId);
        await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, b, tutar);
        return (null, b);
    }

    private async Task<(KasaHareketi? Kasa, BankaHareketi? Banka)> CikisEkleAsync(Guid sirketId, string kaynak, int hesapId, decimal tutar, DateTime tarih,
        string aciklama, GiderPusulasi belge)
    {
        aciklama = aciklama.Length > FinansKurallari.AciklamaUzunlugu ? aciklama[..FinansKurallari.AciklamaUzunlugu] : aciklama;
        var (k, b) = await OdemeYapilabilirAsync(sirketId, kaynak, hesapId, tutar);
        if (k != null)
        {
            var h = new KasaHareketi
            {
                SirketId = sirketId, KasaId = hesapId, Tarih = tarih, Tur = HareketTuru.Gider, FisTuru = KasaFisTuru.Gider,
                FisNo = FinansKurallari.FisNoUret("GP"), Tutar = tutar, Aciklama = aciklama, OzelKod = OzelKod, IlgiliBelgeId = belge.Id, Belge = belge.BelgeNo
            };
            _baglam.KasaHareketleri.Add(h);
            await _baglam.SaveChangesAsync();
            await KasaHareketiFisiEkleAsync(_baglam, h, SaticiHesabi);
            return (h, null);
        }
        var bh = new BankaHareketi
        {
            SirketId = sirketId, BankaHesabiId = hesapId, BankaAdi = b!.Ad, HesapNo = string.IsNullOrWhiteSpace(b.Iban) ? b.HesapNo : b.Iban,
            Tarih = tarih, Tur = HareketTuru.Gider, FisTuru = BankaFisTuru.GidenHavale, OdemeYontemi = OdemeYontemi.Havale,
            FisNo = FinansKurallari.FisNoUret("GP"), Tutar = tutar, Aciklama = aciklama, OzelKod = OzelKod, IlgiliBelgeId = belge.Id, DekontNo = belge.BelgeNo
        };
        _baglam.BankaHareketleri.Add(bh);
        await _baglam.SaveChangesAsync();
        await BankaHareketiFisiEkleAsync(_baglam, bh, SaticiHesabi);
        return (null, bh);
    }

    // İptal: belge fişi ve ödeme hareketi ters kayıtla kapanır; belge "İptal" olarak listede kalır (numara boşluğu oluşmaz).
    public async Task IptalAsync(int id, string neden, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Oturum.YetkiKontrolEt(y => y.FaturaIptal && y.OdemeYap, "Gider pusulası iptali");
        var temizNeden = string.IsNullOrWhiteSpace(neden) ? "Kullanıcı iptal etti" : neden.Trim();
        if (temizNeden.Length > 200) temizNeden = temizNeden[..200];

        await using var islem = await _baglam.Database.BeginTransactionAsync();
        try
        {
            await IptalIcAsync(id, temizNeden, sirketId);
            await islem.CommitAsync();
        }
        catch
        {
            _baglam.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task IptalIcAsync(int id, string temizNeden, Guid sirketId)
    {
        await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", $"giderpusulasi:{sirketId}");
        var belge = await _baglam.GiderPusulalari.FirstOrDefaultAsync(g => g.Id == id && g.SirketId == sirketId)
            ?? throw new KeyNotFoundException("Belge bulunamadı.");
        if (belge.IptalEdildi) throw new InvalidOperationException("Bu belge zaten iptal edilmiş.");
        if (await _baglam.YevmiyeFisleri.AnyAsync(f => f.SirketId == sirketId && f.BelgeTipi == $"YILSONU_{belge.Tarih.Year}_KAPANIS" && f.TersFisId == null))
            throw new InvalidOperationException($"{belge.Tarih.Year} yılı kapatılmış. Önce yıl sonu kapanışını geri alın.");
        await FinansKurallari.DonemAcikOlmaliAsync(_baglam, belge.Tarih, sirketId);
        var bugun = FinansKurallari.TarihNormalizeEt(null);
        await FinansKurallari.DonemAcikOlmaliAsync(_baglam, bugun, sirketId);

        // Belge fişi aynı tarihli ters fişle (gider, stopaj ve satıcı borcu birlikte geri döner).
        if (belge.YevmiyeFisId is int fisId)
        {
            var fis = await _baglam.YevmiyeFisleri.Include(f => f.Satirlar).ThenInclude(s => s.Hesap).SingleAsync(f => f.Id == fisId);
            if (fis.TersFisId == null && fis.Durum == YevmiyeDurumu.Onaylandi)
            {
                var tersTip = $"TERS_{BelgeTipi}";
                await FisEkleAsync(_baglam, sirketId, tersTip, fis.Id, fis.Tarih, $"İptal: {fis.Aciklama} ({temizNeden})", $"Ters Fiş ({fis.FisNo})",
                    fis.Satirlar.Select(s => new Satir(s.Hesap!.HesapKodu, s.Alacak, s.Borc, s.CariKartId)).ToArray());
                var ters = await _baglam.YevmiyeFisleri.SingleAsync(f => f.SirketId == sirketId && f.BelgeTipi == tersTip && f.BelgeId == fis.Id);
                ters.IptalEdilenFisId = fis.Id;
                fis.TersFisId = ters.Id;
            }
        }

        // Ödeme bugünkü tarihle geri girer (kasa/banka geçmişi bozulmasın).
        if (belge.KasaHareketiId is int kid)
        {
            var asil = await _baglam.KasaHareketleri.SingleAsync(x => x.Id == kid);
            if (asil.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            {
                if (asil.KasaId.HasValue) await FinansKurallari.KasaKilitleAsync(_baglam, asil.KasaId.Value);
                asil.IslemDurumu = FinansIslemDurumu.IptalEdildi;
                var ters = new KasaHareketi
                {
                    SirketId = asil.SirketId, KasaId = asil.KasaId, Tarih = bugun, Tur = HareketTuru.Gelir, FisTuru = asil.FisTuru, FisNo = FinansKurallari.FisNoUret("IPT"),
                    Tutar = asil.Tutar, Aciklama = $"Ters Kayıt: {belge.BelgeNo} iptal ({temizNeden})", OzelKod = OzelKod, IlgiliBelgeId = belge.Id,
                    IslemDurumu = FinansIslemDurumu.TersKayit, TersKayitId = asil.Id, Belge = belge.BelgeNo
                };
                _baglam.KasaHareketleri.Add(ters);
                await _baglam.SaveChangesAsync();
                asil.TersKayitId = ters.Id;
                await TersFisEkleAsync(_baglam, true, asil.Id, ters, null);
            }
        }
        else if (belge.BankaHareketiId is int bid)
        {
            var asil = await _baglam.BankaHareketleri.SingleAsync(x => x.Id == bid);
            if (asil.IslemDurumu == FinansIslemDurumu.Tamamlandi)
            {
                if (asil.BankaHesabiId.HasValue) await FinansKurallari.BankaHesabiKilitleAsync(_baglam, asil.BankaHesabiId.Value);
                asil.IslemDurumu = FinansIslemDurumu.IptalEdildi;
                var ters = new BankaHareketi
                {
                    SirketId = asil.SirketId, BankaHesabiId = asil.BankaHesabiId, BankaAdi = asil.BankaAdi, HesapNo = asil.HesapNo, Tarih = bugun, Tur = HareketTuru.Gelir,
                    FisTuru = asil.FisTuru, OdemeYontemi = asil.OdemeYontemi, FisNo = FinansKurallari.FisNoUret("IPT"), Tutar = asil.Tutar,
                    Aciklama = $"Ters Kayıt: {belge.BelgeNo} iptal ({temizNeden})", OzelKod = OzelKod, IlgiliBelgeId = belge.Id,
                    IslemDurumu = FinansIslemDurumu.TersKayit, TersKayitId = asil.Id, DekontNo = belge.BelgeNo
                };
                _baglam.BankaHareketleri.Add(ters);
                await _baglam.SaveChangesAsync();
                asil.TersKayitId = ters.Id;
                await TersFisEkleAsync(_baglam, false, asil.Id, null, ters);
            }
        }

        belge.IptalEdildi = true;
        belge.IptalTarihi = DateTime.UtcNow;
        belge.IptalNedeni = temizNeden;
        belge.GuncellemeTarihi = DateTime.UtcNow;
        await _baglam.SaveChangesAsync();
    }
}
