using Altyapi.Servisler;
using Altyapi.Veri;
using Altyapi.Yardimcilar;
using EtkiAlani.Modeller.Stok;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class UrunKodBarkodIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public UrunKodBarkodIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Eszamanli_otomatik_kayitlar_benzersiz_kod_ve_gecerli_EAN13_alir()
    {
        await using var db1 = new MuhasebeBaglami(_fixture.Options);
        await using var db2 = new MuhasebeBaglami(_fixture.Options);
        var sonuclar = await Task.WhenAll(
            new UrunServisi(db1, StabilizasyonTestOturumu.Olustur()).GuvenliEkleAsync(YeniUrun(), true, true),
            new UrunServisi(db2, StabilizasyonTestOturumu.Olustur()).GuvenliEkleAsync(YeniUrun(), true, true));

        Assert.NotEqual(sonuclar[0].Kod, sonuclar[1].Kod);
        Assert.NotEqual(sonuclar[0].Barkod, sonuclar[1].Barkod);
        Assert.All(sonuclar, x => Assert.Matches("^URN-[0-9]{6}$", x.Kod));
        await using var kontrolDb = new MuhasebeBaglami(_fixture.Options);
        var uretici = new UrunKodBarkodUreteci(kontrolDb);
        Assert.All(sonuclar, x => Assert.True(uretici.Ean13BarkodGecerliMi(x.Barkod!)));
    }

    [Fact]
    public async Task Ayni_manuel_barkod_ikinci_kez_kaydedilemez_ve_barkodla_bulunur()
    {
        var barkod = "869" + Random.Shared.NextInt64(1000000000, 9999999999);
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new UrunServisi(db, StabilizasyonTestOturumu.Olustur());
        var ilk = YeniUrun(); ilk.Kod = "UT-" + Guid.NewGuid().ToString("N")[..10]; ilk.Barkod = barkod;
        await servis.EkleAsync(ilk);

        var bulunan = await servis.BarkodIleGetirAsync(barkod);
        Assert.Equal(ilk.Id, bulunan?.Id);

        var ikinci = YeniUrun(); ikinci.Kod = "UT-" + Guid.NewGuid().ToString("N")[..10]; ikinci.Barkod = barkod;
        var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => servis.EkleAsync(ikinci));
        Assert.Contains("zaten", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Urun YeniUrun() => new()
    {
        Kod = string.Empty,
        Ad = "Kod Barkod Test Ürünü",
        Birim = "Adet",
        KdvOrani = 20,
        Aktif = true
    };
}
