using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Stok yalnızca ürün ilk kaydedilirken (açılış stoğu) karttan girilir; sonra kart düzenlemesi stoğa dokunamaz.
[Collection("PostgreSQL integration")]
public sealed class UrunAcilisStoguIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public UrunAcilisStoguIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Acilis_stogu_lokasyona_yazilir_ve_hareket_kaydi_olusur()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var depoId = await db.Depolar.Where(d => d.AktifMi).OrderBy(d => d.Id).Select(d => d.Id).FirstAsync();
        var urun = YeniUrun();
        urun.AlisFiyati = 40;
        urun.LokasyonStoklari.Add(new UrunLokasyonStogu { DepoId = depoId, MevcutMiktar = 12 });

        await new UrunServisi(db, StabilizasyonTestOturumu.Olustur()).EkleAsync(urun);

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        Assert.Equal(12m, (await kontrol.Urunler.SingleAsync(x => x.Id == urun.Id)).StokMiktari);
        Assert.Equal(12m, (await kontrol.UrunLokasyonStoklari.SingleAsync(x => x.UrunId == urun.Id && x.DepoId == depoId)).MevcutMiktar);
        var hareket = await kontrol.StokHareketleri.SingleAsync(x => x.UrunId == urun.Id);
        Assert.Equal(StokHareketTuru.Giris, hareket.HareketTuru);
        Assert.Equal(StokIslemTuru.DevirGiris, hareket.IslemTuru);
        Assert.Equal(12m, hareket.Miktar);
        Assert.Equal(40m, hareket.BirimFiyat);
        Assert.Equal(depoId, hareket.DepoId);
    }

    [Fact]
    public async Task Kart_duzenlemesi_stogu_degistirmez()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var depoId = await db.Depolar.Where(d => d.AktifMi).OrderBy(d => d.Id).Select(d => d.Id).FirstAsync();
        var servis = new UrunServisi(db, StabilizasyonTestOturumu.Olustur());
        var urun = YeniUrun();
        urun.LokasyonStoklari.Add(new UrunLokasyonStogu { DepoId = depoId, MevcutMiktar = 5 });
        await servis.EkleAsync(urun);

        var duzenleme = YeniUrun();
        duzenleme.Id = urun.Id;
        duzenleme.Kod = urun.Kod;
        duzenleme.Ad = "Adı değişti";
        duzenleme.StokMiktari = 999;
        await servis.KartGuncelleAsync(duzenleme, []);

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        var kayit = await kontrol.Urunler.SingleAsync(x => x.Id == urun.Id);
        Assert.Equal("Adı değişti", kayit.Ad);
        Assert.Equal(5m, kayit.StokMiktari);
        Assert.Equal(5m, (await kontrol.UrunLokasyonStoklari.SingleAsync(x => x.UrunId == urun.Id)).MevcutMiktar);
    }

    [Fact]
    public async Task Gecersiz_depoya_ve_negatif_acilis_stogu_girilemez()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new UrunServisi(db, StabilizasyonTestOturumu.Olustur());

        var yokDepo = YeniUrun();
        yokDepo.LokasyonStoklari.Add(new UrunLokasyonStogu { DepoId = 999_999, MevcutMiktar = 3 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.EkleAsync(yokDepo));

        var depoId = await db.Depolar.Where(d => d.AktifMi).OrderBy(d => d.Id).Select(d => d.Id).FirstAsync();
        var negatif = YeniUrun();
        negatif.LokasyonStoklari.Add(new UrunLokasyonStogu { DepoId = depoId, MevcutMiktar = -1 });
        await Assert.ThrowsAsync<ArgumentException>(() => servis.EkleAsync(negatif));
    }

    private static Urun YeniUrun() => new()
    {
        Kod = "AS-" + Guid.NewGuid().ToString("N")[..10],
        Ad = "Açılış Stoğu Test Ürünü",
        Birim = "Adet",
        KdvOrani = 20,
        Aktif = true
    };
}
