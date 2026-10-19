using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Fatura;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Stok360;
using Uygulama.Dto.StokOperasyon;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

internal sealed class Stok360TestKullanici : IKullaniciServisi
{
    public Kullanici? AktifKullanici { get; set; } = new() { Id = 100, KullaniciAdi = "Stok360 test", Aktif = true };
    public KullaniciYetkileri? AktifYetkiler { get; set; } = new() 
    { 
        StokGoruntule = true, 
        StokEkle = true,
        StokDuzenle = true, 
        StokSil = true,
        DepoTransferYap = true, 
        AuditLogGoruntule = true 
    };

    public bool YetkiliMi(Func<KullaniciYetkileri, bool> f) => AktifKullanici != null && AktifYetkiler != null && f(AktifYetkiler);
    
    public void YetkiKontrolEt(Func<KullaniciYetkileri, bool> f, string a)
    {
        if (!YetkiliMi(f)) throw new YetkisizErisimException(a);
    }

    public Task<bool> GirisYapAsync(string a, string b, CancellationToken c = default) => Task.FromResult(false);
    public void CikisYap() => AktifKullanici = null;
    public Task<List<Kullanici>> TumunuGetirAsync() => throw new NotSupportedException();
    public Task<Kullanici?> GetirAsync(int id) => throw new NotSupportedException();
    public Task<Kullanici> EkleAsync(Kullanici k, string s) => throw new NotSupportedException();
    public Task GuncelleAsync(Kullanici k) => throw new NotSupportedException();
    public Task SifreDegistirAsync(int id, string s) => throw new NotSupportedException();
    public Task SilAsync(int id) => throw new NotSupportedException();
}

