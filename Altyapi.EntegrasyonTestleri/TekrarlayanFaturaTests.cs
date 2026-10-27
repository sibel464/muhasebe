using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Tekrarlayan fatura: kaçırılan her dönem kendi tarihiyle ayrı kesilir, ayın 31'i kaymaz, bitiş tarihine uyulur,
// kesilemeyen dönemde plan durur ve hata saklanır.
[Collection("PostgreSQL integration")]
public sealed class TekrarlayanFaturaTests(IsolatedPostgreSqlFixture fixture)
{
    private static Cari360TestKullanici TamYetkili()
    {
        var y = new KullaniciYetkileri();
        foreach (var p in typeof(KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            p.SetValue(y, true);
        return new Cari360TestKullanici { AktifYetkiler = y };
    }

    private static DateTime T(int y, int a, int g) => new(y, a, g, 0, 0, 0, DateTimeKind.Utc);

    private async Task<(int CariId, int OrnekId)> HazirlaAsync(FaturaTuru tur = FaturaTuru.Satis)
    {
        fixture.RequireDatabase();
        var k = TamYetkili();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var cari = await new CariServisi(c, k).OtomatikKodlaEkleAsync(new CariKart { Unvan = "Kiracı " + Guid.NewGuid().ToString("N")[..6], Aktif = true });
        var hizmet = await new UrunServisi(c, k).GuvenliEkleAsync(new Urun { Ad = "Kira Bedeli", Birim = "Adet", KdvOrani = 20, Aktif = true, StokTakibi = false }, true, true);
        var ornek = await new FaturaServisi(c, k).EkleAsync(new Fatura
        {
            Tur = tur, CariKartId = cari.Id, Tarih = T(2026, 1, 5), Aciklama = "Dükkan kirası",
            Kalemler = [new FaturaKalemi { UrunId = hizmet.Id, Miktar = 1, BirimFiyat = 1000, KdvOrani = 20 }]
        });
        return (cari.Id, ornek.Id);
    }

    [Fact]
    public async Task Kacirilan_donemler_ayri_ayri_kesilir_ayin_31i_kaymaz_ve_onaylanir()
    {
        var x = await HazirlaAsync();
        int planId;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new TekrarlayanFaturaServisi(c, TamYetkili());
            planId = (await s.KaydetAsync(new TekrarlayanFatura { Ad = "Kira", OrnekFaturaId = x.OrnekId, PeriyotAy = 1, BaslangicTarihi = T(2026, 1, 31), VadeGunu = 15, Onayla = true })).Id;
            Assert.Equal(3, (await s.ListeAsync(T(2026, 4, 15))).Single(p => p.Id == planId).BekleyenAdet);

            var sonuc = await s.BekleyenleriKesAsync(T(2026, 4, 15), planId);
            Assert.All(sonuc, r => Assert.Null(r.Hata));
            Assert.Equal(new[] { T(2026, 1, 31), T(2026, 2, 28), T(2026, 3, 31) }, sonuc.Select(r => r.Tarih));
            Assert.Empty(await s.BekleyenleriKesAsync(T(2026, 4, 15), planId));

            var plan = (await s.ListeAsync(T(2026, 4, 15))).Single(p => p.Id == planId);
            Assert.Equal(3, plan.OlusturulanAdet);
            Assert.Equal(0, plan.BekleyenAdet);
            Assert.Equal(T(2026, 4, 30), plan.SiradakiTarih);
            Assert.Equal(sonuc[2].FaturaNo, plan.SonFaturaNo);
        }

        await using var v = new MuhasebeBaglami(fixture.Options);
        var faturalar = await v.Faturalar.AsNoTracking().Include(f => f.Kalemler)
            .Where(f => f.CariKartId == x.CariId && f.Id != x.OrnekId).OrderBy(f => f.Tarih).ToListAsync();
        Assert.Equal(3, faturalar.Count);
        Assert.All(faturalar, f => Assert.Equal(BelgeDurumu.Onaylandi, f.Durum));
        Assert.All(faturalar, f => Assert.Equal(1200m, f.GenelToplam));
        Assert.Equal(T(2026, 3, 15), faturalar[1].VadeTarihi);
        Assert.Equal("Dükkan kirası (Kira - 02/2026)", faturalar[1].Aciklama);
        Assert.Equal(3600m, (await v.CariKartlar.SingleAsync(c => c.Id == x.CariId)).Bakiye);
        Assert.Equal(BelgeDurumu.Taslak, (await v.Faturalar.SingleAsync(f => f.Id == x.OrnekId)).Durum);
    }

    [Fact]
    public async Task Taslak_kesilir_ve_bitis_tarihinden_sonra_kesilmez()
    {
        var x = await HazirlaAsync(FaturaTuru.Alis);
        await using var c = new MuhasebeBaglami(fixture.Options);
        var s = new TekrarlayanFaturaServisi(c, TamYetkili());
        var plan = await s.KaydetAsync(new TekrarlayanFatura { OrnekFaturaId = x.OrnekId, PeriyotAy = 1, BaslangicTarihi = T(2026, 1, 1), BitisTarihi = T(2026, 2, 15) });

        var sonuc = await s.BekleyenleriKesAsync(T(2026, 6, 1), plan.Id);

        Assert.Equal(2, sonuc.Count);
        var dto = (await s.ListeAsync(T(2026, 6, 1))).Single(p => p.Id == plan.Id);
        Assert.True(dto.BittiMi);
        Assert.Equal("Bitti", dto.Durum);
        Assert.StartsWith("Kiracı", dto.Ad); // ad verilmezse cari ünvanı
        var yeni = await c.Faturalar.AsNoTracking().Where(f => f.CariKartId == x.CariId && f.Id != x.OrnekId).ToListAsync();
        Assert.All(yeni, f => Assert.Equal(BelgeDurumu.Taslak, f.Durum));
        Assert.All(yeni, f => Assert.Null(f.VadeTarihi));
        Assert.Equal(0m, (await c.CariKartlar.AsNoTracking().SingleAsync(k => k.Id == x.CariId)).Bakiye);
    }

    [Fact]
    public async Task Kesilemeyen_donemde_plan_durur_hata_saklanir_duzelince_devam_eder()
    {
        var x = await HazirlaAsync();
        int planId;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            planId = (await new TekrarlayanFaturaServisi(c, TamYetkili()).KaydetAsync(
                new TekrarlayanFatura { Ad = "Aidat", OrnekFaturaId = x.OrnekId, PeriyotAy = 3, BaslangicTarihi = T(2026, 1, 10), Onayla = true })).Id;
            await c.CariKartlar.Where(k => k.Id == x.CariId).ExecuteUpdateAsync(u => u.SetProperty(k => k.Aktif, false));
        }

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new TekrarlayanFaturaServisi(c, TamYetkili());
            var sonuc = Assert.Single(await s.BekleyenleriKesAsync(T(2026, 7, 20), planId));
            Assert.Contains("Pasif", sonuc.Hata);
            var dto = (await s.ListeAsync(T(2026, 7, 20))).Single(p => p.Id == planId);
            Assert.Equal(0, dto.OlusturulanAdet);
            Assert.Equal(3, dto.BekleyenAdet); // 10.01, 10.04, 10.07
            Assert.StartsWith("10.01.2026 dönemi kesilemedi", dto.SonHata);
            Assert.False(await c.Faturalar.AnyAsync(f => f.CariKartId == x.CariId && f.Id != x.OrnekId));
            await c.CariKartlar.Where(k => k.Id == x.CariId).ExecuteUpdateAsync(u => u.SetProperty(k => k.Aktif, true));
        }

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new TekrarlayanFaturaServisi(c, TamYetkili());
            Assert.Equal(3, (await s.BekleyenleriKesAsync(T(2026, 7, 20), planId)).Count(r => r.Hata == null));
            var dto = (await s.ListeAsync(T(2026, 7, 20))).Single(p => p.Id == planId);
            Assert.Null(dto.SonHata);
            Assert.Equal(T(2026, 10, 10), dto.SiradakiTarih);
        }
    }

    [Fact]
    public async Task Kesilmeye_baslanmis_planin_takvimi_degismez_iade_faturasi_tekrarlanamaz()
    {
        var x = await HazirlaAsync();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var s = new TekrarlayanFaturaServisi(c, TamYetkili());
        var plan = await s.KaydetAsync(new TekrarlayanFatura { Ad = "Bakım", OrnekFaturaId = x.OrnekId, PeriyotAy = 1, BaslangicTarihi = T(2026, 1, 1) });
        await s.BekleyenleriKesAsync(T(2026, 1, 1), plan.Id);

        var degisik = new TekrarlayanFatura { Id = plan.Id, Ad = "Bakım", OrnekFaturaId = x.OrnekId, PeriyotAy = 3, BaslangicTarihi = T(2026, 1, 1), Aktif = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.KaydetAsync(degisik));
        degisik.PeriyotAy = 1;
        degisik.Aktif = false;
        await s.KaydetAsync(degisik);
        Assert.Empty(await s.BekleyenleriKesAsync(T(2026, 12, 1), plan.Id)); // durdurulan plan kesmez

        var iade = await new FaturaServisi(c, TamYetkili()).EkleAsync(new Fatura
        {
            Tur = FaturaTuru.SatisIade, CariKartId = x.CariId, Tarih = T(2026, 1, 5),
            Kalemler = (await c.FaturaKalemleri.AsNoTracking().Where(k => k.FaturaId == x.OrnekId).ToListAsync())
                .Select(k => new FaturaKalemi { UrunId = k.UrunId, Miktar = 1, BirimFiyat = 10, KdvOrani = 20 }).ToList()
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.KaydetAsync(new TekrarlayanFatura { OrnekFaturaId = iade.Id, PeriyotAy = 1, BaslangicTarihi = T(2026, 2, 1) }));
    }
}
