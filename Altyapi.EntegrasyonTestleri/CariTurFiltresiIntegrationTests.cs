using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// "Müşteri ve Tedarikçi" türündeki cari hem müşteri hem tedarikçi listelerinde (ve fatura cari seçiminde) görünmeli.
[Collection("PostgreSQL integration")]
public sealed class CariTurFiltresiIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public CariTurFiltresiIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Musteri_ve_tedarikci_turu_her_iki_listede_gorunur()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new CariServisi(db, StabilizasyonTestOturumu.Olustur());
        var ek = Guid.NewGuid().ToString("N")[..8];
        var musteri = await servis.EkleAsync(new CariKart { Kod = "TM-" + ek, Unvan = "Tür Müşteri " + ek, Tur = CariTuru.Musteri });
        var tedarikci = await servis.EkleAsync(new CariKart { Kod = "TT-" + ek, Unvan = "Tür Tedarikçi " + ek, Tur = CariTuru.Tedarikci });
        var ikisi = await servis.EkleAsync(new CariKart { Kod = "TI-" + ek, Unvan = "Tür İkisi " + ek, Tur = CariTuru.MusteriTedarikci });

        var musteriler = (await servis.FiltreliAraAsync(new CariFiltre { Tur = CariTuru.Musteri, AramaMetni = ek })).Select(x => x.Id).ToList();
        Assert.Contains(musteri.Id, musteriler);
        Assert.Contains(ikisi.Id, musteriler);
        Assert.DoesNotContain(tedarikci.Id, musteriler);

        var tedarikciler = (await servis.TumunuGetirAsync(CariTuru.Tedarikci)).Select(x => x.Id).ToList();
        Assert.Contains(tedarikci.Id, tedarikciler);
        Assert.Contains(ikisi.Id, tedarikciler);
        Assert.DoesNotContain(musteri.Id, tedarikciler);

        var hepsi = (await servis.FiltreliAraAsync(new CariFiltre { AramaMetni = ek })).Select(x => x.Id).ToList();
        Assert.Equal(3, hepsi.Count);
    }
}
