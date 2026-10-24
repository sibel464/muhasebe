using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Altyapi.Veri;
using Satir = Altyapi.Servisler.FinansMuhasebeKaydi.Satir;

namespace Altyapi.Servisler;

public class KasaServisi : IKasaServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public KasaServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    private IKullaniciServisi Oturum => _kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama");

    private void GoruntulemeYetkisi() =>
        Oturum.YetkiKontrolEt(y => y.KasaGoruntule || y.KasaYonet, "Kasa Görüntüleme");

    public async Task<List<KasaHareketi>> TumunuGetirAsync()
    {
        GoruntulemeYetkisi();
        return await _baglam.KasaHareketleri
            .AsNoTracking()
            .Include(k => k.CariKart)
            .Include(k => k.Kasa)
            .OrderByDescending(k => k.Tarih)
            .ThenByDescending(k => k.Id)
            .ToListAsync();
    }

    public async Task<List<KasaHareketi>> TarihAraligindaGetirAsync(DateTime baslangic, DateTime bitis)
    {
        GoruntulemeYetkisi();
        var basUtc = DateTime.SpecifyKind(baslangic.Date, DateTimeKind.Utc);
        var bitUtc = DateTime.SpecifyKind(bitis.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        return await _baglam.KasaHareketleri
            .AsNoTracking()
            .Include(k => k.CariKart)
            .Include(k => k.Kasa)
            .Where(k => k.Tarih >= basUtc && k.Tarih <= bitUtc)
            .OrderByDescending(k => k.Tarih)
            .ThenByDescending(k => k.Id)
            .ToListAsync();
    }

    public async Task<List<KasaHareketi>> KasaHareketleriGetirAsync(int kasaId)
    {
        GoruntulemeYetkisi();
        return await _baglam.KasaHareketleri
            .AsNoTracking()
            .Include(k => k.CariKart)
            .Include(k => k.Kasa)
            .Where(k => k.KasaId == kasaId)
            .OrderByDescending(k => k.Tarih)
            .ThenByDescending(k => k.Id)
            .ToListAsync();
    }

    public async Task<List<KasaHareketi>> CariHareketleriGetirAsync(int cariKartId)
    {
        GoruntulemeYetkisi();
        return await _baglam.KasaHareketleri
            .AsNoTracking()
            .Include(k => k.CariKart)
            .Include(k => k.Kasa)
            .Where(k => k.CariKartId == cariKartId)
            .OrderByDescending(k => k.Tarih)
            .ThenByDescending(k => k.Id)
            .ToListAsync();
    }

    public async Task<KasaHareketi?> GetirByIdAsync(int id)
    {
        GoruntulemeYetkisi();
        return await _baglam.KasaHareketleri
            .AsNoTracking()
            .Include(k => k.CariKart)
            .Include(k => k.Kasa)
            .FirstOrDefaultAsync(k => k.Id == id);
    }

    // Serbest kasa fişi: yalnızca cari dışı gelir, gider ve açılış/devir fişleri bu yoldan girilir.
    // Transfer ve sayım fişleri kendi metotlarıyla, cari tahsilat/ödeme ise cari hareketiyle birlikte oluşturulur.
    public async Task<KasaHareketi> EkleAsync(KasaHareketi hareket)
    {
        if (hareket.CariKartId is > 0 && hareket.FisTuru is KasaFisTuru.Tahsilat or KasaFisTuru.Odeme)
        {
            return hareket.FisTuru == KasaFisTuru.Tahsilat
                ? await TahsilatEkleAsync(hareket.KasaId ?? 0, hareket.CariKartId.Value, hareket.Tutar, hareket.Aciklama, hareket.MakbuzNo ?? hareket.Belge, hareket.Tarih, hareket.DovizTutar, hareket.Kur)
                : await OdemeEkleAsync(hareket.KasaId ?? 0, hareket.CariKartId.Value, hareket.Tutar, hareket.Aciklama, hareket.MakbuzNo ?? hareket.Belge, hareket.Tarih, hareket.DovizTutar, hareket.Kur);
        }

        switch (hareket.FisTuru)
        {
            case KasaFisTuru.Gelir:
                Oturum.YetkiKontrolEt(y => y.GelirEkle || y.KasaYonet, "Kasa Gelir Fişi");
                hareket.Tur = HareketTuru.Gelir;
                break;
            case KasaFisTuru.Gider:
                Oturum.YetkiKontrolEt(y => y.GiderEkle || y.KasaYonet, "Kasa Gider Fişi");
                hareket.Tur = HareketTuru.Gider;
                break;
            case KasaFisTuru.AcilisDevir:
                Oturum.YetkiKontrolEt(y => y.KasaYonet, "Kasa Açılış / Devir Fişi");
                hareket.Tur = HareketTuru.Gelir;
                break;
            case KasaFisTuru.Tahsilat:
            case KasaFisTuru.Odeme:
                throw new ArgumentException("Cari tahsilat ve ödeme fişleri için cari kart seçilmelidir.");
            default:
                throw new ArgumentException("Virman, banka transferi ve sayım farkı fişleri ilgili işlem ekranından yapılmalıdır.");
        }

        if (hareket.CariKartId.HasValue)
            throw new ArgumentException("Cari kart yalnızca tahsilat ve ödeme fişlerinde seçilebilir.");

        if (hareket.DovizTutar <= 0)
            FinansKurallari.TutarDogrula(hareket.Tutar, "Kasa hareket");
        hareket.Aciklama = FinansKurallari.AciklamaDogrula(hareket.Aciklama);
        hareket.Tarih = FinansKurallari.TarihNormalizeEt(hareket.Tarih);

        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var kasa = hareket.KasaId is > 0
                ? await AktifKasaGetirVeKilitleAsync(hareket.KasaId.Value)
                : await VarsayilanKasaGetirVeyaOlusturAsync();
            if (hareket.KasaId is not > 0)
                await FinansKurallari.KasaKilitleAsync(_baglam, kasa.Id);
            (hareket.Tutar, hareket.ParaBirimiId, hareket.Kur, hareket.DovizTutar) = await FinansKurallari.HesapTutariAsync(
                _baglam, kasa.Ad, kasa.ParaBirimi, hareket.Tutar, hareket.DovizTutar, hareket.Kur, hareket.ParaBirimiId);
            FinansKurallari.TutarDogrula(hareket.Tutar, "Kasa hareket");

            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, hareket.Tarih, kasa.SirketId);
            if (hareket.Tur == HareketTuru.Gider)
                await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, kasa, hareket.Tutar, hareket.DovizTutar);

            hareket.Id = 0;
            hareket.KasaId = kasa.Id;
            hareket.SirketId ??= kasa.SirketId;
            hareket.IlgiliBelgeId = null;
            hareket.TersKayitId = null;
            hareket.IslemDurumu = FinansIslemDurumu.Tamamlandi;
            hareket.OlusturmaTarihi = DateTime.UtcNow;
            hareket.FisNo = string.IsNullOrWhiteSpace(hareket.FisNo) ? FinansKurallari.FisNoUret("KSF") : hareket.FisNo.Trim();

            _baglam.KasaHareketleri.Add(hareket);
            await _baglam.SaveChangesAsync();

            // Gelir: 100 / 649, Gider: 770 / 100, Açılış: 100 / 500
            var kasaKodu = await FinansMuhasebeKaydi.KasaKoduAsync(_baglam, kasa);
            var (karsiKod, kasaBorclu) = hareket.FisTuru switch
            {
                KasaFisTuru.Gider => ("770", false),
                KasaFisTuru.AcilisDevir => ("500", true),
                _ => ("649", true)
            };
            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, hareket.SirketId, FinansMuhasebeKaydi.KasaBelgeTipi, hareket.Id, hareket.Tarih, hareket.Aciklama, hareket.MakbuzNo ?? hareket.FisNo,
                kasaBorclu ? new Satir(kasaKodu, hareket.Tutar, 0) : new Satir(karsiKod, hareket.Tutar, 0),
                kasaBorclu ? new Satir(karsiKod, 0, hareket.Tutar) : new Satir(kasaKodu, 0, hareket.Tutar));

            await transaction.CommitAsync();
            return hareket;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public Task<KasaHareketi> TahsilatEkleAsync(int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null)
    {
        return TahsilatEkleAsync(0, cariKartId, tutar, aciklama, belgeNo, tarih);
    }

    public Task<KasaHareketi> TahsilatEkleAsync(int kasaId, int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null, decimal dovizTutar = 0, decimal kur = 0)
    {
        Oturum.YetkiKontrolEt(y => y.TahsilatYap || y.KasaYonet, "Kasa Tahsilat Girişi");
        return CariIslemEkleAsync(kasaId, cariKartId, tutar, aciklama, belgeNo, tarih, tahsilat: true, dovizTutar, kur);
    }

    public Task<KasaHareketi> OdemeEkleAsync(int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null)
    {
        return OdemeEkleAsync(0, cariKartId, tutar, aciklama, belgeNo, tarih);
    }

    public Task<KasaHareketi> OdemeEkleAsync(int kasaId, int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null, decimal dovizTutar = 0, decimal kur = 0)
    {
        Oturum.YetkiKontrolEt(y => y.OdemeYap || y.KasaYonet, "Kasa Ödeme Çıkışı");
        return CariIslemEkleAsync(kasaId, cariKartId, tutar, aciklama, belgeNo, tarih, tahsilat: false, dovizTutar, kur);
    }

    private async Task<KasaHareketi> CariIslemEkleAsync(int kasaId, int cariKartId, decimal tutar, string aciklama, string? belgeNo, DateTime? tarih, bool tahsilat,
        decimal dovizTutar = 0, decimal kur = 0)
    {
        if (dovizTutar <= 0)
            FinansKurallari.TutarDogrula(tutar, tahsilat ? "Tahsilat" : "Ödeme");
        if (cariKartId <= 0)
            throw new ArgumentException("Geçerli bir cari kart seçilmelidir.");
        var temizAciklama = FinansKurallari.AciklamaDogrula(aciklama);
        var temizBelgeNo = string.IsNullOrWhiteSpace(belgeNo) ? null : belgeNo.Trim();
        var islemTarihi = FinansKurallari.TarihNormalizeEt(tarih);

        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var kasa = kasaId > 0
                ? await AktifKasaGetirVeKilitleAsync(kasaId)
                : await VarsayilanKasaGetirVeyaOlusturAsync();
            if (kasaId <= 0)
                await FinansKurallari.KasaKilitleAsync(_baglam, kasa.Id);
            // Cari TL karşılığıyla işlenir; döviz tutarı ve kur kasa hareketinde kalır.
            (tutar, var paraBirimiId, kur, dovizTutar) = await FinansKurallari.HesapTutariAsync(_baglam, kasa.Ad, kasa.ParaBirimi, tutar, dovizTutar, kur);
            FinansKurallari.TutarDogrula(tutar, tahsilat ? "Tahsilat" : "Ödeme");

            var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == cariKartId)
                ?? throw new KeyNotFoundException("Cari kart bulunamadı.");
            if (!cari.Aktif)
                throw new InvalidOperationException($"Pasif durumdaki cari karta {(tahsilat ? "tahsilat" : "ödeme")} girilemez.");

            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, islemTarihi, kasa.SirketId ?? cari.SirketId);
            if (!tahsilat)
                await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, kasa, tutar, dovizTutar);

            var kasaHareketi = new KasaHareketi
            {
                SirketId = kasa.SirketId ?? cari.SirketId,
                KasaId = kasa.Id,
                Tarih = islemTarihi,
                Tur = tahsilat ? HareketTuru.Gelir : HareketTuru.Gider,
                FisTuru = tahsilat ? KasaFisTuru.Tahsilat : KasaFisTuru.Odeme,
                FisNo = FinansKurallari.FisNoUret("KSF"),
                MakbuzNo = temizBelgeNo,
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
                Aciklama = temizAciklama,
                Belge = temizBelgeNo,
                CariKartId = cariKartId,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.KasaHareketleri.Add(kasaHareketi);
            await _baglam.SaveChangesAsync();

            // Tahsilatta müşterinin alacağı, ödemede tedarikçinin borcu işlenir.
            var cariHareket = new CariHareket
            {
                CariKartId = cariKartId,
                Tarih = islemTarihi,
                HareketTuru = tahsilat ? CariHareketTuru.Tahsilat : CariHareketTuru.Odeme,
                Aciklama = temizAciklama,
                BelgeNo = temizBelgeNo,
                Borc = tahsilat ? 0 : tutar,
                Alacak = tahsilat ? tutar : 0,
                IlgiliBelgeId = kasaHareketi.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.CariHareketler.Add(cariHareket);
            await _baglam.SaveChangesAsync();

            // Tahsilat: 100 / 120, Ödeme: 320 / 100
            var kasaKodu = await FinansMuhasebeKaydi.KasaKoduAsync(_baglam, kasa);
            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, kasaHareketi.SirketId, FinansMuhasebeKaydi.KasaBelgeTipi, kasaHareketi.Id, islemTarihi, temizAciklama, temizBelgeNo ?? kasaHareketi.FisNo,
                tahsilat ? new Satir(kasaKodu, tutar, 0) : new Satir("320", tutar, 0, cariKartId),
                tahsilat ? new Satir("120", 0, tutar, cariKartId) : new Satir(kasaKodu, 0, tutar));

            kasaHareketi.IlgiliBelgeId = cariHareket.Id;
            await _baglam.SaveChangesAsync();

            await transaction.CommitAsync();
            return kasaHareketi;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> KasaVirmanYapAsync(int kaynakKasaId, int hedefKasaId, decimal tutar, string aciklama, DateTime? tarih = null)
    {
        Oturum.YetkiKontrolEt(y => y.KasaYonet, "Kasalar Arası Virman");

        if (kaynakKasaId == hedefKasaId)
            throw new ArgumentException("Kaynak kasa ile hedef kasa aynı olamaz.");
        FinansKurallari.TutarDogrula(tutar, "Virman");
        var temizAciklama = string.IsNullOrWhiteSpace(aciklama) ? "Kasalar arası virman" : FinansKurallari.AciklamaDogrula(aciklama);
        var islemTarihi = FinansKurallari.TarihNormalizeEt(tarih);

        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            // Kilitler her zaman küçük Id'den başlayarak alınır; ters yönlü eşzamanlı virmanlar kilitlenmez.
            foreach (var id in new[] { kaynakKasaId, hedefKasaId }.Order())
                await FinansKurallari.KasaKilitleAsync(_baglam, id);

            var kaynakKasa = await AktifKasaGetirAsync(kaynakKasaId);
            var hedefKasa = await AktifKasaGetirAsync(hedefKasaId);
            if (!string.Equals(kaynakKasa.ParaBirimi, hedefKasa.ParaBirimi, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Farklı para birimindeki kasalar arasında virman yapılamaz.");

            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, islemTarihi, kaynakKasa.SirketId);
            var (tlTutar, paraBirimiId, kur, dovizTutar) = await FinansKurallari.KasaTransferTutariAsync(_baglam, kaynakKasa, tutar);
            await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, kaynakKasa, tutar, dovizTutar);
            tutar = tlTutar;

            var fisNo = FinansKurallari.FisNoUret("VRM");

            var cikis = new KasaHareketi
            {
                SirketId = kaynakKasa.SirketId,
                KasaId = kaynakKasaId,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gider,
                FisTuru = KasaFisTuru.KasaVirman,
                FisNo = fisNo,
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
                Aciklama = $"Virman Çıkış -> {hedefKasa.Ad}: {temizAciklama}",
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.KasaHareketleri.Add(cikis);
            await _baglam.SaveChangesAsync();

            var giris = new KasaHareketi
            {
                SirketId = hedefKasa.SirketId,
                KasaId = hedefKasaId,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                FisTuru = KasaFisTuru.KasaVirman,
                FisNo = fisNo,
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
                Aciklama = $"Virman Giriş <- {kaynakKasa.Ad}: {temizAciklama}",
                TersKayitId = cikis.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.KasaHareketleri.Add(giris);
            await _baglam.SaveChangesAsync();

            cikis.TersKayitId = giris.Id;
            await _baglam.SaveChangesAsync();

            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, kaynakKasa.SirketId, FinansMuhasebeKaydi.KasaBelgeTipi, cikis.Id, islemTarihi, cikis.Aciklama, fisNo,
                new Satir(await FinansMuhasebeKaydi.KasaKoduAsync(_baglam, hedefKasa), tutar, 0),
                new Satir(await FinansMuhasebeKaydi.KasaKoduAsync(_baglam, kaynakKasa), 0, tutar));

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> BankayaParaYatirAsync(int kasaId, int bankaHesabiId, decimal tutar, string aciklama, DateTime? tarih = null)
    {
        Oturum.YetkiKontrolEt(y => y.KasaYonet, "Kasadan Bankaya Para Yatırma");

        FinansKurallari.TutarDogrula(tutar, "Para yatırma");
        var temizAciklama = string.IsNullOrWhiteSpace(aciklama) ? "Kasadan bankaya para yatırma" : FinansKurallari.AciklamaDogrula(aciklama);
        var islemTarihi = FinansKurallari.TarihNormalizeEt(tarih);

        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var kasa = await AktifKasaGetirVeKilitleAsync(kasaId);
            await FinansKurallari.BankaHesabiKilitleAsync(_baglam, bankaHesabiId);
            var banka = await _baglam.BankaHesaplari.FirstOrDefaultAsync(b => b.Id == bankaHesabiId)
                ?? throw new KeyNotFoundException("Banka hesabı bulunamadı.");
            if (!banka.AktifMi)
                throw new InvalidOperationException($"'{banka.Ad}' banka hesabı pasif durumda.");
            if (!string.Equals(kasa.ParaBirimi, banka.ParaBirimi, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Kasa ile banka hesabının para birimi aynı olmalıdır.");

            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, islemTarihi, kasa.SirketId);
            var (tlTutar, paraBirimiId, kur, dovizTutar) = await FinansKurallari.KasaTransferTutariAsync(_baglam, kasa, tutar);
            await FinansKurallari.KasaCikisiYapilabilirAsync(_baglam, kasa, tutar, dovizTutar);
            tutar = tlTutar;

            var fisNo = FinansKurallari.FisNoUret("BNK-YTR");

            var kasaCikis = new KasaHareketi
            {
                SirketId = kasa.SirketId,
                KasaId = kasaId,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gider,
                FisTuru = KasaFisTuru.BankayaYatirma,
                FisNo = fisNo,
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
                Aciklama = $"Bankaya Para Yatırma -> {banka.Ad} ({banka.Iban}): {temizAciklama}",
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.KasaHareketleri.Add(kasaCikis);
            await _baglam.SaveChangesAsync();

            var bankaGiris = new BankaHareketi
            {
                SirketId = banka.SirketId,
                BankaHesabiId = bankaHesabiId,
                BankaAdi = banka.Ad,
                HesapNo = banka.Iban,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                FisTuru = BankaFisTuru.BankayaYatirma,
                FisNo = fisNo,
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
                Aciklama = $"Kasadan Para Yatırma <- {kasa.Ad}: {temizAciklama}",
                TersKayitId = kasaCikis.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(bankaGiris);
            await _baglam.SaveChangesAsync();

            kasaCikis.TersKayitId = bankaGiris.Id;
            await _baglam.SaveChangesAsync();

            // 102 Bankalar / 100 Kasa
            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, kasa.SirketId, FinansMuhasebeKaydi.KasaBelgeTipi, kasaCikis.Id, islemTarihi, kasaCikis.Aciklama, fisNo,
                new Satir(await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, banka), tutar, 0),
                new Satir(await FinansMuhasebeKaydi.KasaKoduAsync(_baglam, kasa), 0, tutar));

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<KasaSayimMutabakat> KasaSayimMutabakatKaydetAsync(KasaSayimMutabakat mutabakat)
    {
        Oturum.YetkiKontrolEt(y => y.KasaYonet, "Kasa Mutabakat / Gün Sonu");

        if (mutabakat.Adet200 < 0 || mutabakat.Adet100 < 0 || mutabakat.Adet50 < 0 || mutabakat.Adet20 < 0 ||
            mutabakat.Adet10 < 0 || mutabakat.Adet5 < 0 || mutabakat.BozukParaToplam < 0)
            throw new ArgumentException("Banknot adetleri ve bozuk para toplamı negatif olamaz.");

        mutabakat.Tarih = FinansKurallari.TarihNormalizeEt(mutabakat.Tarih);
        mutabakat.Aciklama = mutabakat.Aciklama?.Trim() ?? string.Empty;

        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            // Sayım sırasında başka bir hareket sistem bakiyesini değiştirmesin.
            var kasa = await AktifKasaGetirVeKilitleAsync(mutabakat.KasaId);
            // Banknot sayımı TL içindir; dövizli kasada sayım farkı döviz tutarı ve kurla gelir/gider fişi olarak girilir.
            if (FinansKurallari.DovizliMi(kasa.ParaBirimi))
                throw new InvalidOperationException($"Gün sonu sayım mutabakatı TL kasalar içindir. '{kasa.Ad}' ({kasa.ParaBirimi}) kasasında sayım farkını {kasa.ParaBirimi} tutarı ve kuruyla gelir/gider fişi olarak girin.");
            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, mutabakat.Tarih, kasa.SirketId);

            mutabakat.Id = 0;
            mutabakat.SirketId ??= kasa.SirketId;
            mutabakat.SistemBakiyesi = await FinansKurallari.KasaBakiyesiAsync(_baglam, kasa.Id);
            mutabakat.OlusturanKullaniciId = Oturum.AktifKullanici?.Id;
            mutabakat.OlusturmaTarihi = DateTime.UtcNow;

            _baglam.KasaSayimMutabakatlari.Add(mutabakat);
            await _baglam.SaveChangesAsync();

            // Fiziki sayım ile sistem bakiyesi farklıysa sayım farkı fişi otomatik oluşturulur.
            if (mutabakat.Fark != 0)
            {
                var farkHareket = new KasaHareketi
                {
                    SirketId = kasa.SirketId,
                    KasaId = kasa.Id,
                    Tarih = mutabakat.Tarih,
                    Tur = mutabakat.Fark > 0 ? HareketTuru.Gelir : HareketTuru.Gider,
                    FisTuru = KasaFisTuru.SayimFarki,
                    FisNo = FinansKurallari.FisNoUret("SYM"),
                    Tutar = Math.Abs(mutabakat.Fark),
                    Aciklama = $"Kasa Gün Sonu Mutabakat Sayım Farkı ({(mutabakat.Fark > 0 ? "Kasa Fazlası" : "Kasa Noksanı")}): {mutabakat.Aciklama}",
                    IlgiliBelgeId = mutabakat.Id,
                    OlusturmaTarihi = DateTime.UtcNow
                };
                _baglam.KasaHareketleri.Add(farkHareket);
                await _baglam.SaveChangesAsync();

                // Kasa fazlası: 100 / 397, Kasa noksanı: 197 / 100
                var kasaKodu = await FinansMuhasebeKaydi.KasaKoduAsync(_baglam, kasa);
                var fazla = mutabakat.Fark > 0;
                await FinansMuhasebeKaydi.FisEkleAsync(_baglam, farkHareket.SirketId, FinansMuhasebeKaydi.KasaBelgeTipi, farkHareket.Id, farkHareket.Tarih, farkHareket.Aciklama, farkHareket.FisNo,
                    fazla ? new Satir(kasaKodu, farkHareket.Tutar, 0) : new Satir("197", farkHareket.Tutar, 0),
                    fazla ? new Satir("397", 0, farkHareket.Tutar) : new Satir(kasaKodu, 0, farkHareket.Tutar));
            }

            await transaction.CommitAsync();
            return mutabakat;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> SilAsync(int id)
    {
        Oturum.YetkiKontrolEt(y => y.KasaYonet, "Kasa Hareketi Silme");
        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var hareket = await _baglam.KasaHareketleri.FirstOrDefaultAsync(k => k.Id == id);
            if (hareket == null) return false;

            if (hareket.IslemDurumu != FinansIslemDurumu.Tamamlandi)
                throw new InvalidOperationException("İptal edilmiş veya ters kayıt niteliğindeki hareket silinemez.");

            if (hareket.FaturaId.HasValue)
                throw new InvalidOperationException("Bu tahsilat bir faturaya bağlı. Fatura ekranından iptal edilmelidir.");
            if (hareket.OzelKod == CekSenetServisi.CekSenetOzelKodu)
                throw new InvalidOperationException("Bu hareket bir çek/senet tahsilatı veya ödemesidir; kasa/banka ekranından silinemez.");

            var simdi = DateTime.UtcNow;
            var neden = $"Kasa hareketi silindi ({Oturum.AktifKullanici?.KullaniciAdi ?? "bilinmeyen kullanıcı"})";
            var kontrolEdilecekKasalar = new HashSet<int>();
            var kontrolEdilecekBankalar = new HashSet<int>();

            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, hareket.Tarih, hareket.SirketId);
            if (hareket.KasaId.HasValue)
            {
                await FinansKurallari.KasaKilitleAsync(_baglam, hareket.KasaId.Value);
                kontrolEdilecekKasalar.Add(hareket.KasaId.Value);
            }
            SoftSil(hareket, simdi, neden);

            // Cari tahsilat/ödemenin cari tarafı da birlikte kaldırılır.
            if (hareket.CariKartId.HasValue && hareket.IlgiliBelgeId.HasValue &&
                hareket.FisTuru is KasaFisTuru.Tahsilat or KasaFisTuru.Odeme)
            {
                var cariHareket = await _baglam.CariHareketler.FirstOrDefaultAsync(c =>
                    c.Id == hareket.IlgiliBelgeId.Value && c.CariKartId == hareket.CariKartId.Value);
                if (cariHareket != null)
                {
                    if (cariHareket.KapaliTutar > 0)
                        throw new InvalidOperationException("Bu tahsilat/ödeme bir faturayla kapatılmış. Önce belge kapama eşleştirmesini kaldırın.");
                    SoftSil(cariHareket, simdi, neden);
                }
            }

            // Transferin karşı bacağı, kaydın türüne göre kasa veya banka tablosundadır.
            if (hareket.TersKayitId.HasValue)
            {
                if (hareket.FisTuru == KasaFisTuru.KasaVirman)
                {
                    var ters = await _baglam.KasaHareketleri.FirstOrDefaultAsync(k => k.Id == hareket.TersKayitId.Value && k.TersKayitId == hareket.Id);
                    if (ters != null)
                    {
                        if (ters.KasaId.HasValue)
                        {
                            await FinansKurallari.KasaKilitleAsync(_baglam, ters.KasaId.Value);
                            kontrolEdilecekKasalar.Add(ters.KasaId.Value);
                        }
                        SoftSil(ters, simdi, neden);
                    }
                }
                else if (hareket.FisTuru is KasaFisTuru.BankayaYatirma or KasaFisTuru.BankadanCekme)
                {
                    var ters = await _baglam.BankaHareketleri.FirstOrDefaultAsync(b => b.Id == hareket.TersKayitId.Value && b.TersKayitId == hareket.Id);
                    if (ters != null)
                    {
                        if (ters.BankaHesabiId.HasValue)
                        {
                            await FinansKurallari.BankaHesabiKilitleAsync(_baglam, ters.BankaHesabiId.Value);
                            kontrolEdilecekBankalar.Add(ters.BankaHesabiId.Value);
                        }
                        SoftSil(ters, simdi, neden);
                    }
                }
            }

            await FinansMuhasebeKaydi.FisleriSilAsync(_baglam,
                _baglam.ChangeTracker.Entries<KasaHareketi>().Where(e => e.Entity.Silindi).Select(e => e.Entity.Id),
                _baglam.ChangeTracker.Entries<BankaHareketi>().Where(e => e.Entity.Silindi).Select(e => e.Entity.Id),
                simdi, neden);
            await _baglam.SaveChangesAsync();

            // Silinen bir giriş, sonradan yapılmış çıkışlar nedeniyle bakiyeyi eksiye düşürmemeli.
            foreach (var kasaId in kontrolEdilecekKasalar)
            {
                if (await FinansKurallari.KasaBakiyesiAsync(_baglam, kasaId) < 0)
                    throw new InvalidOperationException("Bu hareket silinirse kasa bakiyesi eksiye düşer. Önce sonraki çıkış hareketlerini düzeltin.");
            }
            foreach (var bankaId in kontrolEdilecekBankalar)
            {
                var hesap = await _baglam.BankaHesaplari.FirstAsync(b => b.Id == bankaId);
                if (await FinansKurallari.BankaBakiyesiAsync(_baglam, bankaId) < -Math.Max(0, hesap.KrediLimiti))
                    throw new InvalidOperationException("Bu hareket silinirse banka hesabı bakiyesi eksiye düşer.");
            }

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static void SoftSil(EtkiAlani.Modeller.Ortak.TemelVarlik kayit, DateTime simdi, string neden)
    {
        kayit.Silindi = true;
        kayit.SilinmeTarihi = simdi;
        kayit.SilinmeNedeni = neden;
        kayit.GuncellemeTarihi = simdi;
    }

    public async Task<decimal> BakiyeHesaplaAsync()
    {
        GoruntulemeYetkisi();
        var gelir = await _baglam.KasaHareketleri
            .Where(k => k.IslemDurumu == FinansIslemDurumu.Tamamlandi && k.Tur == HareketTuru.Gelir)
            .SumAsync(k => (decimal?)k.Tutar) ?? 0;

        var gider = await _baglam.KasaHareketleri
            .Where(k => k.IslemDurumu == FinansIslemDurumu.Tamamlandi && k.Tur == HareketTuru.Gider)
            .SumAsync(k => (decimal?)k.Tutar) ?? 0;

        return gelir - gider;
    }

    public Task<decimal> KasaBakiyeHesaplaAsync(int kasaId)
    {
        GoruntulemeYetkisi();
        return FinansKurallari.KasaBakiyesiAsync(_baglam, kasaId);
    }

    public Task<decimal> KasaDovizBakiyeHesaplaAsync(int kasaId)
    {
        GoruntulemeYetkisi();
        return FinansKurallari.KasaDovizBakiyesiAsync(_baglam, kasaId);
    }

    public async Task<Kasa> KasaEkleAsync(Kasa kasa)
    {
        Oturum.YetkiKontrolEt(y => y.KasaYonet, "Kasa Tanımı Ekleme");
        KasaTanimiDogrula(kasa);

        if (await _baglam.Kasalar.AnyAsync(k => k.Kod == kasa.Kod))
            throw new InvalidOperationException($"'{kasa.Kod}' kodlu bir kasa zaten var.");

        // Bakiye her zaman hareketlerden hesaplanır; dışarıdan bakiye verilemez.
        kasa.Id = 0;
        kasa.Bakiye = 0;
        kasa.AktifMi = true;
        kasa.OlusturmaTarihi = DateTime.UtcNow;
        _baglam.Kasalar.Add(kasa);
        await _baglam.SaveChangesAsync();
        return kasa;
    }

    public async Task<Kasa> KasaGuncelleAsync(Kasa kasa)
    {
        Oturum.YetkiKontrolEt(y => y.KasaYonet, "Kasa Tanımı Güncelleme");
        KasaTanimiDogrula(kasa);

        var mevcut = await _baglam.Kasalar.FirstOrDefaultAsync(k => k.Id == kasa.Id)
            ?? throw new KeyNotFoundException("Kasa bulunamadı.");
        if (await _baglam.Kasalar.AnyAsync(k => k.Kod == kasa.Kod && k.Id != kasa.Id))
            throw new InvalidOperationException($"'{kasa.Kod}' kodlu bir kasa zaten var.");

        // Çağıran, aynı bağlamdan aldığı izlenen nesneyi değiştirmiş olabilir; karşılaştırma yüklenen değerlerle yapılır.
        var kayitli = _baglam.Entry(mevcut).OriginalValues;
        var kayitliParaBirimi = kayitli.GetValue<string>(nameof(Kasa.ParaBirimi));
        var kayitliAktif = kayitli.GetValue<bool>(nameof(Kasa.AktifMi));

        var hareketVar = await _baglam.KasaHareketleri.AnyAsync(h => h.KasaId == mevcut.Id);
        if (hareketVar && !string.Equals(kayitliParaBirimi, kasa.ParaBirimi, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Hareket görmüş bir kasanın para birimi değiştirilemez.");
        var bakiye = FinansKurallari.DovizliMi(kayitliParaBirimi)
            ? await FinansKurallari.KasaDovizBakiyesiAsync(_baglam, mevcut.Id)
            : await FinansKurallari.KasaBakiyesiAsync(_baglam, mevcut.Id);
        if (kayitliAktif && !kasa.AktifMi && bakiye != 0)
            throw new InvalidOperationException("Bakiyesi sıfır olmayan kasa pasife alınamaz. Önce bakiyeyi başka bir kasaya virman yapın.");

        mevcut.Kod = kasa.Kod;
        mevcut.Ad = kasa.Ad;
        mevcut.ParaBirimi = kasa.ParaBirimi;
        mevcut.MuhasebeKodu = kasa.MuhasebeKodu;
        mevcut.Aciklama = kasa.Aciklama;
        mevcut.AktifMi = kasa.AktifMi;
        mevcut.GuncellemeTarihi = DateTime.UtcNow;
        await _baglam.SaveChangesAsync();
        return mevcut;
    }

    private static void KasaTanimiDogrula(Kasa kasa)
    {
        kasa.Kod = kasa.Kod?.Trim().ToUpperInvariant() ?? string.Empty;
        kasa.Ad = kasa.Ad?.Trim() ?? string.Empty;
        kasa.ParaBirimi = string.IsNullOrWhiteSpace(kasa.ParaBirimi) ? "TRY" : kasa.ParaBirimi.Trim().ToUpperInvariant();
        kasa.MuhasebeKodu = string.IsNullOrWhiteSpace(kasa.MuhasebeKodu) ? "100.01.001" : kasa.MuhasebeKodu.Trim();
        kasa.Aciklama = kasa.Aciklama?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(kasa.Kod) || kasa.Kod.Length > 30)
            throw new ArgumentException("Kasa kodu boş olamaz ve en fazla 30 karakter olabilir.");
        if (string.IsNullOrWhiteSpace(kasa.Ad) || kasa.Ad.Length > 100)
            throw new ArgumentException("Kasa adı boş olamaz ve en fazla 100 karakter olabilir.");
        if (kasa.ParaBirimi.Length != 3 || !kasa.ParaBirimi.All(char.IsLetter))
            throw new ArgumentException("Para birimi 3 harfli ISO kodu olmalıdır (örn. TRY).");
        if (!kasa.MuhasebeKodu.StartsWith("100", StringComparison.Ordinal))
            throw new ArgumentException("Kasa muhasebe kodu 100 hesap grubu altında olmalıdır.");
    }

    public async Task<Kasa?> KasaGetirIdAsync(int id)
    {
        GoruntulemeYetkisi();
        return await _baglam.Kasalar.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id);
    }

    public async Task<IReadOnlyList<Kasa>> KasalariGetirAsync(bool sadeceAktifler = true)
    {
        GoruntulemeYetkisi();
        var query = _baglam.Kasalar.AsQueryable();
        if (sadeceAktifler)
            query = query.Where(k => k.AktifMi);

        var list = await query.OrderBy(k => k.Kod).ToListAsync();
        if (list.Count == 0 && !await _baglam.Kasalar.AnyAsync())
        {
            var varsayilan = await VarsayilanKasaGetirVeyaOlusturAsync();
            list = new List<Kasa> { varsayilan };
        }
        return list;
    }

    private async Task<Kasa> AktifKasaGetirAsync(int kasaId)
    {
        var kasa = await _baglam.Kasalar.FirstOrDefaultAsync(k => k.Id == kasaId)
            ?? throw new KeyNotFoundException("Kasa bulunamadı.");
        if (!kasa.AktifMi)
            throw new InvalidOperationException($"'{kasa.Ad}' kasası pasif durumda. Pasif kasaya işlem yapılamaz.");
        return kasa;
    }

    private async Task<Kasa> AktifKasaGetirVeKilitleAsync(int kasaId)
    {
        await FinansKurallari.KasaKilitleAsync(_baglam, kasaId);
        return await AktifKasaGetirAsync(kasaId);
    }

    private async Task<Kasa> VarsayilanKasaGetirVeyaOlusturAsync()
    {
        var ilk = await _baglam.Kasalar.Where(k => k.AktifMi).OrderBy(k => k.Id).FirstOrDefaultAsync();
        if (ilk != null) return ilk;
        if (await _baglam.Kasalar.AnyAsync())
            throw new InvalidOperationException("Aktif kasa bulunamadı. Lütfen bir kasa seçin veya tanımlayın.");

        var yeni = new Kasa
        {
            Kod = "KSA-01",
            Ad = "Merkez Nakit Kasa",
            MuhasebeKodu = "100.01.001",
            ParaBirimi = "TRY",
            AktifMi = true,
            OlusturmaTarihi = DateTime.UtcNow
        };
        _baglam.Kasalar.Add(yeni);
        await _baglam.SaveChangesAsync();
        return yeni;
    }
}
