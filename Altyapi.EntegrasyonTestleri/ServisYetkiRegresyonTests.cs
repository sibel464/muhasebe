using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Siparis;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Tanimlar;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class ServisYetkiRegresyonTests
{
    private static MuhasebeBaglami BosBaglam() => new(new DbContextOptionsBuilder<MuhasebeBaglami>().Options);

    [Fact]
    public async Task Siparis_yazma_islemleri_oturum_olmadiginda_reddedilir()
    {
        await using var baglam = BosBaglam();
        var servis = new SiparisServisi(baglam, new Cari360TestKullanici { AktifKullanici = null });

        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.EkleAsync(new Siparis(), true));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.GuncelleAsync(new Siparis()));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.IptalEtAsync(1, 1));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.DurumDegistirAsync(1, SiparisDurumu.Onaylandi, 1));
    }

    [Fact]
    public async Task Siparis_yazma_islemleri_ayri_yetkilerle_korunur()
    {
        await using var baglam = BosBaglam();
        var kullanici = new Cari360TestKullanici { AktifYetkiler = new KullaniciYetkileri { FaturaOlustur = true, FaturaOnayla = false } };
        var servis = new SiparisServisi(baglam, kullanici);

        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.IptalEtAsync(1, 1));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.DurumDegistirAsync(1, SiparisDurumu.Onaylandi, 1));
        kullanici.AktifYetkiler.FaturaOlustur = false;
        kullanici.AktifYetkiler.FaturaIptal = true;
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.EkleAsync(new Siparis(), true));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.GuncelleAsync(new Siparis()));
    }

    [Fact]
    public async Task Depo_yazma_islemleri_yetkisiz_iken_baglanti_acmadan_reddedilir()
    {
        var servis = new DepoServisi(new BosFactory(), new Cari360TestKullanici { AktifKullanici = null });

        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.DepoEkleAsync(new Depo()));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.DepoGuncelleAsync(new Depo()));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.DepoSilAsync(1));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.TransferFisiKaydetAsync(new DepoTransferFisi()));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.DepoSayimFarkiKaydetAsync(1, 1, 1, "test"));
    }

    [Fact]
    public async Task Depo_yazma_islemleri_ayri_yetkilerle_korunur()
    {
        var kullanici = new Cari360TestKullanici { AktifYetkiler = new KullaniciYetkileri { AyarlariYonet = true, DepoTransferYap = false, StokDuzenle = false } };
        var servis = new DepoServisi(new BosFactory(), kullanici);

        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.TransferFisiKaydetAsync(new DepoTransferFisi()));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.DepoSayimFarkiKaydetAsync(1, 1, 1, "test"));
        kullanici.AktifYetkiler.AyarlariYonet = false;
        kullanici.AktifYetkiler.DepoTransferYap = true;
        kullanici.AktifYetkiler.StokDuzenle = true;
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.DepoEkleAsync(new Depo()));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.DepoGuncelleAsync(new Depo()));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.DepoSilAsync(1));
    }

    [Fact]
    public async Task Merkezi_tanim_yazma_islemleri_oturum_olmadiginda_reddedilir()
    {
        await using var baglam = BosBaglam();
        var servis = new MerkeziTanimServisi(baglam, new Cari360TestKullanici { AktifKullanici = null });

        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.KaydetAsync(new MerkeziTanim()));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => servis.AktiflikDegistirAsync(1, false));
    }

    private sealed class BosFactory : IDbContextFactory<MuhasebeBaglami>
    {
        public MuhasebeBaglami CreateDbContext() => throw new InvalidOperationException("Yetkisiz islem veritabanina ulasti.");
    }
}
