using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Yardimcilar;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class CariKartAlanlariIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public CariKartAlanlariIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Risk_vade_ve_iban_kaydedilir_ve_guncellenir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new CariServisi(db, StabilizasyonTestOturumu.Olustur());
        var ek = Guid.NewGuid().ToString("N")[..8];
        var cari = await servis.EkleAsync(new CariKart
        {
            Kod = "RV-" + ek, Unvan = "Risk Vade " + ek,
            RiskLimiti = 50_000, VadeGunu = 30, Iban = "tr33 0006 1005 1978 6457 8413 26"
        });

        cari.RiskLimiti = 75_000;
        cari.VadeGunu = 45;
        await servis.GuncelleAsync(cari);

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        var kayit = await kontrol.CariKartlar.SingleAsync(x => x.Id == cari.Id);
        Assert.Equal(75_000m, kayit.RiskLimiti);
        Assert.Equal(45, kayit.VadeGunu);
        Assert.Equal("TR330006100519786457841326", kayit.Iban);
    }

    [Fact]
    public async Task Gecersiz_iban_ve_negatif_risk_reddedilir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new CariServisi(db, StabilizasyonTestOturumu.Olustur());
        var ek = Guid.NewGuid().ToString("N")[..8];
        await Assert.ThrowsAsync<ArgumentException>(() => servis.EkleAsync(new CariKart { Kod = "IB-" + ek, Unvan = "x", Iban = "TR000000000000000000000000" }));
        await Assert.ThrowsAsync<ArgumentException>(() => servis.EkleAsync(new CariKart { Kod = "RK-" + ek, Unvan = "x", RiskLimiti = -1 }));
    }

    [Theory]
    [InlineData("10000000146", true)]
    [InlineData("10000000147", false)]
    [InlineData("01234567890", false)]
    public void Tc_kimlik_sagalama_kontrolu(string tckn, bool gecerli) => Assert.Equal(gecerli, KimlikNoDogrulayici.TcKimlikNoGecerliMi(tckn));

    [Theory]
    [InlineData("1234567890", true)]
    [InlineData("1234567891", false)]
    [InlineData("123456789", false)]
    public void Vergi_no_saglama_kontrolu(string vkn, bool gecerli) => Assert.Equal(gecerli, KimlikNoDogrulayici.VergiNoGecerliMi(vkn));
}
