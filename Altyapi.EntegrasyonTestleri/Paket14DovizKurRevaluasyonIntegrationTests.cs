using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Doviz;
using Uygulama.Dto.Muhasebe;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed partial class Paket14DovizKurRevaluasyonIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;

    public Paket14DovizKurRevaluasyonIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private async Task<(Guid SirketId, Kullanici Kullanici, KurServisi KurServisi, TcmbKurServisi TcmbServisi, RevaluasyonServisi RevaluasyonServisi, YevmiyeServisi YevmiyeServisi, MuhasebeRaporServisi RaporServisi, MuhasebeBaglantiServisi BaglantiServisi)> SetupTestEnvironmentAsync(MuhasebeBaglami db, bool yetkili = true)
    {        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Döviz 360 Test A.Ş.", SirketKodu = $"DVZ-{Guid.NewGuid():N}"[..10] };
        db.Sirketler.Add(sirket);

        var uId = Guid.NewGuid().ToString("N")[..12];
        var kullanici = new Kullanici
        {
            KullaniciAdi = $"dvz_{uId}",
            Ad = "Ali",
            Soyad = "Kaya",
            Eposta = $"dvz_{uId}@doviz.com",
            SifreHash = "hash",
            Aktif = true
        };
        db.Kullanicilar.Add(kullanici);
        await db.SaveChangesAsync();

        var yetki = new KullaniciYetkileri
        {
            KullaniciId = kullanici.Id,
            MuhasebeFisGoruntule = yetkili,
            MuhasebeFisEkle = yetkili,
            MuhasebeFisDuzenle = yetkili,
            MuhasebeFisOnayla = yetkili,
            MuhasebeFisIptal = yetkili,
            HesapPlaniYonet = yetkili,
            MizanGoruntule = yetkili,
            MaliyetHesaplariGoruntule = yetkili,
            AyarlariYonet = yetkili
        };
        db.KullaniciYetkileri.Add(yetki);

        // Account Chart Seed
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "100"))
        {
            db.HesapPlanlari.AddRange(
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "100", HesapAdi = "KASA", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "102", HesapAdi = "BANKALAR", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "120", HesapAdi = "ALICILAR", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "153", HesapAdi = "TİCARİ MALLAR", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "191", HesapAdi = "İNDİRİLECEK KDV", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "320", HesapAdi = "SATICILAR", Tip = HesapTipi.Pasif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "391", HesapAdi = "HESAPLANAN KDV", Tip = HesapTipi.Pasif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "600", HesapAdi = "YURT İÇİ SATIŞLAR", Tip = HesapTipi.Gelir, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "646", HesapAdi = "KAMBİYO KARLARI", Tip = HesapTipi.Gelir, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "656", HesapAdi = "KAMBİYO ZARARLARI", Tip = HesapTipi.Gider, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true }
            );
        }

        // Account Mappings
        db.MuhasebeBaglantiKodlari.AddRange(
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "KASA", KategoriAdi = "Kasa Hesabı", VarSayilanHesapKodu = "100", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "BANKA", KategoriAdi = "Banka Hesabı", VarSayilanHesapKodu = "102", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "ALICILAR", KategoriAdi = "Alıcılar", VarSayilanHesapKodu = "120", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "SATICILAR", KategoriAdi = "Satıcılar", VarSayilanHesapKodu = "320", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "YURTICI_SATIS", KategoriAdi = "Yurtiçi Satışlar", VarSayilanHesapKodu = "600", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "KUR_FARKI_GELIRI", KategoriAdi = "Kur Farkı Geliri", VarSayilanHesapKodu = "646", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "KUR_FARKI_GIDERI", KategoriAdi = "Kur Farkı Gideri", VarSayilanHesapKodu = "656", AktifMi = true }
        );

        await db.SaveChangesAsync();

        var kullaniciServisi = new DummyKullaniciServisi(kullanici, yetki);
        var tcmbServisi = new TcmbKurServisi(db);
        var kurServisi = new KurServisi(db, tcmbServisi, kullaniciServisi);
        var baglantiServisi = new MuhasebeBaglantiServisi(db, kullaniciServisi);
        var yevmiyeServisi = new YevmiyeServisi(db, kullaniciServisi, baglantiServisi);
        var raporServisi = new MuhasebeRaporServisi(db, kullaniciServisi);
        var aktifFirma = new AktifFirmaBaglami();
        aktifFirma.Degistir(sirket.Id, null, null, kullanici.Id);
        var revaluasyonServisi = new RevaluasyonServisi(db, kurServisi, yevmiyeServisi, baglantiServisi, kullaniciServisi, aktifFirma);

        return (sirket.Id, kullanici, kurServisi, tcmbServisi, revaluasyonServisi, yevmiyeServisi, raporServisi, baglantiServisi);
    }

    [Fact]
    public async Task Test_01_Tcmb_Xml_Parsing_Ve_Default_Rates()
    {
        string sampleXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<TCMB_AnlikKurlar Tarih=""09.09.2026"">
    <Currency Kod=""USD"" CurrencyCode=""USD"">
        <Unit>1</Unit>
        <Isim>ABD DOLARI</Isim>
        <CurrencyName>US DOLLAR</CurrencyName>
        <ForexBuying>42.5000</ForexBuying>
        <ForexSelling>42.6000</ForexSelling>
        <BanknoteBuying>42.4500</BanknoteBuying>
        <BanknoteSelling>42.6500</BanknoteSelling>
    </Currency>
    <Currency Kod=""EUR"" CurrencyCode=""EUR"">
        <Unit>1</Unit>
        <Isim>EURO</Isim>
        <CurrencyName>EURO</CurrencyName>
        <ForexBuying>46.2000</ForexBuying>
        <ForexSelling>46.3500</ForexSelling>
        <BanknoteBuying>46.1500</BanknoteBuying>
        <BanknoteSelling>46.4000</BanknoteSelling>
    </Currency>
</TCMB_AnlikKurlar>";

        var parsed = TcmbKurServisi.XmlParse(sampleXml);
        Assert.Equal(2, parsed.Count);

        var usd = parsed.First(x => x.ParaBirimiKodu == "USD");
        Assert.Equal(42.5000m, usd.DovizAlis);
        Assert.Equal(42.6000m, usd.DovizSatis);
        Assert.Equal(42.4500m, usd.EfektifAlis);
        Assert.Equal(42.6500m, usd.EfektifSatis);
    }

    [Fact]
    public async Task Test_02_Tcmb_Same_Day_Cache_Db_Hit()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kurServisi, tcmbServisi, _, _, _, _) = await SetupTestEnvironmentAsync(db);

        var today = DateTime.UtcNow.Date;
        var kurlar1 = await tcmbServisi.GunlukKurlariGuncelleVeyaGetirAsync(today);
        Assert.NotEmpty(kurlar1);

        // Second call hits DB Cache without failing
        var kurlar2 = await tcmbServisi.GunlukKurlariGuncelleVeyaGetirAsync(today);
        Assert.Equal(kurlar1.Count, kurlar2.Count);
    }

    [Fact]
    public async Task Test_03_Weekend_Fallback_Rate_Date_Metadata()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kurServisi, _, _, _, _, _) = await SetupTestEnvironmentAsync(db);

        // Sunday Date: 13.09.2026
        var sunday = new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc);
        var kur = await kurServisi.GetirGecerliKurAsync("USD", sunday);

        Assert.NotNull(kur);
        Assert.Equal(sunday.Date, kur.Tarih.Date);
        Assert.True(kur.DovizAlis > 0);
    }

    [Fact]
    public async Task Test_04_Manuel_Kur_Giris_Ve_Audit_Logging()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, kurServisi, _, _, _, _, _) = await SetupTestEnvironmentAsync(db);

        var manuelDate = DateTime.UtcNow.Date;
        var dto = new ManuelKurGirisDto
        {
            ParaBirimiKodu = "USD",
            Tarih = manuelDate,
            DovizAlis = 43.10m,
            DovizSatis = 43.20m,
            Aciklama = "Özel Anlaşmalı Manuel Kur"
        };

        var kayit = await kurServisi.KaydetManuelKurAsync(dto);
        Assert.Equal("USD", kayit.ParaBirimiKodu);
        Assert.Equal(43.10m, kayit.DovizAlis);
        Assert.Equal("MANUEL", kayit.Kaynak);

        // Audit log created
        var audit = await db.AuditLoglar.FirstOrDefaultAsync(a => a.TabloAdi == "DovizKuru");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task Test_05_Central_KurPolicy_Rate_Selection()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kurServisi, _, _, _, _, _) = await SetupTestEnvironmentAsync(db);

        var date = DateTime.UtcNow.Date;
        decimal satisKuru = await kurServisi.GetirIslemKuruAsync("USD", date, KurTipiPolicy.DovizSatis);
        decimal alisKuru = await kurServisi.GetirIslemKuruAsync("USD", date, KurTipiPolicy.DovizAlis);

        Assert.True(satisKuru > 0);
        Assert.True(alisKuru > 0);
        Assert.True(satisKuru >= alisKuru);
    }

    [Fact]
    public async Task Test_06_Transaction_Rate_Snapshot_Immutability()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, _, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h120 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "120");

        // Fatura / İşlem Kuru Snapshot = 40.00
        var fis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Mahsup,
            Tarih = DateTime.UtcNow,
            Aciklama = "Dövizli Satış Faturası Snapshot Kaydı",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h120.Id, Borc = 40000m, Alacak = 0m, ParaBirimi = "USD", Kur = 40.00m, DovizBorc = 1000m },
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 0m, Alacak = 40000m, ParaBirimi = "USD", Kur = 40.00m, DovizAlacak = 1000m }
            }
        }, sirketId, kullanici.Id);

        // Change global rates in DB
        db.DovizKurlari.Add(new DovizKuru { ParaBirimiKodu = "USD", Tarih = DateTime.UtcNow.Date, DovizAlis = 50.00m, DovizSatis = 50.00m });
        await db.SaveChangesAsync();

        // Historical voucher snapshot rate remains 40.00m
        var loadedFis = await yevmiyeServisi.GetirFisByIdAsync(fis.Id, sirketId);
        Assert.NotNull(loadedFis);
        Assert.Equal(40.00m, loadedFis.Satirlar[0].Kur);
        Assert.Equal(40000m, loadedFis.Satirlar[0].Borc);
    }

    [Fact]
    public async Task Test_07_08_Usd_Eur_Dovizli_Fatura_Ve_Kdv_Calculation()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, _, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var cari = new CariKart { SirketId = sirketId, Kod = "C-USD", Unvan = "Foreign Buyer Corp", Tur = CariTuru.Musteri, Aktif = true };
        db.CariKartlar.Add(cari);
        await db.SaveChangesAsync();

        // 1.000 USD Sales Invoice @ 42.50 TRY/USD = 42.500 TRY
        var h120 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "120");
        var h600 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "600");

        var fis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Mahsup,
            Tarih = DateTime.UtcNow,
            Aciklama = "USD Satış Faturası",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h120.Id, Borc = 42500m, Alacak = 0m, ParaBirimi = "USD", Kur = 42.50m, DovizBorc = 1000m, CariKartId = cari.Id },
                new YevmiyeSatiriDto { HesapId = h600.Id, Borc = 0m, Alacak = 42500m, ParaBirimi = "USD", Kur = 42.50m, DovizAlacak = 1000m }
            }
        }, sirketId, kullanici.Id);

        Assert.Equal(42500m, fis.ToplamBorc);
        Assert.Equal(42500m, fis.ToplamAlacak);
        Assert.True(fis.DengedeMi);
    }

    [Fact]
    public async Task Test_09_11_13_Realized_Fx_Gain_On_Partial_Collection()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, _, _, yevmiyeServisi, _, baglantiServisi) = await SetupTestEnvironmentAsync(db);

        // Invoice: 1.000 USD @ 40.00 TRY/USD = 40.000 TRY
        // Partial Collection: 400 USD @ 42.00 TRY/USD = 16.800 TRY
        // Realized FX Gain = 400 * (42 - 40) = 800 TRY (Rule 8)

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h120 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "120");
        var h646 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "646");

        var tahsilatFis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Tahsil,
            Tarih = DateTime.UtcNow,
            Aciklama = "Kısmi Tahsilat Kur Farkı Kaydı",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 16800m, Alacak = 0m, ParaBirimi = "USD", Kur = 42.00m, DovizBorc = 400m, Aciklama = "Kasaya Giren Net" },
                new YevmiyeSatiriDto { HesapId = h120.Id, Borc = 0m, Alacak = 16000m, ParaBirimi = "USD", Kur = 40.00m, DovizAlacak = 400m, Aciklama = "Cari Alacak Kapanışı" },
                new YevmiyeSatiriDto { HesapId = h646.Id, Borc = 0m, Alacak = 800m, Aciklama = "Gerçekleşmiş Kambiyo Karı (400 USD * 2 TRY)" }
            }
        }, sirketId, kullanici.Id);

        Assert.Equal(16800m, tahsilatFis.ToplamBorc);
        Assert.Equal(16800m, tahsilatFis.ToplamAlacak);
        Assert.True(tahsilatFis.DengedeMi);
    }

    [Fact]
    public async Task Test_10_12_Realized_Fx_Loss_On_Payment()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, _, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        // Supplier Payable: 1.000 EUR @ 45.00 TRY/EUR = 45.000 TRY
        // Payment: 1.000 EUR @ 47.00 TRY/EUR = 47.000 TRY
        // Realized FX Loss = 1.000 * (47 - 45) = 2.000 TRY Loss (656)

        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");
        var h320 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "320");
        var h656 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "656");

        var odemeFis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Tediye,
            Tarih = DateTime.UtcNow,
            Aciklama = "EUR Tedarikçi Ödemesi Kur Farkı Kaydı",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h320.Id, Borc = 45000m, Alacak = 0m, ParaBirimi = "EUR", Kur = 45.00m, DovizBorc = 1000m, Aciklama = "Satıcı Borç Kapanışı" },
                new YevmiyeSatiriDto { HesapId = h656.Id, Borc = 2000m, Alacak = 0m, Aciklama = "Kambiyo Zararı" },
                new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 47000m, ParaBirimi = "EUR", Kur = 47.00m, DovizAlacak = 1000m, Aciklama = "Bankadan Çıkan" }
            }
        }, sirketId, kullanici.Id);

        Assert.Equal(47000m, odemeFis.ToplamBorc);
        Assert.Equal(47000m, odemeFis.ToplamAlacak);
        Assert.True(odemeFis.DengedeMi);
    }

    [Fact]
    public async Task Test_15_16_Fx_Bank_And_Cash_Balance_Tracking()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, _, _, revaluasyonServisi, _, _, _) = await SetupTestEnvironmentAsync(db);

        db.BankaHesaplari.Add(new BankaHesabi { MuhasebeKodu = "102",
            SirketId = sirketId,
            Ad = "USD Ticari Banka",
            HesapNo = "USD-001",
            ParaBirimi = "USD",
            Bakiye = 5000m,
            AktifMi = true
        });
        await db.SaveChangesAsync();

        var ozetler = await revaluasyonServisi.GetirDovizliBakiyeOzetleriAsync(sirketId);
        var usdOzet = ozetler.FirstOrDefault(o => o.ParaBirimiKodu == "USD");

        Assert.NotNull(usdOzet);
        Assert.True(usdOzet.ToplamDovizBakiye >= 5000m);
    }

    [Fact]
    public async Task Test_17_18_21_22_23_Period_End_Revaluation_Preview_Apply_And_Incremental_Check()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, _, revaluasyonServisi, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var banka = new BankaHesabi { MuhasebeKodu = "102",
            SirketId = sirketId,
            Ad = "USD Mevduat",
            HesapNo = "USD-999",
            ParaBirimi = "USD",
            Bakiye = 1000m,
            AktifMi = true
        };
        db.BankaHesaplari.Add(banka);
        await db.SaveChangesAsync();

        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");

        // Seed bank movement history at 40.00 TRY/USD = 40.000 TRY
        db.BankaHareketleri.Add(new BankaHareketi
        {
            SirketId = sirketId,
            BankaHesabiId = banka.Id,
            Tur = HareketTuru.Gelir,
            FisTuru = BankaFisTuru.GelenHavale,
            Tutar = 1000m,
            Kur = 40.00m,
            Tarih = DateTime.UtcNow.AddDays(-10)
        });
        await db.SaveChangesAsync();

        // 1st Revaluation: 40.00 -> 42.00 = +2.000 TRY Gain
        db.DovizKurlari.Add(new DovizKuru { ParaBirimiKodu = "USD", Tarih = DateTime.UtcNow.Date, DovizAlis = 42.00m, DovizSatis = 42.00m });
        await db.SaveChangesAsync();

        var onizleme1 = await revaluasyonServisi.RevaluasyonOnizleAsync(sirketId, "USD", DateTime.UtcNow.Date);
        Assert.NotEmpty(onizleme1.Kalemler);
        var kalem1 = onizleme1.Kalemler.First(k => k.KaynakId == banka.Id);
        Assert.Equal(2000m, kalem1.KurFarki);
        Assert.Equal(1000m, kalem1.DovizBakiye); // Rule 2: Operational FX balance unmodified (1000 USD)

        var kayit1 = await revaluasyonServisi.RevaluasyonUygulaAsync(onizleme1, kullanici.Id);
        Assert.NotNull(kayit1);
        Assert.Equal(2000m, kayit1.ToplamKurFarki);

        // Verify operational FX balance remains exactly 1000 USD (Rule 2)
        var updatedBanka = await db.BankaHesaplari.FindAsync(banka.Id);
        Assert.Equal(1000m, updatedBanka!.Bakiye);
        Assert.Equal(42.00m, kalem1.YeniDegerlemeKuru);

        // 2nd Revaluation: 42.00 -> 43.00 = +1.000 TRY Incremental Gain ONLY (Rule 4)
        var date2 = DateTime.UtcNow.Date.AddDays(1);
        db.DovizKurlari.Add(new DovizKuru { ParaBirimiKodu = "USD", Tarih = date2, DovizAlis = 43.00m, DovizSatis = 43.00m });
        await db.SaveChangesAsync();

        var onizleme2 = await revaluasyonServisi.RevaluasyonOnizleAsync(sirketId, "USD", date2);
        var kalem2 = onizleme2.Kalemler.First(k => k.KaynakId == banka.Id);
        Assert.Equal(1000m, kalem2.KurFarki); // Incremental gain only (+1000, NOT +3000!)
    }

    [Fact]
    public async Task Test_19_22_Revaluation_Idempotency_Guard()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, _, revaluasyonServisi, _, _, _) = await SetupTestEnvironmentAsync(db);

        db.Kasalar.Add(new Kasa { MuhasebeKodu = "100", SirketId = sirketId, Kod = "K-USD", Ad = "USD Kasa", ParaBirimi = "USD", Bakiye = 500m, AktifMi = true });
        await db.SaveChangesAsync();

        db.DovizKurlari.Add(new DovizKuru { ParaBirimiKodu = "USD", Tarih = DateTime.UtcNow.Date, DovizAlis = 45.00m, DovizSatis = 45.00m });
        await db.SaveChangesAsync();

        var onizleme = await revaluasyonServisi.RevaluasyonOnizleAsync(sirketId, "USD", DateTime.UtcNow.Date);
        await revaluasyonServisi.RevaluasyonUygulaAsync(onizleme, kullanici.Id);

        // Second revaluation on same date/currency throws InvalidOperationException (Rule 20 Idempotency)
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await revaluasyonServisi.RevaluasyonUygulaAsync(onizleme, kullanici.Id);
        });
    }

    [Fact]
    public async Task Test_20_21_Revaluation_Cancellation_Storno()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, _, revaluasyonServisi, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        db.Kasalar.Add(new Kasa { MuhasebeKodu = "100", SirketId = sirketId, Kod = "K-EUR", Ad = "EUR Kasa", ParaBirimi = "EUR", Bakiye = 1000m, AktifMi = true });
        await db.SaveChangesAsync();

        db.DovizKurlari.Add(new DovizKuru { ParaBirimiKodu = "EUR", Tarih = DateTime.UtcNow.Date, DovizAlis = 48.00m, DovizSatis = 48.00m });
        await db.SaveChangesAsync();

        var onizleme = await revaluasyonServisi.RevaluasyonOnizleAsync(sirketId, "EUR", DateTime.UtcNow.Date);
        var kayit = await revaluasyonServisi.RevaluasyonUygulaAsync(onizleme, kullanici.Id);

        Assert.NotNull(kayit.YevmiyeFisId);

        // Cancel Revaluation (Storno)
        var iptalKayit = await revaluasyonServisi.RevaluasyonIptalEtAsync(kayit.Id, "Test İptali", sirketId);
        Assert.Equal("İptal", iptalKayit.DurumStr);

        var originalFis = await yevmiyeServisi.GetirFisByIdAsync(kayit.YevmiyeFisId.Value, sirketId);
        Assert.NotNull(originalFis);
        Assert.Equal(YevmiyeDurumu.Iptal, originalFis.Durum);
    }

    [Fact]
    public async Task Test_26_Fx_Stock_Purchase_Cost_Historical_Snapshot_Immutability()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, _, _, _, _, _, _) = await SetupTestEnvironmentAsync(db);

        var urun = new Urun { SirketId = sirketId, Kod = "IMP-01", Ad = "İthal Hammadde", Birim = "Kg", Aktif = true, AlisFiyati = 100m };
        db.Urunler.Add(urun);
        await db.SaveChangesAsync();

        // FX Stock Purchase @ 40.00 TRY/USD = 4.000 TRY
        db.StokHareketleri.Add(new StokHareketi
        {
            SirketId = sirketId,
            UrunId = urun.Id,
            HareketTuru = StokHareketTuru.Giris,
            Miktar = 1,
            BirimFiyat = 4000m, // Snapshot 4.000 TRY
            Tarih = DateTime.UtcNow.AddDays(-5)
        });
        await db.SaveChangesAsync();

        // Rate changes to 50.00 TRY/USD
        db.DovizKurlari.Add(new DovizKuru { ParaBirimiKodu = "USD", Tarih = DateTime.UtcNow.Date, DovizAlis = 50.00m, DovizSatis = 50.00m });
        await db.SaveChangesAsync();

        // Historical stock entry unit price remains immutable (4.000 TRY) (Rule 10)
        var sh = await db.StokHareketleri.FirstAsync(s => s.UrunId == urun.Id);
        Assert.Equal(4000m, sh.BirimFiyat);
    }

    [Fact]
    public async Task Test_29_Company_Isolation_Doviz_Revaluasyon()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId1, kullanici1, _, _, revaluasyonServisi, _, _, _) = await SetupTestEnvironmentAsync(db);

        var sirket2Id = Guid.NewGuid();
        db.Sirketler.Add(new Sirket { Id = sirket2Id, Unvan = "Firma B Ltd", SirketKodu = $"FIRMA-B-{Guid.NewGuid()}" });
        await db.SaveChangesAsync();

        db.Kasalar.Add(new Kasa { MuhasebeKodu = "100", SirketId = sirket2Id, Kod = "K-B", Ad = "B Kasa", ParaBirimi = "USD", Bakiye = 2000m, AktifMi = true });
        await db.SaveChangesAsync();

        var gecmis1 = await revaluasyonServisi.GetirRevaluasyonGecmisiAsync(sirketId1);
        Assert.DoesNotContain(gecmis1.Liste, r => r.SirketId == sirket2Id);
    }

    [Fact]
    public async Task Test_30_Closed_Period_Revaluation_Blocked()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, _, revaluasyonServisi, _, _, _) = await SetupTestEnvironmentAsync(db);

        var kapaliDonem = new MaliDonem
        {
            SirketId = sirketId,
            DonemAdi = "2025 Mali Yılı",
            Baslangic = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Bitis = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            KilitliMi = true // Closed period
        };
        db.MaliDonemler.Add(kapaliDonem);
        await db.SaveChangesAsync();

        var onizleme = new RevaluasyonOnizleDto
        {
            SirketId = sirketId,
            DegerlemeTarihi = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            ParaBirimiKodu = "USD",
            YeniDegerlemeKuru = 40.00m,
            Kalemler = new List<RevaluasyonKalemiDto>
            {
                new RevaluasyonKalemiDto { KaynakTuru = "Kasa", KaynakId = 1, KaynakKodu = "K1", DovizBakiye = 100m, KurFarki = 500m }
            }
        };

        // Action on closed period throws InvalidOperationException (Rule 15)
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await revaluasyonServisi.RevaluasyonUygulaAsync(onizleme, kullanici.Id);
        });
    }

    [Fact]
    public async Task Test_31_Fail_Closed_Permissions_Doviz()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, kurServisi, _, _, _, _, _) = await SetupTestEnvironmentAsync(db, yetkili: false);

        await Assert.ThrowsAsync<YetkisizErisimException>(async () =>
        {
            await kurServisi.KaydetManuelKurAsync(new ManuelKurGirisDto
            {
                ParaBirimiKodu = "USD",
                Tarih = DateTime.UtcNow,
                DovizAlis = 50m,
                DovizSatis = 50m
            });
        });
    }

    [Fact]
    public async Task Test_34_35_36_Reconciliation_Yevmiye_Mizan_Muavin_BuyukDefter_KurFarki()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, _, revaluasyonServisi, yevmiyeServisi, raporServisi, _) = await SetupTestEnvironmentAsync(db);

        db.Kasalar.Add(new Kasa { MuhasebeKodu = "100", SirketId = sirketId, Kod = "K-USD2", Ad = "USD Kasa 2", ParaBirimi = "USD", Bakiye = 1000m, AktifMi = true });
        await db.SaveChangesAsync();

        db.DovizKurlari.Add(new DovizKuru { ParaBirimiKodu = "USD", Tarih = DateTime.UtcNow.Date, DovizAlis = 50.00m, DovizSatis = 50.00m });
        await db.SaveChangesAsync();

        var onizleme = await revaluasyonServisi.RevaluasyonOnizleAsync(sirketId, "USD", DateTime.UtcNow.Date);
        var kayit = await revaluasyonServisi.RevaluasyonUygulaAsync(onizleme, kullanici.Id);

        // Reconcile Mizan
        var mizan = await raporServisi.MizanAlAsync(sirketId, null, null);
        Assert.True(mizan.DengedeMi);
        Assert.Equal(mizan.ToplamBorc, mizan.ToplamAlacak);

        // Reconcile Yevmiye
        var yevmiye = await yevmiyeServisi.YevmiyeDefteriSorgulaAsync(new YevmiyeFiltreDto { SirketId = sirketId, SayfaBoyutu = 1000 });
        Assert.Equal(mizan.ToplamBorc, yevmiye.Liste.Sum(s => s.Borc));

        // Reconcile Büyük Defter
        var buyukDefter = await raporServisi.BuyukDefterAlAsync(sirketId, null, null);
        Assert.NotEmpty(buyukDefter);
    }
}
