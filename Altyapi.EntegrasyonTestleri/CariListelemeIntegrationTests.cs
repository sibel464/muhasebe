using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class CariListelemeIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public CariListelemeIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Musteri_filtresi_tedarikciyi_listelemez_ve_bakiye_yonleri_dogrudur()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var benzersiz = Guid.NewGuid().ToString("N")[..8];
        db.CariKartlar.AddRange(
            new CariKart { Kod = $"M-{benzersiz}", Unvan = $"Borçlu {benzersiz}", Tur = CariTuru.Musteri, Borc = 100, Alacak = 25 },
            new CariKart { Kod = $"T-{benzersiz}", Unvan = $"Tedarikçi {benzersiz}", Tur = CariTuru.Tedarikci, Borc = 100, Alacak = 0 });
        await db.SaveChangesAsync();

        var sonuc = await new CariServisi(db).FiltreliAraAsync(new CariFiltre
        {
            Tur = CariTuru.Musteri,
            SadeceBorclular = true,
            AramaMetni = benzersiz
        });

        var cari = Assert.Single(sonuc);
        Assert.Equal(CariTuru.Musteri, cari.Tur);
        Assert.Equal(75, cari.Bakiye);
    }

    [Fact]
    public async Task Kod_ve_grup_kodu_filtreleri_birlikte_uygulanir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var benzersiz = Guid.NewGuid().ToString("N")[..8];
        db.CariKartlar.Add(new CariKart
        {
            Kod = $"VIP-{benzersiz}", Unvan = "Filtre Test", GrupKodu = $"GRUP-{benzersiz}", Tur = CariTuru.Musteri
        });
        await db.SaveChangesAsync();

        var sonuc = await new CariServisi(db).FiltreliAraAsync(new CariFiltre
        {
            Kod = $"vip-{benzersiz}", GrupKodu = $"grup-{benzersiz}"
        });

        Assert.Single(sonuc);
    }
}
