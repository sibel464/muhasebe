using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Elle cari fişi, cari hareketiyle birlikte dengeli bir muhasebe fişi oluşturmalı; silinince fiş de silinmeli.
[Collection("PostgreSQL integration")]
public sealed class CariFisMuhasebeIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public CariFisMuhasebeIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Musteri_borc_dekontu_120_borc_karsi_hesap_alacak_fisi_olusturur()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var oturum = StabilizasyonTestOturumu.Olustur();
        var cari = await new CariServisi(db, oturum).EkleAsync(YeniCari(CariTuru.Musteri));
        var servis = new CariHareketServisi(db, oturum);

        var hareket = await servis.EkleAsync(Hareket(cari.Id, borc: 250), "602");

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        var fis = await kontrol.YevmiyeFisleri.Include(f => f.Satirlar).ThenInclude(s => s.Hesap)
            .SingleAsync(f => f.BelgeTipi == CariHareketServisi.CariFisBelgeTipi && f.BelgeId == hareket.Id);
        var borc = Assert.Single(fis.Satirlar, s => s.Borc > 0);
        var alacak = Assert.Single(fis.Satirlar, s => s.Alacak > 0);
        Assert.Equal("120", borc.Hesap!.HesapKodu);
        Assert.Equal(cari.Id, borc.CariKartId);
        Assert.Equal("602", alacak.Hesap!.HesapKodu);
        Assert.Equal(250m, borc.Borc);
        Assert.Equal(250m, (await kontrol.CariKartlar.SingleAsync(c => c.Id == cari.Id)).Borc);
    }

    [Fact]
    public async Task Tedarikci_alacak_devri_320_alacak_ve_silinince_fis_de_silinir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var oturum = StabilizasyonTestOturumu.Olustur();
        var cari = await new CariServisi(db, oturum).EkleAsync(YeniCari(CariTuru.Tedarikci));
        var servis = new CariHareketServisi(db, oturum);

        var devir = Hareket(cari.Id, alacak: 1000);
        devir.HareketTuru = CariHareketTuru.Devir;
        var hareket = await servis.EkleAsync(devir, "500");

        await using (var kontrol = new MuhasebeBaglami(_fixture.Options))
        {
            var fis = await kontrol.YevmiyeFisleri.Include(f => f.Satirlar).ThenInclude(s => s.Hesap)
                .SingleAsync(f => f.BelgeTipi == CariHareketServisi.CariFisBelgeTipi && f.BelgeId == hareket.Id);
            Assert.Equal("320", fis.Satirlar.Single(s => s.Alacak > 0).Hesap!.HesapKodu);
            Assert.Equal("500", fis.Satirlar.Single(s => s.Borc > 0).Hesap!.HesapKodu);
        }

        Assert.True(await servis.SilAsync(hareket.Id));

        await using var son = new MuhasebeBaglami(_fixture.Options);
        Assert.False(await son.YevmiyeFisleri.AnyAsync(f => f.BelgeTipi == CariHareketServisi.CariFisBelgeTipi && f.BelgeId == hareket.Id));
        Assert.Equal(0m, (await son.CariKartlar.SingleAsync(c => c.Id == cari.Id)).Alacak);
    }

    [Fact]
    public async Task Kasa_karsi_hesap_ve_belgeli_tur_reddedilir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var oturum = StabilizasyonTestOturumu.Olustur();
        var cari = await new CariServisi(db, oturum).EkleAsync(YeniCari(CariTuru.Musteri));
        var servis = new CariHareketServisi(db, oturum);

        await Assert.ThrowsAsync<ArgumentException>(() => servis.EkleAsync(Hareket(cari.Id, borc: 10), "100"));
        var fatura = Hareket(cari.Id, borc: 10);
        fatura.HareketTuru = CariHareketTuru.SatisFaturasi;
        await Assert.ThrowsAsync<ArgumentException>(() => servis.EkleAsync(fatura, "602"));
        Assert.False(await db.CariHareketler.AnyAsync(h => h.CariKartId == cari.Id));
    }

    private static CariHareket Hareket(int cariId, decimal borc = 0, decimal alacak = 0) => new()
    {
        CariKartId = cariId, Tarih = DateTime.UtcNow, HareketTuru = CariHareketTuru.Diger,
        BelgeNo = "DK-1", Aciklama = "Test dekontu", Borc = borc, Alacak = alacak
    };

    private static CariKart YeniCari(CariTuru tur)
    {
        var ek = Guid.NewGuid().ToString("N")[..10];
        return new CariKart { Kod = "CF-" + ek, Unvan = "Cari Fiş Test " + ek, Tur = tur };
    }
}
