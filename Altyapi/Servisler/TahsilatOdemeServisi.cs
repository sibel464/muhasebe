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
                if (talep.KasaId is > 0)
                {
                    await FinansKurallari.KasaKilitleAsync(_baglam, talep.KasaId.Value);
                    var kasa = await _baglam.Kasalar.SingleOrDefaultAsync(k => k.Id == talep.KasaId.Value) ?? throw new KeyNotFoundException("Kasa bulunamadı.");
                    if (!kasa.AktifMi) throw new InvalidOperationException($"'{kasa.Ad}' kasası pasif durumda.");
                    if (talep.IslemTuru == FinansIslemTuru.Odeme) await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, kasa, talep.Tutar);
                }
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
                    OdemeYontemi.KrediKarti => $"Taksit:{talep.TaksitSayisi ?? 1} Komisyon:%{talep.KomisyonOrani}",
                    _ => null
                };
                var tamAciklama = notlar != null ? $"{aciklama} [{notlar}]" : aciklama;
                if (talep.BankaHesabiId is > 0)
                {
                    await FinansKurallari.BankaHesabiKilitleAsync(_baglam, talep.BankaHesabiId.Value);
                    var hesap = await _baglam.BankaHesaplari.SingleOrDefaultAsync(b => b.Id == talep.BankaHesabiId.Value) ?? throw new KeyNotFoundException("Banka hesabı bulunamadı.");
                    if (!hesap.AktifMi) throw new InvalidOperationException($"'{hesap.Ad}' banka hesabı pasif durumda.");
                    if (talep.IslemTuru == FinansIslemTuru.Odeme) await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, hesap, talep.Tutar);
                }
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
        if (tur.HasValue) sonuc = sonuc.Where(x => x.IslemTuru == tur.Value);
        if (!string.IsNullOrWhiteSpace(arama)) sonuc = sonuc.Where(x => x.CariKod.Contains(arama, StringComparison.OrdinalIgnoreCase) || x.CariUnvan.Contains(arama, StringComparison.OrdinalIgnoreCase) || (x.BelgeNo?.Contains(arama, StringComparison.OrdinalIgnoreCase) ?? false));
        return sonuc.OrderByDescending(x => x.Tarih).ThenByDescending(x => x.HareketId).ToList();
    }

    public async Task IptalEtAsync(string kaynak, int hareketId, string neden)
    {
        if (string.IsNullOrWhiteSpace(neden)) throw new ArgumentException("İptal nedeni zorunludur.");
        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            int cariId; decimal tutar; DateTime tarih; FinansIslemTuru asilTur; OdemeYontemi yontem; string? banka = null; string? hesap = null; int? cariHareketId;
            if (kaynak == "Kasa")
            {
                var asil = await _baglam.KasaHareketleri.SingleOrDefaultAsync(x => x.Id == hareketId) ?? throw new KeyNotFoundException("İşlem bulunamadı.");
                asilTur = asil.Tur == HareketTuru.Gelir ? FinansIslemTuru.Tahsilat : FinansIslemTuru.Odeme; YetkiKontrol(asilTur);
                if (asil.IslemDurumu != FinansIslemDurumu.Tamamlandi) throw new InvalidOperationException("Bu işlem daha önce iptal edilmiş.");
                if (!asil.CariKartId.HasValue) throw new InvalidOperationException("Bu kasa hareketi bir cari tahsilat/ödeme değil.");
                cariId = asil.CariKartId.Value; tutar = asil.Tutar; tarih = DateTime.UtcNow; yontem = OdemeYontemi.Nakit; cariHareketId = asil.IlgiliBelgeId;
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
                cariId = asil.CariKartId.Value; tutar = asil.Tutar; tarih = DateTime.UtcNow; yontem = asil.OdemeYontemi; banka = asil.BankaAdi; hesap = asil.HesapNo; cariHareketId = asil.IlgiliBelgeId;
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
            var tersTalep = new TahsilatOdemeTalebi(asilTur, yontem, cariId, tutar, tarih, $"İptal: {neden.Trim()}", BankaAdi: banka, HesapNo: hesap);
            _baglam.CariHareketler.Add(new CariHareket { CariKartId = cariId, Tarih = tarih, HareketTuru = CariHareketTuru.Diger, Aciklama = tersTalep.Aciklama,
                Borc = asilTur == FinansIslemTuru.Tahsilat ? tutar : 0, Alacak = asilTur == FinansIslemTuru.Odeme ? tutar : 0, IlgiliBelgeId = hareketId });
            if (cariHareketId.HasValue) { var ch = await _baglam.CariHareketler.FindAsync(cariHareketId.Value); if (ch != null) ch.GuncellemeTarihi = tarih; }
            await YevmiyeEkleAsync(cari, tersTalep, hareketId, tarih, tersTalep.Aciklama, true);
            await _baglam.SaveChangesAsync(); await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    private async Task YevmiyeEkleAsync(CariKart cari, TahsilatOdemeTalebi t, int belgeId, DateTime tarih, string aciklama, bool ters)
    {
        var sirketId = cari.SirketId ?? MuhasebeBaglami.VarsayilanSirketId;
        var nakitKodu = t.OdemeYontemi == OdemeYontemi.Nakit ? "100" : "102";
        var cariKodu = t.IslemTuru == FinansIslemTuru.Tahsilat ? "120" : "320";
        var hesaplar = await _baglam.HesapPlanlari.Where(x => x.SirketId == sirketId && (x.HesapKodu == nakitKodu || x.HesapKodu == cariKodu) && x.AktifMi).ToDictionaryAsync(x => x.HesapKodu);
        if (!hesaplar.ContainsKey(nakitKodu) || !hesaplar.ContainsKey(cariKodu)) throw new InvalidOperationException("Kasa/banka veya cari muhasebe hesabı bulunamadı.");
        var tahsilatYon = t.IslemTuru == FinansIslemTuru.Tahsilat;
        if (ters) tahsilatYon = !tahsilatYon;
        _baglam.YevmiyeFisleri.Add(new YevmiyeFis { SirketId = sirketId, FisNo = $"TO-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), Tarih = tarih,
            BelgeTipi = ters ? "TahsilatOdemeIptal" : "TahsilatOdeme", BelgeId = belgeId, Aciklama = aciklama, Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = tahsilatYon
                ? [new() { HesapId = hesaplar[nakitKodu].Id, Borc = t.Tutar, Aciklama = aciklama }, new() { HesapId = hesaplar[cariKodu].Id, Alacak = t.Tutar, Aciklama = aciklama }]
                : [new() { HesapId = hesaplar[cariKodu].Id, Borc = t.Tutar, Aciklama = aciklama }, new() { HesapId = hesaplar[nakitKodu].Id, Alacak = t.Tutar, Aciklama = aciklama }] });
    }

    private void YetkiKontrol(FinansIslemTuru tur) => _kullanici.YetkiKontrolEt(x => tur == FinansIslemTuru.Tahsilat ? x.TahsilatYap : x.OdemeYap, tur == FinansIslemTuru.Tahsilat ? "Tahsilat" : "Ödeme");
    private static void Dogrula(TahsilatOdemeTalebi t)
    {
        if (t.CariKartId <= 0) throw new ArgumentException("Cari seçimi zorunludur.");
        if (t.Tutar <= 0 || t.Tutar > EnBuyukTutar) throw new ArgumentException("Tutar sıfırdan büyük olmalıdır.");
        // Çek/Senet için zorunlu alan
        if (t.OdemeYontemi is OdemeYontemi.Cek or OdemeYontemi.Senet && string.IsNullOrWhiteSpace(t.CekSenetNo))
            throw new ArgumentException("Çek/Senet numarası zorunludur.");
        // Banka/Havale için zorunlu alanlar (kredi kartı, çek, senet kendi alanlarını kullanır)
        if (t.OdemeYontemi is OdemeYontemi.Banka or OdemeYontemi.Havale or OdemeYontemi.Virman
            && string.IsNullOrWhiteSpace(t.BankaAdi))
            throw new ArgumentException("Banka/Havale işlemlerinde banka adı zorunludur.");
    }
    private static TahsilatOdemeKaydi KaydaDonustur(string kaynak, int id, DateTime tarih, FinansIslemTuru tur, OdemeYontemi yontem, FinansIslemDurumu durum, CariKart cari, decimal tutar, string aciklama, string? belge, string? banka, string? hesap, int? tersId, int? paraBirimiId = null, decimal kur = 1, decimal dovizTutar = 0)
        => new() { Kaynak = kaynak, HareketId = id, Tarih = tarih, IslemTuru = tur, OdemeYontemi = yontem, Durum = durum, CariKartId = cari.Id, CariKod = cari.Kod, CariUnvan = cari.Unvan, Tutar = tutar, Aciklama = aciklama, BelgeNo = belge, BankaAdi = banka, HesapNo = hesap, TersKayitId = tersId, ParaBirimiId = paraBirimiId, Kur = kur, DovizTutar = dovizTutar };
}
