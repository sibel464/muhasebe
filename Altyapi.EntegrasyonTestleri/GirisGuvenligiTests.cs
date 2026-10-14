using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kullanici;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class GirisGuvenligiTests(IsolatedPostgreSqlFixture fixture)
{
    private const string Sifre = "Dogru-Sifre-2026";

    private async Task<(int Id, string Ad)> KullaniciOlusturAsync(string rol = "Kullanici", KullaniciYetkileri? yetkiler = null)
    {
        var ad = "giris_" + Guid.NewGuid().ToString("N")[..12];
        await using var baglam = new MuhasebeBaglami(fixture.Options);
        var kullanici = new Kullanici
        {
            KullaniciAdi = ad,
            SifreHash = BCrypt.Net.BCrypt.HashPassword(Sifre),
            Rol = rol,
            Ad = "Giriş",
            Soyad = "Test",
            Yetkiler = yetkiler ?? new KullaniciYetkileri()
        };
        baglam.Kullanicilar.Add(kullanici);
        await baglam.SaveChangesAsync();
        return (kullanici.Id, ad);
    }

    private KullaniciServisi Servis() => new(new BaglamFabrikasi(fixture.Options));

    [Fact]
    public async Task Bes_hatali_denemeden_sonra_hesap_kilitlenir_dogru_sifre_de_reddedilir()
    {
        fixture.RequireDatabase();
        var (id, ad) = await KullaniciOlusturAsync();
        var servis = Servis();

        for (var i = 1; i < KullaniciServisi.AzamiHataliDeneme; i++)
        {
            var sonuc = await servis.GirisDeneAsync(ad, "yanlis-sifre-1");
            Assert.Equal(GirisDurumu.HataliBilgi, sonuc.Durum);
            Assert.Equal(KullaniciServisi.AzamiHataliDeneme - i, sonuc.KalanDeneme);
        }

        var kilit = await servis.GirisDeneAsync(ad, "yanlis-sifre-1");
        Assert.Equal(GirisDurumu.HesapKilitli, kilit.Durum);
        Assert.Equal(GirisDurumu.HesapKilitli, (await servis.GirisDeneAsync(ad, Sifre)).Durum);
        Assert.Null(servis.AktifKullanici);

        await using var baglam = new MuhasebeBaglami(fixture.Options);
        Assert.True(await baglam.AuditLoglar.AnyAsync(x => x.TabloAdi == "Oturum" && x.KayitId == id.ToString() && x.IslemDetayi!.Contains("kilitlendi")));
    }

    [Fact]
    public async Task Basarili_giris_hatali_deneme_sayacini_sifirlar()
    {
        fixture.RequireDatabase();
        var (id, ad) = await KullaniciOlusturAsync();
        var servis = Servis();

        await servis.GirisDeneAsync(ad, "yanlis-sifre-1");
        await servis.GirisDeneAsync(ad, "yanlis-sifre-1");
        Assert.True((await servis.GirisDeneAsync(ad, Sifre)).Basarili);

        await using var baglam = new MuhasebeBaglami(fixture.Options);
        Assert.Equal(0, (await baglam.Kullanicilar.SingleAsync(x => x.Id == id)).HataliGirisSayisi);
    }

    [Fact]
    public async Task Varsayilan_admin_sifresi_ilk_giriste_degistirilmeli()
    {
        fixture.RequireDatabase();
        await using var baglam = new MuhasebeBaglami(fixture.Options);
        var admin = await baglam.Kullanicilar.SingleAsync(x => x.Id == 1);
        Assert.True(admin.SifreDegistirmeli);
    }

    [Fact]
    public async Task Kullanici_kendi_sifresini_kurallara_uygun_degistirir()
    {
        fixture.RequireDatabase();
        var (id, ad) = await KullaniciOlusturAsync();
        var servis = Servis();
        Assert.True(await servis.GirisYapAsync(ad, Sifre));

        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.KendiSifreniDegistirAsync("yanlis-mevcut-1", "YeniSifre2026"));
        await Assert.ThrowsAsync<ArgumentException>(() => servis.KendiSifreniDegistirAsync(Sifre, "kisa1"));
        await Assert.ThrowsAsync<ArgumentException>(() => servis.KendiSifreniDegistirAsync(Sifre, "sadeceharf"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.KendiSifreniDegistirAsync(Sifre, Sifre));

        await servis.KendiSifreniDegistirAsync(Sifre, "YeniSifre2026");

        Assert.True(await Servis().GirisYapAsync(ad, "YeniSifre2026"));
        Assert.False(await Servis().GirisYapAsync(ad, Sifre));
        await using var baglam = new MuhasebeBaglami(fixture.Options);
        Assert.False((await baglam.Kullanicilar.SingleAsync(x => x.Id == id)).SifreDegistirmeli);
    }

    [Fact]
    public async Task Yonetici_olmayan_kullanici_yoneticisi_yetki_yukseltemez()
    {
        fixture.RequireDatabase();
        var (yoneticiId, yoneticiAdi) = await KullaniciOlusturAsync(yetkiler: new KullaniciYetkileri { KullaniciYonet = true, AyarlariYonet = false });
        var (hedefId, hedefAdi) = await KullaniciOlusturAsync();
        var servis = Servis();
        Assert.True(await servis.GirisYapAsync(yoneticiAdi, Sifre));

        // Kendine yönetici rolü veremez.
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.GuncelleAsync(new Kullanici
        {
            Id = yoneticiId, KullaniciAdi = yoneticiAdi, Ad = "Giriş", Soyad = "Test", Aktif = true, Rol = "Yonetici",
            Yetkiler = new KullaniciYetkileri { KullaniciYonet = true }
        }));

        // Kendisinde olmayan yetkiyi başkasına veremez.
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.GuncelleAsync(new Kullanici
        {
            Id = hedefId, KullaniciAdi = hedefAdi, Ad = "Giriş", Soyad = "Test", Aktif = true, Rol = "Kullanici",
            Yetkiler = new KullaniciYetkileri { AyarlariYonet = true }
        }));

        // Yönetici rolünde yeni kullanıcı oluşturamaz.
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.EkleAsync(
            new Kullanici { KullaniciAdi = "yeni_" + Guid.NewGuid().ToString("N")[..8], Rol = "Yönetici" }, "GeciciSifre2026"));
    }

    [Fact]
    public async Task Yeni_kullanici_zayif_sifreyle_olusturulamaz_ve_ilk_giriste_sifre_degistirmeli()
    {
        fixture.RequireDatabase();
        var (_, yoneticiAdi) = await KullaniciOlusturAsync(rol: "Yonetici");
        var servis = Servis();
        Assert.True(await servis.GirisYapAsync(yoneticiAdi, Sifre));

        var yeniAd = "yeni_" + Guid.NewGuid().ToString("N")[..8];
        await Assert.ThrowsAsync<ArgumentException>(() => servis.EkleAsync(new Kullanici { KullaniciAdi = yeniAd }, "123456"));
        await servis.EkleAsync(new Kullanici { KullaniciAdi = yeniAd, Ad = "Yeni", Soyad = "Kullanıcı" }, "GeciciSifre2026");

        var sonuc = await Servis().GirisDeneAsync(yeniAd, "GeciciSifre2026");
        Assert.True(sonuc.Basarili);
        Assert.True(sonuc.SifreDegistirmeli);
    }

    [Fact]
    public void Yeni_kullanici_hicbir_yetkiyle_baslamaz()
    {
        var acikYetkiler = typeof(KullaniciYetkileri).GetProperties()
            .Where(p => p.PropertyType == typeof(bool) && p.DeclaringType == typeof(KullaniciYetkileri))
            .Where(p => (bool)p.GetValue(new KullaniciYetkileri())!)
            .Select(p => p.Name)
            .ToList();
        Assert.Empty(acikYetkiler);
    }

    private sealed class BaglamFabrikasi(DbContextOptions<MuhasebeBaglami> options) : IDbContextFactory<MuhasebeBaglami>
    {
        public MuhasebeBaglami CreateDbContext() => new(options);
    }
}
