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

    // Benzersiz ve sağlama haneleri doğru bir TR IBAN üretir (IBAN tekil olmalı).
    private static string RastgeleIban()
    {
        var bban = "000610" + string.Concat(Enumerable.Range(0, 16).Select(_ => Random.Shared.Next(10)));
        var sayi = System.Numerics.BigInteger.Parse(bban + "2927" + "00");
        return $"TR{98 - (int)(sayi % 97):00}{bban}";
    }

    [Fact]
    public async Task Dovizli_kasa_ve_banka_doviz_tutari_ve_kurla_calisir()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirket, cari) = await SeedSirketVeCariAsync(db);
        var oturum = StabilizasyonTestOturumu.Olustur();
        var kasaServisi = new KasaServisi(db, oturum);
        var bankaServisi = new BankaServisi(db, oturum);
        var kod = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        // Hesap türü ile para birimi uyumlu olmalı
        await Assert.ThrowsAsync<ArgumentException>(() => bankaServisi.BankaHesabiEkleAsync(new BankaHesabi { SirketId = sirket.Id, Kod = "X" + kod, Ad = "Yanlış", ParaBirimi = "USD", Iban = RastgeleIban() }));
        await Assert.ThrowsAsync<ArgumentException>(() => bankaServisi.BankaHesabiEkleAsync(new BankaHesabi { SirketId = sirket.Id, Kod = "Y" + kod, Ad = "Yanlış", HesapTuru = BankaHesapTuru.VadesizDoviz, Iban = RastgeleIban() }));

        var usdKasa = await kasaServisi.KasaEkleAsync(new Kasa { SirketId = sirket.Id, Kod = "U" + kod, Ad = "USD Kasa", ParaBirimi = "USD" });
        var tlKasa = await kasaServisi.KasaEkleAsync(new Kasa { SirketId = sirket.Id, Kod = "T" + kod, Ad = "TL Kasa" });
        var usdBanka = await bankaServisi.BankaHesabiEkleAsync(new BankaHesabi { SirketId = sirket.Id, Kod = "B" + kod, Ad = "USD Banka", ParaBirimi = "USD", HesapTuru = BankaHesapTuru.VadesizDoviz, Iban = RastgeleIban() });

        // Açılış: 1.000 USD × 40 = 40.000 TL; döviz tutarı/kur girilmeden dövizli kasaya fiş girilemez
        await Assert.ThrowsAsync<ArgumentException>(() => kasaServisi.EkleAsync(new KasaHareketi { KasaId = usdKasa.Id, FisTuru = KasaFisTuru.AcilisDevir, Tutar = 100m, Aciklama = "Kursuz" }));
        var acilis = await kasaServisi.EkleAsync(new KasaHareketi { KasaId = usdKasa.Id, FisTuru = KasaFisTuru.AcilisDevir, DovizTutar = 1000m, Kur = 40m, Aciklama = "Açılış" });
        Assert.Equal(40000m, acilis.Tutar);
        Assert.Equal(1000m, await new KasaServisi(db, oturum).KasaDovizBakiyeHesaplaAsync(usdKasa.Id));

        // Bakiye döviz cinsinden denetlenir
        var yetersiz = await Assert.ThrowsAsync<InvalidOperationException>(() => kasaServisi.EkleAsync(new KasaHareketi { KasaId = usdKasa.Id, FisTuru = KasaFisTuru.Gider, DovizTutar = 1500m, Kur = 40m, Aciklama = "Fazla" }));
        Assert.Contains("USD", yetersiz.Message);

        // Cari tahsilat: 200 USD × 42 = 8.400 TL; cari TL karşılığıyla alacaklanır
        var tahsilatServisi = new TahsilatOdemeServisi(db, oturum);
        var tahsilat = await tahsilatServisi.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 0m,
            DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc), "USD tahsilat", Kur: 42m, DovizTutar: 200m, KasaId: usdKasa.Id));
        Assert.Equal(8400m, tahsilat.Tutar);
        var cariHareket = await db.CariHareketler.SingleAsync(c => c.CariKartId == cari.Id && c.HareketTuru == CariHareketTuru.Tahsilat);
        Assert.Equal(8400m, cariHareket.Alacak);
        Assert.Equal(0m, cariHareket.DovizAlacak);
        Assert.Equal(1200m, await new KasaServisi(db, oturum).KasaDovizBakiyeHesaplaAsync(usdKasa.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => tahsilatServisi.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 500m,
            DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc), "Kursuz", KasaId: usdKasa.Id)));

        // USD kasadan USD bankaya 600 USD: TL karşılığı ortalama defter kuruyla (48.400 / 1.200)
        await kasaServisi.BankayaParaYatirAsync(usdKasa.Id, usdBanka.Id, 600m, "Yatırma");
        Assert.Equal(600m, await new KasaServisi(db, oturum).KasaDovizBakiyeHesaplaAsync(usdKasa.Id));
        Assert.Equal(600m, await bankaServisi.HesapDovizBakiyeHesaplaAsync(usdBanka.Id));
        Assert.Equal(24200m, await bankaServisi.HesapBakiyeHesaplaAsync(usdBanka.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => kasaServisi.BankayaParaYatirAsync(tlKasa.Id, usdBanka.Id, 10m, "Farklı para"));

        // Döviz bilgisi taşımayan akış (fatura peşin, POS...) dövizli hesaba yazamaz
        db.KasaHareketleri.Add(new KasaHareketi { SirketId = sirket.Id, KasaId = usdKasa.Id, Tur = HareketTuru.Gelir, FisTuru = KasaFisTuru.Gelir, Tutar = 100m, Aciklama = "Dövizsiz" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // Tahsilat iptali aynı döviz tutarıyla ters kayıt atar
        await new TahsilatOdemeServisi(db, oturum).IptalEtAsync("Kasa", tahsilat.HareketId, "Yanlış giriş");
        Assert.Equal(400m, await new KasaServisi(db, oturum).KasaDovizBakiyeHesaplaAsync(usdKasa.Id));

        // Bakiyesi olan dövizli kasa pasife alınamaz
        await Assert.ThrowsAsync<InvalidOperationException>(() => new KasaServisi(db, oturum).KasaGuncelleAsync(new Kasa { Id = usdKasa.Id, Kod = usdKasa.Kod, Ad = usdKasa.Ad, ParaBirimi = "USD", AktifMi = false }));
        // Hareket görmüş kasanın para birimi değişmez
        await Assert.ThrowsAsync<InvalidOperationException>(() => new KasaServisi(db, oturum).KasaGuncelleAsync(new Kasa { Id = usdKasa.Id, Kod = usdKasa.Kod, Ad = usdKasa.Ad, ParaBirimi = "EUR", AktifMi = true }));
    }

    [Fact]
    public async Task Kasa_secilmeden_eklenen_hareket_varsayilan_kasaya_baglanir()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirket, _) = await SeedSirketVeCariAsync(db);
        await SeedHesaplarAsync(db, sirket.Id);
        var varsayilan = await db.Kasalar.Where(k => k.AktifMi).OrderBy(k => k.Id).FirstAsync();

        var hareket = new KasaHareketi { Tur = HareketTuru.Gelir, FisTuru = KasaFisTuru.Gelir, Tutar = 10m, Aciklama = "Kasasız kayıt" };
        db.KasaHareketleri.Add(hareket);
        await db.SaveChangesAsync();

        Assert.Equal(varsayilan.Id, hareket.KasaId);
    }

    [Fact]
    public async Task Faturaya_bagli_tahsilat_kasa_ekranindan_silinemez()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirket, cari) = await SeedSirketVeCariAsync(db);
        var (kasa, _, _) = await SeedHesaplarAsync(db, sirket.Id);
        var hareket = new KasaHareketi { KasaId = kasa.Id, Tur = HareketTuru.Gelir, FisTuru = KasaFisTuru.Tahsilat, Tutar = 50m, Aciklama = "Nakit fatura", CariKartId = cari.Id, FaturaId = 999999, IlgiliBelgeId = 999999 };
        db.KasaHareketleri.Add(hareket);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new KasaServisi(db, StabilizasyonTestOturumu.Olustur()).SilAsync(hareket.Id));
    }
}
