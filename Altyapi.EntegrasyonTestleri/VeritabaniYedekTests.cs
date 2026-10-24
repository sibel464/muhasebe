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
    public async Task Ikinci_konum_tanimliysa_her_yedek_oraya_da_kopyalanir_ve_ayar_kalicidir()
    {
        fixture.RequireDatabase();
        var klasor = GeciciKlasor();
        var ikinci = GeciciKlasor();
        var yonetici = StabilizasyonTestOturumu.Olustur();
        yonetici.AktifYetkiler!.AyarlariYonet = true;
        var servis = new VeritabaniYedekServisi(BaglantiDizesi(), klasor, saklanacakYedekSayisi: 3, kullaniciServisi: yonetici);
        try
        {
            Assert.Throws<ArgumentException>(() => servis.IkinciYedekKlasoruAyarla("goreli\\klasor"));
            Assert.Throws<ArgumentException>(() => servis.IkinciYedekKlasoruAyarla(klasor));
            servis.IkinciYedekKlasoruAyarla(ikinci);

            for (var i = 0; i < 4; i++)
            {
                var yedek = await servis.ElleYedekAlAsync();
                var kopya = Path.Combine(ikinci, yedek.DosyaAdi);
                Assert.True(File.Exists(kopya));
                Assert.Equal(yedek.Boyut, new FileInfo(kopya).Length);
            }
            Assert.Equal(3, Directory.GetFiles(ikinci, "*.dump").Length);
            Assert.Empty(Directory.GetFiles(ikinci, "*.tmp"));
            Assert.StartsWith("✅", servis.IkinciKopyaDurumu);

            // Program yeniden açıldığında ayar hatırlanır.
            var yeni = new VeritabaniYedekServisi(BaglantiDizesi(), klasor, kullaniciServisi: yonetici);
            Assert.Equal(ikinci, yeni.IkinciYedekKlasoru);
            yeni.IkinciYedekKlasoruAyarla(null);
            Assert.Null(new VeritabaniYedekServisi(BaglantiDizesi(), klasor).IkinciYedekKlasoru);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
            if (Directory.Exists(ikinci)) Directory.Delete(ikinci, recursive: true);
        }
    }

    [Fact]
    public async Task Yarim_kalmis_yedek_dosyasi_dogrulamada_yakalanir()
    {
        fixture.RequireDatabase();
        var klasor = GeciciKlasor();
        var yonetici = StabilizasyonTestOturumu.Olustur();
        yonetici.AktifYetkiler!.AyarlariYonet = true;
        try
        {
            var yedek = await new VeritabaniYedekServisi(BaglantiDizesi(), klasor, kullaniciServisi: yonetici).ElleYedekAlAsync();
            await VeritabaniYedekServisi.YedekDosyasiniDogrulaAsync(yedek.TamYol);

            var yarim = Path.Combine(klasor, "yarim.dump");
            var icerik = await File.ReadAllBytesAsync(yedek.TamYol);
            await File.WriteAllBytesAsync(yarim, icerik[..(icerik.Length * 2 / 3)]);
            await Assert.ThrowsAsync<InvalidDataException>(() => VeritabaniYedekServisi.YedekDosyasiniDogrulaAsync(yarim));
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    [Fact]
    public async Task Ikinci_konuma_yazilamazsa_asil_yedek_yine_alinir()
    {
        fixture.RequireDatabase();
        var klasor = GeciciKlasor();
        var ikinci = GeciciKlasor();
        var yonetici = StabilizasyonTestOturumu.Olustur();
        yonetici.AktifYetkiler!.AyarlariYonet = true;
        var servis = new VeritabaniYedekServisi(BaglantiDizesi(), klasor, kullaniciServisi: yonetici);
        try
        {
            servis.IkinciYedekKlasoruAyarla(ikinci);
            // USB bellek çıkarıldı: klasör yerine aynı adda bir dosya var, kopya yazılamaz.
            Directory.Delete(ikinci);
            File.WriteAllText(ikinci, "usb yok");

            var yedek = await servis.ElleYedekAlAsync();
            Assert.True(File.Exists(yedek.TamYol));
            Assert.StartsWith("❌", servis.IkinciKopyaDurumu);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
            if (File.Exists(ikinci)) File.Delete(ikinci);
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
