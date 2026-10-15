using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class CariSilmeIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public CariSilmeIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Islem_gormus_cari_silinemez_neden_zorunludur()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new CariServisi(db, SilmeYetkiliOturum());
        var cari = await servis.EkleAsync(YeniCari());

        await Assert.ThrowsAsync<ArgumentException>(() => servis.SilAsync(cari.Id, ""));

        db.CariHareketler.Add(new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow, HareketTuru = CariHareketTuru.Devir, Borc = 10, Aciklama = "test" });
        await db.SaveChangesAsync();
        var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => servis.SilAsync(cari.Id, "Artık çalışmıyoruz"));
        Assert.Contains("pasif", hata.Message);
    }

    [Fact]
    public async Task Silinen_cari_listelenir_kodu_yeniden_kullanilir_ve_cakismada_geri_alinamaz()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new CariServisi(db, SilmeYetkiliOturum());
        var eski = await servis.EkleAsync(YeniCari());

        Assert.True(await servis.SilAsync(eski.Id, "Yanlış açılmış"));
        Assert.Contains(await servis.SilinenleriGetirAsync(), x => x.Id == eski.Id);

        var yeni = YeniCari();
        yeni.Kod = eski.Kod;
        await servis.EkleAsync(yeni);

        var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => servis.GeriAlAsync(eski.Id));
        Assert.Contains("kullanılıyor", hata.Message);
    }

    [Fact]
    public async Task Silinen_cari_geri_alinabilir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var servis = new CariServisi(db, SilmeYetkiliOturum());
        var cari = await servis.EkleAsync(YeniCari());
        await servis.SilAsync(cari.Id, "Deneme silme");

        await servis.GeriAlAsync(cari.Id);

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        Assert.NotNull(await new CariServisi(kontrol, SilmeYetkiliOturum()).IdIleGetirAsync(cari.Id));
    }

    private static Uygulama.Arayuzler.IKullaniciServisi SilmeYetkiliOturum()
    {
        var oturum = (Cari360TestKullanici)StabilizasyonTestOturumu.Olustur();
        oturum.AktifYetkiler!.CariSil = true;
        return oturum;
    }

    private static CariKart YeniCari()
    {
        var ek = Guid.NewGuid().ToString("N")[..10];
        return new CariKart { Kod = "CS-" + ek, Unvan = "Silme Test Carisi " + ek, Tur = CariTuru.Musteri };
    }
}
