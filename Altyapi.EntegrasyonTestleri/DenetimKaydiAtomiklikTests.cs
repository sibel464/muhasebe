using Altyapi.Veri;
using EtkiAlani.Modeller.Kullanici;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class DenetimKaydiAtomiklikTests(IsolatedPostgreSqlFixture fixture)
{
    [Fact]
    public async Task Denetim_kaydi_yazilamazsa_ana_kayit_geri_alinir()
    {
        fixture.RequireDatabase();
        var kullaniciAdi = "audit_atomic_" + Guid.NewGuid().ToString("N");
        var kesici = new IkinciKaydiBozanKesici();
        var secenekler = new DbContextOptionsBuilder<MuhasebeBaglami>(fixture.Options)
            .AddInterceptors(kesici)
            .Options;

        await using (var baglam = new MuhasebeBaglami(secenekler))
        {
            baglam.Kullanicilar.Add(new Kullanici
            {
                KullaniciAdi = kullaniciAdi,
                SifreHash = "test-hash",
                Ad = "Denetim",
                Soyad = "Test"
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() => baglam.SaveChangesAsync());
        }

        Assert.Equal(2, kesici.CagriSayisi);
        await using var kontrolBaglami = new MuhasebeBaglami(fixture.Options);
        Assert.False(await kontrolBaglami.Kullanicilar.IgnoreQueryFilters()
            .AnyAsync(x => x.KullaniciAdi == kullaniciAdi));
    }

    private sealed class IkinciKaydiBozanKesici : SaveChangesInterceptor
    {
        public int CagriSayisi { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (++CagriSayisi == 2)
                throw new InvalidOperationException("Denetim kaydı yazımı test için engellendi.");

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
