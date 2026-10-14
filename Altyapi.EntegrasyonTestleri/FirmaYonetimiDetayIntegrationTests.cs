using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kurumsal;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class FirmaYonetimiDetayIntegrationTests
{
    private static SirketServisi Servis(MuhasebeBaglami db) => new(db, new Cari360TestKullanici { AktifYetkiler = new() { AyarlariYonet = true } });
    private readonly IsolatedPostgreSqlFixture _fixture;
    public FirmaYonetimiDetayIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Sube_kodu_normalize_edilir_ve_ayni_firmada_tekrarlanamaz()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = Servis(db);
        var firma = await servis.KaydetAsync(YeniFirma("SB"), false);
        var sube = await servis.SubeKaydetAsync(new Sube { SirketId = firma.Id, SubeKodu = " merkez ", SubeAdi = " Merkez Şube " });
        Assert.Equal("MERKEZ", sube.SubeKodu);
        Assert.Equal("Merkez Şube", sube.SubeAdi);
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.SubeKaydetAsync(
            new Sube { SirketId = firma.Id, SubeKodu = "MERKEZ", SubeAdi = "Tekrar" }));
    }

    [Fact]
    public async Task Mali_donemler_cakisamaz_ve_kilitli_donemin_tarihleri_degistirilemez()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = Servis(db);
        var firma = await servis.KaydetAsync(YeniFirma("MD"), false);
        var donem = await servis.MaliDonemKaydetAsync(new MaliDonem
        {
            SirketId = firma.Id, DonemAdi = "2026", Baslangic = new DateTime(2026, 1, 1), Bitis = new DateTime(2026, 12, 31)
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.MaliDonemKaydetAsync(new MaliDonem
        {
            SirketId = firma.Id, DonemAdi = "Çakışan", Baslangic = new DateTime(2026, 6, 1), Bitis = new DateTime(2027, 5, 31)
        }));
        await servis.MaliDonemKilitleAsync(donem.Id, true);
        donem.Baslangic = new DateTime(2025, 1, 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.MaliDonemKaydetAsync(donem));
    }

    [Fact]
    public async Task Belge_serisi_eszamanli_benzersiz_numara_uretir()
    {
        int seriId;
        await using (var db = new MuhasebeBaglami(_fixture.Options))
        {
            var servis = Servis(db);
            var firma = await servis.KaydetAsync(YeniFirma("BS"), false);
            var seri = await servis.BelgeSerisiKaydetAsync(new BelgeSerisi
            {
                SirketId = firma.Id, BelgeTipi = "Fatura", Seri = "A", Prefix = "FTR-", Format = "FTR-{0:000000}"
            });
            seriId = seri.Id;
        }
        await using var db1 = new MuhasebeBaglami(_fixture.Options);
        await using var db2 = new MuhasebeBaglami(_fixture.Options);
        var numaralar = await Task.WhenAll(
            Servis(db1).SonrakiBelgeNoAsync(seriId),
            Servis(db2).SonrakiBelgeNoAsync(seriId));
        Assert.Equal(2, numaralar.Distinct().Count());
        Assert.All(numaralar, x => Assert.Matches("^FTR-[0-9]{6}$", x));
    }

    [Fact]
    public async Task Aktif_subesi_bulunan_firma_pasife_alinamaz()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = Servis(db);
        var firma = await servis.KaydetAsync(YeniFirma("PS"), false);
        await servis.SubeKaydetAsync(new Sube { SirketId = firma.Id, SubeKodu = "S1", SubeAdi = "Aktif Şube" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.DurumDegistirAsync(firma.Id, false));
    }

    [Fact]
    public async Task Vergi_numarasi_rakam_ve_eposta_format_kontrolunden_gecer()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = Servis(db);
        var firma = YeniFirma("VN"); firma.VergiNo = "ABCDEFGHIJ";
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.KaydetAsync(firma, false));
        firma = YeniFirma("EM"); firma.Email = "gecersiz";
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.KaydetAsync(firma, false));
    }

    private static Sirket YeniFirma(string onEk) => new()
    {
        SirketKodu = $"{onEk}-{Guid.NewGuid():N}", Unvan = "Detaylı Firma Testi", AktifMi = true
    };
}
