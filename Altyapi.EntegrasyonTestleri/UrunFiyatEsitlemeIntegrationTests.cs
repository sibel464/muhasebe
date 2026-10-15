using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Peşin (tek çekim) fiyat ile fatura/siparişin kullandığı satış fiyatı her zaman aynı kalmalı.
[Collection("PostgreSQL integration")]
public sealed class UrunFiyatEsitlemeIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public UrunFiyatEsitlemeIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Taksit_fiyatlariyla_eklenen_urunun_satis_fiyati_pesin_fiyattir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new UrunServisi(db, StabilizasyonTestOturumu.Olustur());
        var urun = YeniUrun();
        urun.SatisFiyatlari.Add(new UrunSatisFiyati { TaksitSayisi = 1, Fiyat = 1500, ParaBirimiId = 1 });
        urun.SatisFiyatlari.Add(new UrunSatisFiyati { TaksitSayisi = 3, Fiyat = 1700, ParaBirimiId = 1 });

        await servis.EkleAsync(urun);

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        Assert.Equal(1500m, (await kontrol.Urunler.SingleAsync(x => x.Id == urun.Id)).PerakendeSatisFiyati);
    }

    [Fact]
    public async Task Satis_fiyati_degisince_pesin_fiyat_da_degisir_ve_tersi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new UrunServisi(db, StabilizasyonTestOturumu.Olustur());
        var urun = YeniUrun();
        urun.SatisFiyatlari.Add(new UrunSatisFiyati { TaksitSayisi = 1, Fiyat = 100, ParaBirimiId = 1 });
        await servis.EkleAsync(urun);

        urun.PerakendeSatisFiyati = 120;
        await servis.GuncelleAsync(urun);
        await using (var kontrol = new MuhasebeBaglami(_fixture.Options))
            Assert.Equal(120m, (await kontrol.UrunSatisFiyatlari.SingleAsync(x => x.UrunId == urun.Id && x.TaksitSayisi == 1)).Fiyat);

        await servis.FiyatlariKaydetAsync(urun.Id, [new UrunSatisFiyati { TaksitSayisi = 1, Fiyat = 140, ParaBirimiId = 1 }]);
        await using (var kontrol = new MuhasebeBaglami(_fixture.Options))
            Assert.Equal(140m, (await kontrol.Urunler.SingleAsync(x => x.Id == urun.Id)).PerakendeSatisFiyati);
    }

    private static Urun YeniUrun() => new()
    {
        Kod = "FY-" + Guid.NewGuid().ToString("N")[..10],
        Ad = "Fiyat Eşitleme Test Ürünü",
        Birim = "Adet",
        KdvOrani = 20,
        Aktif = true
    };
}
