using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Stok;
using Uygulama.Dto.Rapor;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Ba-Bs: aynı cariyle bir ayda KDV hariç 5.000 TL ve üzeri alış (Ba) ve satış (Bs) toplamları; iptal edilen belge sayılmaz.
[Collection("PostgreSQL integration")]
public sealed class BaBsRaporuTests(IsolatedPostgreSqlFixture fixture)
{
    private static Cari360TestKullanici TamYetkili()
    {
        var y = new KullaniciYetkileri();
        foreach (var p in typeof(KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            p.SetValue(y, true);
        return new Cari360TestKullanici { AktifYetkiler = y };
    }

    private static Fatura Yeni(FaturaTuru tur, int cariId, int urunId, decimal fiyat) => new()
    {
        Tur = tur, CariKartId = cariId, Tarih = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc),
        Kalemler = [new FaturaKalemi { UrunId = urunId, Miktar = 1, BirimFiyat = fiyat, KdvOrani = 20 }]
    };

    private async Task<List<BaBsKalemiDto>> RaporAsync(int cariId)
    {
        await using var c = new MuhasebeBaglami(fixture.Options);
        var f = new SatisRaporFiltreDto { BaslangicTarihi = DateTime.Today.AddDays(-1), BitisTarihi = DateTime.Today.AddDays(1), CariId = cariId };
        return await StabilizasyonTestOturumu.Rapor(c, MuhasebeBaglami.VarsayilanSirketId).GetBaBsRaporuAsync(f);
    }

    [Fact]
    public async Task Sinir_ustu_satis_ve_alis_kdv_haric_kusuratsiz_listelenir_iptal_sayilmaz()
    {
        fixture.RequireDatabase();
        var k = TamYetkili();
        int cariId, vknsizId, hizmetId;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            cariId = (await new CariServisi(c, k).OtomatikKodlaEkleAsync(new CariKart { Unvan = "BaBs Cari " + Guid.NewGuid().ToString("N")[..6], VergiNo = "1234567890", VergiDairesi = "Kadıköy", Aktif = true })).Id;
            vknsizId = (await new CariServisi(c, k).OtomatikKodlaEkleAsync(new CariKart { Unvan = "BaBs VKN'siz " + Guid.NewGuid().ToString("N")[..6], Aktif = true })).Id;
            hizmetId = (await new UrunServisi(c, k).GuvenliEkleAsync(new Urun { Ad = "Danışmanlık", Birim = "Saat", KdvOrani = 20, Aktif = true, StokTakibi = false }, true, true)).Id;
        }
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new FaturaServisi(c, k);
            async Task<int> OnayliAsync(FaturaTuru tur, int cari, decimal fiyat)
            {
                var id = (await s.EkleAsync(Yeni(tur, cari, hizmetId, fiyat))).Id;
                await s.OnaylaAsync(id);
                return id;
            }
            await OnayliAsync(FaturaTuru.Satis, cariId, 4000.40m);
            await OnayliAsync(FaturaTuru.Satis, cariId, 1000.30m);   // Bs: 5.000,70 → 5.000
            await OnayliAsync(FaturaTuru.Alis, cariId, 4999.99m);    // Ba: sınırın altında
            var iptal = await OnayliAsync(FaturaTuru.Satis, cariId, 9000m);
            await s.IptalEtAsync(iptal, "test");
            await OnayliAsync(FaturaTuru.Alis, vknsizId, 3000m);
            await OnayliAsync(FaturaTuru.Alis, vknsizId, 3000m);     // Ba: 6.000
        }

        var rapor = await RaporAsync(cariId);
        var bs = Assert.Single(rapor);
        Assert.Equal("Bs", bs.Form);
        Assert.Equal(2, bs.BelgeSayisi);
        Assert.Equal(5000m, bs.Tutar);
        Assert.Equal("1234567890", bs.VergiKimlikNo);
        Assert.Equal("", bs.Uyari);
        Assert.Equal(DateTime.Now.Month, bs.Ay);

        var ba = Assert.Single(await RaporAsync(vknsizId));
        Assert.Equal("Ba", ba.Form);
        Assert.Equal(6000m, ba.Tutar);
        Assert.NotEqual("", ba.Uyari);
    }
}
