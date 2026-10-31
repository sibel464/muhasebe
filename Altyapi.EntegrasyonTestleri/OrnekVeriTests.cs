using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kullanici;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Tanıtım veritabanı: boş kurulumda programın servisleriyle 3 aylık örnek veri üretilir; muhasebe dengede kalır,
// dolu veritabanına ikinci kez yazılmaz, tanıtım veritabanı dışında çalışmaz.
public sealed class OrnekVeriTests : IAsyncLifetime
{
    private readonly IsolatedPostgreSqlFixture _db = new();
    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Ornek_veri_dengeli_uretilir_ve_bir_kez_yazilir()
    {
        _db.RequireDatabase();
        var y = new KullaniciYetkileri();
        foreach (var p in typeof(KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            p.SetValue(y, true);
        var oturum = new Cari360TestKullanici { AktifYetkiler = y };
        MuhasebeBaglami Uret() => new(_db.Options);

        // Gerçek veritabanı adında değilse (ad denetimi açıkken) yazmaz.
        await Assert.ThrowsAsync<InvalidOperationException>(() => OrnekVeriServisi.GerekirseDoldurAsync(Uret, oturum));

        Assert.True(await OrnekVeriServisi.GerekirseDoldurAsync(Uret, oturum, adDenetimi: false));
        Assert.False(await OrnekVeriServisi.GerekirseDoldurAsync(Uret, oturum, adDenetimi: false));

        await using var c = Uret();
        Assert.Equal(11, await c.CariKartlar.CountAsync());
        Assert.Equal(12, await c.Urunler.CountAsync());
        Assert.True(await c.Faturalar.CountAsync(f => f.Tur == FaturaTuru.Satis && f.Durum == BelgeDurumu.Onaylandi) > 40);
        Assert.Equal(9, await c.Faturalar.CountAsync(f => f.Tur == FaturaTuru.Alis && f.Durum == BelgeDurumu.Onaylandi));
        Assert.All(await c.Urunler.ToListAsync(), u => Assert.True(u.StokMiktari >= 0, u.Ad));
        Assert.True((await c.CariKartlar.ToListAsync()).Any(x => x.Bakiye > 0), "açık alacak olmalı");

        var rapor = new MuhasebeRaporServisi(c, oturum);
        Assert.True((await rapor.MizanAlAsync(MuhasebeBaglami.VarsayilanSirketId, null, null)).DengedeMi);
        Assert.Empty(await rapor.GetirMuhasebelesmemisBelgelerAsync(MuhasebeBaglami.VarsayilanSirketId));
        Assert.True((await rapor.SaglikPaneliGetirAsync(MuhasebeBaglami.VarsayilanSirketId)).SaglikliMi);
    }
}
