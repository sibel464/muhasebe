using Altyapi.Veri;
using EtkiAlani.Modeller.Guvenlik;
using EtkiAlani.Modeller.Kullanici;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class DenetimKaydiGizlilikTests(IsolatedPostgreSqlFixture fixture)
{
    [Fact]
    public async Task Migration_eski_denetim_kayitlarindaki_hash_alanini_temizler()
    {
        fixture.RequireDatabase();
        await using var baglam = new MuhasebeBaglami(fixture.Options);
        await baglam.Database.MigrateAsync("20260914085722_StabilizasyonEksikIliskiIndeksleri");

        var eskiKayit = new AuditLog
        {
            TabloAdi = nameof(Kullanici),
            KayitId = "eski-test-kayit",
            EskiDegerJson = "{\"SifreHash\":\"eski-hash\",\"Ad\":\"Eski\"}",
            YeniDegerJson = "{\"SifreHash\":\"yeni-hash\",\"Ad\":\"Yeni\"}"
        };
        baglam.AuditLoglar.Add(eskiKayit);
        await baglam.SaveChangesAsync();

        await baglam.Database.MigrateAsync();
        await baglam.Entry(eskiKayit).ReloadAsync();

        Assert.DoesNotContain("SifreHash", eskiKayit.EskiDegerJson);
        Assert.DoesNotContain("SifreHash", eskiKayit.YeniDegerJson);
        Assert.Contains("Eski", eskiKayit.EskiDegerJson);
        Assert.Contains("Yeni", eskiKayit.YeniDegerJson);
    }

    [Fact]
    public async Task Kullanici_ekleme_ve_sifre_degistirme_denetim_kaydi_hash_icermez()
    {
        fixture.RequireDatabase();
        await using var baglam = new MuhasebeBaglami(fixture.Options);
        var kullanici = new Kullanici
        {
            KullaniciAdi = "denetim_gizlilik_" + Guid.NewGuid().ToString("N"),
            SifreHash = "ilk-test-hash",
            Ad = "Denetim",
            Soyad = "Test"
        };

        baglam.Kullanicilar.Add(kullanici);
        await baglam.SaveChangesAsync();

        kullanici.SifreHash = "ikinci-test-hash";
        await baglam.SaveChangesAsync();

        var kayitlar = await baglam.AuditLoglar.AsNoTracking()
            .Where(x => x.TabloAdi == nameof(Kullanici) && x.KayitId == kullanici.Id.ToString())
            .ToListAsync();

        Assert.NotEmpty(kayitlar);
        Assert.Contains(kayitlar, x => x.YeniDegerJson != null && x.YeniDegerJson.Contains(nameof(Kullanici.KullaniciAdi)));
        Assert.DoesNotContain(kayitlar, x =>
            (x.EskiDegerJson?.Contains(nameof(Kullanici.SifreHash)) ?? false) ||
            (x.YeniDegerJson?.Contains(nameof(Kullanici.SifreHash)) ?? false) ||
            (x.EskiDegerJson?.Contains("ilk-test-hash") ?? false) ||
            (x.YeniDegerJson?.Contains("ikinci-test-hash") ?? false));
    }
}
