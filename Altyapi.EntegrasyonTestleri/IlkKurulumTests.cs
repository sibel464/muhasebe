using Altyapi.Veri;
using EtkiAlani.Modeller.Tanimlar;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Yeni kurulan boş veritabanı, müşteri hiçbir tanım yapmadan ilk işlemlerine başlayabilecek durumda olmalı.
[Collection("PostgreSQL integration")]
public sealed class IlkKurulumTests(IsolatedPostgreSqlFixture fixture)
{
    [Fact]
    public async Task Yeni_kurulumda_standart_olcu_birimleri_hazir_gelir()
    {
        fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(fixture.Options);
        var birimler = await db.MerkeziTanimlar.Where(x => x.Grup == TanimGruplari.Birim && x.AktifMi).ToListAsync();
        Assert.Contains(birimler, x => x.Kod == "ADET");
        Assert.Contains(birimler, x => x.Kod == "KG");
        Assert.True(birimler.Count >= 10);
        // Her birimin e-Fatura birim kodu tanınır (tanınmayan birim sessizce "adet" sayılırdı).
        Assert.All(birimler.Where(x => x.Kod != "ADET"), x => Assert.NotEqual("C62", Altyapi.EBelge.UblTrFaturaUretici.BirimKodu(x.KisaAd ?? x.Ad)));
    }
}