[Collection("PostgreSQL integration")]
public sealed class Stok360OperasyonPaket10IntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;

    public Stok360OperasyonPaket10IntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private async Task<(MuhasebeBaglami Db, Sirket Sirket, Depo Magaza, Depo Depo1, Depo Depo2, Urun Urun, Stok360Servisi Stok360, StokOperasyonServisi Operasyon, Stok360TestKullanici KullaniciServisi)> HazirlaAsync()
    {
        _fixture.RequireDatabase();
        var db = new MuhasebeBaglami(_fixture.Options);

        var sirket = new Sirket { Id = Guid.NewGuid(), SirketKodu = "STK-" + Guid.NewGuid().ToString("N")[..8], Unvan = "Stok Test A.Ş." };
        db.Sirketler.Add(sirket);

        var sube = new Sube { SirketId = sirket.Id, SubeKodu = "SUB-1", SubeAdi = "Merkez Şube", AktifMi = true };
        db.Subeler.Add(sube);
        await db.SaveChangesAsync();

        var magaza = new Depo { SubeId = sube.Id, DepoKodu = "MAGAZA-" + Guid.NewGuid().ToString("N")[..6], DepoAdi = "Merkez Mağaza", DepoTuru = DepoTuru.Magaza, AktifMi = true };
        var depo1 = new Depo { SubeId = sube.Id, DepoKodu = "DEP1-" + Guid.NewGuid().ToString("N")[..6], DepoAdi = "Ana Depo 1", DepoTuru = DepoTuru.Depo, AktifMi = true };
        var depo2 = new Depo { SubeId = sube.Id, DepoKodu = "DEP2-" + Guid.NewGuid().ToString("N")[..6], DepoAdi = "Yedek Depo 2", DepoTuru = DepoTuru.Depo, AktifMi = true };
        db.Depolar.AddRange(magaza, depo1, depo2);

        var urun = new Urun
        {
            SirketId = sirket.Id,
            Kod = "URUN-" + Guid.NewGuid().ToString("N")[..6],
            Barkod = "869" + Random.Shared.Next(10000000, 99999999),
            Ad = "Laptop Pro 15",
            Kategori = "Bilgisayar",
            Birim = "Adet",
            AlisFiyati = 10000,
            PerakendeSatisFiyati = 15000,
            MinimumStok = 5,
            Aktif = true
        };
        db.Urunler.Add(urun);
        await db.SaveChangesAsync();

        var kullaniciServisi = new Stok360TestKullanici();

        var stok360 = new Stok360Servisi(db, kullaniciServisi);
        var operasyon = new StokOperasyonServisi(db, kullaniciServisi);

        return (db, sirket, magaza, depo1, depo2, urun, stok360, operasyon, kullaniciServisi);
    }

    private static async Task StokVeLokasyonEkleAsync(MuhasebeBaglami db, Guid sirketId, int urunId, int depoId, decimal miktar, decimal birimFiyat = 0)
    {
        db.StokHareketleri.Add(new StokHareketi
        {
            SirketId = sirketId,
            UrunId = urunId,
            DepoId = depoId,
            Miktar = miktar,
            BirimFiyat = birimFiyat,
            HareketTuru = StokHareketTuru.Giris,
            IslemTuru = StokIslemTuru.DevirGiris,
            Tarih = DateTime.UtcNow
        });

        var lok = await db.UrunLokasyonStoklari.FirstOrDefaultAsync(l => l.UrunId == urunId && l.DepoId == depoId);
        if (lok == null)
        {
            lok = new UrunLokasyonStogu { UrunId = urunId, DepoId = depoId, MevcutMiktar = miktar };
            db.UrunLokasyonStoklari.Add(lok);
        }
        else
        {
            lok.MevcutMiktar += miktar;
        }

        var urun = await db.Urunler.FindAsync(urunId);
        if (urun != null)
        {
            urun.StokMiktari += miktar;
        }

        await db.SaveChangesAsync();
    }

    #region 1. Stok Operasyon Merkezi Matris & Filtre Testleri

    [Fact]
    public async Task Matris_tum_depolar_ve_stok_miktarlarini_dogru_hesaplar()
    {
        var (db, sirket, magaza, depo1, depo2, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, magaza.Id, 10);
        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, depo1.Id, 20);
        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, depo2.Id, 5);

        var matris = await operasyon.GetMatrisOzetAsync(sirket.Id);

        var item = Assert.Single(matris, u => u.UrunId == urun.Id);
        Assert.Equal(10, item.MagazaFiziksel);
        Assert.Equal(20, item.Depo1Fiziksel);
        Assert.Equal(5, item.Depo2Fiziksel);
        Assert.Equal(35, item.ToplamFiziksel);
        Assert.Equal(35, item.ToplamKullanilabilir);
    }

    [Theory]
    [InlineData("Laptop", true)]
    [InlineData("URUN-9999999", false)]
    [InlineData("Bilgisayar", true)]
    public async Task Matris_AramaMetni_filtresi_kod_barkod_ad_kategori_sorgular(string arama, bool bekleniyor)
    {
        var (db, sirket, _, _, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        var matris = await operasyon.GetMatrisOzetAsync(sirket.Id, arama: arama);

        if (bekleniyor)
            Assert.Contains(matris, u => u.UrunId == urun.Id);
        else
            Assert.DoesNotContain(matris, u => u.UrunId == urun.Id);
    }

    [Fact]
    public async Task Matris_KritikStok_filtresi_sadece_seviye_altindaki_urunleri_getirir()
    {
        var (db, sirket, magaza, _, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, magaza.Id, 2);

        var matris = await operasyon.GetMatrisOzetAsync(sirket.Id, sadeceKritik: true);

        Assert.Contains(matris, u => u.UrunId == urun.Id && u.StokDurumu == "Kritik Stok");
    }

    [Fact]
    public async Task Matris_StokYok_durumunu_dogru_isaretler()
    {
        var (db, sirket, _, _, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        var matris = await operasyon.GetMatrisOzetAsync(sirket.Id);

        var item = Assert.Single(matris, u => u.UrunId == urun.Id);
        Assert.Equal(0, item.ToplamFiziksel);
        Assert.Equal("Stok Yok", item.StokDurumu);
    }

    #endregion

    #region 2. Ürün 360° Sekme ve Detay Testleri

    [Fact]
    public async Task Urun360_GenelBilgi_Sekmesi_Ozet_ve_KPI_verilerini_eksiksiz_doner()
    {
        var (db, sirket, magaza, depo1, _, urun, stok360, _, _) = await HazirlaAsync();
        await using var _ = db;

        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, magaza.Id, 50);
        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, depo1.Id, 30);

        var ozet = await stok360.GetUrunOzetAsync(urun.Id, sirket.Id);
        Assert.NotNull(ozet);
        Assert.Equal(urun.Kod, ozet.Kod);
        Assert.Equal(80, ozet.ToplamFizikselStok);
        Assert.Equal(80, ozet.ToplamKullanilabilirStok);
        Assert.Equal(0, ozet.ToplamRezerveStok);
    }

    [Fact]
    public async Task Urun360_LokasyonStoklari_her_deponun_stokunu_ayri_doner()
    {
        var (db, sirket, magaza, depo1, _, urun, stok360, _, _) = await HazirlaAsync();
        await using var _ = db;

        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, magaza.Id, 12);
        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, depo1.Id, 18);

        var lokasyonlar = await stok360.GetLokasyonStoklariAsync(urun.Id, sirket.Id);
        Assert.NotEmpty(lokasyonlar);

        var magazaOzet = Assert.Single(lokasyonlar, l => l.DepoId == magaza.Id);
        Assert.Equal(12, magazaOzet.FizikselStok);

        var depo1Ozet = Assert.Single(lokasyonlar, l => l.DepoId == depo1.Id);
        Assert.Equal(18, depo1Ozet.FizikselStok);
    }

    [Fact]
    public async Task Urun360_StokHareketleri_YuruyenBakiye_kronolojik_dogru_hesaplar()
    {
        var (db, sirket, magaza, _, _, urun, stok360, _, _) = await HazirlaAsync();
        await using var _ = db;

        var t1 = DateTime.UtcNow.AddHours(-3);
        var t2 = DateTime.UtcNow.AddHours(-2);
        var t3 = DateTime.UtcNow.AddHours(-1);

        db.StokHareketleri.Add(new StokHareketi { SirketId = sirket.Id, UrunId = urun.Id, DepoId = magaza.Id, Miktar = 100, HareketTuru = StokHareketTuru.Giris, IslemTuru = StokIslemTuru.DevirGiris, Tarih = t1 });
        db.StokHareketleri.Add(new StokHareketi { SirketId = sirket.Id, UrunId = urun.Id, DepoId = magaza.Id, Miktar = 30, HareketTuru = StokHareketTuru.Cikis, IslemTuru = StokIslemTuru.SatisFaturasi, Tarih = t2 });
        db.StokHareketleri.Add(new StokHareketi { SirketId = sirket.Id, UrunId = urun.Id, DepoId = magaza.Id, Miktar = 15, HareketTuru = StokHareketTuru.Giris, IslemTuru = StokIslemTuru.Diger, Tarih = t3 });
        await db.SaveChangesAsync();

        var sorgu = new UrunStok360Sorgu();
        var liste = await stok360.GetYuruyenStokHareketleriAsync(urun.Id, sirket.Id, sorgu);

        Assert.Equal(3, liste.Count);
        Assert.Equal(85, liste[0].YuruyenBakiye);
        Assert.Equal(70, liste[1].YuruyenBakiye);
        Assert.Equal(100, liste[2].YuruyenBakiye);
    }

    [Fact]
    public async Task Urun360_MaliyetVeKarlilik_AgirlikliOrtalama_hesaplar()
    {
        var (db, sirket, magaza, _, _, urun, stok360, _, _) = await HazirlaAsync();
        await using var _ = db;

        db.StokHareketleri.Add(new StokHareketi { SirketId = sirket.Id, UrunId = urun.Id, DepoId = magaza.Id, Miktar = 10, BirimFiyat = 1000, HareketTuru = StokHareketTuru.Giris, IslemTuru = StokIslemTuru.DevirGiris, Tarih = DateTime.UtcNow.AddDays(-5) });
        db.StokHareketleri.Add(new StokHareketi { SirketId = sirket.Id, UrunId = urun.Id, DepoId = magaza.Id, Miktar = 10, BirimFiyat = 2000, HareketTuru = StokHareketTuru.Giris, IslemTuru = StokIslemTuru.AlisFaturasi, Tarih = DateTime.UtcNow.AddDays(-2) });
        db.StokHareketleri.Add(new StokHareketi { SirketId = sirket.Id, UrunId = urun.Id, DepoId = magaza.Id, Miktar = 5, BirimFiyat = 3000, HareketTuru = StokHareketTuru.Cikis, IslemTuru = StokIslemTuru.SatisFaturasi, Tarih = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        var mk = await stok360.GetMaliyetKarlilikAsync(urun.Id, sirket.Id);
        Assert.NotNull(mk);
        Assert.Equal(1500, mk.AgirlikliOrtalamaMaliyet);
        Assert.Equal(2000, mk.SonAlisMaliyeti);
        Assert.Equal(15000, mk.ToplamSatisCirosu);
        Assert.Equal(7500, mk.ToplamSatisMaliyeti);
        Assert.Equal(7500, mk.BrutKar);
        Assert.Equal(50, mk.BrutKarYuzdesi);
    }

    #endregion

    #region 3. Depolar Arası Transfer Testleri

    [Fact]
    public async Task Transfer_baska_depoya_atomik_stok_gecisi_saglar()
    {
        var (db, sirket, magaza, depo1, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, magaza.Id, 50);

        var istek = new StokTransferIstegi(magaza.Id, depo1.Id, urun.Id, 20, 10000, DateTime.UtcNow, "TRS-1001", "Hızlı Transfer");

        var transferFis = await operasyon.TransferOlusturAsync(istek, sirket.Id, 100);
        Assert.NotNull(transferFis);

        var matris = await operasyon.GetMatrisOzetAsync(sirket.Id);
        var item = Assert.Single(matris, u => u.UrunId == urun.Id);

        Assert.Equal(30, item.MagazaFiziksel);
        Assert.Equal(20, item.Depo1Fiziksel);
    }

    [Fact]
    public async Task Transfer_yetersiz_stokta_hata_firlatir_ve_stoklari_degistirmez()
    {
        var (db, sirket, magaza, depo1, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, magaza.Id, 5);

        var istek = new StokTransferIstegi(magaza.Id, depo1.Id, urun.Id, 10, 10000, DateTime.UtcNow, null, "Aşırı Transfer");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => operasyon.TransferOlusturAsync(istek, sirket.Id, 100));
        Assert.Contains("stok", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transfer_iptali_stok_hareketlerini_tersine_cevirir()
    {
        var (db, sirket, magaza, depo1, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, magaza.Id, 100);

        var istek = new StokTransferIstegi(magaza.Id, depo1.Id, urun.Id, 40, 10000, DateTime.UtcNow, null, "Geçici Transfer");
        var transferFis = await operasyon.TransferOlusturAsync(istek, sirket.Id, 100);

        await operasyon.TransferIptalEtAsync(transferFis.Id, sirket.Id, 100, "Hatalı transfer iptali");

        var matris = await operasyon.GetMatrisOzetAsync(sirket.Id);
        var item = Assert.Single(matris, u => u.UrunId == urun.Id);

        Assert.Equal(100, item.MagazaFiziksel);
        Assert.Equal(0, item.Depo1Fiziksel);
    }

    #endregion

    #region 4. Manuel Stok Giriş / Çıkış ve Stok Sayım Testleri

    [Fact]
    public async Task Manuel_Stok_Giris_fiziksel_stoku_arttirir()
    {
        var (db, sirket, magaza, _, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        var istek = new StokGirisIstegi(urun.Id, magaza.Id, 15, 12000, DateTime.UtcNow, "Sayım Fazlası", null, "Giriş Fişi");
        await operasyon.StokGirisiYapAsync(istek, sirket.Id, 100);

        var matris = await operasyon.GetMatrisOzetAsync(sirket.Id);
        var item = Assert.Single(matris, u => u.UrunId == urun.Id);
        Assert.Equal(15, item.MagazaFiziksel);
    }

    [Fact]
    public async Task Manuel_Stok_Cikis_yetersiz_stokta_engellenir()
    {
        var (db, sirket, magaza, _, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        var istek = new StokCikisIstegi(urun.Id, magaza.Id, 5, 0, DateTime.UtcNow, "Fire", null, "Çıkış Fişi");
        await Assert.ThrowsAsync<InvalidOperationException>(() => operasyon.StokCikisiYapAsync(istek, sirket.Id, 100));
    }

    [Fact]
    public async Task StokSayimi_Taslak_olusturma_ve_Onaylama_otomatik_duzeltme_hareketi_uretir()
    {
        var (db, sirket, magaza, _, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, magaza.Id, 10);

        var sayimIstek = new StokSayimIstegi(magaza.Id, DateTime.UtcNow, "Rutin Sayım", new List<StokSayimKalemIstegi>
        {
            new(urun.Id, 14)
        });

        var sayim = await operasyon.SayimOlusturAsync(sayimIstek, sirket.Id, 100);
        Assert.Equal(StokSayimDurumu.Sayimda, sayim.Durum);

        await operasyon.SayimOnaylaAsync(sayim.Id, sirket.Id, 100);

        var matris = await operasyon.GetMatrisOzetAsync(sirket.Id);
        var item = Assert.Single(matris, u => u.UrunId == urun.Id);
        Assert.Equal(14, item.MagazaFiziksel);
    }

    [Fact]
    public async Task Manuel_giris_cikis_ve_sayim_urun_kartindaki_toplam_stogu_bir_kez_degistirir()
    {
        var (db, sirket, magaza, _, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        await operasyon.StokGirisiYapAsync(new StokGirisIstegi(urun.Id, magaza.Id, 15, 100, DateTime.UtcNow, "Alım", null, "Giriş"), sirket.Id, 100);
        await operasyon.StokCikisiYapAsync(new StokCikisIstegi(urun.Id, magaza.Id, 5, 0, DateTime.UtcNow, "Fire", null, "Çıkış"), sirket.Id, 100);
        var sayim = await operasyon.SayimOlusturAsync(new StokSayimIstegi(magaza.Id, DateTime.UtcNow, "Sayım", new List<StokSayimKalemIstegi> { new(urun.Id, 12) }), sirket.Id, 100);
        await operasyon.SayimOnaylaAsync(sayim.Id, sirket.Id, 100);

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        Assert.Equal(12, (await kontrol.Urunler.SingleAsync(u => u.Id == urun.Id)).StokMiktari);
        Assert.Equal(12, await kontrol.UrunLokasyonStoklari.Where(l => l.UrunId == urun.Id).SumAsync(l => l.MevcutMiktar));
    }

    [Fact]
    public async Task Sayim_acildiktan_sonra_stok_degisirse_onay_farki_guncel_stoktan_alinir()
    {
        var (db, sirket, magaza, _, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        await operasyon.StokGirisiYapAsync(new StokGirisIstegi(urun.Id, magaza.Id, 10, 100, DateTime.UtcNow, "Alım", null, "Giriş"), sirket.Id, 100);
        var sayim = await operasyon.SayimOlusturAsync(new StokSayimIstegi(magaza.Id, DateTime.UtcNow, "Sayım", new List<StokSayimKalemIstegi> { new(urun.Id, 8) }), sirket.Id, 100);
        // Sayım açıkken 3 adet daha çıkış: depoda 7 kaldı, sayılan 8 → fark +1 olmalı (açılıştaki 10'a göre -2 değil).
        await operasyon.StokCikisiYapAsync(new StokCikisIstegi(urun.Id, magaza.Id, 3, 0, DateTime.UtcNow, "Fire", null, "Çıkış"), sirket.Id, 100);
        await operasyon.SayimOnaylaAsync(sayim.Id, sirket.Id, 100);

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        Assert.Equal(8, (await kontrol.Urunler.SingleAsync(u => u.Id == urun.Id)).StokMiktari);
        Assert.Equal(8, await kontrol.UrunLokasyonStoklari.Where(l => l.UrunId == urun.Id).SumAsync(l => l.MevcutMiktar));
        var duzeltme = await kontrol.StokHareketleri.SingleAsync(h => h.IlgiliBelgeId == sayim.Id && h.IslemTuru == StokIslemTuru.SayimFarki);
        Assert.Equal(StokHareketTuru.Giris, duzeltme.HareketTuru);
        Assert.Equal(1, duzeltme.Miktar);
    }

    [Fact]
    public async Task Sayimda_ayni_urun_iki_satirda_veya_eksi_miktarla_olamaz()
    {
        var (db, sirket, magaza, _, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        await Assert.ThrowsAsync<ArgumentException>(() => operasyon.SayimOlusturAsync(
            new StokSayimIstegi(magaza.Id, DateTime.UtcNow, "Sayım", new List<StokSayimKalemIstegi> { new(urun.Id, 5), new(urun.Id, 6) }), sirket.Id, 100));
        await Assert.ThrowsAsync<ArgumentException>(() => operasyon.SayimOlusturAsync(
            new StokSayimIstegi(magaza.Id, DateTime.UtcNow, "Sayım", new List<StokSayimKalemIstegi> { new(urun.Id, -1) }), sirket.Id, 100));
    }

    [Fact]
    public async Task Baska_firmanin_deposuna_manuel_giris_yapilamaz()
    {
        var (db, sirketA, _, _, _, urunA, _, operasyonA, _) = await HazirlaAsync();
        await using var _ = db;

        var sirketB = new Sirket { Id = Guid.NewGuid(), SirketKodu = "STK-B3-" + Guid.NewGuid().ToString("N")[..6], Unvan = "B3" };
        var subeB = new Sube { SirketId = sirketB.Id, SubeKodu = "SUB-B3", SubeAdi = "B3 Şube", AktifMi = true };
        db.AddRange(sirketB, subeB);
        await db.SaveChangesAsync();
        var magazaB = new Depo { SubeId = subeB.Id, DepoKodu = "MAG-B3-" + Guid.NewGuid().ToString("N")[..6], DepoAdi = "B3", DepoTuru = DepoTuru.Magaza, AktifMi = true };
        db.Depolar.Add(magazaB);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => operasyonA.StokGirisiYapAsync(
            new StokGirisIstegi(urunA.Id, magazaB.Id, 5, 100, DateTime.UtcNow, "Alım", null, "Giriş"), sirketA.Id, 100));

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        Assert.False(await kontrol.UrunLokasyonStoklari.AnyAsync(l => l.DepoId == magazaB.Id));
    }

    #endregion

    #region 5. Multi-Tenant İzolasyon & Güvenlik Testleri

    [Fact]
    public async Task Baska_sirketin_stok_hareketleri_izole_edilir()
    {
        var (db, sirketA, magazaA, _, _, urunA, stok360A, _, _) = await HazirlaAsync();
        await using var _ = db;

        var sirketB = new Sirket { Id = Guid.NewGuid(), SirketKodu = "STK-B", Unvan = "Rakip Şirket B" };
        db.Sirketler.Add(sirketB);
        var subeB = new Sube { SirketId = sirketB.Id, SubeKodu = "SUB-B", SubeAdi = "B Şube", AktifMi = true };
        db.Subeler.Add(subeB);
        await db.SaveChangesAsync();

        var magazaB = new Depo { SubeId = subeB.Id, DepoKodu = "MAG-B", DepoAdi = "B Mağaza", DepoTuru = DepoTuru.Magaza, AktifMi = true };
        var urunB = new Urun { SirketId = sirketB.Id, Kod = "URUN-B-" + Guid.NewGuid().ToString("N")[..6], Ad = "Aynı Kodlu Ürün B", Birim = "Adet", Aktif = true };
        db.AddRange(magazaB, urunB);
        await db.SaveChangesAsync();

        await StokVeLokasyonEkleAsync(db, sirketA.Id, urunA.Id, magazaA.Id, 100);
        await StokVeLokasyonEkleAsync(db, sirketB.Id, urunB.Id, magazaB.Id, 500);

        var ozetA = await stok360A.GetUrunOzetAsync(urunA.Id, sirketA.Id);
        Assert.Equal(100, ozetA!.ToplamFizikselStok);

        var ozetB_from_A = await stok360A.GetUrunOzetAsync(urunB.Id, sirketA.Id);
        Assert.Null(ozetB_from_A);
    }

    [Fact]
    public async Task Capraz_sirketler_arasi_transfer_yapilamaz()
    {
        var (db, sirketA, magazaA, _, _, urunA, _, operasyonA, _) = await HazirlaAsync();
        await using var _ = db;

        var sirketB = new Sirket { Id = Guid.NewGuid(), SirketKodu = "STK-B2", Unvan = "B" };
        var subeB = new Sube { SirketId = sirketB.Id, SubeKodu = "SUB-B2", SubeAdi = "B2 Şube", AktifMi = true };
        db.AddRange(sirketB, subeB);
        await db.SaveChangesAsync();

        var magazaB = new Depo { SubeId = subeB.Id, DepoKodu = "MAG-B2", DepoAdi = "B", DepoTuru = DepoTuru.Magaza, AktifMi = true };
        db.Depolar.Add(magazaB);
        await db.SaveChangesAsync();

        var istek = new StokTransferIstegi(magazaA.Id, magazaB.Id, urunA.Id, 1, 1000, DateTime.UtcNow, null, "Çapraz Transfer");

        await Assert.ThrowsAsync<InvalidOperationException>(() => operasyonA.TransferOlusturAsync(istek, sirketA.Id, 100));
    }

    [Fact]
    public async Task Yetkisiz_kullanici_stok_operasyonu_yapamaz()
    {
        var (db, sirket, magaza, depo1, _, urun, _, operasyon, kullaniciServisi) = await HazirlaAsync();
        await using var _ = db;

        kullaniciServisi.AktifYetkiler!.StokDuzenle = false;

        var istek = new StokTransferIstegi(magaza.Id, depo1.Id, urun.Id, 1, 1000, DateTime.UtcNow, null, "");

        await Assert.ThrowsAsync<YetkisizErisimException>(() => operasyon.TransferOlusturAsync(istek, sirket.Id, 100));
    }

    [Fact]
    public async Task Transfer_iptalinde_hedef_depoda_stok_yetersizse_iptal_engellenir()
    {
        var (db, sirket, magaza, depo1, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        // Step 1: Initialize Depo 1 with 10 units
        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, depo1.Id, 10);

        // Step 2: Transfer 10 units from Depo 1 to Mağaza
        var istek = new StokTransferIstegi(depo1.Id, magaza.Id, urun.Id, 10, 10000, DateTime.UtcNow, "TRS-RISK", "Risk testi");
        var transferFis = await operasyon.TransferOlusturAsync(istek, sirket.Id, 100);

        // Step 3: Mağaza now sells 8 units (leaving only 2 units in Mağaza)
        var cikisIstek = new StokCikisIstegi(urun.Id, magaza.Id, 8, 15000, DateTime.UtcNow, "Satış", null, "Mağaza Satışı");
        await operasyon.StokCikisiYapAsync(cikisIstek, sirket.Id, 100);

        // Step 4: Attempt to cancel the 10-unit transfer (which requires 10 units from Mağaza, but only 2 exist)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => operasyon.TransferIptalEtAsync(transferFis.Id, sirket.Id, 100, "İptal denemesi"));
        Assert.Contains("Hedef depodaki stok miktarı transfer edilen miktardan daha az", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Verify stock numbers remain intact (Mağaza = 2, Depo 1 = 0)
        var matris = await operasyon.GetMatrisOzetAsync(sirket.Id);
        var item = Assert.Single(matris, u => u.UrunId == urun.Id);
        Assert.Equal(2, item.MagazaFiziksel);
        Assert.Equal(0, item.Depo1Fiziksel);
    }

    [Fact]
    public async Task Onaylanmis_stok_sayimi_tekrar_onaylanamaz_veya_iptal_edilemez()
    {
        var (db, sirket, magaza, _, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, magaza.Id, 10);

        var sayim = await operasyon.SayimOlusturAsync(new StokSayimIstegi(magaza.Id, DateTime.UtcNow, "Sayım", new List<StokSayimKalemIstegi> { new(urun.Id, 12) }), sirket.Id, 100);
        await operasyon.SayimOnaylaAsync(sayim.Id, sirket.Id, 100);

        // Attempt second approval -> blocked
        await Assert.ThrowsAsync<InvalidOperationException>(() => operasyon.SayimOnaylaAsync(sayim.Id, sirket.Id, 100));

        // Attempt cancel -> blocked
        await Assert.ThrowsAsync<InvalidOperationException>(() => operasyon.SayimIptalEtAsync(sayim.Id, sirket.Id, 100));
    }

    [Fact]
    public async Task Transfer_eszamanli_isteklerde_concurrency_ve_negatif_stok_engeli()
    {
        var (db, sirket, magaza, depo1, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, magaza.Id, 5);

        // Concurrency test: 2 parallel transfers of 5 units each from Mağaza to Depo 1
        var istek1 = new StokTransferIstegi(magaza.Id, depo1.Id, urun.Id, 5, 10000, DateTime.UtcNow, "CONC-1", "T1");
        var istek2 = new StokTransferIstegi(magaza.Id, depo1.Id, urun.Id, 5, 10000, DateTime.UtcNow, "CONC-2", "T2");

        var task1 = operasyon.TransferOlusturAsync(istek1, sirket.Id, 100);
        var task2 = operasyon.TransferOlusturAsync(istek2, sirket.Id, 100);

        var aggregateEx = await Record.ExceptionAsync(async () => await Task.WhenAll(task1, task2));
        Assert.NotNull(aggregateEx);

        var matris = await operasyon.GetMatrisOzetAsync(sirket.Id);
        var item = Assert.Single(matris, u => u.UrunId == urun.Id);
        Assert.True(item.MagazaFiziksel >= 0, "Stock must never become negative!");
        Assert.Equal(5, item.ToplamFiziksel);
    }

    [Fact]
    public async Task Hareketli_agirlikli_ortalama_maliyet_dogru_hesaplanir()
    {
        var (db, sirket, magaza, _, _, urun, stok360, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        // Step 1: Initial Purchase: 10 units @ 100 TL (Value = 1000 TL, Avg = 100 TL)
        await operasyon.StokGirisiYapAsync(new StokGirisIstegi(urun.Id, magaza.Id, 10, 100, DateTime.UtcNow.AddDays(-10), "Alış 1", null, "Alış 1"), sirket.Id, 100);

        // Step 2: Second Purchase: 10 units @ 200 TL (Total 20 units, Total Value = 3000 TL, Avg = 150 TL)
        await operasyon.StokGirisiYapAsync(new StokGirisIstegi(urun.Id, magaza.Id, 10, 200, DateTime.UtcNow.AddDays(-5), "Alış 2", null, "Alış 2"), sirket.Id, 100);

        // Step 3: Sale of 10 units (COGS = 10 * 150 = 1500 TL. Remaining 10 units, Value = 1500 TL)
        await operasyon.StokCikisiYapAsync(new StokCikisIstegi(urun.Id, magaza.Id, 10, 300, DateTime.UtcNow.AddDays(-2), "Satış 1", null, "Satış 1"), sirket.Id, 100);

        // Step 4: Third Purchase: 10 units @ 300 TL (Total 20 units, Total Value = 1500 + 3000 = 4500 TL, New Avg = 225 TL)
        await operasyon.StokGirisiYapAsync(new StokGirisIstegi(urun.Id, magaza.Id, 10, 300, DateTime.UtcNow.AddDays(-1), "Alış 3", null, "Alış 3"), sirket.Id, 100);

        var mk = await stok360.GetMaliyetKarlilikAsync(urun.Id, sirket.Id);
        Assert.NotNull(mk);
        Assert.Equal(225, mk.AgirlikliOrtalamaMaliyet);
        Assert.Equal(1500, mk.ToplamSatisMaliyeti); // 10 units sold * 150 TL at sale time = 1500 TL
    }

    [Fact]
    public async Task Transfer_sirket_toplam_stok_degerini_korur()
    {
        var (db, sirket, magaza, depo1, _, urun, stok360, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        await operasyon.StokGirisiYapAsync(new StokGirisIstegi(urun.Id, depo1.Id, 20, 100, DateTime.UtcNow.AddDays(-2), "Giriş", null, ""), sirket.Id, 100);

        var mkBefore = await stok360.GetMaliyetKarlilikAsync(urun.Id, sirket.Id);
        Assert.Equal(100, mkBefore.AgirlikliOrtalamaMaliyet);

        // Transfer 10 units from Depo1 to Mağaza
        var istek = new StokTransferIstegi(depo1.Id, magaza.Id, urun.Id, 10, 100, DateTime.UtcNow, "TRS-VAL", "Transfer");
        await operasyon.TransferOlusturAsync(istek, sirket.Id, 100);

        var mkAfter = await stok360.GetMaliyetKarlilikAsync(urun.Id, sirket.Id);
        Assert.Equal(100, mkAfter.AgirlikliOrtalamaMaliyet); // Unit cost stays 100 TL
    }

    [Fact]
    public async Task Rezerve_stok_bagimsiz_satislar_tarafindan_tuketilemez()
    {
        var (db, sirket, magaza, _, _, urun, _, operasyon, _) = await HazirlaAsync();
        await using var _ = db;

        // Physical = 10
        await StokVeLokasyonEkleAsync(db, sirket.Id, urun.Id, magaza.Id, 10);

        // Add 6 reserved units to Mağaza
        var lok = await db.UrunLokasyonStoklari.FirstAsync(l => l.UrunId == urun.Id && l.DepoId == magaza.Id);
        lok.RezerveMiktar = 6;
        await db.SaveChangesAsync();

        // Available = 10 - 6 = 4. Independent sale of 5 items must be BLOCKED!
        var cikisIstek = new StokCikisIstegi(urun.Id, magaza.Id, 5, 15000, DateTime.UtcNow, "Satış", null, "Bağımsız Satış");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => operasyon.StokCikisiYapAsync(cikisIstek, sirket.Id, 100));
        Assert.Contains("yeterli stok yok", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
