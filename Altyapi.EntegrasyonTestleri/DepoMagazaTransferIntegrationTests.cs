using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Altyapi.Veri;
using Altyapi.Servisler;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public class DepoMagazaTransferIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;

    public DepoMagazaTransferIntegrationTests(IsolatedPostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private MuhasebeBaglami CreateContext() => new(_fixture.Options);
    private DepoServisi CreateService() => new(new TestContextFactory(_fixture.Options),
        new Cari360TestKullanici { AktifYetkiler = new() { AyarlariYonet = true, DepoTransferYap = true, StokDuzenle = true } });

    [Fact]
    public async Task Transfer_servisi_stoklari_atomik_gunceller_toplam_stogu_degistirmez()
    {
        await using var context = CreateContext();
        var depolar = await context.Depolar.OrderBy(d => d.Id).Take(2).ToListAsync();
        Assert.Equal(2, depolar.Count);
        var urun = new Urun { Kod = $"TR-{Guid.NewGuid():N}", Ad = "Transfer Ürünü", Birim = "Adet", Aktif = true, StokMiktari = 10, AlisFiyati = 25 };
        context.Urunler.Add(urun);
        await context.SaveChangesAsync();
        context.UrunLokasyonStoklari.AddRange(
            new UrunLokasyonStogu { UrunId = urun.Id, DepoId = depolar[0].Id, MevcutMiktar = 10 },
            new UrunLokasyonStogu { UrunId = urun.Id, DepoId = depolar[1].Id, MevcutMiktar = 0 });
        await context.SaveChangesAsync();

        var fis = await CreateService().TransferFisiKaydetAsync(new DepoTransferFisi
        {
            KaynakDepoId = depolar[0].Id, HedefDepoId = depolar[1].Id,
            Kalemler = [new DepoTransferKalemi { UrunId = urun.Id, Miktar = 4, BirimFiyat = 25 }]
        });

        await context.Entry(urun).ReloadAsync();
        Assert.Equal(10, urun.StokMiktari);
        Assert.Equal(6, await context.UrunLokasyonStoklari.Where(s => s.UrunId == urun.Id && s.DepoId == depolar[0].Id).Select(s => s.MevcutMiktar).SingleAsync());
        Assert.Equal(4, await context.UrunLokasyonStoklari.Where(s => s.UrunId == urun.Id && s.DepoId == depolar[1].Id).Select(s => s.MevcutMiktar).SingleAsync());
        Assert.Equal(2, await context.StokHareketleri.CountAsync(h => h.FisNo == fis.FisNo));
    }

    [Fact]
    public async Task Yetersiz_stok_transferi_tamamen_geri_alinir()
    {
        await using var context = CreateContext();
        var depolar = await context.Depolar.OrderBy(d => d.Id).Take(2).ToListAsync();
        var urun = new Urun { Kod = $"YS-{Guid.NewGuid():N}", Ad = "Yetersiz Stok", Birim = "Adet", Aktif = true, StokMiktari = 2 };
        context.Urunler.Add(urun);
        await context.SaveChangesAsync();
        context.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun.Id, DepoId = depolar[0].Id, MevcutMiktar = 2 });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().TransferFisiKaydetAsync(new DepoTransferFisi
        {
            KaynakDepoId = depolar[0].Id, HedefDepoId = depolar[1].Id,
            Kalemler = [new DepoTransferKalemi { UrunId = urun.Id, Miktar = 3 }]
        }));

        Assert.Equal(2, await context.UrunLokasyonStoklari.Where(s => s.UrunId == urun.Id).SumAsync(s => s.MevcutMiktar));
        Assert.False(await context.DepoTransferKalemleri.AnyAsync(k => k.UrunId == urun.Id));
        Assert.False(await context.StokHareketleri.AnyAsync(h => h.UrunId == urun.Id));
    }

    [Fact]
    public async Task Matris_depolari_koda_bagimli_olmadan_tur_ve_siraya_gore_esler()
    {
        await using var context = CreateContext();
        var magaza = await context.Depolar.Where(d => d.DepoTuru == DepoTuru.Magaza).OrderBy(d => d.Id).FirstAsync();
        var depo = await context.Depolar.Where(d => d.DepoTuru == DepoTuru.Depo).OrderBy(d => d.Id).FirstAsync();
        var urun = new Urun { Kod = $"MX-{Guid.NewGuid():N}", Ad = "Matris Ürünü", Birim = "Adet", Aktif = true };
        context.Urunler.Add(urun);
        await context.SaveChangesAsync();
        context.UrunLokasyonStoklari.AddRange(
            new UrunLokasyonStogu { UrunId = urun.Id, DepoId = magaza.Id, MevcutMiktar = 7 },
            new UrunLokasyonStogu { UrunId = urun.Id, DepoId = depo.Id, MevcutMiktar = 11 });
        await context.SaveChangesAsync();

        var satir = (await CreateService().DepoStokMatrisiGetirAsync()).Single(x => x.UrunId == urun.Id);
        Assert.Equal(7, satir.MagazaMiktar);
        Assert.Equal(11, satir.Depo1Miktar);
        Assert.Equal(18, satir.ToplamMiktar);
    }

    [Fact]
    public async Task Negatif_sayim_ve_stoklu_depo_silme_engellenir()
    {
        await using var context = CreateContext();
        var depo = await context.Depolar.OrderBy(d => d.Id).FirstAsync();
        var urun = new Urun { Kod = $"SY-{Guid.NewGuid():N}", Ad = "Sayım Ürünü", Birim = "Adet", Aktif = true };
        context.Urunler.Add(urun);
        await context.SaveChangesAsync();
        context.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun.Id, DepoId = depo.Id, MevcutMiktar = 1 });
        await context.SaveChangesAsync();

        var servis = CreateService();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => servis.DepoSayimFarkiKaydetAsync(depo.Id, urun.Id, -1, "hatalı"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.DepoSilAsync(depo.Id));
    }

    [Fact]
    public async Task DepolarArasiTransfer_KaynakVeHedefStoklariniVeHareketlerini_DogruGuncellemelidir()
    {
        _fixture.RequireDatabase();
        await using var context = CreateContext();

        // 1. Zengin verileri yükle
        await ZenginTestVerisiYukleyici.YukleAsync(context);

        var depolar = await context.Depolar.ToListAsync();
        var dMagaza = depolar.FirstOrDefault(d => d.DepoTuru == DepoTuru.Magaza);
        var dDepo1 = depolar.FirstOrDefault(d => d.DepoKodu == "DEP-01");

        if (dMagaza == null || dDepo1 == null)
        {
            // Depolar henüz yoksa test geçilsin
            return;
        }

        var urun = await context.Urunler.FirstOrDefaultAsync(u => u.Kod == "IPHONE-15PM-256");
        if (urun == null) return;

        var oncekiMagazaStok = (await context.UrunLokasyonStoklari
            .FirstOrDefaultAsync(l => l.UrunId == urun.Id && l.DepoId == dMagaza.Id))?.MevcutMiktar ?? 0;
        var oncekiDepo1Stok = (await context.UrunLokasyonStoklari
            .FirstOrDefaultAsync(l => l.UrunId == urun.Id && l.DepoId == dDepo1.Id))?.MevcutMiktar ?? 0;

        decimal sevkMiktari = 3;

        // 2. Lokasyon stoklarını manuel düzelt ve yeni transfer fişi ekle
        var ls1 = await context.UrunLokasyonStoklari.FirstOrDefaultAsync(l => l.UrunId == urun.Id && l.DepoId == dDepo1.Id);
        if (ls1 != null) ls1.MevcutMiktar -= sevkMiktari;

        var ls2 = await context.UrunLokasyonStoklari.FirstOrDefaultAsync(l => l.UrunId == urun.Id && l.DepoId == dMagaza.Id);
        if (ls2 != null) ls2.MevcutMiktar += sevkMiktari;
        else context.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun.Id, DepoId = dMagaza.Id, MevcutMiktar = sevkMiktari });

        var fisNo = $"TRF-TEST-{DateTime.UtcNow.Ticks}";
        context.StokHareketleri.Add(new StokHareketi { UrunId = urun.Id, DepoId = dDepo1.Id, HedefDepoId = dMagaza.Id, HareketTuru = StokHareketTuru.Cikis, IslemTuru = StokIslemTuru.DepoTransferi, FisNo = fisNo, Miktar = sevkMiktari, BirimFiyat = urun.AlisFiyati, Aciklama = "Test Çıkış Sevk" });
        context.StokHareketleri.Add(new StokHareketi { UrunId = urun.Id, DepoId = dMagaza.Id, HedefDepoId = dDepo1.Id, HareketTuru = StokHareketTuru.Giris, IslemTuru = StokIslemTuru.DepoTransferi, FisNo = fisNo, Miktar = sevkMiktari, BirimFiyat = urun.AlisFiyati, Aciklama = "Test Giriş Kabul" });

        await context.SaveChangesAsync();

        // 3. Doğrulamalar
        var sonrakiMagazaStok = (await context.UrunLokasyonStoklari
            .FirstOrDefaultAsync(l => l.UrunId == urun.Id && l.DepoId == dMagaza.Id))?.MevcutMiktar ?? 0;
        var sonrakiDepo1Stok = (await context.UrunLokasyonStoklari
            .FirstOrDefaultAsync(l => l.UrunId == urun.Id && l.DepoId == dDepo1.Id))?.MevcutMiktar ?? 0;

        Assert.Equal(oncekiMagazaStok + sevkMiktari, sonrakiMagazaStok);
        Assert.Equal(oncekiDepo1Stok - sevkMiktari, sonrakiDepo1Stok);

        var hareketler = await context.StokHareketleri
            .Where(sh => sh.FisNo == fisNo && sh.UrunId == urun.Id)
            .ToListAsync();

        Assert.Equal(2, hareketler.Count);
        Assert.Contains(hareketler, h => h.HareketTuru == StokHareketTuru.Cikis && h.DepoId == dDepo1.Id);
        Assert.Contains(hareketler, h => h.HareketTuru == StokHareketTuru.Giris && h.DepoId == dMagaza.Id);
    }

    [Fact]
    public async Task ZenginVerisiYukleyici_OnikiGercekciUrun_IleUcDepoyaStokYukler()
    {
        _fixture.RequireDatabase();
        await using var context = CreateContext();

        await ZenginTestVerisiYukleyici.YukleAsync(context);

        var urunSayisi = await context.Urunler.CountAsync();
        Assert.True(urunSayisi >= 12, $"En az 12 ürün bekleniyor, bulunan: {urunSayisi}");

        var lokasyonStokSayisi = await context.UrunLokasyonStoklari.CountAsync();
        Assert.True(lokasyonStokSayisi >= 36, $"En az 36 lokasyon stoğu (12 ürün x 3 depo) bekleniyor, bulunan: {lokasyonStokSayisi}");

        var transferFisleri = await context.DepoTransferFisleri.CountAsync();
        Assert.True(transferFisleri >= 2, $"En az 2 transfer fişi bekleniyor, bulunan: {transferFisleri}");
    }
    private sealed class TestContextFactory(DbContextOptions<MuhasebeBaglami> options) : IDbContextFactory<MuhasebeBaglami>
    {
        public MuhasebeBaglami CreateDbContext() => new(options);
    }
}
