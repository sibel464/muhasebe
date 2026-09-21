using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Tanimlar;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class UrunKayitDevelopmentDbTests
{
    private const string TestKodu = "RT_P1_FINAL_20260827";
    private readonly IsolatedPostgreSqlFixture _fixture;

    public UrunKayitDevelopmentDbTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task UrunServisi_GecerliUrunu_PostgreSqlVeritabaninaKaydeder()
    {
        _fixture.RequireDatabase();
        var options = _fixture.Options;

        await using (var temizleme = new MuhasebeBaglami(options))
        {
            var onceki = await temizleme.Urunler.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Kod == TestKodu);
            if (onceki is not null)
            {
                temizleme.Urunler.Remove(onceki);
                await temizleme.SaveChangesAsync();
            }
        }

        int? kategoriId;
        int? markaId;
        int? birimId;
        await using (var tanimBaglami = new MuhasebeBaglami(options))
        {
            kategoriId = await TanimIdAsync(tanimBaglami, TanimGruplari.UrunKategorisi);
            markaId = await TanimIdAsync(tanimBaglami, TanimGruplari.Marka);
            birimId = await TanimIdAsync(tanimBaglami, TanimGruplari.Birim);
        }

        await using (var kayitBaglami = new MuhasebeBaglami(options))
        {
            var servis = new UrunServisi(kayitBaglami, StabilizasyonTestOturumu.Olustur());
            var sonuc = await servis.EkleAsync(new Urun
            {
                Kod = TestKodu,
                Ad = "Runtime Test Ürün",
                Birim = "Adet",
                KategoriTanimiId = kategoriId,
                MarkaTanimiId = markaId,
                BirimTanimiId = birimId,
                AlisFiyati = 12.50m,
                PerakendeSatisFiyati = 25.50m,
                ToptanSatisFiyati = 18.75m,
                KdvOrani = 20m,
                StokMiktari = 7m,
                MinimumStok = 2m,
                Aktif = true
            });
            Assert.True(sonuc.Id > 0);
        }

        await using var dogrulama = new MuhasebeBaglami(options);
        var kayit = await dogrulama.Urunler.AsNoTracking().SingleAsync(x => x.Kod == TestKodu);
        Assert.Equal("Runtime Test Ürün", kayit.Ad);
        Assert.Equal(MuhasebeBaglami.VarsayilanSirketId, kayit.SirketId);
        Assert.Equal(kategoriId, kayit.KategoriTanimiId);
        Assert.Equal(markaId, kayit.MarkaTanimiId);
        Assert.Equal(birimId, kayit.BirimTanimiId);
    }

    private static Task<int?> TanimIdAsync(MuhasebeBaglami baglam, string grup) =>
        baglam.Set<MerkeziTanim>()
            .Where(x => x.Grup == grup && x.AktifMi)
            .OrderBy(x => x.SiraNo)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
}
