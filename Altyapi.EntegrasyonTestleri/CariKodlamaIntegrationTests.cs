using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class CariKodlamaIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public CariKodlamaIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Manuel_kod_buyuk_harfe_grup_kodu_ise_tr_TR_ile_normalize_edilir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var kod = "test-" + Guid.NewGuid().ToString("N")[..8];
        var cari = await new CariServisi(db, StabilizasyonTestOturumu.Olustur()).EkleAsync(YeniCari(kod, " perakende "));
        Assert.Equal(kod.ToUpperInvariant(), cari.Kod);
        Assert.Equal("PERAKENDE", cari.GrupKodu);
        Assert.Equal("Ada", cari.Ad); Assert.Equal("Lovelace", cari.Soyad);
    }

    [Fact]
    public async Task Manuel_kod_buyuk_kucuk_harf_farkiyla_tekrarlanamaz()
    {
        var kod = "dup-" + Guid.NewGuid().ToString("N")[..8];
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new CariServisi(db, StabilizasyonTestOturumu.Olustur());
        await servis.EkleAsync(YeniCari(kod, "VIP"));
        var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => servis.EkleAsync(YeniCari(kod.ToUpperInvariant(), "VIP")));
        Assert.Contains("zaten", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Eszamanli_otomatik_kayitlar_benzersiz_kod_alir()
    {
        await using var db1 = new MuhasebeBaglami(_fixture.Options);
        await using var db2 = new MuhasebeBaglami(_fixture.Options);
        var sonuc = await Task.WhenAll(new CariServisi(db1, StabilizasyonTestOturumu.Olustur()).OtomatikKodlaEkleAsync(YeniCari("", ""), true), new CariServisi(db2, StabilizasyonTestOturumu.Olustur()).OtomatikKodlaEkleAsync(YeniCari("", ""), true));
        Assert.NotEqual(sonuc[0].Kod, sonuc[1].Kod);
        Assert.NotEqual(sonuc[0].GrupKodu, sonuc[1].GrupKodu);
        Assert.All(sonuc, x => Assert.Matches("^C-[0-9]{6}$", x.Kod));
        Assert.All(sonuc, x => Assert.Matches("^GRP-[0-9]{6}$", x.GrupKodu!));
    }

    private static CariKart YeniCari(string kod, string grup) => new()
    { Kod = kod, GrupKodu = grup, CariTipiId = CariTipiTanimi.KurumsalId, Tur = CariTuru.Musteri, Unvan = "Test Cari", Ad = "Ada", Soyad = "Lovelace", Aktif = true };
}
