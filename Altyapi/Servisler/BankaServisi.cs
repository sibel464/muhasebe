using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Yardimcilar;
using Uygulama.Arayuzler;
using Altyapi.Veri;
using Satir = Altyapi.Servisler.FinansMuhasebeKaydi.Satir;

namespace Altyapi.Servisler;

public class BankaServisi : IBankaServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public BankaServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    private IKullaniciServisi Oturum => _kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama");

    private void GoruntulemeYetkisi() =>
        Oturum.YetkiKontrolEt(y => y.BankaGoruntule || y.BankaEkle || y.BankaDuzenle, "Banka Görüntüleme");

    public async Task<List<BankaHareketi>> TumunuGetirAsync()
    {
        GoruntulemeYetkisi();
        return await _baglam.BankaHareketleri
            .AsNoTracking()
            .Include(b => b.CariKart)
            .Include(b => b.BankaHesabi)
            .OrderByDescending(b => b.Tarih)
            .ThenByDescending(b => b.Id)
            .ToListAsync();
    }

    public async Task<List<BankaHareketi>> TarihAraligindaGetirAsync(DateTime baslangic, DateTime bitis)
    {
        GoruntulemeYetkisi();
        var basUtc = DateTime.SpecifyKind(baslangic.Date, DateTimeKind.Utc);
        var bitUtc = DateTime.SpecifyKind(bitis.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        return await _baglam.BankaHareketleri
            .AsNoTracking()
            .Include(b => b.CariKart)
            .Include(b => b.BankaHesabi)
            .Where(b => b.Tarih >= basUtc && b.Tarih <= bitUtc)
            .OrderByDescending(b => b.Tarih)
            .ThenByDescending(b => b.Id)
            .ToListAsync();
    }

    public async Task<List<BankaHareketi>> HesapHareketleriGetirAsync(int bankaHesabiId)
    {
        GoruntulemeYetkisi();
        return await _baglam.BankaHareketleri
            .AsNoTracking()
            .Include(b => b.CariKart)
            .Include(b => b.BankaHesabi)
            .Where(b => b.BankaHesabiId == bankaHesabiId)
            .OrderByDescending(b => b.Tarih)
            .ThenByDescending(b => b.Id)
            .ToListAsync();
    }

    public async Task<List<BankaHareketi>> CariHareketleriGetirAsync(int cariKartId)
    {
        GoruntulemeYetkisi();
        return await _baglam.BankaHareketleri
            .AsNoTracking()
            .Include(b => b.CariKart)
            .Include(b => b.BankaHesabi)
            .Where(b => b.CariKartId == cariKartId)
            .OrderByDescending(b => b.Tarih)
            .ThenByDescending(b => b.Id)
            .ToListAsync();
    }

    public async Task<BankaHareketi?> GetirByIdAsync(int id)
    {
        GoruntulemeYetkisi();
        return await _baglam.BankaHareketleri
            .AsNoTracking()
            .Include(b => b.CariKart)
            .Include(b => b.BankaHesabi)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    // Serbest banka fişi: faiz/gelir, masraf ve kredi ödemesi. Cari havaleleri cari hareketiyle birlikte,
    // transferler ise kendi metotlarıyla oluşturulur.
    public async Task<BankaHareketi> EkleAsync(BankaHareketi hareket)
    {
        if (hareket.CariKartId is > 0 && hareket.FisTuru is BankaFisTuru.GelenHavale or BankaFisTuru.GidenHavale)
        {
            var hesapId = hareket.BankaHesabiId ?? 0;
            var belgeNo = hareket.DekontNo;
            return hareket.FisTuru == BankaFisTuru.GelenHavale
                ? await HesabaTahsilatEkleAsync(hesapId, hareket.CariKartId.Value, hareket.Tutar, hareket.Aciklama, belgeNo, hareket.Tarih, hareket.DovizTutar, hareket.Kur)
                : await HesaptanOdemeEkleAsync(hesapId, hareket.CariKartId.Value, hareket.Tutar, hareket.Aciklama, belgeNo, hareket.Tarih, hareket.DovizTutar, hareket.Kur);
        }

        Oturum.YetkiKontrolEt(y => y.BankaEkle || y.BankaDuzenle, "Banka Hareketi Ekleme");
        hareket.Tur = hareket.FisTuru switch
        {
            BankaFisTuru.BankaGelir => HareketTuru.Gelir,
            BankaFisTuru.BankaGiderMasraf or BankaFisTuru.KrediOdemesi => HareketTuru.Gider,
            BankaFisTuru.GelenHavale or BankaFisTuru.GidenHavale =>
                throw new ArgumentException("Gelen/giden havale fişleri için cari kart seçilmelidir."),
            _ => throw new ArgumentException("Virman, kasa transferi ve POS geçiş fişleri ilgili işlem ekranından yapılmalıdır.")
        };

        if (hareket.CariKartId.HasValue)
            throw new ArgumentException("Cari kart yalnızca gelen/giden havale fişlerinde seçilebilir.");

        if (hareket.DovizTutar <= 0)
            FinansKurallari.TutarDogrula(hareket.Tutar, "Banka hareket");
        hareket.Aciklama = FinansKurallari.AciklamaDogrula(hareket.Aciklama);
        hareket.Tarih = FinansKurallari.TarihNormalizeEt(hareket.Tarih);

        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var hesap = await AktifHesapGetirVeKilitleAsync(hareket.BankaHesabiId ?? 0);
            (hareket.Tutar, hareket.ParaBirimiId, hareket.Kur, hareket.DovizTutar) = await FinansKurallari.HesapTutariAsync(
                _baglam, hesap.Ad, hesap.ParaBirimi, hareket.Tutar, hareket.DovizTutar, hareket.Kur, hareket.ParaBirimiId);
            FinansKurallari.TutarDogrula(hareket.Tutar, "Banka hareket");
            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, hareket.Tarih, hesap.SirketId);
            if (hareket.Tur == HareketTuru.Gider)
                await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, hesap, hareket.Tutar, hareket.DovizTutar);

            hareket.Id = 0;
            hareket.BankaHesabiId = hesap.Id;
            hareket.SirketId ??= hesap.SirketId;
            hareket.BankaAdi = hesap.Ad;
            hareket.HesapNo = string.IsNullOrWhiteSpace(hesap.Iban) ? hesap.HesapNo : hesap.Iban;
            hareket.IlgiliBelgeId = null;
            hareket.TersKayitId = null;
            hareket.IslemDurumu = FinansIslemDurumu.Tamamlandi;
            hareket.DekontNo = string.IsNullOrWhiteSpace(hareket.DekontNo) ? null : hareket.DekontNo.Trim();
            hareket.OlusturmaTarihi = DateTime.UtcNow;
            hareket.FisNo = string.IsNullOrWhiteSpace(hareket.FisNo) ? FinansKurallari.FisNoUret("BNK") : hareket.FisNo.Trim();

            _baglam.BankaHareketleri.Add(hareket);
            await _baglam.SaveChangesAsync();

            // Faiz/gelir: 102 / 642, Masraf: 780 / 102, Kredi ödemesi: 300 / 102
            var bankaKodu = await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, hesap);
            var (karsiKod, bankaBorclu) = hareket.FisTuru switch
            {
                BankaFisTuru.BankaGelir => ("642", true),
                BankaFisTuru.KrediOdemesi => ("300", false),
                _ => ("780", false)
            };
            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, hareket.SirketId, FinansMuhasebeKaydi.BankaBelgeTipi, hareket.Id, hareket.Tarih, hareket.Aciklama, hareket.DekontNo ?? hareket.FisNo,
                bankaBorclu ? new Satir(bankaKodu, hareket.Tutar, 0) : new Satir(karsiKod, hareket.Tutar, 0),
                bankaBorclu ? new Satir(karsiKod, 0, hareket.Tutar) : new Satir(bankaKodu, 0, hareket.Tutar));

            await transaction.CommitAsync();
            return hareket;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Geriye dönük uyumluluk: hesap, verilen IBAN/hesap no veya banka adına göre bulunur.
    public async Task<BankaHareketi> TahsilatEkleAsync(int cariKartId, string bankaAdi, string hesapNo, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null)
    {
        var hesapId = await HesapIdBulAsync(bankaAdi, hesapNo);
        return await HesabaTahsilatEkleAsync(hesapId, cariKartId, tutar, aciklama, belgeNo, tarih);
    }

    public async Task<BankaHareketi> OdemeEkleAsync(int cariKartId, string bankaAdi, string hesapNo, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null)
    {
        var hesapId = await HesapIdBulAsync(bankaAdi, hesapNo);
        return await HesaptanOdemeEkleAsync(hesapId, cariKartId, tutar, aciklama, belgeNo, tarih);
    }

    public Task<BankaHareketi> HesabaTahsilatEkleAsync(int bankaHesabiId, int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null, decimal dovizTutar = 0, decimal kur = 0)
    {
        Oturum.YetkiKontrolEt(y => y.TahsilatYap || y.BankaEkle, "Banka Tahsilat Girişi");
        return CariIslemEkleAsync(bankaHesabiId, cariKartId, tutar, aciklama, belgeNo, tarih, tahsilat: true, dovizTutar, kur);
    }

    public Task<BankaHareketi> HesaptanOdemeEkleAsync(int bankaHesabiId, int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null, decimal dovizTutar = 0, decimal kur = 0)
    {
        Oturum.YetkiKontrolEt(y => y.OdemeYap || y.BankaEkle, "Banka Ödeme Çıkışı");
        return CariIslemEkleAsync(bankaHesabiId, cariKartId, tutar, aciklama, belgeNo, tarih, tahsilat: false, dovizTutar, kur);
    }

    private async Task<BankaHareketi> CariIslemEkleAsync(int bankaHesabiId, int cariKartId, decimal tutar, string aciklama, string? belgeNo, DateTime? tarih, bool tahsilat,
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
            var hesap = await AktifHesapGetirVeKilitleAsync(bankaHesabiId);
            // Cari TL karşılığıyla işlenir; döviz tutarı ve kur hesap hareketinde kalır.
            (tutar, var paraBirimiId, kur, dovizTutar) = await FinansKurallari.HesapTutariAsync(_baglam, hesap.Ad, hesap.ParaBirimi, tutar, dovizTutar, kur);
            FinansKurallari.TutarDogrula(tutar, tahsilat ? "Tahsilat" : "Ödeme");

            var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == cariKartId)
                ?? throw new KeyNotFoundException("Cari kart bulunamadı.");
            if (!cari.Aktif)
                throw new InvalidOperationException($"Pasif durumdaki cari karta banka {(tahsilat ? "tahsilatı" : "ödemesi")} girilemez.");

            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, islemTarihi, hesap.SirketId ?? cari.SirketId);
            if (!tahsilat)
                await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, hesap, tutar, dovizTutar);

            var bankaHareketi = new BankaHareketi
            {
                SirketId = hesap.SirketId ?? cari.SirketId,
                BankaHesabiId = hesap.Id,
                Tarih = islemTarihi,
                Tur = tahsilat ? HareketTuru.Gelir : HareketTuru.Gider,
                FisTuru = tahsilat ? BankaFisTuru.GelenHavale : BankaFisTuru.GidenHavale,
                FisNo = FinansKurallari.FisNoUret("BNK"),
                DekontNo = temizBelgeNo,
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
                BankaAdi = hesap.Ad,
                HesapNo = string.IsNullOrWhiteSpace(hesap.Iban) ? hesap.HesapNo : hesap.Iban,
                Aciklama = temizAciklama,
                CariKartId = cariKartId,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(bankaHareketi);
            await _baglam.SaveChangesAsync();

            var cariHareket = new CariHareket
            {
                CariKartId = cariKartId,
                Tarih = islemTarihi,
                HareketTuru = tahsilat ? CariHareketTuru.Tahsilat : CariHareketTuru.Odeme,
                Aciklama = temizAciklama,
                BelgeNo = temizBelgeNo,
                Borc = tahsilat ? 0 : tutar,
                Alacak = tahsilat ? tutar : 0,
                IlgiliBelgeId = bankaHareketi.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.CariHareketler.Add(cariHareket);
            await _baglam.SaveChangesAsync();

            // Gelen havale: 102 / 120, Giden havale: 320 / 102
            var bankaKodu = await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, hesap);
            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, bankaHareketi.SirketId, FinansMuhasebeKaydi.BankaBelgeTipi, bankaHareketi.Id, islemTarihi, temizAciklama, temizBelgeNo ?? bankaHareketi.FisNo,
                tahsilat ? new Satir(bankaKodu, tutar, 0) : new Satir("320", tutar, 0, cariKartId),
                tahsilat ? new Satir("120", 0, tutar, cariKartId) : new Satir(bankaKodu, 0, tutar));

            bankaHareketi.IlgiliBelgeId = cariHareket.Id;
            await _baglam.SaveChangesAsync();

            await transaction.CommitAsync();
            return bankaHareketi;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> BankaVirmanYapAsync(int kaynakBankaHesabiId, int hedefBankaHesabiId, decimal tutar, string aciklama, DateTime? tarih = null)
    {
        Oturum.YetkiKontrolEt(y => y.BankaEkle || y.BankaDuzenle, "Bankalar Arası Virman");

        if (kaynakBankaHesabiId == hedefBankaHesabiId)
            throw new ArgumentException("Kaynak banka hesabı ile hedef banka hesabı aynı olamaz.");
        FinansKurallari.TutarDogrula(tutar, "Virman");
        var temizAciklama = string.IsNullOrWhiteSpace(aciklama) ? "Bankalar arası virman" : FinansKurallari.AciklamaDogrula(aciklama);
        var islemTarihi = FinansKurallari.TarihNormalizeEt(tarih);

        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            foreach (var id in new[] { kaynakBankaHesabiId, hedefBankaHesabiId }.Order())
                await FinansKurallari.BankaHesabiKilitleAsync(_baglam, id);

            var kaynak = await AktifHesapGetirAsync(kaynakBankaHesabiId);
            var hedef = await AktifHesapGetirAsync(hedefBankaHesabiId);
            if (!string.Equals(kaynak.ParaBirimi, hedef.ParaBirimi, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Farklı para birimindeki banka hesapları arasında virman yapılamaz.");

            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, islemTarihi, kaynak.SirketId);
            var (tlTutar, paraBirimiId, kur, dovizTutar) = await FinansKurallari.BankaTransferTutariAsync(_baglam, kaynak, tutar);
            await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, kaynak, tutar, dovizTutar);
            tutar = tlTutar;

            var fisNo = FinansKurallari.FisNoUret("BNK-VRM");

            var cikis = new BankaHareketi
            {
                SirketId = kaynak.SirketId,
                BankaHesabiId = kaynakBankaHesabiId,
                BankaAdi = kaynak.Ad,
                HesapNo = kaynak.Iban,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gider,
                FisTuru = BankaFisTuru.BankaVirman,
                OdemeYontemi = OdemeYontemi.Virman,
                FisNo = fisNo,
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
                Aciklama = $"Virman Çıkış -> {hedef.Ad}: {temizAciklama}",
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(cikis);
            await _baglam.SaveChangesAsync();

            var giris = new BankaHareketi
            {
                SirketId = hedef.SirketId,
                BankaHesabiId = hedefBankaHesabiId,
                BankaAdi = hedef.Ad,
                HesapNo = hedef.Iban,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                FisTuru = BankaFisTuru.BankaVirman,
                OdemeYontemi = OdemeYontemi.Virman,
                FisNo = fisNo,
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
                Aciklama = $"Virman Giriş <- {kaynak.Ad}: {temizAciklama}",
                TersKayitId = cikis.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(giris);
            await _baglam.SaveChangesAsync();

            cikis.TersKayitId = giris.Id;
            await _baglam.SaveChangesAsync();

            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, kaynak.SirketId, FinansMuhasebeKaydi.BankaBelgeTipi, cikis.Id, islemTarihi, cikis.Aciklama, fisNo,
                new Satir(await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, hedef), tutar, 0),
                new Satir(await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, kaynak), 0, tutar));

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> BankadanKasaCekAsync(int bankaHesabiId, int kasaId, decimal tutar, string aciklama, DateTime? tarih = null)
    {
        Oturum.YetkiKontrolEt(y => y.KasaYonet && (y.BankaEkle || y.BankaDuzenle), "Bankadan Kasaya Para Çekme");

        FinansKurallari.TutarDogrula(tutar, "Para çekme");
        var temizAciklama = string.IsNullOrWhiteSpace(aciklama) ? "Bankadan kasaya para çekme" : FinansKurallari.AciklamaDogrula(aciklama);
        var islemTarihi = FinansKurallari.TarihNormalizeEt(tarih);

        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var banka = await AktifHesapGetirVeKilitleAsync(bankaHesabiId);
            await FinansKurallari.KasaKilitleAsync(_baglam, kasaId);
            var kasa = await _baglam.Kasalar.FirstOrDefaultAsync(k => k.Id == kasaId)
                ?? throw new KeyNotFoundException("Kasa bulunamadı.");
            if (!kasa.AktifMi)
                throw new InvalidOperationException($"'{kasa.Ad}' kasası pasif durumda.");
            if (!string.Equals(kasa.ParaBirimi, banka.ParaBirimi, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Kasa ile banka hesabının para birimi aynı olmalıdır.");

            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, islemTarihi, banka.SirketId);
            var (tlTutar, paraBirimiId, kur, dovizTutar) = await FinansKurallari.BankaTransferTutariAsync(_baglam, banka, tutar);
            await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, banka, tutar, dovizTutar);
            tutar = tlTutar;

            var fisNo = FinansKurallari.FisNoUret("BNK-CEK");

            var bankaCikis = new BankaHareketi
            {
                SirketId = banka.SirketId,
                BankaHesabiId = bankaHesabiId,
                BankaAdi = banka.Ad,
                HesapNo = banka.Iban,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gider,
                FisTuru = BankaFisTuru.KasadanCekme,
                FisNo = fisNo,
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
                Aciklama = $"Kasaya Para Çekme -> {kasa.Ad}: {temizAciklama}",
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(bankaCikis);
            await _baglam.SaveChangesAsync();

            var kasaGiris = new KasaHareketi
            {
                SirketId = kasa.SirketId,
                KasaId = kasaId,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                FisTuru = KasaFisTuru.BankadanCekme,
                FisNo = fisNo,
                Tutar = tutar,
                ParaBirimiId = paraBirimiId,
                Kur = kur,
                DovizTutar = dovizTutar,
                Aciklama = $"Bankadan Para Çekme <- {banka.Ad}: {temizAciklama}",
                TersKayitId = bankaCikis.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.KasaHareketleri.Add(kasaGiris);
            await _baglam.SaveChangesAsync();

            bankaCikis.TersKayitId = kasaGiris.Id;
            await _baglam.SaveChangesAsync();

            // 100 Kasa / 102 Bankalar
            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, banka.SirketId, FinansMuhasebeKaydi.BankaBelgeTipi, bankaCikis.Id, islemTarihi, bankaCikis.Aciklama, fisNo,
                new Satir(await FinansMuhasebeKaydi.KasaKoduAsync(_baglam, kasa), tutar, 0),
                new Satir(await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, banka), 0, tutar));

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> PosBlokeHesabaGecirAsync(int posHesapId, int vadesizHesapId, decimal tutar, decimal komisyonTutari, string aciklama, DateTime? tarih = null)
    {
        Oturum.YetkiKontrolEt(y => y.BankaEkle || y.BankaDuzenle, "POS Hesaba Geçiş İşlemi");

        if (posHesapId == vadesizHesapId)
            throw new ArgumentException("POS hesabı ile vadesiz hesap aynı olamaz.");
        FinansKurallari.TutarDogrula(tutar, "POS geçiş");
        if (komisyonTutari < 0)
            throw new ArgumentException("Komisyon tutarı negatif olamaz.");
        if (decimal.Round(komisyonTutari, 2) != komisyonTutari)
            throw new ArgumentException("Komisyon tutarı en fazla 2 ondalık basamak içerebilir.");

        var netTutar = tutar - komisyonTutari;
        if (netTutar <= 0)
            throw new ArgumentException("Komisyon tutarı toplam tutara eşit veya ondan büyük olamaz.");

        var temizAciklama = string.IsNullOrWhiteSpace(aciklama) ? "POS bloke çözümü" : FinansKurallari.AciklamaDogrula(aciklama);
        var islemTarihi = FinansKurallari.TarihNormalizeEt(tarih);

        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            foreach (var id in new[] { posHesapId, vadesizHesapId }.Order())
                await FinansKurallari.BankaHesabiKilitleAsync(_baglam, id);

            var posHesap = await AktifHesapGetirAsync(posHesapId);
            var vadesizHesap = await AktifHesapGetirAsync(vadesizHesapId);
            if (posHesap.HesapTuru != BankaHesapTuru.POSHesabi)
                throw new InvalidOperationException($"'{posHesap.Ad}' bir POS hesabı değil.");
            if (vadesizHesap.HesapTuru == BankaHesapTuru.POSHesabi)
                throw new InvalidOperationException("Hedef hesap vadesiz bir banka hesabı olmalıdır.");
            if (!string.Equals(posHesap.ParaBirimi, vadesizHesap.ParaBirimi, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("POS hesabı ile vadesiz hesabın para birimi aynı olmalıdır.");

            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, islemTarihi, posHesap.SirketId);
            await FinansKurallari.BankaCikisiYapilabilirAsync(_baglam, posHesap, tutar);

            var fisNo = FinansKurallari.FisNoUret("POS-GEC");

            var posCikis = new BankaHareketi
            {
                SirketId = posHesap.SirketId,
                BankaHesabiId = posHesapId,
                BankaAdi = posHesap.Ad,
                HesapNo = posHesap.Iban,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gider,
                FisTuru = BankaFisTuru.PosHesabaGecis,
                FisNo = fisNo,
                Tutar = tutar,
                KomisyonTutari = komisyonTutari,
                Aciklama = $"POS Bloke Çözümü Vadesiz Hesaba Transfer -> {vadesizHesap.Ad}: {temizAciklama}",
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(posCikis);
            await _baglam.SaveChangesAsync();

            var vadesizGiris = new BankaHareketi
            {
                SirketId = vadesizHesap.SirketId,
                BankaHesabiId = vadesizHesapId,
                BankaAdi = vadesizHesap.Ad,
                HesapNo = vadesizHesap.Iban,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                FisTuru = BankaFisTuru.PosHesabaGecis,
                FisNo = fisNo,
                Tutar = netTutar,
                KomisyonTutari = komisyonTutari,
                Aciklama = $"POS Bloke Hesabından Geçiş <- {posHesap.Ad} (Net: {netTutar:N2} ₺, Komisyon: {komisyonTutari:N2} ₺): {temizAciklama}",
                TersKayitId = posCikis.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(vadesizGiris);
            await _baglam.SaveChangesAsync();

            posCikis.TersKayitId = vadesizGiris.Id;

            // Toplu geçiş, Finans Merkezi'nde bekleyen kartlı satışları en eskiden başlayarak aktarılan tutar kadar
            // kapatır; aynı para oradan ikinci kez aktarılmasın.
            var kalan = tutar;
            foreach (var pos in await _baglam.PosIslemleri.Where(p => p.BankaHesabiId == posHesapId && p.Durum == PosIslemDurumu.Bekliyor)
                         .OrderBy(p => p.IslemTarihi).ThenBy(p => p.Id).ToListAsync())
            {
                if (pos.BrutTutar > kalan) break;
                kalan -= pos.BrutTutar;
                pos.Durum = PosIslemDurumu.HesabaGecti;
                pos.HesabaGecisTarihi = islemTarihi;
            }
            await _baglam.SaveChangesAsync();

            // 102 Bankalar (net) + 653 Komisyon / 108 POS (brüt)
            var satirlar = new List<Satir> { new(await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, vadesizHesap), netTutar, 0) };
            if (komisyonTutari > 0) satirlar.Add(new Satir("653", komisyonTutari, 0));
            satirlar.Add(new Satir(await FinansMuhasebeKaydi.BankaKoduAsync(_baglam, posHesap), 0, tutar));
            await FinansMuhasebeKaydi.FisEkleAsync(_baglam, posHesap.SirketId, FinansMuhasebeKaydi.BankaBelgeTipi, posCikis.Id, islemTarihi, posCikis.Aciklama, fisNo, satirlar.ToArray());

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> SilAsync(int id)
    {
        Oturum.YetkiKontrolEt(y => y.BankaSil, "Banka Hareketi Silme");
        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var hareket = await _baglam.BankaHareketleri.FirstOrDefaultAsync(b => b.Id == id);
            if (hareket == null) return false;

            if (hareket.IslemDurumu != FinansIslemDurumu.Tamamlandi)
                throw new InvalidOperationException("İptal edilmiş veya ters kayıt niteliğindeki hareket silinemez.");

            if (hareket.FaturaId.HasValue)
                throw new InvalidOperationException("Bu tahsilat bir faturaya bağlı. Fatura ekranından iptal edilmelidir.");
            if (hareket.OzelKod == CekSenetServisi.CekSenetOzelKodu)
                throw new InvalidOperationException("Bu hareket bir çek/senet tahsilatı veya ödemesidir; kasa/banka ekranından silinemez.");
            if (hareket.OzelKod == PersonelServisi.PersonelOzelKodu)
                throw new InvalidOperationException("Bu hareket bir maaş ödemesi veya personel avansıdır; Personel ve Maaş ekranından iptal edilmelidir.");

            var simdi = DateTime.UtcNow;
            var neden = $"Banka hareketi silindi ({Oturum.AktifKullanici?.KullaniciAdi ?? "bilinmeyen kullanıcı"})";
            var kontrolEdilecekBankalar = new HashSet<int>();
            var kontrolEdilecekKasalar = new HashSet<int>();

            await FinansKurallari.DonemAcikOlmaliAsync(_baglam, hareket.Tarih, hareket.SirketId);
            if (hareket.BankaHesabiId.HasValue)
            {
                await FinansKurallari.BankaHesabiKilitleAsync(_baglam, hareket.BankaHesabiId.Value);
                kontrolEdilecekBankalar.Add(hareket.BankaHesabiId.Value);
            }
            SoftSil(hareket, simdi, neden);

            if (hareket.CariKartId.HasValue && hareket.IlgiliBelgeId.HasValue &&
                hareket.FisTuru is BankaFisTuru.GelenHavale or BankaFisTuru.GidenHavale)
            {
                var cariHareket = await _baglam.CariHareketler.FirstOrDefaultAsync(c =>
                    c.Id == hareket.IlgiliBelgeId.Value && c.CariKartId == hareket.CariKartId.Value);
                if (cariHareket != null)
                {
                    if (cariHareket.KapaliTutar > 0)
                        throw new InvalidOperationException("Bu havale bir faturayla kapatılmış. Önce belge kapama eşleştirmesini kaldırın.");
                    SoftSil(cariHareket, simdi, neden);
                }
            }

            // Transferin karşı bacağı: banka virmanı ve POS geçişi bankada, kasa transferleri kasada.
            if (hareket.TersKayitId.HasValue)
            {
                if (hareket.FisTuru is BankaFisTuru.BankaVirman or BankaFisTuru.PosHesabaGecis)
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
                else if (hareket.FisTuru is BankaFisTuru.KasadanCekme or BankaFisTuru.BankayaYatirma)
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
            }

            await FinansMuhasebeKaydi.FisleriSilAsync(_baglam,
                _baglam.ChangeTracker.Entries<KasaHareketi>().Where(e => e.Entity.Silindi).Select(e => e.Entity.Id),
                _baglam.ChangeTracker.Entries<BankaHareketi>().Where(e => e.Entity.Silindi).Select(e => e.Entity.Id),
                simdi, neden);
            await _baglam.SaveChangesAsync();

            foreach (var bankaId in kontrolEdilecekBankalar)
            {
                var hesap = await _baglam.BankaHesaplari.FirstAsync(b => b.Id == bankaId);
                if (await FinansKurallari.BankaHesapBakiyesiAsync(_baglam, bankaId) < -Math.Max(0, hesap.KrediLimiti))
                    throw new InvalidOperationException("Bu hareket silinirse banka hesabı bakiyesi eksiye düşer. Önce sonraki çıkış hareketlerini düzeltin.");
            }
            foreach (var kasaId in kontrolEdilecekKasalar)
            {
                if (await FinansKurallari.KasaHesapBakiyesiAsync(_baglam, kasaId) < 0)
                    throw new InvalidOperationException("Bu hareket silinirse kasa bakiyesi eksiye düşer.");
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
        var gelir = await _baglam.BankaHareketleri
            .Where(b => b.IslemDurumu == FinansIslemDurumu.Tamamlandi && b.Tur == HareketTuru.Gelir)
            .SumAsync(b => (decimal?)b.Tutar) ?? 0;

        var gider = await _baglam.BankaHareketleri
            .Where(b => b.IslemDurumu == FinansIslemDurumu.Tamamlandi && b.Tur == HareketTuru.Gider)
            .SumAsync(b => (decimal?)b.Tutar) ?? 0;

        return gelir - gider;
    }

    public async Task<decimal> HesapBakiyeHesaplaAsync(string bankaAdi, string? hesapNo = null)
    {
        GoruntulemeYetkisi();
        var query = _baglam.BankaHareketleri.Where(b => b.BankaAdi == bankaAdi);
        if (!string.IsNullOrWhiteSpace(hesapNo))
        {
            query = query.Where(k => k.HesapNo == hesapNo);
        }

        var gelir = await query.Where(k => k.Tur == HareketTuru.Gelir).SumAsync(k => (decimal?)k.Tutar) ?? 0;
        var gider = await query.Where(k => k.Tur == HareketTuru.Gider).SumAsync(k => (decimal?)k.Tutar) ?? 0;

        return gelir - gider;
    }

    public Task<decimal> HesapBakiyeHesaplaAsync(int bankaHesabiId)
    {
        GoruntulemeYetkisi();
        return FinansKurallari.BankaBakiyesiAsync(_baglam, bankaHesabiId);
    }

    public Task<decimal> HesapDovizBakiyeHesaplaAsync(int bankaHesabiId)
    {
        GoruntulemeYetkisi();
        return FinansKurallari.BankaDovizBakiyesiAsync(_baglam, bankaHesabiId);
    }

    public async Task<BankaHesabi> BankaHesabiEkleAsync(BankaHesabi hesap)
    {
        Oturum.YetkiKontrolEt(y => y.BankaEkle, "Banka Hesabı Tanımlama");
        BankaHesabiDogrula(hesap);

        if (await _baglam.BankaHesaplari.AnyAsync(b => b.Kod == hesap.Kod))
            throw new InvalidOperationException($"'{hesap.Kod}' kodlu bir banka hesabı zaten var.");
        if (!string.IsNullOrEmpty(hesap.Iban) && await _baglam.BankaHesaplari.AnyAsync(b => b.Iban == hesap.Iban))
            throw new InvalidOperationException("Bu IBAN başka bir banka hesabında kayıtlı.");

        hesap.Id = 0;
        hesap.Bakiye = 0;
        hesap.BlokeBakiye = 0;
        hesap.AktifMi = true;
        hesap.OlusturmaTarihi = DateTime.UtcNow;
        _baglam.BankaHesaplari.Add(hesap);
        await _baglam.SaveChangesAsync();
        return hesap;
    }

    public async Task<BankaHesabi> BankaHesabiGuncelleAsync(BankaHesabi hesap)
    {
        Oturum.YetkiKontrolEt(y => y.BankaDuzenle, "Banka Hesabı Güncelleme");
        BankaHesabiDogrula(hesap);

        var mevcut = await _baglam.BankaHesaplari.FirstOrDefaultAsync(b => b.Id == hesap.Id)
            ?? throw new KeyNotFoundException("Banka hesabı bulunamadı.");
        if (await _baglam.BankaHesaplari.AnyAsync(b => b.Kod == hesap.Kod && b.Id != hesap.Id))
            throw new InvalidOperationException($"'{hesap.Kod}' kodlu bir banka hesabı zaten var.");
        if (!string.IsNullOrEmpty(hesap.Iban) && await _baglam.BankaHesaplari.AnyAsync(b => b.Iban == hesap.Iban && b.Id != hesap.Id))
            throw new InvalidOperationException("Bu IBAN başka bir banka hesabında kayıtlı.");

        var kayitli = _baglam.Entry(mevcut).OriginalValues;
        var kayitliParaBirimi = kayitli.GetValue<string>(nameof(BankaHesabi.ParaBirimi));
        var kayitliAktif = kayitli.GetValue<bool>(nameof(BankaHesabi.AktifMi));

        var hareketVar = await _baglam.BankaHareketleri.AnyAsync(h => h.BankaHesabiId == mevcut.Id);
        if (hareketVar && !string.Equals(kayitliParaBirimi, hesap.ParaBirimi, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Hareket görmüş bir banka hesabının para birimi değiştirilemez.");
        var bakiye = FinansKurallari.DovizliMi(kayitliParaBirimi)
            ? await FinansKurallari.BankaDovizBakiyesiAsync(_baglam, mevcut.Id)
            : await FinansKurallari.BankaBakiyesiAsync(_baglam, mevcut.Id);
        if (kayitliAktif && !hesap.AktifMi && bakiye != 0)
            throw new InvalidOperationException("Bakiyesi sıfır olmayan banka hesabı pasife alınamaz.");

        mevcut.Kod = hesap.Kod;
        mevcut.Ad = hesap.Ad;
        mevcut.HesapTuru = hesap.HesapTuru;
        mevcut.Sube = hesap.Sube;
        mevcut.HesapNo = hesap.HesapNo;
        mevcut.Iban = hesap.Iban;
        mevcut.ParaBirimi = hesap.ParaBirimi;
        mevcut.PosKomisyonOrani = hesap.PosKomisyonOrani;
        mevcut.ValorGunu = hesap.ValorGunu;
        mevcut.KrediLimiti = hesap.KrediLimiti;
        mevcut.MuhasebeKodu = hesap.MuhasebeKodu;
        mevcut.AktifMi = hesap.AktifMi;
        mevcut.GuncellemeTarihi = DateTime.UtcNow;
        await _baglam.SaveChangesAsync();
        return mevcut;
    }

    private static void BankaHesabiDogrula(BankaHesabi hesap)
    {
        hesap.Kod = hesap.Kod?.Trim().ToUpperInvariant() ?? string.Empty;
        hesap.Ad = hesap.Ad?.Trim() ?? string.Empty;
        hesap.Sube = hesap.Sube?.Trim() ?? string.Empty;
        hesap.HesapNo = hesap.HesapNo?.Trim() ?? string.Empty;
        hesap.ParaBirimi = string.IsNullOrWhiteSpace(hesap.ParaBirimi) ? "TRY" : hesap.ParaBirimi.Trim().ToUpperInvariant();
        hesap.MuhasebeKodu = string.IsNullOrWhiteSpace(hesap.MuhasebeKodu) ? "102.01.001" : hesap.MuhasebeKodu.Trim();

        if (string.IsNullOrWhiteSpace(hesap.Kod) || hesap.Kod.Length > 30)
            throw new ArgumentException("Banka hesap kodu boş olamaz ve en fazla 30 karakter olabilir.");
        if (string.IsNullOrWhiteSpace(hesap.Ad) || hesap.Ad.Length > 100)
            throw new ArgumentException("Banka / hesap adı boş olamaz ve en fazla 100 karakter olabilir.");
        if (hesap.ParaBirimi.Length != 3 || !hesap.ParaBirimi.All(char.IsLetter))
            throw new ArgumentException("Para birimi 3 harfli ISO kodu olmalıdır (örn. TRY).");

        var iban = hesap.Iban?.Trim() ?? string.Empty;
        if (iban.Length > 0)
        {
            if (!IbanDogrulayici.Gecerlidir(iban, out var gecerliIban))
                throw new ArgumentException("Geçerli bir IBAN giriniz.");
            iban = gecerliIban;
        }
        else if (hesap.HesapTuru is BankaHesapTuru.VadesizTL or BankaHesapTuru.VadesizDoviz)
        {
            throw new ArgumentException("Vadesiz banka hesapları için IBAN zorunludur.");
        }
        hesap.Iban = iban;
        if (string.IsNullOrWhiteSpace(hesap.HesapNo))
            hesap.HesapNo = iban;

        // Döviz yalnız vadesiz döviz hesabında tutulur; POS ve kredi akışları TL çalışır.
        if (hesap.HesapTuru == BankaHesapTuru.VadesizDoviz && !FinansKurallari.DovizliMi(hesap.ParaBirimi))
            throw new ArgumentException("Vadesiz döviz hesabının para birimi TRY dışında olmalıdır (örn. USD, EUR).");
        if (hesap.HesapTuru != BankaHesapTuru.VadesizDoviz && FinansKurallari.DovizliMi(hesap.ParaBirimi))
            throw new ArgumentException("Dövizli banka hesabı için hesap türü 'Vadesiz Döviz' seçilmelidir.");

        if (hesap.KrediLimiti < 0)
            throw new ArgumentException("Kredi limiti negatif olamaz.");
        if (hesap.HesapTuru != BankaHesapTuru.KrediHesabi && hesap.KrediLimiti > 0)
            throw new ArgumentException("Kredi limiti yalnızca kredi hesaplarında tanımlanabilir.");
        if (hesap.PosKomisyonOrani < 0 || hesap.PosKomisyonOrani > 100)
            throw new ArgumentException("POS komisyon oranı 0 ile 100 arasında olmalıdır.");
        if (hesap.ValorGunu < 0 || hesap.ValorGunu > 365)
            throw new ArgumentException("Valör günü 0 ile 365 arasında olmalıdır.");
        if (!hesap.MuhasebeKodu.StartsWith("102", StringComparison.Ordinal) &&
            !hesap.MuhasebeKodu.StartsWith("108", StringComparison.Ordinal) &&
            !hesap.MuhasebeKodu.StartsWith("300", StringComparison.Ordinal))
            throw new ArgumentException("Banka muhasebe kodu 102, 108 veya 300 hesap grubu altında olmalıdır.");
    }

    public async Task<BankaHesabi?> BankaHesabiGetirIdAsync(int id)
    {
        GoruntulemeYetkisi();
        return await _baglam.BankaHesaplari.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id);
    }

    // Banka hesabı otomatik uydurulmaz; kullanıcı gerçek hesabını tanımlamalıdır.
    public async Task<IReadOnlyList<BankaHesabi>> BankaHesaplariGetirAsync(bool sadeceAktifler = true)
    {
        GoruntulemeYetkisi();
        var query = _baglam.BankaHesaplari.AsQueryable();
        if (sadeceAktifler)
            query = query.Where(k => k.AktifMi);

        return await query.OrderBy(k => k.Kod).ToListAsync();
    }

    private async Task<int> HesapIdBulAsync(string? bankaAdi, string? hesapNo)
    {
        var no = hesapNo?.Trim() ?? string.Empty;
        if (no.StartsWith("TR", StringComparison.OrdinalIgnoreCase))
        {
            if (!IbanDogrulayici.Gecerlidir(no, out var iban))
                throw new ArgumentException("Geçerli bir IBAN giriniz.");
            no = iban;
        }

        var aktifler = _baglam.BankaHesaplari.Where(b => b.AktifMi);
        BankaHesabi? hesap = null;
        if (no.Length > 0)
            hesap = await aktifler.FirstOrDefaultAsync(b => b.Iban == no || b.HesapNo == no);
        if (hesap == null && !string.IsNullOrWhiteSpace(bankaAdi))
        {
            var ad = bankaAdi.Trim();
            var adaGore = await aktifler.Where(b => b.Ad == ad).Take(2).ToListAsync();
            if (adaGore.Count == 1) hesap = adaGore[0];
        }
        if (hesap == null)
        {
            var tumAktifler = await aktifler.Take(2).ToListAsync();
            if (tumAktifler.Count == 1) hesap = tumAktifler[0];
        }
        return hesap?.Id ?? throw new InvalidOperationException("İşlem yapılacak banka hesabı belirlenemedi. Lütfen bir banka hesabı seçin.");
    }

    private async Task<BankaHesabi> AktifHesapGetirAsync(int bankaHesabiId)
    {
        if (bankaHesabiId <= 0)
            throw new ArgumentException("Lütfen bir banka hesabı seçin.");
        var hesap = await _baglam.BankaHesaplari.FirstOrDefaultAsync(b => b.Id == bankaHesabiId)
            ?? throw new KeyNotFoundException("Banka hesabı bulunamadı.");
        if (!hesap.AktifMi)
            throw new InvalidOperationException($"'{hesap.Ad}' banka hesabı pasif durumda. Pasif hesaba işlem yapılamaz.");
        return hesap;
    }

    private async Task<BankaHesabi> AktifHesapGetirVeKilitleAsync(int bankaHesabiId)
    {
        if (bankaHesabiId > 0)
            await FinansKurallari.BankaHesabiKilitleAsync(_baglam, bankaHesabiId);
        return await AktifHesapGetirAsync(bankaHesabiId);
    }
}
