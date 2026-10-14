using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class KasaBankaMuhasebeFisiTests(IsolatedPostgreSqlFixture fixture)
{
    private static async Task<decimal> HesapNetiAsync(MuhasebeBaglami db, Guid sirketId, string hesapKodu) =>
        await db.YevmiyeSatirlari
            .Where(s => s.Fis!.SirketId == sirketId && s.Hesap!.HesapKodu == hesapKodu)
            .SumAsync(s => (decimal?)(s.Borc - s.Alacak)) ?? 0;

    [Fact]
    public async Task Kasa_ve_banka_islemleri_mizanla_tutarli_fis_uretir()
    {
        fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(fixture.Options);
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Fiş Test", SirketKodu = $"FIS-{Guid.NewGuid():N}"[..12] };
        var cari = new CariKart { SirketId = sirket.Id, Kod = $"C-{Guid.NewGuid():N}"[..12], Unvan = "Fiş Cari", Aktif = true };
        var kasa = new Kasa { SirketId = sirket.Id, Kod = $"K-{Guid.NewGuid():N}"[..12], Ad = "Fiş Kasa", AktifMi = true };
        var banka = new BankaHesabi { SirketId = sirket.Id, Kod = $"B-{Guid.NewGuid():N}"[..12], Ad = "Fiş Banka", Iban = "TR330006100519786457841326", AktifMi = true };
        db.AddRange(sirket, cari, kasa, banka);
        await db.SaveChangesAsync();

        var oturum = StabilizasyonTestOturumu.Olustur();
        var kasaServisi = new KasaServisi(db, oturum);
        var bankaServisi = new BankaServisi(db, oturum);

        await kasaServisi.TahsilatEkleAsync(kasa.Id, cari.Id, 1000m, "Tahsilat");
        await kasaServisi.EkleAsync(new KasaHareketi { KasaId = kasa.Id, FisTuru = KasaFisTuru.Gider, Tutar = 200m, Aciklama = "Kırtasiye" });
        await kasaServisi.BankayaParaYatirAsync(kasa.Id, banka.Id, 300m, "Yatırma");
        await bankaServisi.EkleAsync(new BankaHareketi { BankaHesabiId = banka.Id, FisTuru = BankaFisTuru.BankaGiderMasraf, Tutar = 10m, Aciklama = "EFT masrafı" });

        // Kasa ve banka bakiyesi, mizandaki 100 ve 102 hesaplarıyla aynı olmalı.
        Assert.Equal(await kasaServisi.KasaBakiyeHesaplaAsync(kasa.Id), await HesapNetiAsync(db, sirket.Id, "100"));
        Assert.Equal(await bankaServisi.HesapBakiyeHesaplaAsync(banka.Id), await HesapNetiAsync(db, sirket.Id, "102"));
        Assert.Equal(-1000m, await HesapNetiAsync(db, sirket.Id, "120"));
        Assert.Equal(200m, await HesapNetiAsync(db, sirket.Id, "770"));
        Assert.Equal(10m, await HesapNetiAsync(db, sirket.Id, "780"));

        // Silinen hareketin fişi de kalkar; mizan yine kasa/banka bakiyesiyle tutar.
        var gider = await db.KasaHareketleri.SingleAsync(h => h.KasaId == kasa.Id && h.FisTuru == KasaFisTuru.Gider);
        await kasaServisi.SilAsync(gider.Id);
        Assert.Equal(700m, await HesapNetiAsync(db, sirket.Id, "100"));
        Assert.Equal(0m, await HesapNetiAsync(db, sirket.Id, "770"));
        Assert.Equal(await kasaServisi.KasaBakiyeHesaplaAsync(kasa.Id), await HesapNetiAsync(db, sirket.Id, "100"));

        // Her fiş borç = alacak dengesinde olmalı.
        var dengesiz = await db.YevmiyeFisleri.Where(f => f.SirketId == sirket.Id)
            .AnyAsync(f => f.Satirlar.Sum(s => s.Borc) != f.Satirlar.Sum(s => s.Alacak));
        Assert.False(dengesiz);
    }
}
