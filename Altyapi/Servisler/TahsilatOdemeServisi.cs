using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

public sealed class TahsilatOdemeServisi : ITahsilatOdemeServisi
{
    private const decimal EnBuyukTutar = 99999999999999.9999m;
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi _kullanici;

    public TahsilatOdemeServisi(MuhasebeBaglami baglam, IKullaniciServisi kullanici)
    { _baglam = baglam; _kullanici = kullanici; }

    public async Task<TahsilatOdemeKaydi> KaydetAsync(TahsilatOdemeTalebi talep)
    {
        YetkiKontrol(talep.IslemTuru);
        Dogrula(talep);
        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var cari = await _baglam.CariKartlar.SingleOrDefaultAsync(x => x.Id == talep.CariKartId)
                ?? throw new KeyNotFoundException("Cari kart bulunamadı.");
            if (!cari.Aktif) throw new InvalidOperationException("Pasif cari kartla işlem yapılamaz.");
            if (talep.IslemTuru == FinansIslemTuru.Tahsilat && cari.Tur == CariTuru.Tedarikci)
                throw new InvalidOperationException("Tahsilat için müşteri veya müşteri/tedarikçi cari seçilmelidir.");
            if (talep.IslemTuru == FinansIslemTuru.Odeme && cari.Tur == CariTuru.Musteri)
                throw new InvalidOperationException("Ödeme için tedarikçi veya müşteri/tedarikçi cari seçilmelidir.");

            var tarih = DateTime.SpecifyKind(talep.Tarih, DateTimeKind.Utc);
            var aciklama = string.IsNullOrWhiteSpace(talep.Aciklama) 
                ? $"{(talep.IslemTuru == FinansIslemTuru.Tahsilat ? "Tahsilat" : "Ödeme")} - {talep.OdemeYontemi}"
                : talep.Aciklama.Trim();
            int hareketId;
            string kaynak;
            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, cari.SirketId);
            if (talep.OdemeYontemi == OdemeYontemi.Nakit)
            {
                // Para hangi kasaya girip çıktığı bilinmeden kaydedilmez; tek aktif kasa varsa o kullanılır.
                var kasaId = await KasaBelirleAsync(talep.KasaId);
                talep = talep with { KasaId = kasaId };
                await FinansKurallari.KasaKilitleAsync(_baglam, kasaId);
                var kasa = await _baglam.Kasalar.SingleOrDefaultAsync(k => k.Id == kasaId) ?? throw new KeyNotFoundException("Kasa bulunamadı.");
                if (!kasa.AktifMi) throw new InvalidOperationException($"'{kasa.Ad}' kasası pasif durumda.");
                if (talep.IslemTuru == FinansIslemTuru.Odeme) await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, kasa, talep.Tutar);
                var h = new KasaHareketi { SirketId = cari.SirketId, Tarih = tarih, Tur = talep.IslemTuru == FinansIslemTuru.Tahsilat ? HareketTuru.Gelir : HareketTuru.Gider,
                    FisTuru = talep.IslemTuru == FinansIslemTuru.Tahsilat ? KasaFisTuru.Tahsilat : KasaFisTuru.Odeme,
                    Tutar = talep.Tutar, Aciklama = aciklama, Belge = talep.BelgeNo?.Trim(), CariKartId = cari.Id, 
                    ParaBirimiId = talep.ParaBirimiId, Kur = talep.Kur, DovizTutar = talep.DovizTutar, KasaId = talep.KasaId };
                _baglam.KasaHareketleri.Add(h); await _baglam.SaveChangesAsync(); hareketId = h.Id; kaynak = "Kasa";
            }
            else if (talep.OdemeYontemi == OdemeYontemi.Cek || talep.OdemeYontemi == OdemeYontemi.Senet)
            {
                // Çek / Senet Portföye girer
                var cekTuru = talep.OdemeYontemi == OdemeYontemi.Cek 
                    ? (talep.IslemTuru == FinansIslemTuru.Tahsilat ? CekSenetTuru.AlinanCek : CekSenetTuru.VerilenCek)
                    : (talep.IslemTuru == FinansIslemTuru.Tahsilat ? CekSenetTuru.AlinanSenet : CekSenetTuru.VerilenSenet);

                var cek = new CekSenet
                {
                    SirketId = cari.SirketId,
                    Tur = cekTuru,
                    Durum = CekSenetDurumu.Portfoyde,
                    BelgeNo = talep.CekSenetNo?.Trim() ?? string.Empty,
                    VadeTarihi = talep.VadeTarihi ?? tarih.AddDays(30),
                    DuzenlemeTarihi = tarih,
                    Tutar = talep.Tutar,
                    CariKartId = cari.Id,
                    Kesideci = talep.Kesideci?.Trim() ?? string.Empty,
                    BankaAdi = talep.BankaAdi?.Trim(),
                    HesapNo = talep.HesapNo?.Trim(),
                    Aciklama = aciklama,
                    ParaBirimiId = talep.ParaBirimiId,
                    Kur = talep.Kur,
                    DovizTutar = talep.DovizTutar
                };
                _baglam.CekSenetler.Add(cek); await _baglam.SaveChangesAsync(); hareketId = cek.Id; kaynak = "Portfoy";
            }
            else
            {
                // KrediKarti, Banka, Havale, Virman → BankaHareketi
                var notlar = talep.OdemeYontemi switch
                {
                    OdemeYontemi.KrediKarti => $"Taksit:{talep.TaksitSayisi ?? 1}",
                    _ => null
                };
                var tamAciklama = notlar != null ? $"{aciklama} [{notlar}]" : aciklama;
                // Para hangi banka hesabına girip çıktığı bilinmeden kaydedilmez; tek aktif hesap varsa o kullanılır.
                var hesapId = await BankaHesabiBelirleAsync(talep.BankaHesabiId);
                await FinansKurallari.BankaHesabiKilitleAsync(_baglam, hesapId);
                var hesap = await _baglam.BankaHesaplari.SingleOrDefaultAsync(b => b.Id == hesapId) ?? throw new KeyNotFoundException("Banka hesabı bulunamadı.");
                if (!hesap.AktifMi) throw new InvalidOperationException($"'{hesap.Ad}' banka hesabı pasif durumda.");
                if (talep.IslemTuru == FinansIslemTuru.Odeme) await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, hesap, talep.Tutar);
                talep = talep with
                {
                    BankaHesabiId = hesapId,
                    BankaAdi = string.IsNullOrWhiteSpace(talep.BankaAdi) ? hesap.Ad : talep.BankaAdi,
                    HesapNo = string.IsNullOrWhiteSpace(talep.HesapNo) ? (string.IsNullOrWhiteSpace(hesap.Iban) ? hesap.HesapNo : hesap.Iban) : talep.HesapNo
                };
                var h = new BankaHareketi { SirketId = cari.SirketId, Tarih = tarih, Tur = talep.IslemTuru == FinansIslemTuru.Tahsilat ? HareketTuru.Gelir : HareketTuru.Gider,
                    FisTuru = talep.IslemTuru == FinansIslemTuru.Tahsilat ? BankaFisTuru.GelenHavale : BankaFisTuru.GidenHavale,
                    Tutar = talep.Tutar, Aciklama = tamAciklama, BankaAdi = talep.BankaAdi?.Trim(), HesapNo = talep.HesapNo?.Trim(), CariKartId = cari.Id,
                    OdemeYontemi = talep.OdemeYontemi, ParaBirimiId = talep.ParaBirimiId, Kur = talep.Kur, DovizTutar = talep.DovizTutar, BankaHesabiId = talep.BankaHesabiId };
                _baglam.BankaHareketleri.Add(h); await _baglam.SaveChangesAsync(); hareketId = h.Id; kaynak = "Banka";
            }

            var cariHareket = new CariHareket { CariKartId = cari.Id, Tarih = tarih,
                HareketTuru = talep.IslemTuru == FinansIslemTuru.Tahsilat ? CariHareketTuru.Tahsilat : CariHareketTuru.Odeme,
                Aciklama = aciklama, BelgeNo = talep.BelgeNo?.Trim(), IlgiliBelgeId = hareketId,
                Borc = talep.IslemTuru == FinansIslemTuru.Odeme ? talep.Tutar : 0,
                Alacak = talep.IslemTuru == FinansIslemTuru.Tahsilat ? talep.Tutar : 0,
                ParaBirimiId = talep.ParaBirimiId, Kur = talep.Kur,
                DovizBorc = talep.IslemTuru == FinansIslemTuru.Odeme ? talep.DovizTutar : 0,
                DovizAlacak = talep.IslemTuru == FinansIslemTuru.Tahsilat ? talep.DovizTutar : 0 };
            _baglam.CariHareketler.Add(cariHareket);
            await YevmiyeEkleAsync(cari, talep, hareketId, tarih, aciklama, false);
            await _baglam.SaveChangesAsync();
            // Seçilen belgeler aynı işlemde kapatılır: kapama başarısız olursa tahsilat da kaydedilmez (yarım kayıt/tekrar giriş olmaz).
            await BelgeleriKapatAsync(cariHareket, talep);
            if (kaynak == "Kasa") (await _baglam.KasaHareketleri.FindAsync(hareketId))!.IlgiliBelgeId = cariHareket.Id;
            else if (kaynak == "Banka") (await _baglam.BankaHareketleri.FindAsync(hareketId))!.IlgiliBelgeId = cariHareket.Id;
            // Portfoy için CekSenet tablosunda IlgiliBelgeId tutmuyoruz (veya gerek yok)
            await _baglam.SaveChangesAsync(); await tx.CommitAsync();
            return KaydaDonustur(kaynak, hareketId, tarih, talep.IslemTuru, talep.OdemeYontemi, FinansIslemDurumu.Tamamlandi, cari, talep.Tutar, aciklama, talep.BelgeNo, talep.BankaAdi, talep.HesapNo, null, talep.ParaBirimiId, talep.Kur, talep.DovizTutar);
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task<IReadOnlyList<TahsilatOdemeKaydi>> GecmisiGetirAsync(FinansIslemTuru? tur = null, string? arama = null)
    {
        var kasalar = await _baglam.KasaHareketleri.Include(x => x.CariKart).Where(x => x.CariKartId != null).ToListAsync();
        var bankalar = await _baglam.BankaHareketleri.Include(x => x.CariKart).Where(x => x.CariKartId != null).ToListAsync();
        var sonuc = kasalar.Select(x => KaydaDonustur("Kasa", x.Id, x.Tarih, x.Tur == HareketTuru.Gelir ? FinansIslemTuru.Tahsilat : FinansIslemTuru.Odeme,
                OdemeYontemi.Nakit, x.IslemDurumu, x.CariKart!, x.Tutar, x.Aciklama, x.Belge, null, null, x.TersKayitId, x.ParaBirimiId, x.Kur, x.DovizTutar))
            .Concat(bankalar.Select(x => KaydaDonustur("Banka", x.Id, x.Tarih, x.Tur == HareketTuru.Gelir ? FinansIslemTuru.Tahsilat : FinansIslemTuru.Odeme,
                x.OdemeYontemi, x.IslemDurumu, x.CariKart!, x.Tutar, x.Aciklama, null, x.BankaAdi, x.HesapNo, x.TersKayitId, x.ParaBirimiId, x.Kur, x.DovizTutar)));
        // Bu ekrandan alınan/verilen çek ve senetler (tahsilat fişi olanlar); iptal edilenler de görünür.
        var cekFisleri = await _baglam.YevmiyeFisleri.AsNoTracking()
            .Where(f => (f.BelgeTipi == "TahsilatOdeme" || f.BelgeTipi == "TahsilatOdemeIptal") && f.Satirlar.Any(s => PortfoyKodlari.Contains(s.Hesap!.HesapKodu)))
            .Select(f => new { f.BelgeId, f.BelgeTipi }).ToListAsync();
        var cekIdleri = cekFisleri.Where(f => f.BelgeTipi == "TahsilatOdeme").Select(f => f.BelgeId).ToHashSet();
        var iptalCekler = cekFisleri.Where(f => f.BelgeTipi == "TahsilatOdemeIptal").Select(f => f.BelgeId).ToHashSet();
        var cekler = await _baglam.CekSenetler.IgnoreQueryFilters([MuhasebeBaglami.SilinmisFiltresi]).AsNoTracking().Include(x => x.CariKart)
            .Where(x => cekIdleri.Contains(x.Id)).ToListAsync();
        sonuc = sonuc.Concat(cekler.Select(x => KaydaDonustur("Portfoy", x.Id, x.DuzenlemeTarihi,
            x.Tur is CekSenetTuru.AlinanCek or CekSenetTuru.AlinanSenet ? FinansIslemTuru.Tahsilat : FinansIslemTuru.Odeme,
            x.Tur is CekSenetTuru.AlinanCek or CekSenetTuru.VerilenCek ? OdemeYontemi.Cek : OdemeYontemi.Senet,
            iptalCekler.Contains(x.Id) ? FinansIslemDurumu.IptalEdildi : FinansIslemDurumu.Tamamlandi,
            x.CariKart!, x.Tutar, x.Aciklama, x.BelgeNo, x.BankaAdi, x.HesapNo, null, x.ParaBirimiId, x.Kur, x.DovizTutar)));
        if (tur.HasValue) sonuc = sonuc.Where(x => x.IslemTuru == tur.Value);
        if (!string.IsNullOrWhiteSpace(arama)) sonuc = sonuc.Where(x => x.CariKod.Contains(arama, StringComparison.OrdinalIgnoreCase) || x.CariUnvan.Contains(arama, StringComparison.OrdinalIgnoreCase) || (x.BelgeNo?.Contains(arama, StringComparison.OrdinalIgnoreCase) ?? false));
        return sonuc.OrderByDescending(x => x.Tarih).ThenByDescending(x => x.HareketId).ToList();
    }

    public async Task IptalEtAsync(string kaynak, int hareketId, string neden)
    {
        if (string.IsNullOrWhiteSpace(neden)) throw new ArgumentException("İptal nedeni zorunludur.");
        if (kaynak is not ("Kasa" or "Banka" or "Portfoy")) throw new ArgumentException("Geçersiz işlem kaynağı.");
        // İptal (para iadesi) bugün olur; belge tarihleri yerel saatle (UTC etiketli) saklanır.
        var simdi = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
        int? kasaIdTers = null, bankaIdTers = null;
        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            int cariId; decimal tutar; DateTime tarih; FinansIslemTuru asilTur; OdemeYontemi yontem; string? banka = null; string? hesap = null; int? cariHareketId;
            if (kaynak == "Portfoy")
            {
                // Yalnız portföyde duran (ciro/tahsil/ödeme görmemiş) çek/senet geri alınabilir.
                var cek = await _baglam.CekSenetler.SingleOrDefaultAsync(x => x.Id == hareketId) ?? throw new KeyNotFoundException("Çek/senet bulunamadı veya zaten iptal edilmiş.");
                asilTur = cek.Tur is CekSenetTuru.AlinanCek or CekSenetTuru.AlinanSenet ? FinansIslemTuru.Tahsilat : FinansIslemTuru.Odeme; YetkiKontrol(asilTur);
                if (cek.Durum != CekSenetDurumu.Portfoyde)
                    throw new InvalidOperationException("Çek/senet portföyden çıkmış (ciro, tahsil, ödeme vb.); önce Çek/Senet ekranından o işlemi geri alın.");
                cariId = cek.CariKartId; tutar = cek.Tutar; tarih = simdi;
                yontem = cek.Tur is CekSenetTuru.AlinanCek or CekSenetTuru.VerilenCek ? OdemeYontemi.Cek : OdemeYontemi.Senet;
                await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, cek.SirketId);
                // Asıl cari kaydı: aynı çeke bağlı, aynı tarih ve tutardaki tahsilat/ödeme satırı.
                var asilCariHareket = await _baglam.CariHareketler.FirstOrDefaultAsync(c => c.IlgiliBelgeId == cek.Id && c.CariKartId == cek.CariKartId &&
                    c.Tarih == cek.DuzenlemeTarihi && (c.HareketTuru == CariHareketTuru.Tahsilat || c.HareketTuru == CariHareketTuru.Odeme) && c.Borc + c.Alacak == cek.Tutar);
                cariHareketId = asilCariHareket?.Id;
                cek.SilindiMi = true;
                cek.SilmeNedeni = $"Tahsilat/ödeme iptali: {neden.Trim()}";
            }
            else if (kaynak == "Kasa")
            {
                var asil = await _baglam.KasaHareketleri.SingleOrDefaultAsync(x => x.Id == hareketId) ?? throw new KeyNotFoundException("İşlem bulunamadı.");
                asilTur = asil.Tur == HareketTuru.Gelir ? FinansIslemTuru.Tahsilat : FinansIslemTuru.Odeme; YetkiKontrol(asilTur);
                if (asil.IslemDurumu != FinansIslemDurumu.Tamamlandi) throw new InvalidOperationException("Bu işlem daha önce iptal edilmiş.");
                if (!asil.CariKartId.HasValue) throw new InvalidOperationException("Bu kasa hareketi bir cari tahsilat/ödeme değil.");
                cariId = asil.CariKartId.Value; tutar = asil.Tutar; tarih = simdi; yontem = OdemeYontemi.Nakit; cariHareketId = asil.IlgiliBelgeId; kasaIdTers = asil.KasaId;
                await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, asil.SirketId);
                if (asil.KasaId.HasValue)
                {
                    await FinansKurallari.KasaKilitleAsync(_baglam, asil.KasaId.Value);
                    if (asil.Tur == HareketTuru.Gelir)
                        await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, await _baglam.Kasalar.SingleAsync(k => k.Id == asil.KasaId.Value), tutar);
                }
                var ters = new KasaHareketi { SirketId = asil.SirketId, KasaId = asil.KasaId, FisTuru = asil.FisTuru, Tarih = tarih, Tur = asil.Tur == HareketTuru.Gelir ? HareketTuru.Gider : HareketTuru.Gelir, Tutar = tutar, CariKartId = cariId, Aciklama = $"İptal: {neden.Trim()}", IslemDurumu = FinansIslemDurumu.TersKayit };
                _baglam.KasaHareketleri.Add(ters); await _baglam.SaveChangesAsync(); asil.IslemDurumu = FinansIslemDurumu.IptalEdildi; asil.TersKayitId = ters.Id; ters.TersKayitId = asil.Id;
            }
            else
            {
                var asil = await _baglam.BankaHareketleri.SingleOrDefaultAsync(x => x.Id == hareketId) ?? throw new KeyNotFoundException("İşlem bulunamadı.");
                asilTur = asil.Tur == HareketTuru.Gelir ? FinansIslemTuru.Tahsilat : FinansIslemTuru.Odeme; YetkiKontrol(asilTur);
                if (asil.IslemDurumu != FinansIslemDurumu.Tamamlandi) throw new InvalidOperationException("Bu işlem daha önce iptal edilmiş.");
                if (!asil.CariKartId.HasValue) throw new InvalidOperationException("Bu banka hareketi bir cari tahsilat/ödeme değil.");
                cariId = asil.CariKartId.Value; tutar = asil.Tutar; tarih = simdi; yontem = asil.OdemeYontemi; banka = asil.BankaAdi; hesap = asil.HesapNo; cariHareketId = asil.IlgiliBelgeId; bankaIdTers = asil.BankaHesabiId;
                await FinansKurallari.DonemAcikOlmaliAsync(_baglam, tarih, asil.SirketId);
                if (asil.BankaHesabiId.HasValue)
                {
                    await FinansKurallari.BankaHesabiKilitleAsync(_baglam, asil.BankaHesabiId.Value);
                    if (asil.Tur == HareketTuru.Gelir)
                        await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, await _baglam.BankaHesaplari.SingleAsync(b => b.Id == asil.BankaHesabiId.Value), tutar);
                }
                var ters = new BankaHareketi { SirketId = asil.SirketId, BankaHesabiId = asil.BankaHesabiId, FisTuru = asil.FisTuru, Tarih = tarih, Tur = asil.Tur == HareketTuru.Gelir ? HareketTuru.Gider : HareketTuru.Gelir, Tutar = tutar, CariKartId = cariId, BankaAdi = banka, HesapNo = hesap, OdemeYontemi = yontem, Aciklama = $"İptal: {neden.Trim()}", IslemDurumu = FinansIslemDurumu.TersKayit };
                _baglam.BankaHareketleri.Add(ters); await _baglam.SaveChangesAsync(); asil.IslemDurumu = FinansIslemDurumu.IptalEdildi; asil.TersKayitId = ters.Id; ters.TersKayitId = asil.Id;
            }
            var cari = await _baglam.CariKartlar.SingleAsync(x => x.Id == cariId);
            var tersTalep = new TahsilatOdemeTalebi(asilTur, yontem, cariId, tutar, tarih, $"İptal: {neden.Trim()}", BankaAdi: banka, HesapNo: hesap,
                KasaId: kasaIdTers, BankaHesabiId: bankaIdTers);
            var tersCari = new CariHareket { CariKartId = cariId, Tarih = tarih, HareketTuru = CariHareketTuru.Diger, Aciklama = tersTalep.Aciklama,
                Borc = asilTur == FinansIslemTuru.Tahsilat ? tutar : 0, Alacak = asilTur == FinansIslemTuru.Odeme ? tutar : 0, IlgiliBelgeId = hareketId };
            _baglam.CariHareketler.Add(tersCari);
            await TersYevmiyeEkleAsync(cari, tersTalep, kaynak, hareketId, tarih);
            await _baglam.SaveChangesAsync();
            if (cariHareketId.HasValue && await _baglam.CariHareketler.FindAsync(cariHareketId.Value) is CariHareket asilCari)
                await IptalEslestirAsync(_baglam, asilCari, tersCari);
            await _baglam.SaveChangesAsync(); await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    private async Task YevmiyeEkleAsync(CariKart cari, TahsilatOdemeTalebi t, int belgeId, DateTime tarih, string aciklama, bool ters)
    {
        var sirketId = _baglam.SirketIdCoz(cari.SirketId);
        // Çek/senet portföye girer (101/121) veya verilen çek/senet olarak izlenir (103/321); kasaya/bankaya değil.
        // Kasa/banka satırı seçilen kasanın/hesabın muhasebe koduna yazılır (POS hesabı 108, kredi hesabı 300 gibi).
        var nakitKodu = t.OdemeYontemi switch
        {
            OdemeYontemi.Nakit => t.KasaId is int kasaId && await _baglam.Kasalar.AsNoTracking().FirstOrDefaultAsync(k => k.Id == kasaId) is Kasa kasa
                ? await FinansMuhasebeKaydi.KasaKoduAsync(_baglam, kasa) : "100",
            OdemeYontemi.Cek => t.IslemTuru == FinansIslemTuru.Tahsilat ? "101" : "103",
            OdemeYontemi.Senet => t.IslemTuru == FinansIslemTuru.Tahsilat ? "121" : "321",
            _ => t.BankaHesabiId is int hesapId && await _baglam.BankaHesaplari.AsNoTracking().FirstOrDefaultAsync(b => b.Id == hesapId) is BankaHesabi hesap
                ? await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, hesap) : "102"
        };
        var cariKodu = t.IslemTuru == FinansIslemTuru.Tahsilat ? "120" : "320";
        var hesaplar = new Dictionary<string, (int Id, string HesapKodu)>
        {
            [nakitKodu] = (await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(_baglam, sirketId, nakitKodu), nakitKodu),
            [cariKodu] = (await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(_baglam, sirketId, cariKodu), cariKodu)
        };
        var tahsilatYon = t.IslemTuru == FinansIslemTuru.Tahsilat;
        if (ters) tahsilatYon = !tahsilatYon;
        _baglam.YevmiyeFisleri.Add(new YevmiyeFis { SirketId = sirketId, FisNo = $"TO-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), Tarih = tarih,
            BelgeTipi = ters ? "TahsilatOdemeIptal" : "TahsilatOdeme", BelgeId = belgeId, Aciklama = aciklama, Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = tahsilatYon
                ? [new() { HesapId = hesaplar[nakitKodu].Id, Borc = t.Tutar, Aciklama = aciklama }, new() { HesapId = hesaplar[cariKodu].Id, Alacak = t.Tutar, Aciklama = aciklama, CariKartId = cari.Id }]
                : [new() { HesapId = hesaplar[cariKodu].Id, Borc = t.Tutar, Aciklama = aciklama, CariKartId = cari.Id }, new() { HesapId = hesaplar[nakitKodu].Id, Alacak = t.Tutar, Aciklama = aciklama }] });
    }

    public async Task<IReadOnlyList<Kasa>> AktifKasalariGetirAsync() =>
        await _baglam.Kasalar.AsNoTracking().Where(k => k.AktifMi).OrderBy(k => k.Id).ToListAsync();

    public async Task<IReadOnlyList<BankaHesabi>> AktifBankaHesaplariniGetirAsync() =>
        await _baglam.BankaHesaplari.AsNoTracking().Where(b => b.AktifMi).OrderBy(b => b.Id).ToListAsync();

    private async Task<int> KasaBelirleAsync(int? kasaId)
    {
        if (kasaId is > 0) return kasaId.Value;
        var aktifler = await _baglam.Kasalar.Where(k => k.AktifMi).Select(k => k.Id).Take(2).ToListAsync();
        return aktifler.Count switch
        {
            1 => aktifler[0],
            0 => throw new InvalidOperationException("Tanımlı aktif kasa yok. Önce Kasa ekranından bir kasa tanımlayın."),
            _ => throw new ArgumentException("Nakit işlem için kasa seçiniz.")
        };
    }

    private async Task<int> BankaHesabiBelirleAsync(int? hesapId)
    {
        if (hesapId is > 0) return hesapId.Value;
        var aktifler = await _baglam.BankaHesaplari.Where(b => b.AktifMi).Select(b => b.Id).Take(2).ToListAsync();
        return aktifler.Count switch
        {
            1 => aktifler[0],
            0 => throw new InvalidOperationException("Tanımlı aktif banka hesabı yok. Önce Banka ekranından bir hesap tanımlayın."),
            _ => throw new ArgumentException("Banka/kart işlemi için banka hesabı seçiniz.")
        };
    }

    // Tahsilat müşterinin borç belgelerini (satış faturası vb.), ödeme tedarikçiye olan alacak belgelerini kapatır.
    private async Task BelgeleriKapatAsync(CariHareket odeme, TahsilatOdemeTalebi talep)
    {
        if (talep.KapatilacakCariHareketIdleri is not { Count: > 0 } idler) return;
        var kalan = talep.Tutar;
        foreach (var id in idler.Distinct())
        {
            if (kalan <= 0) break;
            var belge = await _baglam.CariHareketler.SingleOrDefaultAsync(x => x.Id == id)
                ?? throw new KeyNotFoundException("Kapatılacak belge bulunamadı.");
            if (belge.CariKartId != odeme.CariKartId)
                throw new InvalidOperationException("Kapatılacak belge seçilen cariye ait değil.");
            var borcBelgesi = belge.Borc > 0;
            if (borcBelgesi != (talep.IslemTuru == FinansIslemTuru.Tahsilat))
                throw new InvalidOperationException(talep.IslemTuru == FinansIslemTuru.Tahsilat
                    ? "Tahsilatla yalnız carinin borç belgeleri (satış faturası vb.) kapatılabilir."
                    : "Ödemeyle yalnız carinin alacak belgeleri (alış faturası vb.) kapatılabilir.");
            var acik = (borcBelgesi ? belge.Borc : belge.Alacak) - belge.KapaliTutar;
            if (acik <= 0) throw new InvalidOperationException($"'{belge.BelgeNo ?? belge.Aciklama}' belgesi zaten kapalı. Listeyi yenileyin.");
            var tutar = Math.Min(acik, kalan);
            _baglam.BelgeKapamalar.Add(new BelgeKapama { OdenenCariHareketId = odeme.Id, FaturaCariHareketId = belge.Id, KapananTutar = tutar, KapamaTarihi = DateTime.UtcNow });
            belge.KapaliTutar += tutar;
            odeme.KapaliTutar += tutar;
            kalan -= tutar;
        }
        await _baglam.SaveChangesAsync();
    }

    // İptal edilen tahsilat/ödemenin belge kapamaları çözülür (fatura yeniden açık olur) ve asıl kayıt ile
    // ters kaydı birbirini kapatır; böylece ikisi de açık belge listesinde görünmez.
    internal static async Task IptalEslestirAsync(MuhasebeBaglami baglam, CariHareket asil, CariHareket ters)
    {
        foreach (var kapama in await baglam.BelgeKapamalar.Where(k => k.OdenenCariHareketId == asil.Id && !k.Silindi).ToListAsync())
        {
            if (await baglam.CariHareketler.FindAsync(kapama.FaturaCariHareketId) is CariHareket belge)
                belge.KapaliTutar -= kapama.KapananTutar;
            asil.KapaliTutar -= kapama.KapananTutar;
            kapama.Silindi = true;
            kapama.SilinmeTarihi = DateTime.UtcNow;
        }
        var tutar = asil.Borc + asil.Alacak;
        baglam.BelgeKapamalar.Add(new BelgeKapama { OdenenCariHareketId = ters.Id, FaturaCariHareketId = asil.Id, KapananTutar = tutar, KapamaTarihi = DateTime.UtcNow });
        asil.KapaliTutar = tutar;
        ters.KapaliTutar = tutar;
    }

    // Bu özellikten önce iptal edilmiş tahsilat/ödemelerin asıl ve ters cari kayıtlarını eşleştirir (tekrar çalışması güvenli).
    public static async Task<int> EskiIptalleriEslestirAsync(MuhasebeBaglami baglam, CancellationToken cancellationToken = default)
    {
        var iptaller = (await baglam.KasaHareketleri.AsNoTracking()
                .Where(h => h.IslemDurumu == FinansIslemDurumu.IptalEdildi && h.CariKartId != null && h.IlgiliBelgeId != null)
                .Select(h => new { h.Id, CariKartId = h.CariKartId!.Value, CariHareketId = h.IlgiliBelgeId!.Value, h.Tutar }).ToListAsync(cancellationToken))
            .Concat(await baglam.BankaHareketleri.AsNoTracking()
                .Where(h => h.IslemDurumu == FinansIslemDurumu.IptalEdildi && h.CariKartId != null && h.IlgiliBelgeId != null)
                .Select(h => new { h.Id, CariKartId = h.CariKartId!.Value, CariHareketId = h.IlgiliBelgeId!.Value, h.Tutar }).ToListAsync(cancellationToken))
            .ToList();
        var sayi = 0;
        foreach (var iptal in iptaller)
        {
            var asil = await baglam.CariHareketler.SingleOrDefaultAsync(c => c.Id == iptal.CariHareketId && c.CariKartId == iptal.CariKartId &&
                (c.HareketTuru == CariHareketTuru.Tahsilat || c.HareketTuru == CariHareketTuru.Odeme) && c.KapaliTutar < c.Borc + c.Alacak, cancellationToken);
            if (asil == null || asil.Borc + asil.Alacak != iptal.Tutar) continue;
            var ters = await baglam.CariHareketler.FirstOrDefaultAsync(c => c.IlgiliBelgeId == iptal.Id && c.CariKartId == iptal.CariKartId &&
                c.HareketTuru == CariHareketTuru.Diger && c.KapaliTutar == 0 && c.Aciklama.StartsWith("İptal:") &&
                c.Borc == asil.Alacak && c.Alacak == asil.Borc, cancellationToken);
            if (ters == null) continue;
            await IptalEslestirAsync(baglam, asil, ters);
            await baglam.SaveChangesAsync(cancellationToken);
            sayi++;
        }
        return sayi;
    }


    private static readonly string[] PortfoyKodlari = ["101", "103", "121", "321"];

    // İptal fişi asıl tahsilat fişinin birebir tersidir (aynı hesaplar, borç/alacak yer değiştirir).
    // Aynı BelgeId'yi kasa, banka ve çek fişleri paylaşabildiği için fiş, kaynağın hesap satırından ayırt edilir.
    private async Task TersYevmiyeEkleAsync(CariKart cari, TahsilatOdemeTalebi tersTalep, string kaynak, int belgeId, DateTime tarih)
    {
        var adaylar = await _baglam.YevmiyeFisleri.AsNoTracking().Include(f => f.Satirlar).ThenInclude(s => s.Hesap)
            .Where(f => f.BelgeTipi == "TahsilatOdeme" && f.BelgeId == belgeId && f.Durum == YevmiyeDurumu.Onaylandi).ToListAsync();
        bool KaynakSatiri(string kod) => kaynak switch
        {
            "Kasa" => kod.StartsWith("100", StringComparison.Ordinal),
            "Portfoy" => PortfoyKodlari.Contains(kod),
            _ => kod.StartsWith("102", StringComparison.Ordinal) || kod.StartsWith("108", StringComparison.Ordinal) || kod.StartsWith("300", StringComparison.Ordinal)
        };
        var asil = adaylar.FirstOrDefault(f => f.Satirlar.Any(s => KaynakSatiri(s.Hesap!.HesapKodu)));
        if (asil == null)
        {
            await YevmiyeEkleAsync(cari, tersTalep, belgeId, tarih, tersTalep.Aciklama, true);
            return;
        }
        _baglam.YevmiyeFisleri.Add(new YevmiyeFis { SirketId = asil.SirketId, FisNo = $"TO-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), Tarih = tarih,
            BelgeTipi = "TahsilatOdemeIptal", BelgeId = belgeId, Aciklama = tersTalep.Aciklama, Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = asil.Satirlar.Select(s => new YevmiyeSatiri { HesapId = s.HesapId, Borc = s.Alacak, Alacak = s.Borc, CariKartId = s.CariKartId, Aciklama = tersTalep.Aciklama }).ToList() });
    }

    private void YetkiKontrol(FinansIslemTuru tur) => _kullanici.YetkiKontrolEt(x => tur == FinansIslemTuru.Tahsilat ? x.TahsilatYap : x.OdemeYap, tur == FinansIslemTuru.Tahsilat ? "Tahsilat" : "Ödeme");
    private static void Dogrula(TahsilatOdemeTalebi t)
    {
        if (t.CariKartId <= 0) throw new ArgumentException("Cari seçimi zorunludur.");
        if (t.Tutar <= 0 || t.Tutar > EnBuyukTutar) throw new ArgumentException("Tutar sıfırdan büyük olmalıdır.");
        // Çek/Senet için zorunlu alan
        if (t.OdemeYontemi is OdemeYontemi.Cek or OdemeYontemi.Senet && string.IsNullOrWhiteSpace(t.CekSenetNo))
            throw new ArgumentException("Çek/Senet numarası zorunludur.");
    }
    private static TahsilatOdemeKaydi KaydaDonustur(string kaynak, int id, DateTime tarih, FinansIslemTuru tur, OdemeYontemi yontem, FinansIslemDurumu durum, CariKart cari, decimal tutar, string aciklama, string? belge, string? banka, string? hesap, int? tersId, int? paraBirimiId = null, decimal kur = 1, decimal dovizTutar = 0)
        => new() { Kaynak = kaynak, HareketId = id, Tarih = tarih, IslemTuru = tur, OdemeYontemi = yontem, Durum = durum, CariKartId = cari.Id, CariKod = cari.Kod, CariUnvan = cari.Unvan, Tutar = tutar, Aciklama = aciklama, BelgeNo = belge, BankaAdi = banka, HesapNo = hesap, TersKayitId = tersId, ParaBirimiId = paraBirimiId, Kur = kur, DovizTutar = dovizTutar };
}
