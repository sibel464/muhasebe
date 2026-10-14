using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Ayar;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public class YetkiSiniriTests
{
    [Fact]
    public async Task Kullanici_yonetimi_yetkisiz_oturumu_veritabanina_gitmeden_reddeder()
    {
        var servis = new KullaniciServisi(new KullanilmamasiGerekenBaglamFabrikasi());

        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.TumunuGetirAsync());
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.GetirAsync(1));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.EkleAsync(new Kullanici(), "parola"));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.GuncelleAsync(new Kullanici()));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.SifreDegistirAsync(1, "parola"));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.SilAsync(1));
    }

    [Fact]
    public async Task Fatura_duzenleme_ve_silme_yetkisiz_oturumu_veritabanina_gitmeden_reddeder()
    {
        await using var baglam = new MuhasebeBaglami(new DbContextOptionsBuilder<MuhasebeBaglami>().Options);
        var servis = new FaturaServisi(baglam);

        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.GuncelleAsync(new Fatura()));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.SilAsync(1));
    }

    [Fact]
    public async Task Firma_ayar_ve_denetim_servisleri_yetkisiz_erisimleri_reddeder()
    {
        await using var baglam = new MuhasebeBaglami(new DbContextOptionsBuilder<MuhasebeBaglami>().Options);
        var kullanici = new Cari360TestKullanici { AktifYetkiler = new KullaniciYetkileri() };
        var firmaServisi = new SirketServisi(baglam, kullanici);
        var ayarServisi = new SistemAyarlariServisi(baglam, kullanici);
        var denetimServisi = new AuditLogServisi(baglam, kullanici);

        await Assert.ThrowsAsync<YetkisizErisimException>(() => firmaServisi.KaydetAsync(new Sirket(), false));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => firmaServisi.SonrakiBelgeNoAsync(1));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => ayarServisi.AyarlariKaydetAsync(new SistemAyarlari()));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => denetimServisi.LoglariGetirAsync());
    }

    private sealed class KullanilmamasiGerekenBaglamFabrikasi : IDbContextFactory<MuhasebeBaglami>
    {
        public MuhasebeBaglami CreateDbContext() => throw new InvalidOperationException("Yetki kontrolü atlandı.");
    }
}
