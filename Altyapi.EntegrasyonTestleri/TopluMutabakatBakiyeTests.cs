using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Toplu mutabakat, her carinin seçilen günün sonundaki bakiyesini kullanır: o günün hareketleri dahil, sonrakiler hariç.
[Collection("PostgreSQL integration")]
public sealed class TopluMutabakatBakiyeTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public TopluMutabakatBakiyeTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Tarihteki_bakiye_o_gunu_dahil_eder_sonrakileri_haric_tutar()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var oturum = StabilizasyonTestOturumu.Olustur();
        var cariServisi = new CariServisi(db, oturum);
        var musteri = await cariServisi.EkleAsync(YeniCari(CariTuru.Musteri));
        var tedarikci = await cariServisi.EkleAsync(YeniCari(CariTuru.Tedarikci));
        var servis = new CariHareketServisi(db, oturum);

        var gun = new DateTime(2026, 6, 30);
        await servis.EkleAsync(Hareket(musteri.Id, gun.AddDays(-10), borc: 1000), "600");
        await servis.EkleAsync(Hareket(musteri.Id, gun.AddHours(18), alacak: 300), "600");
        await servis.EkleAsync(Hareket(musteri.Id, gun.AddDays(1), borc: 5000), "600");
        await servis.EkleAsync(Hareket(tedarikci.Id, gun.AddDays(-1), alacak: 750), "153");

        var bakiyeler = await servis.TarihtekiBakiyelerAsync(gun);

        Assert.Equal(700m, bakiyeler[musteri.Id]);
        Assert.Equal(-750m, bakiyeler[tedarikci.Id]);
        Assert.False((await servis.TarihtekiBakiyelerAsync(gun.AddDays(-20))).ContainsKey(musteri.Id));
        Assert.Equal(5700m, (await servis.TarihtekiBakiyelerAsync(gun.AddDays(1)))[musteri.Id]);
    }

    private static CariHareket Hareket(int cariId, DateTime tarih, decimal borc = 0, decimal alacak = 0) => new()
    {
        CariKartId = cariId, Tarih = DateTime.SpecifyKind(tarih, DateTimeKind.Utc), HareketTuru = CariHareketTuru.Diger,
        BelgeNo = "TM-1", Aciklama = "Toplu mutabakat testi", Borc = borc, Alacak = alacak
    };

    private static CariKart YeniCari(CariTuru tur)
    {
        var ek = Guid.NewGuid().ToString("N")[..10];
        return new CariKart { Kod = "TM-" + ek, Unvan = "Mutabakat Test " + ek, Tur = tur };
    }
}
