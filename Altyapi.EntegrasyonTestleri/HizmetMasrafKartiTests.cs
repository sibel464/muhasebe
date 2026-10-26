using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Hizmet / masraf kartı: stok tutulmaz; alışı ticari mallar (153) yerine gider hesabına, KDV'si 191'e yazılır.
[Collection("PostgreSQL integration")]
public sealed class HizmetMasrafKartiTests(IsolatedPostgreSqlFixture fixture)
{
    private static Cari360TestKullanici TamYetkili()
    {
        var y = new KullaniciYetkileri();
        foreach (var p in typeof(KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            p.SetValue(y, true);
        return new Cari360TestKullanici { AktifYetkiler = y };
    }

    private async Task<(int CariId, int StokluId, int MasrafId, int HizmetId)> HazirlaAsync()
    {
        fixture.RequireDatabase();
        var k = TamYetkili();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var cari = await new CariServisi(c, k).OtomatikKodlaEkleAsync(new CariKart { Unvan = "Hizmet Test Cari " + Guid.NewGuid().ToString("N")[..6], Aktif = true });
        var urunler = new UrunServisi(c, k);
        var stoklu = await urunler.GuvenliEkleAsync(new Urun { Ad = "Stoklu Mal", Birim = "Adet", KdvOrani = 20, Aktif = true }, true, true);
        var masraf = await urunler.GuvenliEkleAsync(new Urun { Ad = "Elektrik", Birim = "Adet", KdvOrani = 20, Aktif = true, StokTakibi = false, GiderHesapKodu = "770" }, true, true);
        var hizmet = await urunler.GuvenliEkleAsync(new Urun { Ad = "İşçilik", Birim = "Saat", KdvOrani = 20, Aktif = true, StokTakibi = false }, true, true);
        return (cari.Id, stoklu.Id, masraf.Id, hizmet.Id);
    }

    private static async Task<Dictionary<string, (decimal Borc, decimal Alacak)>> FisAsync(MuhasebeBaglami c, int faturaId) =>
        (await c.YevmiyeSatirlari.Include(s => s.Hesap).Where(s => s.Fis!.BelgeTipi == "Fatura" && s.Fis.BelgeId == faturaId).ToListAsync())
        .GroupBy(s => s.Hesap!.HesapKodu).ToDictionary(g => g.Key, g => (g.Sum(s => s.Borc), g.Sum(s => s.Alacak)));

    [Fact]
    public async Task Karisik_alis_faturasinda_mal_153e_masraf_770e_kdv_191e_yazilir_masrafa_stok_girmez()
    {
        var x = await HazirlaAsync();
        int faturaId;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new FaturaServisi(c, TamYetkili());
            var f = await s.EkleAsync(new Fatura
            {
                Tur = FaturaTuru.Alis, CariKartId = x.CariId, Tarih = DateTime.UtcNow,
                Kalemler =
                [
                    new FaturaKalemi { UrunId = x.StokluId, Miktar = 2, BirimFiyat = 100, KdvOrani = 20 },
                    new FaturaKalemi { UrunId = x.MasrafId, Miktar = 1, BirimFiyat = 500, KdvOrani = 20 }
                ]
            });
            faturaId = (await s.OnaylaAsync(f.Id)).Id;
        }

        await using var v = new MuhasebeBaglami(fixture.Options);
        var fis = await FisAsync(v, faturaId);
        Assert.Equal(200m, fis["153"].Borc);
        Assert.Equal(500m, fis["770"].Borc);
        Assert.Equal(140m, fis["191"].Borc);
        Assert.Equal(840m, fis["320"].Alacak);
        Assert.Equal(2, (await v.Urunler.SingleAsync(u => u.Id == x.StokluId)).StokMiktari);
        Assert.Equal(0, (await v.Urunler.SingleAsync(u => u.Id == x.MasrafId)).StokMiktari);
        Assert.False(await v.StokHareketleri.AnyAsync(h => h.UrunId == x.MasrafId));
    }

    [Fact]
    public async Task Genel_iskontolu_masraf_faturasi_dengeli_ve_iptali_tam_geri_alir()
    {
        var x = await HazirlaAsync();
        int faturaId;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new FaturaServisi(c, TamYetkili());
            var f = await s.EkleAsync(new Fatura
            {
                Tur = FaturaTuru.Alis, CariKartId = x.CariId, Tarih = DateTime.UtcNow, GenelIskontoOrani = 10,
                Kalemler =
                [
                    new FaturaKalemi { UrunId = x.StokluId, Miktar = 3, BirimFiyat = 33.33m, KdvOrani = 20 },
                    new FaturaKalemi { UrunId = x.MasrafId, Miktar = 1, BirimFiyat = 77.77m, KdvOrani = 10 }
                ]
            });
            faturaId = (await s.OnaylaAsync(f.Id)).Id;
            await s.IptalEtAsync(faturaId, "test");
        }

        await using var v = new MuhasebeBaglami(fixture.Options);
        var fis = await FisAsync(v, faturaId);
        Assert.Equal(fis.Values.Sum(t => t.Borc), fis.Values.Sum(t => t.Alacak));
        Assert.True(fis["770"].Borc > 0 && fis["153"].Borc > 0);
        var iptal = await v.YevmiyeSatirlari.Where(s => s.Fis!.BelgeTipi == "FaturaIptal" && s.Fis.BelgeId == faturaId).ToListAsync();
        Assert.Equal(fis.Values.Sum(t => t.Borc), iptal.Sum(s => s.Alacak));
        Assert.Equal(0, (await v.Urunler.SingleAsync(u => u.Id == x.StokluId)).StokMiktari);
    }

