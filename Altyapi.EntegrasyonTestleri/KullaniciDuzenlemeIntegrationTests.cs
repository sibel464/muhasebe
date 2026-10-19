using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kullanici;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class KullaniciDuzenlemeIntegrationTests(IsolatedPostgreSqlFixture fixture)
{
    [Fact]
    public async Task Kendi_yonetici_rolunu_kaldiran_kullanici_yetkiyi_aninda_kaybeder()
    {
        fixture.RequireDatabase();
        var kullaniciAdi = "rol_test_" + Guid.NewGuid().ToString("N");
        int kullaniciId;
        await using (var baglam = new MuhasebeBaglami(fixture.Options))
        {
            var kullanici = new Kullanici
            {
                KullaniciAdi = kullaniciAdi,
                SifreHash = BCrypt.Net.BCrypt.HashPassword("rol-test-parolasi"),
                Rol = "Yonetici",
                Ad = "Rol",
                Soyad = "Test",
                Yetkiler = new KullaniciYetkileri { KullaniciYonet = false }
            };
            baglam.Kullanicilar.Add(kullanici);
            await baglam.SaveChangesAsync();
            kullaniciId = kullanici.Id;
        }

        var servis = new KullaniciServisi(new BaglamFabrikasi(fixture.Options));
        Assert.True(await servis.GirisYapAsync(kullaniciAdi, "rol-test-parolasi"));
        await servis.GuncelleAsync(new Kullanici
        {
            Id = kullaniciId,
            KullaniciAdi = kullaniciAdi,
            Ad = "Rol",
            Soyad = "Test",
            Rol = "Kullanici",
            Yetkiler = new KullaniciYetkileri { KullaniciYonet = false }
        });

        Assert.False(servis.YetkiliMi(y => y.KullaniciYonet));
        await Assert.ThrowsAsync<Uygulama.Arayuzler.YetkisizErisimException>(() => servis.TumunuGetirAsync());
    }

    [Fact]
    public async Task Kullanici_bilgisi_degistirilirken_sifre_korunur_ve_yetkiler_kaydedilir()
    {
        fixture.RequireDatabase();
        var yoneticiAdi = "yonetici_" + Guid.NewGuid().ToString("N");
        var hedefAdi = "hedef_" + Guid.NewGuid().ToString("N");
        var hedefHash = BCrypt.Net.BCrypt.HashPassword("hedef-parolasi");
        int hedefId;

        await using (var baglam = new MuhasebeBaglami(fixture.Options))
        {
            baglam.Kullanicilar.Add(new Kullanici
            {
                KullaniciAdi = yoneticiAdi,
                SifreHash = BCrypt.Net.BCrypt.HashPassword("yonetici-parolasi"),
                Rol = "Yonetici",
                Ad = "Yönetici",
                Soyad = "Test"
            });
            var hedef = new Kullanici
            {
                KullaniciAdi = hedefAdi,
                SifreHash = hedefHash,
                Ad = "Eski",
                Soyad = "Ad",
                Yetkiler = new KullaniciYetkileri { CariSil = false }
            };
            baglam.Kullanicilar.Add(hedef);
            await baglam.SaveChangesAsync();
            hedefId = hedef.Id;
        }

        var servis = new KullaniciServisi(new BaglamFabrikasi(fixture.Options));
        Assert.True(await servis.GirisYapAsync(yoneticiAdi, "yonetici-parolasi"));
        await servis.GuncelleAsync(new Kullanici
        {
            Id = hedefId,
            KullaniciAdi = hedefAdi,
            Ad = "Yeni",
            Soyad = "Ad",
            Rol = "Kullanici",
            Yetkiler = new KullaniciYetkileri { CariSil = true }
        });

        await using var kontrolBaglami = new MuhasebeBaglami(fixture.Options);
        var kaydedilen = await kontrolBaglami.Kullanicilar.Include(x => x.Yetkiler)
            .SingleAsync(x => x.Id == hedefId);
        Assert.Equal("Yeni", kaydedilen.Ad);
        Assert.Equal(hedefHash, kaydedilen.SifreHash);
        Assert.True(kaydedilen.Yetkiler!.CariSil);
    }

    [Fact]
    public async Task Silinmis_kullanicinin_adi_duzenlemeyle_baskasina_verilemez()
    {
        fixture.RequireDatabase();
        var yoneticiAdi = "yonetici_" + Guid.NewGuid().ToString("N");
        var silinenAdi = "silinen_" + Guid.NewGuid().ToString("N");
        int hedefId;

        await using (var baglam = new MuhasebeBaglami(fixture.Options))
        {
            baglam.Kullanicilar.Add(new Kullanici { KullaniciAdi = yoneticiAdi, SifreHash = BCrypt.Net.BCrypt.HashPassword("yonetici-parolasi"), Rol = "Yonetici", Ad = "Y", Soyad = "T" });
            baglam.Kullanicilar.Add(new Kullanici { KullaniciAdi = silinenAdi, SifreHash = "x", Ad = "S", Soyad = "T", Silindi = true, Aktif = false });
            var hedef = new Kullanici { KullaniciAdi = "hedef_" + Guid.NewGuid().ToString("N"), SifreHash = "x", Ad = "H", Soyad = "T" };
            baglam.Kullanicilar.Add(hedef);
            await baglam.SaveChangesAsync();
            hedefId = hedef.Id;
        }

        var servis = new KullaniciServisi(new BaglamFabrikasi(fixture.Options));
        Assert.True(await servis.GirisYapAsync(yoneticiAdi, "yonetici-parolasi"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.GuncelleAsync(new Kullanici { Id = hedefId, KullaniciAdi = silinenAdi, Ad = "H", Soyad = "T", Rol = "Kullanici", Aktif = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => servis.GuncelleAsync(new Kullanici { Id = hedefId, KullaniciAdi = "a b", Ad = "H", Soyad = "T", Rol = "Kullanici", Aktif = true }));
    }

    private sealed class BaglamFabrikasi(DbContextOptions<MuhasebeBaglami> options)
        : IDbContextFactory<MuhasebeBaglami>
    {
        public MuhasebeBaglami CreateDbContext() => new(options);
    }
}
