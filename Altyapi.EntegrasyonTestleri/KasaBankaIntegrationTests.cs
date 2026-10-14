using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public class KasaBankaIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;

    public KasaBankaIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private async Task<(Sirket sirket, CariKart cari)> SeedSirketVeCariAsync(MuhasebeBaglami db)
    {
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Finans Test A.Ş.", SirketKodu = $"FN-{Guid.NewGuid():N}" };
        db.Sirketler.Add(sirket);
        await db.SaveChangesAsync();

        var cari = new CariKart { SirketId = sirket.Id, Kod = $"C-FN-{Guid.NewGuid():N}", Unvan = "Finans Cari Ltd.", Aktif = true };
        db.CariKartlar.Add(cari);
        await db.SaveChangesAsync();

        return (sirket, cari);
    }

    [Fact]
    public async Task Kasa_Gelir_Gider_Hareketi_Kaydeder_Ve_Varsayilan_Kasa_Olusturur()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (_, cari) = await SeedSirketVeCariAsync(db);

        var kasaServisi = new KasaServisi(db, StabilizasyonTestOturumu.Olustur());

        var gelir = new KasaHareketi
        {
            Tur = HareketTuru.Gelir,
            FisTuru = KasaFisTuru.Tahsilat,
            Tutar = 1500m,
            Aciklama = "Nakit Tahsilat",
            Tarih = DateTime.UtcNow,
            CariKartId = cari.Id
        };

        var eklenenGelir = await kasaServisi.EkleAsync(gelir);
        Assert.NotNull(eklenenGelir);
        Assert.True(eklenenGelir.Id > 0);
        Assert.True(eklenenGelir.KasaId.HasValue);

        var tumHareketler = await kasaServisi.TumunuGetirAsync();
        Assert.Contains(tumHareketler, x => x.Id == eklenenGelir.Id && x.Tutar == 1500m);
    }

    [Fact]
    public async Task Banka_Gelir_Gider_Hareketi_Kaydeder_Ve_Tarih_Filtresiyle_Listeler()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (_, cari) = await SeedSirketVeCariAsync(db);

        var bankaHesabi = new BankaHesabi
        {
            Kod = $"BNK-{Guid.NewGuid():N}",
            Ad = "Test Bankası",
            HesapTuru = BankaHesapTuru.VadesizTL,
            HesapNo = "1234567890",
            Iban = "TR2600061000123456789005001",
            AktifMi = true
        };
        db.BankaHesaplari.Add(bankaHesabi);
        await db.SaveChangesAsync();

        var bankaServisi = new BankaServisi(db, StabilizasyonTestOturumu.Olustur());

        var bankaHareketi = new BankaHareketi
        {
            BankaHesabiId = bankaHesabi.Id,
            BankaAdi = bankaHesabi.Ad,
            HesapNo = bankaHesabi.HesapNo,
            Tur = HareketTuru.Gelir,
            FisTuru = BankaFisTuru.GelenHavale,
            Tutar = 5000m,
            Aciklama = "Havale Tahsilatı",
            Tarih = DateTime.UtcNow,
            CariKartId = cari.Id,
            OdemeYontemi = OdemeYontemi.Havale
        };

        var eklenen = await bankaServisi.EkleAsync(bankaHareketi);
        Assert.NotNull(eklenen);
        Assert.True(eklenen.Id > 0);
        Assert.Equal(bankaHesabi.Id, eklenen.BankaHesabiId);

        var filtrelenmis = await bankaServisi.TarihAraligindaGetirAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        Assert.Contains(filtrelenmis, x => x.Id == eklenen.Id && x.Tutar == 5000m);
    }

    private static async Task<(Kasa Kasa, BankaHesabi Banka1, BankaHesabi Banka2)> SeedHesaplarAsync(MuhasebeBaglami db, Guid sirketId)
    {
        var kasa = new Kasa { SirketId = sirketId, Kod = $"K-{Guid.NewGuid():N}"[..12], Ad = "Test Kasa", AktifMi = true };
        var b1 = new BankaHesabi { SirketId = sirketId, Kod = $"B1-{Guid.NewGuid():N}"[..12], Ad = "Banka Bir", Iban = "TR2600061000123456789005001", AktifMi = true };
        var b2 = new BankaHesabi { SirketId = sirketId, Kod = $"B2-{Guid.NewGuid():N}"[..12], Ad = "Banka İki", Iban = "TR2600061000123456789005001", AktifMi = true };
        db.AddRange(kasa, b1, b2);
        await db.SaveChangesAsync();
        return (kasa, b1, b2);
    }

    [Fact]
    public async Task Bos_kasadan_odeme_ve_virman_yapilamaz()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirket, cari) = await SeedSirketVeCariAsync(db);
        var (kasa, banka, _) = await SeedHesaplarAsync(db, sirket.Id);
        var servis = new KasaServisi(db, StabilizasyonTestOturumu.Olustur());

        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.OdemeEkleAsync(kasa.Id, cari.Id, 100m, "Ödeme"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.BankayaParaYatirAsync(kasa.Id, banka.Id, 100m, "Yatırma"));

        await servis.TahsilatEkleAsync(kasa.Id, cari.Id, 150m, "Tahsilat");
        await servis.OdemeEkleAsync(kasa.Id, cari.Id, 150m, "Ödeme");
        Assert.Equal(0m, await servis.KasaBakiyeHesaplaAsync(kasa.Id));
    }

    [Fact]
    public async Task Banka_tahsilati_secilen_hesaba_yazilir()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirket, cari) = await SeedSirketVeCariAsync(db);
        var (_, _, ikinci) = await SeedHesaplarAsync(db, sirket.Id);
        var servis = new BankaServisi(db, StabilizasyonTestOturumu.Olustur());

        var hareket = await servis.HesabaTahsilatEkleAsync(ikinci.Id, cari.Id, 750m, "Havale");

        Assert.Equal(ikinci.Id, hareket.BankaHesabiId);
        Assert.Equal(750m, await servis.HesapBakiyeHesaplaAsync(ikinci.Id));
    }

    [Fact]
    public async Task Bankaya_yatirma_silinince_banka_bacagi_da_silinir()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirket, cari) = await SeedSirketVeCariAsync(db);
        var (kasa, banka, _) = await SeedHesaplarAsync(db, sirket.Id);
        var kasaServisi = new KasaServisi(db, StabilizasyonTestOturumu.Olustur());
        await kasaServisi.TahsilatEkleAsync(kasa.Id, cari.Id, 500m, "Tahsilat");
        await kasaServisi.BankayaParaYatirAsync(kasa.Id, banka.Id, 200m, "Yatırma");

        var cikis = await db.KasaHareketleri.SingleAsync(x => x.KasaId == kasa.Id && x.FisTuru == KasaFisTuru.BankayaYatirma);
        Assert.True(await kasaServisi.SilAsync(cikis.Id));

        Assert.Equal(500m, await kasaServisi.KasaBakiyeHesaplaAsync(kasa.Id));
        Assert.Equal(0m, await new BankaServisi(db, StabilizasyonTestOturumu.Olustur()).HesapBakiyeHesaplaAsync(banka.Id));
    }

    [Fact]
    public async Task Kilitli_doneme_kasa_hareketi_girilemez()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirket, cari) = await SeedSirketVeCariAsync(db);
        var (kasa, _, _) = await SeedHesaplarAsync(db, sirket.Id);
        var gecen = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-40), DateTimeKind.Utc);
        db.MaliDonemler.Add(new MaliDonem { SirketId = sirket.Id, DonemAdi = "Kilitli", Baslangic = gecen.AddDays(-5), Bitis = gecen.AddDays(5), KilitliMi = true });
        await db.SaveChangesAsync();

        var servis = new KasaServisi(db, StabilizasyonTestOturumu.Olustur());
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.TahsilatEkleAsync(kasa.Id, cari.Id, 100m, "Eski tarihli", tarih: gecen));
    }

    [Fact]
    public async Task Kasa_ve_banka_tanimi_yetkisiz_kullaniciya_kapali()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var yetkisiz = new Cari360TestKullanici();

        await Assert.ThrowsAsync<Uygulama.Arayuzler.YetkisizErisimException>(() => new KasaServisi(db, yetkisiz).KasaEkleAsync(new Kasa { Kod = "X", Ad = "X" }));
        await Assert.ThrowsAsync<Uygulama.Arayuzler.YetkisizErisimException>(() => new BankaServisi(db, yetkisiz).BankaHesabiEkleAsync(new BankaHesabi { Kod = "X", Ad = "X" }));
        await Assert.ThrowsAsync<Uygulama.Arayuzler.YetkisizErisimException>(() => new KasaServisi(db).TumunuGetirAsync());
    }
}
