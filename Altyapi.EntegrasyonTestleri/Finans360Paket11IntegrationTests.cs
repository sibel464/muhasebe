using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Finans360;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class Finans360Paket11IntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public Finans360Paket11IntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    // Para çıkışı testleri boş kasa/hesapla başlayamaz; açılış bakiyesi devir fişiyle verilir.
    private static async Task AcilisBakiyesiEkleAsync(MuhasebeBaglami db, Guid sirketId, Kasa? kasa = null, BankaHesabi? banka = null, decimal tutar = 100000m)
    {
        if (kasa != null)
            db.KasaHareketleri.Add(new KasaHareketi { SirketId = sirketId, KasaId = kasa.Id, Tarih = DateTime.UtcNow.AddDays(-1), Tur = HareketTuru.Gelir, FisTuru = KasaFisTuru.AcilisDevir, Tutar = tutar, Aciklama = "Açılış devri" });
        if (banka != null)
            db.BankaHareketleri.Add(new BankaHareketi { SirketId = sirketId, BankaHesabiId = banka.Id, Tarih = DateTime.UtcNow.AddDays(-1), Tur = HareketTuru.Gelir, FisTuru = BankaFisTuru.BankaGelir, Tutar = tutar, Aciklama = "Açılış devri" });
        await db.SaveChangesAsync();
    }

    private async Task<(Guid SirketId, CariKart Cari, Kasa Kasa, BankaHesabi Banka)> SeedTemelVeriAsync(MuhasebeBaglami db)
    {
        var sirket = new EtkiAlani.Modeller.Kurumsal.Sirket { Id = Guid.NewGuid(), Unvan = "Test Sirket", SirketKodu = $"ST-{Guid.NewGuid():N}"[..10] };
        db.Sirketler.Add(sirket);

        var cari = new CariKart { SirketId = sirket.Id, Kod = $"C-{Guid.NewGuid():N}"[..8], Unvan = "Test Müşteri Ltd", Tur = CariTuru.MusteriTedarikci, Aktif = true };
        var kasa = new Kasa { SirketId = sirket.Id, Kod = $"K-{Guid.NewGuid():N}"[..8], Ad = "Merkez Kasa", Bakiye = 0m, AktifMi = true };
        var banka = new BankaHesabi { SirketId = sirket.Id, Kod = $"B-{Guid.NewGuid():N}"[..8], Ad = "Garanti BBVA", Iban = "TR123456789012345678901234", HesapNo = "123456", Bakiye = 0m, AktifMi = true };

        db.CariKartlar.Add(cari);
        db.Kasalar.Add(kasa);
        db.BankaHesaplari.Add(banka);

        // Hesap Planı
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "100"))
            db.HesapPlanlari.Add(new HesapPlani { SirketId = sirket.Id, HesapKodu = "100", HesapAdi = "Kasa", Tip = HesapTipi.Aktif, AktifMi = true });
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "102"))
            db.HesapPlanlari.Add(new HesapPlani { SirketId = sirket.Id, HesapKodu = "102", HesapAdi = "Banka", Tip = HesapTipi.Aktif, AktifMi = true });
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "108"))
            db.HesapPlanlari.Add(new HesapPlani { SirketId = sirket.Id, HesapKodu = "108", HesapAdi = "Diğer Hazır Değerler / POS", Tip = HesapTipi.Aktif, AktifMi = true });
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "120"))
            db.HesapPlanlari.Add(new HesapPlani { SirketId = sirket.Id, HesapKodu = "120", HesapAdi = "Alıcılar", Tip = HesapTipi.Aktif, AktifMi = true });
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "320"))
            db.HesapPlanlari.Add(new HesapPlani { SirketId = sirket.Id, HesapKodu = "320", HesapAdi = "Satıcılar", Tip = HesapTipi.Pasif, AktifMi = true });

        // Para Birimleri
        if (!await db.ParaBirimleri.AnyAsync(x => x.Id == 2))
            db.ParaBirimleri.Add(new EtkiAlani.Modeller.Kurumsal.ParaBirimi { Id = 2, Kod = "USD", Sembol = "$", Ad = "Amerikan Doları" });

        await db.SaveChangesAsync();
        return (sirket.Id, cari, kasa, banka);
    }

    private static IKullaniciServisi AuthorizedUser(bool permit = true) => new MockUserServisi(permit);

    private class MockUserServisi : IKullaniciServisi
    {
        private readonly bool _permit;
        public MockUserServisi(bool permit = true) => _permit = permit;

        public Kullanici? AktifKullanici => new() { Id = 1, KullaniciAdi = "TestUser", Ad = "Test", Soyad = "User", Yetkiler = AktifYetkiler };
        public KullaniciYetkileri? AktifYetkiler => new();
        public Task<bool> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken cancellationToken = default) => Task.FromResult(_permit);
        public void CikisYap() { }
        public bool YetkiliMi(Func<KullaniciYetkileri, bool> yetkiPredikati) => _permit;
        public void YetkiKontrolEt(Func<KullaniciYetkileri, bool> yetkiPredikati, string islemAdi)
        {
            if (!_permit) throw new YetkisizErisimException(islemAdi);
        }
        public Task<List<Kullanici>> TumunuGetirAsync() => Task.FromResult(new List<Kullanici>());
        public Task<Kullanici?> GetirAsync(int id) => Task.FromResult<Kullanici?>(AktifKullanici);
        public Task<Kullanici> EkleAsync(Kullanici kullanici, string sifre) => Task.FromResult(kullanici);
        public Task GuncelleAsync(Kullanici kullanici) => Task.CompletedTask;
        public Task SifreDegistirAsync(int id, string yeniSifre) => Task.CompletedTask;
        public Task SilAsync(int id) => Task.CompletedTask;
    }

    // 1. Nakit Tahsilat
    [Fact]
    public async Task Test_1_Nakit_Tahsilat()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        var result = await service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 1000m, DateTime.UtcNow, "Nakit Tahsilat", KasaId: kasa.Id));

        Assert.Equal(1000m, result.Tutar);
        Assert.Equal("Kasa", result.Kaynak);
        Assert.Single(await db.KasaHareketleri.Where(x => x.KasaId == kasa.Id && x.Tur == HareketTuru.Gelir).ToListAsync());
    }

    // 2. Banka Tahsilat
    [Fact]
    public async Task Test_2_Banka_Tahsilat()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, _, banka) = await SeedTemelVeriAsync(db);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        var result = await service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Tahsilat, OdemeYontemi.Banka, cari.Id, 2500m, DateTime.UtcNow, "Banka Tahsilat", BankaAdi: banka.Ad, BankaHesabiId: banka.Id));

        Assert.Equal(2500m, result.Tutar);
        Assert.Equal("Banka", result.Kaynak);
        Assert.Single(await db.BankaHareketleri.Where(x => x.BankaHesabiId == banka.Id && x.Tur == HareketTuru.Gelir).ToListAsync());
    }

    // 3. Kart Tahsilat
    [Fact]
    public async Task Test_3_Kart_Tahsilat()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, _, banka) = await SeedTemelVeriAsync(db);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        var result = await service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Tahsilat, OdemeYontemi.KrediKarti, cari.Id, 1500m, DateTime.UtcNow, "POS Kart Tahsilat", BankaAdi: banka.Ad, BankaHesabiId: banka.Id, TaksitSayisi: 3, KomisyonOrani: 2m));

        Assert.Equal(1500m, result.Tutar);
        Assert.Equal(OdemeYontemi.KrediKarti, result.OdemeYontemi);
    }

    // 4. Nakit Ödeme
    [Fact]
    public async Task Test_4_Nakit_Odeme()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        await AcilisBakiyesiEkleAsync(db, sirketId, kasa: kasa);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        var result = await service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Odeme, OdemeYontemi.Nakit, cari.Id, 800m, DateTime.UtcNow, "Nakit Ödeme", KasaId: kasa.Id));

        Assert.Equal(800m, result.Tutar);
        Assert.Single(await db.KasaHareketleri.Where(x => x.KasaId == kasa.Id && x.Tur == HareketTuru.Gider).ToListAsync());
    }

    // 5. Banka Ödeme
    [Fact]
    public async Task Test_5_Banka_Odeme()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, _, banka) = await SeedTemelVeriAsync(db);
        await AcilisBakiyesiEkleAsync(db, sirketId, banka: banka);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        var result = await service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Odeme, OdemeYontemi.Havale, cari.Id, 3000m, DateTime.UtcNow, "Banka Ödeme", BankaAdi: banka.Ad, BankaHesabiId: banka.Id));

        Assert.Equal(3000m, result.Tutar);
        Assert.Single(await db.BankaHareketleri.Where(x => x.BankaHesabiId == banka.Id && x.Tur == HareketTuru.Gider).ToListAsync());
    }

    // 6. Tahsilat Cari Bakiye
    [Fact]
    public async Task Test_6_Tahsilat_Cari_Bakiye()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        await service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 5000m, DateTime.UtcNow, "Tahsilat Bakiye", KasaId: kasa.Id));

        var cariHareket = await db.CariHareketler.SingleAsync(x => x.CariKartId == cari.Id);
        Assert.Equal(5000m, cariHareket.Alacak);
        Assert.Equal(0m, cariHareket.Borc);
    }

    // 7. Ödeme Cari Bakiye
    [Fact]
    public async Task Test_7_Odeme_Cari_Bakiye()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        await AcilisBakiyesiEkleAsync(db, sirketId, kasa: kasa);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        await service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Odeme, OdemeYontemi.Nakit, cari.Id, 2000m, DateTime.UtcNow, "Ödeme Bakiye", KasaId: kasa.Id));

        var cariHareket = await db.CariHareketler.SingleAsync(x => x.CariKartId == cari.Id);
        Assert.Equal(2000m, cariHareket.Borc);
        Assert.Equal(0m, cariHareket.Alacak);
    }

    // 8. Fatura Tam Kapama
    [Fact]
    public async Task Test_8_Fatura_Tam_Kapama()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        var toServis = new TahsilatOdemeServisi(db, AuthorizedUser());
        var tahsilat = await toServis.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 10000m, DateTime.UtcNow, "Fatura Kapama Tahsilat", KasaId: kasa.Id));
        var odenenCariHareket = await db.CariHareketler.SingleAsync(x => x.CariKartId == cari.Id && x.HareketTuru == CariHareketTuru.Tahsilat);

        var faturaCariHareket = new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow, HareketTuru = CariHareketTuru.SatisFaturasi, Aciklama = "Satış Faturası", Borc = 10000m, Alacak = 0m };
        db.CariHareketler.Add(faturaCariHareket);
        await db.SaveChangesAsync();

        var kapamaServis = new BelgeKapamaServisi(db);
        var kapama = await kapamaServis.KapatAsync(odenenCariHareket.Id, faturaCariHareket.Id, 10000m);

        Assert.Equal(10000m, kapama.KapananTutar);
        Assert.Equal(10000m, faturaCariHareket.KapaliTutar);
    }

    // 9. Fatura Kısmi Kapama
    [Fact]
    public async Task Test_9_Fatura_Kismi_Kapama()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        var toServis = new TahsilatOdemeServisi(db, AuthorizedUser());
        var tahsilat = await toServis.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 6000m, DateTime.UtcNow, "Kısmi Tahsilat", KasaId: kasa.Id));
        var odenenCariHareket = await db.CariHareketler.SingleAsync(x => x.CariKartId == cari.Id && x.HareketTuru == CariHareketTuru.Tahsilat);

        var faturaCariHareket = new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow, HareketTuru = CariHareketTuru.SatisFaturasi, Aciklama = "Satış Faturası", Borc = 10000m, Alacak = 0m };
        db.CariHareketler.Add(faturaCariHareket);
        await db.SaveChangesAsync();

        var kapamaServis = new BelgeKapamaServisi(db);
        var kapama = await kapamaServis.KapatAsync(odenenCariHareket.Id, faturaCariHareket.Id, 6000m);

        Assert.Equal(6000m, kapama.KapananTutar);
        Assert.Equal(6000m, faturaCariHareket.KapaliTutar);
    }

    // 10. Otomatik Kapama / Açık Belgeler Listeleme
    [Fact]
    public async Task Test_10_Otomatik_Kapama()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, _, _) = await SeedTemelVeriAsync(db);
        var f1 = new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow.AddDays(-5), HareketTuru = CariHareketTuru.SatisFaturasi, Aciklama = "FAT-A", Borc = 5000m, Alacak = 0m };
        var f2 = new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow, HareketTuru = CariHareketTuru.SatisFaturasi, Aciklama = "FAT-B", Borc = 5000m, Alacak = 0m };
        db.CariHareketler.AddRange(f1, f2);
        await db.SaveChangesAsync();

        var kapamaServis = new BelgeKapamaServisi(db);
        var acikBelgeler = await kapamaServis.AcikBelgeleriGetirAsync(cari.Id);

        Assert.Equal(2, acikBelgeler.Count);
    }

    // 11. Manuel Kapama
    [Fact]
    public async Task Test_11_Manuel_Kapama()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        var toServis = new TahsilatOdemeServisi(db, AuthorizedUser());
        var tahsilat = await toServis.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 4000m, DateTime.UtcNow, "Manuel Tahsilat", KasaId: kasa.Id));
        var odenenCariHareket = await db.CariHareketler.SingleAsync(x => x.CariKartId == cari.Id && x.HareketTuru == CariHareketTuru.Tahsilat);

        var faturaCariHareket = new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow, HareketTuru = CariHareketTuru.SatisFaturasi, Aciklama = "FAT-MAN", Borc = 4000m, Alacak = 0m };
        db.CariHareketler.Add(faturaCariHareket);
        await db.SaveChangesAsync();

        var kapamaServis = new BelgeKapamaServisi(db);
        await kapamaServis.KapatAsync(odenenCariHareket.Id, faturaCariHareket.Id, 4000m);
        Assert.Equal(4000m, faturaCariHareket.KapaliTutar);
    }

    // 12. Kasa Virman
    [Fact]
    public async Task Test_12_Kasa_Virman()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kasa1, banka) = await SeedTemelVeriAsync(db);
        await AcilisBakiyesiEkleAsync(db, sirketId, kasa: kasa1);
        var kasa2 = new Kasa { SirketId = sirketId, Kod = "K2", Ad = "Kasa B", Bakiye = 0m, AktifMi = true };
        db.Kasalar.Add(kasa2);
        await db.SaveChangesAsync();

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        await finansServis.KasaBankaTransferiYapAsync(new KasaBankaTransferIstegiDto(kasa1.Id, banka.Id, 5000m, "Kasa Virman"), sirketId, 1);

        var hGider = await db.KasaHareketleri.FirstOrDefaultAsync(x => x.KasaId == kasa1.Id && x.Tur == HareketTuru.Gider);
        Assert.NotNull(hGider);
        Assert.Equal(5000m, hGider.Tutar);
    }

    // 13. Banka Virman
    [Fact]
    public async Task Test_13_Banka_Virman()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, _, banka1) = await SeedTemelVeriAsync(db);
        await AcilisBakiyesiEkleAsync(db, sirketId, banka: banka1);
        var banka2 = new BankaHesabi { SirketId = sirketId, Kod = "B2", Ad = "İş Bankası", Iban = "TR999", Bakiye = 0m, AktifMi = true };
        db.BankaHesaplari.Add(banka2);
        await db.SaveChangesAsync();

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        await finansServis.BankaVirmaniYapAsync(new BankaVirmanIstegiDto(banka1.Id, banka2.Id, 7000m, 50m, "Banka Virmanı"), sirketId, 1);

        var bh1 = await db.BankaHareketleri.Where(x => x.BankaHesabiId == banka1.Id && x.Tur == HareketTuru.Gider).ToListAsync();
        var bh2 = await db.BankaHareketleri.FirstOrDefaultAsync(x => x.BankaHesabiId == banka2.Id && x.Tur == HareketTuru.Gelir);

        Assert.NotEmpty(bh1);
        Assert.NotNull(bh2);
        Assert.Equal(7000m, bh2.Tutar);
    }

    // 14. Kasa -> Banka
    [Fact]
    public async Task Test_14_Kasa_To_Banka()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kasa, banka) = await SeedTemelVeriAsync(db);
        await AcilisBakiyesiEkleAsync(db, sirketId, kasa: kasa);

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        await finansServis.KasaBankaTransferiYapAsync(new KasaBankaTransferIstegiDto(kasa.Id, banka.Id, 2000m, "Kasa -> Banka Yatırma"), sirketId, 1);

        var kh = await db.KasaHareketleri.SingleAsync(x => x.KasaId == kasa.Id && x.FisTuru == KasaFisTuru.BankayaYatirma);
        var bh = await db.BankaHareketleri.SingleAsync(x => x.BankaHesabiId == banka.Id);

        Assert.Equal(HareketTuru.Gider, kh.Tur);
        Assert.Equal(HareketTuru.Gelir, bh.Tur);
        Assert.Equal(2000m, kh.Tutar);
        Assert.Equal(2000m, bh.Tutar);
    }

    // 15. Banka -> Kasa
    [Fact]
    public async Task Test_15_Banka_To_Kasa()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kasa, banka) = await SeedTemelVeriAsync(db);
        await AcilisBakiyesiEkleAsync(db, sirketId, banka: banka);

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        await finansServis.BankaKasaTransferiYapAsync(new BankaKasaTransferIstegiDto(banka.Id, kasa.Id, 3000m, "Bankadan Para Çekme"), sirketId, 1);

        var bh = await db.BankaHareketleri.SingleAsync(x => x.BankaHesabiId == banka.Id && x.FisTuru == BankaFisTuru.KasadanCekme);
        var kh = await db.KasaHareketleri.SingleAsync(x => x.KasaId == kasa.Id);

        Assert.Equal(HareketTuru.Gider, bh.Tur);
        Assert.Equal(HareketTuru.Gelir, kh.Tur);
        Assert.Equal(3000m, bh.Tutar);
        Assert.Equal(3000m, kh.Tutar);
    }

    // 16. Virman Rollback
    [Fact]
    public async Task Test_16_Virman_Rollback()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kasa, _) = await SeedTemelVeriAsync(db);

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        await Assert.ThrowsAnyAsync<Exception>(() => finansServis.KasaBankaTransferiYapAsync(new KasaBankaTransferIstegiDto(kasa.Id, 999999, 1000m, "Hatalı Transfer"), sirketId, 1));

        Assert.Empty(await db.KasaHareketleri.Where(x => x.KasaId == kasa.Id).ToListAsync());
    }

    // 17. Tahsilat Rollback
    [Fact]
    public async Task Test_17_Tahsilat_Rollback()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, _, _) = await SeedTemelVeriAsync(db);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        await Assert.ThrowsAnyAsync<Exception>(() => service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, -500m, DateTime.UtcNow, "Geçersiz Tutar")));

        Assert.Empty(await db.CariHareketler.Where(x => x.CariKartId == cari.Id).ToListAsync());
    }

    // 18. Ödeme Rollback
    [Fact]
    public async Task Test_18_Odeme_Rollback()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, _, _) = await SeedTemelVeriAsync(db);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        await Assert.ThrowsAnyAsync<Exception>(() => service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Odeme, OdemeYontemi.Nakit, 99999, 100m, DateTime.UtcNow, "Geçersiz Cari")));

        Assert.Empty(await db.CariHareketler.Where(x => x.CariKartId == cari.Id).ToListAsync());
    }

    // 19. İptal Ters Kayıt
    [Fact]
    public async Task Test_19_Iptal_Ters_Kayit()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        var kayit = await service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 1200m, DateTime.UtcNow, "Tahsilat İptal Testi", KasaId: kasa.Id));

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        await finansServis.FinansIslemIptalEtAsync(kayit.HareketId, "Kasa", sirketId, 1, "Müşteri vazgeçti");

        var asil = await db.KasaHareketleri.FindAsync(kayit.HareketId);
        Assert.NotNull(asil);
        Assert.Equal(FinansIslemDurumu.IptalEdildi, asil.IslemDurumu);
        Assert.NotNull(asil.TersKayitId);

        var ters = await db.KasaHareketleri.FindAsync(asil.TersKayitId.Value);
        Assert.NotNull(ters);
        Assert.Equal(FinansIslemDurumu.TersKayit, ters.IslemDurumu);
        Assert.Equal(HareketTuru.Gider, ters.Tur);
    }

    // 20. Double Cancel Engeli
    [Fact]
    public async Task Test_20_Double_Cancel_Engeli()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        var kayit = await service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 1500m, DateTime.UtcNow, "Double İptal", KasaId: kasa.Id));

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        await finansServis.FinansIslemIptalEtAsync(kayit.HareketId, "Kasa", sirketId, 1, "İlk İptal");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finansServis.FinansIslemIptalEtAsync(kayit.HareketId, "Kasa", sirketId, 1, "Tekrar İptal"));
    }

    // 21. POS Bekleyen
    [Fact]
    public async Task Test_21_POS_Bekleyen()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, _, banka) = await SeedTemelVeriAsync(db);

        var pos = new PosIslemi
        {
            SirketId = sirketId,
            BankaHesabiId = banka.Id,
            BankaAdi = banka.Ad,
            PosCihazAdi = "POS-01",
            KartTuru = "Bonus",
            TaksitSayisi = 1,
            BrutTutar = 1000m,
            KomisyonOrani = 2m,
            KomisyonTutar = 20m,
            NetTutar = 980m,
            IslemTarihi = DateTime.UtcNow,
            BeklenenHesabaGecisTarihi = DateTime.UtcNow.AddDays(1),
            Durum = PosIslemDurumu.Bekliyor
        };
        db.PosIslemleri.Add(pos);
        await db.SaveChangesAsync();

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        var poslar = await finansServis.GetPosIslemleriAsync(sirketId);

        Assert.Single(poslar);
        Assert.Equal(PosIslemDurumu.Bekliyor, poslar[0].Durum);
    }

    // 22. POS Hesaba Geçiş
    [Fact]
    public async Task Test_22_POS_Hesaba_Gecis()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, _, banka) = await SeedTemelVeriAsync(db);

        var pos = new PosIslemi
        {
            SirketId = sirketId,
            BankaHesabiId = banka.Id,
            BankaAdi = banka.Ad,
            PosCihazAdi = "POS-01",
            KartTuru = "Bonus",
            TaksitSayisi = 1,
            BrutTutar = 1000m,
            KomisyonOrani = 2m,
            KomisyonTutar = 20m,
            NetTutar = 980m,
            IslemTarihi = DateTime.UtcNow,
            BeklenenHesabaGecisTarihi = DateTime.UtcNow.AddDays(1),
            Durum = PosIslemDurumu.Bekliyor
        };
        db.PosIslemleri.Add(pos);
        await db.SaveChangesAsync();

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        await finansServis.PosIslemiHesabaGecirAsync(pos.Id, sirketId, 1);

        var guncelPos = await db.PosIslemleri.FindAsync(pos.Id);
        Assert.NotNull(guncelPos);
        Assert.Equal(PosIslemDurumu.HesabaGecti, guncelPos.Durum);
        Assert.NotNull(guncelPos.HesabaGecisTarihi);

        var bh = await db.BankaHareketleri.SingleAsync(x => x.BankaHesabiId == banka.Id);
        Assert.Equal(980m, bh.Tutar);
    }

    // 23. POS Double Settlement Engeli
    [Fact]
    public async Task Test_23_POS_Double_Settlement_Engeli()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, _, banka) = await SeedTemelVeriAsync(db);

        var pos = new PosIslemi
        {
            SirketId = sirketId,
            BankaHesabiId = banka.Id,
            BankaAdi = banka.Ad,
            PosCihazAdi = "POS-01",
            KartTuru = "Bonus",
            TaksitSayisi = 1,
            BrutTutar = 1000m,
            KomisyonOrani = 2m,
            KomisyonTutar = 20m,
            NetTutar = 980m,
            IslemTarihi = DateTime.UtcNow,
            BeklenenHesabaGecisTarihi = DateTime.UtcNow.AddDays(1),
            Durum = PosIslemDurumu.Bekliyor
        };
        db.PosIslemleri.Add(pos);
        await db.SaveChangesAsync();

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        await finansServis.PosIslemiHesabaGecirAsync(pos.Id, sirketId, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finansServis.PosIslemiHesabaGecirAsync(pos.Id, sirketId, 1));
    }

    // 24. Kasa Yürüyen Bakiye
    [Fact]
    public async Task Test_24_Kasa_Yuruyen_Bakiye()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);

        db.KasaHareketleri.Add(new KasaHareketi { SirketId = sirketId, KasaId = kasa.Id, Tarih = DateTime.UtcNow.AddHours(-3), Tur = HareketTuru.Gelir, Tutar = 1000m, IslemDurumu = FinansIslemDurumu.Tamamlandi });
        db.KasaHareketleri.Add(new KasaHareketi { SirketId = sirketId, KasaId = kasa.Id, Tarih = DateTime.UtcNow.AddHours(-2), Tur = HareketTuru.Gider, Tutar = 300m, IslemDurumu = FinansIslemDurumu.Tamamlandi });
        db.KasaHareketleri.Add(new KasaHareketi { SirketId = sirketId, KasaId = kasa.Id, Tarih = DateTime.UtcNow.AddHours(-1), Tur = HareketTuru.Gelir, Tutar = 500m, IslemDurumu = FinansIslemDurumu.Tamamlandi });
        await db.SaveChangesAsync();

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        var hareketler = await finansServis.GetKasaHareketleri360Async(kasa.Id, sirketId);

        Assert.Equal(3, hareketler.Count);
        Assert.Equal(1200m, hareketler[0].Bakiye); // Son durum yürüyen bakiye
    }

    // 25. Banka Yürüyen Bakiye
    [Fact]
    public async Task Test_25_Banka_Yuruyen_Bakiye()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, _, banka) = await SeedTemelVeriAsync(db);

        db.BankaHareketleri.Add(new BankaHareketi { SirketId = sirketId, BankaHesabiId = banka.Id, Tarih = DateTime.UtcNow.AddHours(-3), Tur = HareketTuru.Gelir, Tutar = 5000m, IslemDurumu = FinansIslemDurumu.Tamamlandi });
        db.BankaHareketleri.Add(new BankaHareketi { SirketId = sirketId, BankaHesabiId = banka.Id, Tarih = DateTime.UtcNow.AddHours(-2), Tur = HareketTuru.Gider, Tutar = 1500m, IslemDurumu = FinansIslemDurumu.Tamamlandi });
        await db.SaveChangesAsync();

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        var hareketler = await finansServis.GetBankaHareketleri360Async(banka.Id, sirketId);

        Assert.Equal(2, hareketler.Count);
        Assert.Equal(3500m, hareketler[0].Bakiye);
    }

    // 26. Mutabakat Farkı
    [Fact]
    public async Task Test_26_Mutabakat_Farki()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kasa, _) = await SeedTemelVeriAsync(db);

        db.KasaHareketleri.Add(new KasaHareketi { SirketId = sirketId, KasaId = kasa.Id, Tarih = DateTime.UtcNow, Tur = HareketTuru.Gelir, Tutar = 1000m, IslemDurumu = FinansIslemDurumu.Tamamlandi });
        await db.SaveChangesAsync();

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        await finansServis.KasaMutabakatKaydetAsync(new KasaMutabakatKayitDto(kasa.Id, 950m, "Kasa noksanı var", DuzeltmeHareketOlustur: true), sirketId, 1);

        var mutabakat = await db.KasaSayimMutabakatlari.SingleAsync(x => x.KasaId == kasa.Id);
        Assert.Equal(1000m, mutabakat.SistemBakiyesi);
        Assert.Equal(950m, mutabakat.FizikiToplam);
        Assert.Equal(-50m, mutabakat.Fark);
    }

    // 27. Firma İzolasyonu
    [Fact]
    public async Task Test_27_Firma_Izolasyonu()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketA, _, kasaA, _) = await SeedTemelVeriAsync(db);
        var (sirketB, _, kasaB, _) = await SeedTemelVeriAsync(db);

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        var kasalarA = await finansServis.GetKasalarAsync(sirketA);

        Assert.Single(kasalarA);
        Assert.Equal(kasaA.Id, kasalarA[0].Id);
        Assert.DoesNotContain(kasalarA, x => x.Id == kasaB.Id);
    }

    // 28. Cross-company Access Prevention
    [Fact]
    public async Task Test_28_Cross_Company_Prevention()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketA, _, kasaA, bankaA) = await SeedTemelVeriAsync(db);
        var (sirketB, _, kasaB, bankaB) = await SeedTemelVeriAsync(db);

        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser());
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            finansServis.KasaBankaTransferiYapAsync(new KasaBankaTransferIstegiDto(kasaA.Id, bankaB.Id, 1000m, "Cross Company"), sirketA, 1));
    }

    // 29. Yetkisiz Tahsilat
    [Fact]
    public async Task Test_29_Yetkisiz_Tahsilat()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser(permit: false));

        await Assert.ThrowsAsync<YetkisizErisimException>(() => service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 500m, DateTime.UtcNow, "Yetkisiz", KasaId: kasa.Id)));
    }

    // 30. Yetkisiz Ödeme
    [Fact]
    public async Task Test_30_Yetkisiz_Odeme()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser(permit: false));

        await Assert.ThrowsAsync<YetkisizErisimException>(() => service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Odeme, OdemeYontemi.Nakit, cari.Id, 500m, DateTime.UtcNow, "Yetkisiz", KasaId: kasa.Id)));
    }

    // 31. Yetkisiz Virman
    [Fact]
    public async Task Test_31_Yetkisiz_Virman()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kasa, banka) = await SeedTemelVeriAsync(db);
        var finansServis = new FinansMerkeziServisi(db, AuthorizedUser(permit: false));

        await Assert.ThrowsAsync<YetkisizErisimException>(() =>
            finansServis.KasaBankaTransferiYapAsync(new KasaBankaTransferIstegiDto(kasa.Id, banka.Id, 1000m, "Yetkisiz Virman"), sirketId, 1));
    }

    // 32. Belge Zinciri
    [Fact]
    public async Task Test_32_Belge_Zinciri()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        var kayit = await service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 2500m, DateTime.UtcNow, "Belge Zinciri Test", BelgeNo: "BLG-999", KasaId: kasa.Id));

        var kh = await db.KasaHareketleri.FindAsync(kayit.HareketId);
        Assert.NotNull(kh);
        Assert.NotNull(kh.IlgiliBelgeId);

        var ch = await db.CariHareketler.FindAsync(kh.IlgiliBelgeId.Value);
        Assert.NotNull(ch);
        Assert.Equal(cari.Id, ch.CariKartId);

        var yevmiye = await db.YevmiyeFisleri.FirstOrDefaultAsync(x => x.BelgeId == kayit.HareketId && x.BelgeTipi == "TahsilatOdeme");
        Assert.NotNull(yevmiye);
    }

    // 33. Döviz Tutar / Kur Snapshot
    [Fact]
    public async Task Test_33_Doviz_Tutar_Kur_Snapshot()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);
        var service = new TahsilatOdemeServisi(db, AuthorizedUser());

        var result = await service.KaydetAsync(new TahsilatOdemeTalebi(
            FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 32000m, DateTime.UtcNow, "USD Tahsilat", KasaId: kasa.Id, ParaBirimiId: 2, Kur: 32.0m, DovizTutar: 1000m));

        Assert.Equal(32000m, result.Tutar);
        Assert.Equal(32.0m, result.Kur);
        Assert.Equal(1000m, result.DovizTutar);

        var kh = await db.KasaHareketleri.FindAsync(result.HareketId);
        Assert.NotNull(kh);
        Assert.Equal(32.0m, kh.Kur);
        Assert.Equal(1000m, kh.DovizTutar);
    }

    // 34. Concurrency Test
    [Fact]
    public async Task Test_34_Concurrency_Test()
    {
        await using var dbSeed = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari1, kasa, _) = await SeedTemelVeriAsync(dbSeed);

        var cari2 = new CariKart { SirketId = sirketId, Kod = $"C-{Guid.NewGuid():N}"[..8], Unvan = "Test Müşteri 2", Tur = CariTuru.MusteriTedarikci, Aktif = true };
        dbSeed.CariKartlar.Add(cari2);
        await dbSeed.SaveChangesAsync();

        await using var db1 = new MuhasebeBaglami(_fixture.Options);
        await using var db2 = new MuhasebeBaglami(_fixture.Options);

        var service1 = new TahsilatOdemeServisi(db1, AuthorizedUser());
        var service2 = new TahsilatOdemeServisi(db2, AuthorizedUser());

        var t1 = service1.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari1.Id, 100m, DateTime.UtcNow, "Conc 1", KasaId: kasa.Id));
        var t2 = service2.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari2.Id, 200m, DateTime.UtcNow, "Conc 2", KasaId: kasa.Id));

        await Task.WhenAll(t1, t2);

        await using var dbCheck = new MuhasebeBaglami(_fixture.Options);
        var toplamGiris = await dbCheck.KasaHareketleri.Where(x => x.KasaId == kasa.Id).SumAsync(x => x.Tutar);
        Assert.Equal(300m, toplamGiris);
    }
}

