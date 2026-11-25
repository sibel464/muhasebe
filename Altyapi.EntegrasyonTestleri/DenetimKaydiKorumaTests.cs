using Altyapi.Veri;
using EtkiAlani.Modeller.Guvenlik;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Denetim kaydı veritabanı düzeyinde korunur: silinemez, boşaltılamaz, kimlik alanları değiştirilemez.
[Collection("PostgreSQL integration")]
public sealed class DenetimKaydiKorumaTests(IsolatedPostgreSqlFixture fixture)
{
    private async Task<int> KayitEkleAsync()
    {
        await using var baglam = new MuhasebeBaglami(fixture.Options);
        await baglam.Database.MigrateAsync();
        var log = new AuditLog
        {
            TabloAdi = "Deneme", KayitId = Guid.NewGuid().ToString("N"), KullaniciAdi = "denetci",
            EskiDegerJson = "{\"Ad\":\"Eski\"}", YeniDegerJson = "{\"Ad\":\"Yeni\"}"
        };
        baglam.AuditLoglar.Add(log);
        await baglam.SaveChangesAsync();
        return log.Id;
    }

    private static void KorumaHatasi(Exception ex)
    {
        var pg = ex as PostgresException ?? ex.InnerException as PostgresException ?? ex.InnerException?.InnerException as PostgresException;
        Assert.NotNull(pg);
        Assert.Contains("Denetim kaydı", pg!.MessageText);
    }

    [Fact]
    public async Task Denetim_kaydi_silinemez_ve_bosaltilamaz()
    {
        fixture.RequireDatabase();
        var id = await KayitEkleAsync();

        await using var baglam = new MuhasebeBaglami(fixture.Options);
        KorumaHatasi(await Assert.ThrowsAnyAsync<Exception>(() => baglam.AuditLoglar.Where(l => l.Id == id).ExecuteDeleteAsync()));
        KorumaHatasi(await Assert.ThrowsAnyAsync<Exception>(() => baglam.Database.ExecuteSqlRawAsync("TRUNCATE \"AuditLoglar\"")));
        Assert.True(await baglam.AuditLoglar.AnyAsync(l => l.Id == id));
    }

    [Fact]
    public async Task Denetim_kaydinin_kim_ne_zaman_bilgisi_degistirilemez_kvkk_maskelemesi_yapilabilir()
    {
        fixture.RequireDatabase();
        var id = await KayitEkleAsync();

        await using var baglam = new MuhasebeBaglami(fixture.Options);
        KorumaHatasi(await Assert.ThrowsAnyAsync<Exception>(() => baglam.AuditLoglar.Where(l => l.Id == id)
            .ExecuteUpdateAsync(g => g.SetProperty(l => l.KullaniciAdi, "baskasi"))));
        KorumaHatasi(await Assert.ThrowsAnyAsync<Exception>(() => baglam.AuditLoglar.Where(l => l.Id == id)
            .ExecuteUpdateAsync(g => g.SetProperty(l => l.Tarih, DateTime.UtcNow.AddYears(-3)))));

        // KVKK: kişisel değerler maskelenebilir.
        await baglam.AuditLoglar.Where(l => l.Id == id)
            .ExecuteUpdateAsync(g => g.SetProperty(l => l.EskiDegerJson, "{\"Ad\":\"***\"}").SetProperty(l => l.YeniDegerJson, "{\"Ad\":\"***\"}"));
        var log = await baglam.AuditLoglar.AsNoTracking().SingleAsync(l => l.Id == id);
        Assert.Equal("denetci", log.KullaniciAdi);
        Assert.Contains("***", log.EskiDegerJson);
    }
}
