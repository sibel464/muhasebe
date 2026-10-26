using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Kısmi KDV tevkifatı: satıcı tevkif edilen KDV'yi beyan etmez (391 eksik), alıcı tam KDV'yi indirir (191) ve
// tevkif ettiğini 360'a yazar; cari ve tahsilat "ödenecek tutar" (vergiler dahil toplam - tevkifat) üzerinden yürür.
[Collection("PostgreSQL integration")]
public sealed class KdvTevkifatiTests(IsolatedPostgreSqlFixture fixture)
{
    private static Cari360TestKullanici TamYetkili()
    {
        var y = new KullaniciYetkileri();
        foreach (var p in typeof(KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            p.SetValue(y, true);
        return new Cari360TestKullanici { AktifYetkiler = y };
    }

    private async Task<(int CariId, int HizmetId)> HazirlaAsync()
    {
        fixture.RequireDatabase();
        var k = TamYetkili();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var cari = await new CariServisi(c, k).OtomatikKodlaEkleAsync(new CariKart { Unvan = "Tevkifat Cari " + Guid.NewGuid().ToString("N")[..6], Aktif = true });
        var hizmet = await new UrunServisi(c, k).GuvenliEkleAsync(new Urun { Ad = "Temizlik Hizmeti", Birim = "Saat", KdvOrani = 20, Aktif = true, StokTakibi = false }, true, true);
        return (cari.Id, hizmet.Id);
    }

    private static Fatura Yeni(FaturaTuru tur, int cariId, int urunId, string? kod = "612", FaturaOdemeSekli odeme = FaturaOdemeSekli.AcikHesap) => new()
    {
        Tur = tur, CariKartId = cariId, Tarih = DateTime.UtcNow, TevkifatKodu = kod, OdemeSekli = odeme,
        Kalemler = [new FaturaKalemi { UrunId = urunId, Miktar = 10, BirimFiyat = 100, KdvOrani = 20 }]
    };

    private static async Task<Dictionary<string, (decimal Borc, decimal Alacak)>> FisAsync(MuhasebeBaglami c, int faturaId) =>
        (await c.YevmiyeSatirlari.Include(s => s.Hesap).Where(s => s.Fis!.BelgeTipi == "Fatura" && s.Fis.BelgeId == faturaId).ToListAsync())
        .GroupBy(s => s.Hesap!.HesapKodu).ToDictionary(g => g.Key, g => (g.Sum(s => s.Borc), g.Sum(s => s.Alacak)));

    [Fact]
    public async Task Tevkifatli_satis_cariye_odenecek_tutari_yazar_391e_yalniz_tevkif_edilmeyen_kdv_gider()
    {
        var x = await HazirlaAsync();
        Fatura f;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new FaturaServisi(c, TamYetkili());
            f = await s.EkleAsync(Yeni(FaturaTuru.Satis, x.CariId, x.HizmetId));
            await s.OnaylaAsync(f.Id);
        }
        Assert.Equal(1200m, f.GenelToplam);
        Assert.Equal(180m, f.TevkifatTutari); // 200 KDV × 9/10
        Assert.Equal(0.9m, f.TevkifatOrani);

        await using var v = new MuhasebeBaglami(fixture.Options);
        var fis = await FisAsync(v, f.Id);
        Assert.Equal(1020m, fis["120"].Borc);
        Assert.Equal(1000m, fis["600"].Alacak);
        Assert.Equal(20m, fis["391"].Alacak);
        Assert.Equal(1020m, (await v.CariKartlar.SingleAsync(c => c.Id == x.CariId)).Bakiye);
    }

    [Fact]
    public async Task Tevkifatli_alis_tam_kdvyi_indirir_tevkif_edileni_360a_yazar()
    {
        var x = await HazirlaAsync();
        int id;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new FaturaServisi(c, TamYetkili());
            id = (await s.EkleAsync(Yeni(FaturaTuru.Alis, x.CariId, x.HizmetId))).Id;
            await s.OnaylaAsync(id);
        }

        await using var v = new MuhasebeBaglami(fixture.Options);
        var fis = await FisAsync(v, id);
        Assert.Equal(1000m, fis["770"].Borc);
        Assert.Equal(200m, fis["191"].Borc);
        Assert.Equal(1020m, fis["320"].Alacak);
        Assert.Equal(180m, fis["360"].Alacak);
        Assert.Equal(-1020m, (await v.CariKartlar.SingleAsync(c => c.Id == x.CariId)).Bakiye);
    }

    [Fact]
    public async Task Pesin_tevkifatli_satista_kasaya_odenecek_tutar_girer_ve_iptal_tam_geri_alir()
    {
        var x = await HazirlaAsync();
        int id;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new FaturaServisi(c, TamYetkili());
            id = (await s.PosSatisiniTamamlaAsync(Yeni(FaturaTuru.Satis, x.CariId, x.HizmetId, "624", FaturaOdemeSekli.Nakit), "Nakit")).Id;
        }
        await using (var v = new MuhasebeBaglami(fixture.Options))
        {
            Assert.Equal(1160m, (await v.KasaHareketleri.SingleAsync(h => h.FaturaId == id)).Tutar); // 1200 - 200 × 2/10
            Assert.Equal(0m, (await v.CariKartlar.SingleAsync(c => c.Id == x.CariId)).Bakiye);
        }
        await using (var c = new MuhasebeBaglami(fixture.Options))
            await new FaturaServisi(c, TamYetkili()).IptalEtAsync(id, "test");
        await using (var v = new MuhasebeBaglami(fixture.Options))
        {
            Assert.Equal(0m, (await v.CariKartlar.SingleAsync(c => c.Id == x.CariId)).Bakiye);
            var satirlar = await v.YevmiyeSatirlari.Where(s => (s.Fis!.BelgeTipi == "Fatura" || s.Fis.BelgeTipi == "FaturaIptal") && s.Fis.BelgeId == id).ToListAsync();
            Assert.All(satirlar.GroupBy(s => s.HesapId), g => Assert.Equal(g.Sum(s => s.Borc), g.Sum(s => s.Alacak)));
        }
    }

    [Fact]
    public async Task Iade_faturasinda_ve_bilinmeyen_kodda_tevkifat_reddedilir_oran_degistirilebilir()
    {
        var x = await HazirlaAsync();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var s = new FaturaServisi(c, TamYetkili());
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.EkleAsync(Yeni(FaturaTuru.SatisIade, x.CariId, x.HizmetId)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.EkleAsync(Yeni(FaturaTuru.Satis, x.CariId, x.HizmetId, "999")));
        var ozel = Yeni(FaturaTuru.Satis, x.CariId, x.HizmetId, "612");
        ozel.TevkifatOrani = 0.5m;
        Assert.Equal(100m, (await s.EkleAsync(ozel)).TevkifatTutari);
        var hatali = Yeni(FaturaTuru.Satis, x.CariId, x.HizmetId, "612");
        hatali.TevkifatOrani = 0.55m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.EkleAsync(hatali));
    }
}
