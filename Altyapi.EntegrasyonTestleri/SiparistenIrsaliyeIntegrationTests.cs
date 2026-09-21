using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Irsaliye;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Siparis;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class SiparistenIrsaliyeIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public SiparistenIrsaliyeIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact] public async Task Tam_sevkiyat_siparisi_tamamlar() { var x = await Hazirla(10); await Sevk(x, 10, (1, 10)); await Kontrol(x, s => Assert.Equal(SiparisDurumu.Tamamlandi, s.Durum)); }
    [Fact] public async Task Kismi_sevkiyat_miktari_ve_durumu_gunceller() { var x = await Hazirla(10); await Sevk(x, 4, (1, 4)); await Kontrol(x, s => { Assert.Equal(4, s.Kalemler.Single().SevkEdilenMiktar); Assert.Equal(SiparisDurumu.KismenKarsilandi, s.Durum); }); }
    [Fact] public async Task Ikinci_sevkiyat_siparisi_tamamlar() { var x = await Hazirla(10); await Sevk(x, 4, (1, 4)); await Sevk(x, 6, (2, 6)); await Kontrol(x, s => Assert.Equal(SiparisDurumu.Tamamlandi, s.Durum)); }
    [Fact] public async Task Uc_lokasyona_dagilim_aynen_uygulanir() { var x = await Hazirla(10); var i = await Sevk(x, 10, (1, 2), (2, 3), (3, 5)); await using var c = Yeni(); Assert.Equal(3, await c.IrsaliyeKalemiStokDagilimlari.CountAsync(d => d.IrsaliyeKalemi!.IrsaliyeId == i.Id)); Assert.Equal(new[] { 8m, 7m, 5m }, await Stoklar(c, x.UrunId)); }
    [Fact] public async Task Secilmeyen_lokasyonun_stogu_korunur() { var x = await Hazirla(5); await Sevk(x, 2, (1, 2)); await using var c = Yeni(); Assert.Equal(10, (await Stoklar(c, x.UrunId))[1]); Assert.Equal(10, (await Stoklar(c, x.UrunId))[2]); }
    [Fact] public async Task Yetersiz_lokasyon_stogu_engellenir() { var x = await Hazirla(8, 3); await Assert.ThrowsAsync<InvalidOperationException>(() => Sevk(x, 5, (1, 5))); await using var c = Yeni(); Assert.Equal(3, (await Stoklar(c, x.UrunId))[0]); }
    [Fact] public async Task Kalan_miktardan_fazla_sevk_engellenir() { var x = await Hazirla(5); await Assert.ThrowsAsync<InvalidOperationException>(() => Sevk(x, 6, (1, 6))); }
    [Fact] public async Task Irsaliye_kalem_dagilim_hareket_ve_siparis_atomik_kaydedilir() { var x = await Hazirla(3); var i = await Sevk(x, 3, (1, 3)); await using var c = Yeni(); Assert.True(await c.Irsaliyeler.AnyAsync(y => y.Id == i.Id)); Assert.True(await c.IrsaliyeKalemleri.AnyAsync(y => y.IrsaliyeId == i.Id)); Assert.True(await c.StokHareketleri.AnyAsync(y => y.IlgiliBelgeId == i.Id)); }
    [Fact] public async Task Hatali_dagilim_tum_islemi_rollback_yapar() { var x = await Hazirla(5); await Assert.ThrowsAsync<InvalidOperationException>(() => Sevk(x, 5, (1, 2))); await using var c = Yeni(); Assert.False(await c.Irsaliyeler.AnyAsync(y => y.SiparisId == x.SiparisId)); Assert.Equal(10, (await Stoklar(c, x.UrunId))[0]); }
    [Fact] public async Task Son_stok_icin_eszamanli_isteklerden_yalniz_biri_kazanir() { var x = await Hazirla(5, 5); var sonuc = await Task.WhenAll(Dene(() => Sevk(x, 5, (1, 5))), Dene(() => Sevk(x, 5, (1, 5)))); Assert.Single(sonuc, y => y); await using var c = Yeni(); Assert.Equal(0, (await Stoklar(c, x.UrunId))[0]); }
    [Fact] public async Task Irsaliye_iptali_fiziksel_silmez() { var x = await Hazirla(4); var i = await Sevk(x, 4, (1, 4)); await Iptal(i.Id); await using var c = Yeni(); Assert.Equal(IrsaliyeDurumu.Iptal, (await c.Irsaliyeler.SingleAsync(y => y.Id == i.Id)).Durum); }
    [Fact] public async Task Iptalde_stok_orijinal_lokasyonlara_doner() { var x = await Hazirla(6); var i = await Sevk(x, 6, (1, 1), (2, 2), (3, 3)); await Iptal(i.Id); await using var c = Yeni(); Assert.Equal(new[] { 10m, 10m, 10m }, await Stoklar(c, x.UrunId)); }
    [Fact] public async Task Iptalde_sevk_edilen_miktar_geri_alinir() { var x = await Hazirla(4); var i = await Sevk(x, 4, (1, 4)); await Iptal(i.Id); await Kontrol(x, s => Assert.Equal(0, s.Kalemler.Single().SevkEdilenMiktar)); }
    [Fact] public async Task Iptal_sonrasi_siparis_durumu_yeniden_hesaplanir() { var x = await Hazirla(10); var i = await Sevk(x, 4, (1, 4)); await Iptal(i.Id); await Kontrol(x, s => Assert.Equal(SiparisDurumu.Onaylandi, s.Durum)); }
    [Fact] public async Task Sevkiyat_finansal_hareket_uretmez() { var x = await Hazirla(2); await using var once = Yeni(); var sayi = new[] { await once.CariHareketler.CountAsync(), await once.KasaHareketleri.CountAsync(), await once.BankaHareketleri.CountAsync(), await once.YevmiyeFisleri.CountAsync() }; await Sevk(x, 2, (1, 2)); await using var sonra = Yeni(); Assert.Equal(sayi, new[] { await sonra.CariHareketler.CountAsync(), await sonra.KasaHareketleri.CountAsync(), await sonra.BankaHareketleri.CountAsync(), await sonra.YevmiyeFisleri.CountAsync() }); }

    private async Task<(int SiparisId, int KalemId, int UrunId)> Hazirla(decimal miktar, decimal magaza = 10)
    {
        await using var c = Yeni();
        var sirket = await c.Sirketler.FirstOrDefaultAsync();
        if (sirket == null)
        {
            sirket = new EtkiAlani.Modeller.Kurumsal.Sirket { Unvan = "Test Şirketi", SirketKodu = "TEST-01" };
            c.Sirketler.Add(sirket);
            await c.SaveChangesAsync();
        }

        var cari = await c.CariKartlar.FirstOrDefaultAsync();
        if (cari == null)
        {
            cari = new EtkiAlani.Modeller.Cari.CariKart { SirketId = sirket.Id, Kod = "SVK-CARI-01", Unvan = "Sevk Test Cari", Aktif = true };
            c.CariKartlar.Add(cari);
            await c.SaveChangesAsync();
        }

        var kod = "SVK" + Guid.NewGuid().ToString("N")[..10];
        var u = new Urun { SirketId = sirket.Id, Kod = kod, Ad = "Sevk Test Ürünü", Birim = "Adet", Aktif = true, StokMiktari = magaza + 20, KdvOrani = 20 };
        c.Urunler.Add(u); await c.SaveChangesAsync();
        c.UrunLokasyonStoklari.AddRange(Enumerable.Range(1, 3).Select(d => new UrunLokasyonStogu { UrunId = u.Id, DepoId = d, MevcutMiktar = d == 1 ? magaza : 10 }));
        var s = new Siparis { SiparisNo = "SIP-T-" + Guid.NewGuid().ToString("N")[..12], SiparisTuru = SiparisTuru.Satis, CariKartId = cari.Id, ParaBirimiId = 1, Kur = 1, Durum = SiparisDurumu.Onaylandi, OlusturanKullaniciId = 1, Kalemler = [new SiparisKalemi { UrunId = u.Id, UrunKodu = u.Kod, UrunAdi = u.Ad, Birim = u.Birim, Miktar = miktar, BirimFiyat = 10, KdvOrani = 20, SatirToplami = miktar * 12 }] };
        c.Siparisler.Add(s); await c.SaveChangesAsync(); return (s.Id, s.Kalemler.Single().Id, u.Id);
    }
    private async Task<Irsaliye> Sevk((int SiparisId, int KalemId, int UrunId) x, decimal miktar, params (int Depo, decimal Miktar)[] dagilim)
    { await using var c = Yeni(); return await Servis(c).SiparistenSevkEtAsync(new Irsaliye { SiparisId = x.SiparisId, SevkTarihi = DateTime.UtcNow, Kalemler = [new IrsaliyeKalemi { SiparisKalemiId = x.KalemId, Miktar = miktar, StokDagilimlari = dagilim.Select(d => new IrsaliyeKalemiStokDagilimi { DepoId = d.Depo, Miktar = d.Miktar }).ToList() }] }); }
    private async Task Iptal(int id) { await using var c = Yeni(); await Servis(c).IptalEtAsync(id); }
    private async Task Kontrol((int SiparisId, int KalemId, int UrunId) x, Action<Siparis> assert) { await using var c = Yeni(); assert(await c.Siparisler.Include(s => s.Kalemler).SingleAsync(s => s.Id == x.SiparisId)); }
    private static async Task<decimal[]> Stoklar(MuhasebeBaglami c, int urunId) => await c.UrunLokasyonStoklari.Where(x => x.UrunId == urunId).OrderBy(x => x.DepoId).Select(x => x.MevcutMiktar).ToArrayAsync();
    private static async Task<bool> Dene(Func<Task<Irsaliye>> islem) { try { await islem(); return true; } catch { return false; } }
    private MuhasebeBaglami Yeni() { _fixture.RequireDatabase(); return new MuhasebeBaglami(_fixture.Options); }
    private static IrsaliyeServisi Servis(MuhasebeBaglami c) => new(c, new KullaniciServisi());
    private sealed class KullaniciServisi : IKullaniciServisi
    {
        public Kullanici? AktifKullanici { get; } = new() { Id = 1, KullaniciAdi = "admin" }; public KullaniciYetkileri? AktifYetkiler => null;
        public Task<bool> GirisYapAsync(string a, string s, CancellationToken c = default) => Task.FromResult(true); public void CikisYap() { } public bool YetkiliMi(Func<KullaniciYetkileri, bool> f) => true; public void YetkiKontrolEt(Func<KullaniciYetkileri, bool> f, string i) { }
        public Task<List<Kullanici>> TumunuGetirAsync() => throw new NotSupportedException(); public Task<Kullanici?> GetirAsync(int id) => throw new NotSupportedException(); public Task<Kullanici> EkleAsync(Kullanici k, string s) => throw new NotSupportedException(); public Task GuncelleAsync(Kullanici k) => throw new NotSupportedException(); public Task SifreDegistirAsync(int id, string s) => throw new NotSupportedException(); public Task SilAsync(int id) => throw new NotSupportedException();
    }
}
