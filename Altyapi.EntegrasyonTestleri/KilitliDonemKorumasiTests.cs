using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class KilitliDonemKorumasiTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public KilitliDonemKorumasiTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private static readonly DateTime KilitliGun = new(2001, 6, 15, 10, 0, 0, DateTimeKind.Utc);

    private async Task<(Guid SirketId, int CariId)> HazirlaAsync()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Kilit Test", SirketKodu = $"KT-{Guid.NewGuid():N}"[..10] };
        db.Sirketler.Add(sirket);
        var cari = new CariKart { SirketId = sirket.Id, Kod = $"KT-{Guid.NewGuid():N}", Unvan = "Kilit Cari", Aktif = true };
        db.CariKartlar.Add(cari);
        await db.SaveChangesAsync();
        // Dönem kilitlenmeden önce o döneme ait bir hareket vardır.
        db.CariHareketler.Add(new CariHareket { SirketId = sirket.Id, CariKartId = cari.Id, Tarih = KilitliGun, Borc = 100, BelgeNo = "ESKI", Aciklama = "Eski fatura" });
        db.MaliDonemler.Add(new MaliDonem { SirketId = sirket.Id, DonemAdi = "2001", Baslangic = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc), Bitis = new DateTime(2001, 12, 31, 23, 59, 59, DateTimeKind.Utc) });
        await db.SaveChangesAsync();
        var donem = await db.MaliDonemler.SingleAsync(d => d.SirketId == sirket.Id);
        donem.KilitliMi = true;
        await db.SaveChangesAsync();
        return (sirket.Id, cari.Id);
    }

    [Fact]
    public async Task Kilitli_doneme_hareket_ve_fis_eklenemez_silinemez()
    {
        var (sirketId, cariId) = await HazirlaAsync();

        await using (var db = new MuhasebeBaglami(_fixture.Options))
        {
            db.CariHareketler.Add(new CariHareket { SirketId = sirketId, CariKartId = cariId, Tarih = KilitliGun, Borc = 5, Aciklama = "Geç kayıt" });
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            Assert.Contains("kilitli", ex.Message);
        }
        await using (var db = new MuhasebeBaglami(_fixture.Options))
        {
            db.YevmiyeFisleri.Add(new YevmiyeFis { SirketId = sirketId, FisNo = $"KT-{Guid.NewGuid():N}"[..15], Tarih = KilitliGun, Durum = YevmiyeDurumu.Onaylandi, Aciklama = "Geç fiş" });
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using (var db = new MuhasebeBaglami(_fixture.Options))
        {
            var eski = await db.CariHareketler.SingleAsync(h => h.CariKartId == cariId && h.BelgeNo == "ESKI");
            eski.Silindi = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Bugunku_tahsilat_kilitli_donemdeki_faturayi_kapatabilir()
    {
        var (sirketId, cariId) = await HazirlaAsync();

        await using var db = new MuhasebeBaglami(_fixture.Options);
        var eski = await db.CariHareketler.SingleAsync(h => h.CariKartId == cariId && h.BelgeNo == "ESKI");
        eski.KapaliTutar = 100;
        db.CariHareketler.Add(new CariHareket { SirketId = sirketId, CariKartId = cariId, Tarih = DateTime.UtcNow, Alacak = 100, Aciklama = "Tahsilat" });
        await db.SaveChangesAsync();

        Assert.Equal(100, (await db.CariHareketler.AsNoTracking().SingleAsync(h => h.Id == eski.Id)).KapaliTutar);
    }
}
