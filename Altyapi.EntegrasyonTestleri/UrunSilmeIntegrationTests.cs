using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class UrunSilmeIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public UrunSilmeIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Stogu_olan_urun_silinemez_neden_zorunludur()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var depoId = await db.Depolar.Where(d => d.AktifMi).OrderBy(d => d.Id).Select(d => d.Id).FirstAsync();
        var servis = new UrunServisi(db, SilmeYetkiliOturum());
        var urun = YeniUrun();
        urun.LokasyonStoklari.Add(new UrunLokasyonStogu { DepoId = depoId, MevcutMiktar = 2 });
        await servis.EkleAsync(urun);

        await Assert.ThrowsAsync<ArgumentException>(() => servis.SilAsync(urun.Id, " "));
        var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => servis.SilAsync(urun.Id, "Artık satılmıyor"));
        Assert.Contains("stoğu var", hata.Message);

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        Assert.False((await kontrol.Urunler.SingleAsync(x => x.Id == urun.Id)).Silindi);
    }

    [Fact]
    public async Task Silinen_urun_listelenir_kodu_yeniden_kullanilir_ve_cakisma_varsa_geri_alinamaz()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new UrunServisi(db, SilmeYetkiliOturum());
        var eski = YeniUrun();
        await servis.EkleAsync(eski);

        Assert.True(await servis.SilAsync(eski.Id, "Yanlış açılmış kart"));
        var silinen = Assert.Single(await servis.SilinenleriGetirAsync(), x => x.Id == eski.Id);
        Assert.StartsWith("Yanlış açılmış kart", silinen.SilinmeNedeni);
        Assert.Null(await servis.IdIleGetirAsync(eski.Id));

        // Silinen ürünün kodu yeni bir ürüne verilebilir...
        var yeni = YeniUrun();
        yeni.Kod = eski.Kod;
        await servis.EkleAsync(yeni);

        // ...bu durumda eski ürün geri alınamaz.
        var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => servis.GeriAlAsync(eski.Id));
        Assert.Contains("kullanılıyor", hata.Message);
    }

    [Fact]
    public async Task Silinen_urun_geri_alinabilir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new UrunServisi(db, SilmeYetkiliOturum());
        var urun = YeniUrun();
        await servis.EkleAsync(urun);
        await servis.SilAsync(urun.Id, "Deneme silme");

        await servis.GeriAlAsync(urun.Id);

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        var kayit = await kontrol.Urunler.SingleAsync(x => x.Id == urun.Id);
        Assert.False(kayit.Silindi);
        Assert.True(kayit.Aktif);
        Assert.Null(kayit.SilinmeNedeni);
    }

    [Fact]
    public async Task Silinen_urunun_stok_hareketleri_gecmiste_kalir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var urun = YeniUrun();
        db.Urunler.Add(urun);
        await db.SaveChangesAsync();
        db.StokHareketleri.Add(new StokHareketi { UrunId = urun.Id, HareketTuru = StokHareketTuru.Giris, Miktar = 1, Aciklama = "test" });
        urun.Silindi = true;
        await db.SaveChangesAsync();

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        // Varsayılan sorgu ürünü birlikte yüklerken silinen ürünün hareketini tamamen kaybeder;
        // geçmişi gösteren sorgular bu yüzden adlı filtreyi kaldırır.
        Assert.Empty(await kontrol.StokHareketleri.Include(h => h.Urun).Where(h => h.UrunId == urun.Id).ToListAsync());
        var hareket = await kontrol.StokHareketleri.IgnoreQueryFilters([MuhasebeBaglami.SilinmisUrunFiltresi])
            .Include(h => h.Urun).SingleAsync(h => h.UrunId == urun.Id);
        Assert.Equal(urun.Ad, hareket.Urun!.Ad);
    }

    private static Uygulama.Arayuzler.IKullaniciServisi SilmeYetkiliOturum()
    {
        var oturum = (Cari360TestKullanici)StabilizasyonTestOturumu.Olustur();
        oturum.AktifYetkiler!.StokSil = true;
        return oturum;
    }

    private static Urun YeniUrun() => new()
    {
        Kod = "SL-" + Guid.NewGuid().ToString("N")[..10],
        Ad = "Silme Test Ürünü",
        Birim = "Adet",
        KdvOrani = 20,
        Aktif = true
    };
}
