using Altyapi.Veri;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class VeritabaniYedekTests(IsolatedPostgreSqlFixture fixture)
{
    private string BaglantiDizesi()
    {
        using var db = new MuhasebeBaglami(fixture.Options);
        return db.Database.GetConnectionString()!;
    }

    private static string GeciciKlasor() => Path.Combine(Path.GetTempPath(), "muhasebe-yedek-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Yedek_alinir_ve_yalnizca_son_yedekler_saklanir()
    {
        fixture.RequireDatabase();
        var klasor = GeciciKlasor();
        var yonetici = StabilizasyonTestOturumu.Olustur();
        yonetici.AktifYetkiler!.AyarlariYonet = true;
        var servis = new VeritabaniYedekServisi(BaglantiDizesi(), klasor, saklanacakYedekSayisi: 3, kullaniciServisi: yonetici);
        try
        {
            for (var i = 0; i < 4; i++)
            {
                var yedek = await servis.ElleYedekAlAsync();
                Assert.True(File.Exists(yedek.TamYol));
                Assert.True(yedek.Boyut > 0);
            }

            Assert.Equal(3, servis.YedekleriListele().Count);
            Assert.Empty(Directory.GetFiles(klasor, "*.tmp"));
            // Az önce yedek alındığı için günlük yedek tekrar alınmaz.
            Assert.Null(await servis.GerekirseGunlukYedekAlAsync());
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    [Fact]
    public async Task Yetkisiz_kullanici_elle_yedek_alamaz()
    {
        fixture.RequireDatabase();
        var servis = new VeritabaniYedekServisi(BaglantiDizesi(), GeciciKlasor(), kullaniciServisi: new Cari360TestKullanici());
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.ElleYedekAlAsync());
    }
}