    [Fact]
    public async Task Stoksuz_hizmet_satilir_stok_hareketi_olusmaz()
    {
        var x = await HazirlaAsync();
        int faturaId;
        await using (var c = new MuhasebeBaglami(fixture.Options))
            faturaId = (await new FaturaServisi(c, TamYetkili()).PosSatisiniTamamlaAsync(new Fatura
            {
                Tur = FaturaTuru.Satis, CariKartId = x.CariId, Tarih = DateTime.UtcNow,
                Kalemler = [new FaturaKalemi { UrunId = x.HizmetId, Miktar = 3, BirimFiyat = 400, KdvOrani = 20 }]
            }, "Açık Hesap")).Id;

        await using var v = new MuhasebeBaglami(fixture.Options);
        var fis = await FisAsync(v, faturaId);
        Assert.Equal(1440m, fis["120"].Borc);
        Assert.Equal(1200m, fis["600"].Alacak);
        Assert.False(await v.StokHareketleri.AnyAsync(h => h.UrunId == x.HizmetId));
        Assert.Equal(0, (await v.Urunler.SingleAsync(u => u.Id == x.HizmetId)).StokMiktari);
    }

    [Fact]
    public async Task Hizmet_kartina_stok_hareketi_yazilamaz_ve_kullanilmis_kartin_turu_degistirilemez()
    {
        var x = await HazirlaAsync();
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            c.StokHareketleri.Add(new StokHareketi { UrunId = x.HizmetId, Tarih = DateTime.UtcNow, HareketTuru = StokHareketTuru.Giris, Miktar = 5, Aciklama = "deneme" });
            var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => c.SaveChangesAsync());
            Assert.Contains("hizmet / masraf", hata.Message);
        }

        // Stoklu ürün alışla hareket gördükten sonra hizmete çevrilemez.
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new FaturaServisi(c, TamYetkili());
            var f = await s.EkleAsync(new Fatura
            {
                Tur = FaturaTuru.Alis, CariKartId = x.CariId, Tarih = DateTime.UtcNow,
                Kalemler = [new FaturaKalemi { UrunId = x.StokluId, Miktar = 1, BirimFiyat = 10, KdvOrani = 20 }]
            });
            await s.OnaylaAsync(f.Id);
        }
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var urun = await c.Urunler.AsNoTracking().SingleAsync(u => u.Id == x.StokluId);
            urun.StokTakibi = false;
            await Assert.ThrowsAsync<InvalidOperationException>(() => new UrunServisi(c, TamYetkili()).GuncelleAsync(urun));
        }
    }

    [Fact]
    public async Task Hizmet_kartina_acilis_stogu_girilemez_ve_gider_kodu_dogrulanir()
    {
        fixture.RequireDatabase();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var s = new UrunServisi(c, TamYetkili());
        var hizmet = new Urun { Ad = "Danışmanlık", Birim = "Saat", KdvOrani = 20, Aktif = true, StokTakibi = false };
        hizmet.LokasyonStoklari.Add(new UrunLokasyonStogu { DepoId = 1, MevcutMiktar = 5 });
        await Assert.ThrowsAsync<ArgumentException>(() => s.GuvenliEkleAsync(hizmet, true, true));
        await Assert.ThrowsAsync<ArgumentException>(() => s.GuvenliEkleAsync(
            new Urun { Ad = "Kira", Birim = "Adet", KdvOrani = 20, Aktif = true, StokTakibi = false, GiderHesapKodu = "kira hesabı" }, true, true));
    }
}
