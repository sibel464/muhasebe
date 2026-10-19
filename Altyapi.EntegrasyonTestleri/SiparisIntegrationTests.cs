using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Siparis;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class SiparisIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public SiparisIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Satis_siparisi_kalem_cari_ve_toplamla_kaydedilir()
    {
        var siparis = await EkleAsync(SiparisTuru.Satis, false, (1, 2m, 100m, 10m, 20m));
        await using var kontrol = YeniBaglam();
        var kayit = await kontrol.Siparisler.Include(x => x.Kalemler).SingleAsync(x => x.Id == siparis.Id);
        Assert.Equal(SiparisTuru.Satis, kayit.SiparisTuru);
        Assert.Equal(1, kayit.CariKartId);
        Assert.Equal(216m, kayit.GenelToplam);
        Assert.Single(kayit.Kalemler);
    }

    [Fact]
    public async Task Alis_siparisi_alis_fiyatiyla_kalici_kaydedilir()
    {
        await using var baglam = YeniBaglam();
        var urun = await baglam.Urunler.AsNoTracking().SingleAsync(x => x.Id == 1);
        var once = new[] { await baglam.StokHareketleri.CountAsync(), await baglam.CariHareketler.CountAsync(), await baglam.KasaHareketleri.CountAsync(), await baglam.BankaHareketleri.CountAsync(), await baglam.YevmiyeFisleri.CountAsync(), await baglam.YevmiyeSatirlari.CountAsync() };
        var siparis = await EkleAsync(SiparisTuru.Alis, false, (1, 2m, urun.AlisFiyati, 0m, urun.KdvOrani));
        await using var kontrol = YeniBaglam();
        var kayit = await kontrol.Siparisler.Include(x => x.Kalemler).SingleAsync(x => x.Id == siparis.Id);
        Assert.Equal(SiparisTuru.Alis, kayit.SiparisTuru);
        Assert.Equal(urun.AlisFiyati, kayit.Kalemler.Single().BirimFiyat);
        Assert.Equal(once, new[] { await kontrol.StokHareketleri.CountAsync(), await kontrol.CariHareketler.CountAsync(), await kontrol.KasaHareketleri.CountAsync(), await kontrol.BankaHareketleri.CountAsync(), await kontrol.YevmiyeFisleri.CountAsync(), await kontrol.YevmiyeSatirlari.CountAsync() });
    }

    [Fact]
    public async Task Siparis_ve_kalem_ayni_transactionda_kaydedilir()
    {
        var siparis = await EkleAsync(SiparisTuru.Satis, true, (1, 1m, 10m, 0m, 20m));
        await using var kontrol = YeniBaglam();
        Assert.True(await kontrol.Siparisler.AnyAsync(x => x.Id == siparis.Id));
        Assert.True(await kontrol.SiparisKalemleri.AnyAsync(x => x.SiparisId == siparis.Id));
    }

    [Fact]
    public async Task Uc_farkli_urun_tum_kalemleriyle_kaydedilir()
    {
        await UrunleriTamamlaAsync();
        var siparis = await EkleAsync(SiparisTuru.Satis, true,
            (1, 1m, 10m, 0m, 20m), (2, 2m, 20m, 0m, 20m), (3, 3m, 30m, 0m, 20m));
        await using var kontrol = YeniBaglam();
        Assert.Equal(3, await kontrol.SiparisKalemleri.CountAsync(x => x.SiparisId == siparis.Id));
    }

    [Fact]
    public async Task Decimal_kdv_iskonto_ve_toplam_kesin_hesaplanir()
    {
        var siparis = await EkleAsync(SiparisTuru.Satis, true, (1, 12.50m, 80m, 7.5m, 20m));
        Assert.Equal(1000m, siparis.BrutToplam);
        Assert.Equal(75m, siparis.IskontoToplami);
        Assert.Equal(925m, siparis.AraToplam);
        Assert.Equal(185m, siparis.KdvToplami);
        Assert.Equal(1110m, siparis.GenelToplam);
    }

    [Fact]
    public async Task Taslak_siparis_stok_ve_finans_hareketi_uretmez()
    {
        await NegatifEtkiSayimiylaEkleAsync(SiparisTuru.Satis, true);
    }

    [Fact]
    public async Task Taslak_siparis_ayni_kayit_ve_yeni_kalemlerle_guncellenir()
    {
        var siparis = await EkleAsync(SiparisTuru.Satis, true, (1, 1m, 10m, 0m, 20m));
        siparis.ReferansNo = "REV-2";
        siparis.Kalemler = [new SiparisKalemi { UrunId = 2, Miktar = 4, BirimFiyat = 25, KdvOrani = 20 }];
        await using var baglam = YeniBaglam();
        var guncel = await Servis(baglam).GuncelleAsync(siparis);
        await using var kontrol = YeniBaglam();
        Assert.Equal(1, await kontrol.Siparisler.CountAsync(x => x.Id == siparis.Id));
        var kalem = await kontrol.SiparisKalemleri.SingleAsync(x => x.SiparisId == siparis.Id);
        Assert.Equal(2, kalem.UrunId);
        Assert.Equal("REV-2", guncel.ReferansNo);
    }

    [Fact]
    public async Task Iptal_fiziksel_silme_ve_finans_hareketi_yapmaz()
    {
        var siparis = await EkleAsync(SiparisTuru.Satis, true, (1, 1m, 10m, 0m, 20m));
        await using var once = YeniBaglam();
        var stok = await once.StokHareketleri.CountAsync();
        var cari = await once.CariHareketler.CountAsync();
        await using var baglam = YeniBaglam();
        await Servis(baglam).IptalEtAsync(siparis.Id, siparis.Versiyon);
        await using var kontrol = YeniBaglam();
        Assert.Equal(SiparisDurumu.Iptal, (await kontrol.Siparisler.SingleAsync(x => x.Id == siparis.Id)).Durum);
        Assert.Equal(stok, await kontrol.StokHareketleri.CountAsync());
        Assert.Equal(cari, await kontrol.CariHareketler.CountAsync());
    }

    [Fact]
    public async Task Duplicate_siparis_numarasi_unique_constraint_ile_engellenir()
    {
        var siparis = await EkleAsync(SiparisTuru.Satis, true, (1, 1m, 10m, 0m, 20m));
        await using var baglam = YeniBaglam();
        baglam.Siparisler.Add(YalinSiparis(siparis.SiparisNo));
        await Assert.ThrowsAsync<DbUpdateException>(() => baglam.SaveChangesAsync());
    }

    [Fact]
    public async Task Eski_versiyon_ikinci_context_guncellemesini_engeller()
    {
        var siparis = await EkleAsync(SiparisTuru.Satis, true, (1, 1m, 10m, 0m, 20m));
        await using var c1 = YeniBaglam(); await using var c2 = YeniBaglam();
        var s1 = await Servis(c1).GetirAsync(siparis.Id); var s2 = await Servis(c2).GetirAsync(siparis.Id);
        s1!.ReferansNo = "BIR"; await Servis(c1).GuncelleAsync(s1);
        s2!.ReferansNo = "IKI";
        var hata = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Servis(c2).GuncelleAsync(s2));
        Assert.Contains("başka bir kullanıcı", hata.Message);
    }

    [Fact]
    public async Task Hatali_kalemde_transaction_yarim_siparis_birakmaz()
    {
        await using var once = YeniBaglam(); var adet = await once.Siparisler.CountAsync();
        var hatali = YalinSiparis("");
        hatali.Kalemler = [new SiparisKalemi { UrunId = 999999, Miktar = 1, BirimFiyat = 10, KdvOrani = 20 }];
        await using var baglam = YeniBaglam();
        await Assert.ThrowsAsync<ArgumentException>(() => Servis(baglam).EkleAsync(hatali, true));
        await using var kontrol = YeniBaglam();
        Assert.Equal(adet, await kontrol.Siparisler.CountAsync());
        Assert.False(await kontrol.SiparisKalemleri.AnyAsync(x => x.UrunId == 999999));
    }

    [Fact]
    public async Task Gece_yarisi_girilen_siparisin_tarihi_kaymaz_ve_tarih_filtresinde_bulunur()
    {
        _fixture.RequireDatabase();
        await using var baglam = YeniBaglam();
        await VarsayilanVerileriOlusturAsync(baglam);
        var gun = new DateTime(2026, 3, 15, 0, 30, 0, DateTimeKind.Local);
        var siparis = YalinSiparis("");
        siparis.SiparisTarihi = gun; siparis.TeslimTarihi = gun.Date;
        var eklenen = await Servis(baglam).EkleAsync(siparis, false);
        await using var kontrol = YeniBaglam();
        var kayit = await kontrol.Siparisler.AsNoTracking().SingleAsync(x => x.Id == eklenen.Id);
        Assert.Equal(gun.Date, kayit.SiparisTarihi.Date);
        Assert.Equal(gun.Date, kayit.TeslimTarihi!.Value.Date);
        var liste = await Servis(kontrol).ListeleAsync(new SiparisFiltre { BaslangicTarihi = gun.Date, BitisTarihi = gun.Date });
        Assert.Contains(liste, x => x.Id == eklenen.Id);
    }

    private async Task NegatifEtkiSayimiylaEkleAsync(SiparisTuru tur, bool taslak)
    {
        await using var once = YeniBaglam();
        var sayilar = new[] { await once.StokHareketleri.CountAsync(), await once.CariHareketler.CountAsync(), await once.KasaHareketleri.CountAsync(), await once.BankaHareketleri.CountAsync(), await once.YevmiyeFisleri.CountAsync(), await once.YevmiyeSatirlari.CountAsync() };
        await EkleAsync(tur, taslak, (1, 1m, 10m, 0m, 20m));
        await using var sonra = YeniBaglam();
        Assert.Equal(sayilar, new[] { await sonra.StokHareketleri.CountAsync(), await sonra.CariHareketler.CountAsync(), await sonra.KasaHareketleri.CountAsync(), await sonra.BankaHareketleri.CountAsync(), await sonra.YevmiyeFisleri.CountAsync(), await sonra.YevmiyeSatirlari.CountAsync() });
    }

    private async Task VarsayilanVerileriOlusturAsync(MuhasebeBaglami baglam)
    {
        var sirket = await baglam.Sirketler.FirstOrDefaultAsync();
        if (sirket == null)
        {
            sirket = new EtkiAlani.Modeller.Kurumsal.Sirket { Id = Guid.NewGuid(), Unvan = "Siparis Test Sirket", SirketKodu = "SIP-SRK" };
            baglam.Sirketler.Add(sirket);
            await baglam.SaveChangesAsync();
        }

        if (!await baglam.CariKartlar.AnyAsync(x => x.Id == 1))
        {
            baglam.CariKartlar.Add(new EtkiAlani.Modeller.Cari.CariKart { Id = 1, SirketId = sirket.Id, Kod = "SIP-CARI-01", Unvan = "Test Cari", Aktif = true });
            await baglam.SaveChangesAsync();
        }

        if (!await baglam.Urunler.AnyAsync(x => x.Id == 1))
        {
            baglam.Urunler.Add(new Urun { Id = 1, SirketId = sirket.Id, Kod = "URN001", Ad = "Test Ürünü 1", Birim = "Adet", Aktif = true, KdvOrani = 20, AlisFiyati = 50, PerakendeSatisFiyati = 100 });
            await baglam.SaveChangesAsync();
        }
        if (!await baglam.Urunler.AnyAsync(x => x.Id == 2))
        {
            baglam.Urunler.Add(new Urun { Id = 2, SirketId = sirket.Id, Kod = "URN002", Ad = "Test Ürünü 2", Birim = "Adet", Aktif = true, KdvOrani = 20, AlisFiyati = 60, PerakendeSatisFiyati = 120 });
            await baglam.SaveChangesAsync();
        }
    }

    private async Task<Siparis> EkleAsync(SiparisTuru tur, bool taslak, params (int UrunId, decimal Miktar, decimal Fiyat, decimal Iskonto, decimal Kdv)[] satirlar)
    {
        _fixture.RequireDatabase();
        await using var baglam = YeniBaglam();
        await VarsayilanVerileriOlusturAsync(baglam);
        var siparis = YalinSiparis("");
        siparis.SiparisTuru = tur;
        siparis.Kalemler = satirlar.Select(x => new SiparisKalemi { UrunId = x.UrunId, Miktar = x.Miktar, BirimFiyat = x.Fiyat, IskontoOrani = x.Iskonto, KdvOrani = x.Kdv }).ToList();
        return await Servis(baglam).EkleAsync(siparis, taslak);
    }

    private static Siparis YalinSiparis(string no) => new()
    {
        SiparisNo = no, CariKartId = 1, ParaBirimiId = 1, Kur = 1, SiparisTarihi = DateTime.UtcNow,
        TeslimatAdSoyad = "Test Cari", OlusturanKullaniciId = 1,
        Kalemler = [new SiparisKalemi { UrunId = 1, UrunKodu = "URN001", UrunAdi = "Ürün", Birim = "Adet", Miktar = 1, BirimFiyat = 10, KdvOrani = 20 }]
    };

    private async Task UrunleriTamamlaAsync()
    {
        await using var baglam = YeniBaglam();
        await VarsayilanVerileriOlusturAsync(baglam);
        var sirket = await baglam.Sirketler.FirstAsync();
        if (!await baglam.Urunler.AnyAsync(x => x.Id == 3))
        {
            baglam.Urunler.Add(new Urun { Id = 3, SirketId = sirket.Id, Kod = "URN003", Ad = "Üçüncü Ürün", Birim = "Adet", Aktif = true, KdvOrani = 20 });
            await baglam.SaveChangesAsync();
        }
    }

    private MuhasebeBaglami YeniBaglam() { _fixture.RequireDatabase(); return new MuhasebeBaglami(_fixture.Options); }
    private static SiparisServisi Servis(MuhasebeBaglami baglam) => new(baglam, new TestKullaniciServisi());

    private sealed class TestKullaniciServisi : IKullaniciServisi
    {
        public Kullanici? AktifKullanici { get; } = new() { Id = 1, KullaniciAdi = "admin" };
        public KullaniciYetkileri? AktifYetkiler => null;
        public Task<bool> GirisYapAsync(string a, string s, CancellationToken c = default) => Task.FromResult(true);
        public void CikisYap() { }
        public bool YetkiliMi(Func<KullaniciYetkileri, bool> f) => true;
        public void YetkiKontrolEt(Func<KullaniciYetkileri, bool> f, string i) { }
        public Task<List<Kullanici>> TumunuGetirAsync() => throw new NotSupportedException();
        public Task<Kullanici?> GetirAsync(int id) => throw new NotSupportedException();
        public Task<Kullanici> EkleAsync(Kullanici k, string s) => throw new NotSupportedException();
        public Task GuncelleAsync(Kullanici k) => throw new NotSupportedException();
        public Task SifreDegistirAsync(int id, string s) => throw new NotSupportedException();
        public Task SilAsync(int id) => throw new NotSupportedException();
    }
}
