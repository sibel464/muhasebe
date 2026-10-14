using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class PosSatisIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public PosSatisIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("Nakit", 1, 0, 2)]
    [InlineData("Kredi Kartı", 0, 1, 2)]
    [InlineData("Banka", 0, 1, 2)]
    [InlineData("Açık Hesap", 0, 0, 1)]
    public async Task Pos_satisi_odeme_stok_cari_ve_yevmiye_ile_atomik_tamamlanir(
        string odemeYontemi, int kasaSayisi, int bankaSayisi, int cariSayisi)
    {
        var veri = await SeedAsync();
        await using (var context = new MuhasebeBaglami(_fixture.Options))
        {
            var service = new FaturaServisi(context, new AuthorizedUserService());
            await service.PosSatisiniTamamlaAsync(FaturaOlustur(veri, odemeYontemi), odemeYontemi);
        }

        await using var verify = new MuhasebeBaglami(_fixture.Options);
        var fatura = await verify.Faturalar.SingleAsync(x => x.FaturaNo == veri.FaturaNo);
        Assert.Equal(BelgeDurumu.Onaylandi, fatura.Durum);
        Assert.Equal(kasaSayisi, await verify.KasaHareketleri.CountAsync(x => x.FaturaId == fatura.Id));
        Assert.Equal(bankaSayisi, await verify.BankaHareketleri.CountAsync(x => x.FaturaId == fatura.Id));
        Assert.Equal(cariSayisi, await verify.CariHareketler.CountAsync(x => x.IlgiliBelgeId == fatura.Id));
        Assert.Single(await verify.YevmiyeFisleri.Where(x => x.BelgeTipi == "Fatura" && x.BelgeId == fatura.Id).ToListAsync());
        // Peşin tahsilat da muhasebeleşir: 100/102 borç - 120 alacak
        var kasaIdleri = await verify.KasaHareketleri.Where(x => x.FaturaId == fatura.Id).Select(x => x.Id).ToListAsync();
        var bankaIdleri = await verify.BankaHareketleri.Where(x => x.FaturaId == fatura.Id).Select(x => x.Id).ToListAsync();
        Assert.Equal(kasaSayisi, await verify.YevmiyeFisleri.CountAsync(x => x.BelgeTipi == "KasaHareketi" && kasaIdleri.Contains(x.BelgeId)));
        Assert.Equal(bankaSayisi, await verify.YevmiyeFisleri.CountAsync(x => x.BelgeTipi == "BankaHareketi" && bankaIdleri.Contains(x.BelgeId)));
        var hareketler = await verify.StokHareketleri.Where(x => x.IlgiliBelgeId == fatura.Id).OrderBy(x => x.DepoId).ToListAsync();
        Assert.Equal(new decimal[] { 2, 3, 5 }, hareketler.Select(x => x.Miktar));
        Assert.Equal(new int?[] { 1, 2, 3 }, hareketler.Select(x => x.DepoId));
    }

    [Fact]
    public async Task Nakit_fatura_iptalinde_kasadaki_para_iade_edilir_ve_fisi_ters_kaydedilir()
    {
        var veri = await SeedAsync();
        int faturaId;
        await using (var context = new MuhasebeBaglami(_fixture.Options))
        {
            var service = new FaturaServisi(context, new AuthorizedUserService());
            faturaId = (await service.PosSatisiniTamamlaAsync(FaturaOlustur(veri, "Nakit"), "Nakit")).Id;
            await service.IptalEtAsync(faturaId);
        }

        await using var verify = new MuhasebeBaglami(_fixture.Options);
        var asil = await verify.KasaHareketleri.SingleAsync(x => x.FaturaId == faturaId);
        Assert.Equal(EtkiAlani.Modeller.Finans.FinansIslemDurumu.IptalEdildi, asil.IslemDurumu);
        var ters = await verify.KasaHareketleri.SingleAsync(x => x.Id == asil.TersKayitId);
        Assert.Equal(EtkiAlani.Modeller.Finans.HareketTuru.Gider, ters.Tur);
        Assert.Equal(asil.Tutar, ters.Tutar);

        // Tahsilat fişi ile iade fişi birbirini sıfırlar.
        var fisSatirlari = await verify.YevmiyeSatirlari
            .Where(s => s.Fis!.BelgeTipi == "KasaHareketi" && (s.Fis.BelgeId == asil.Id || s.Fis.BelgeId == ters.Id))
            .ToListAsync();
        Assert.Equal(4, fisSatirlari.Count);
        Assert.Equal(fisSatirlari.Sum(s => s.Borc), fisSatirlari.Sum(s => s.Alacak));
        Assert.All(fisSatirlari.GroupBy(s => s.HesapId), g => Assert.Equal(g.Sum(s => s.Borc), g.Sum(s => s.Alacak)));
    }

    [Fact]
    public async Task Son_asama_hatasi_tum_pos_kayitlarini_geri_alir()
    {
        var veri = await SeedAsync();
        await using (var context = new MuhasebeBaglami(_fixture.Options))
        {
            var service = new FaturaServisi(context, new AuthorizedUserService());
            await Assert.ThrowsAsync<ArgumentException>(() => service.PosSatisiniTamamlaAsync(FaturaOlustur(veri, "Kontrollü Hata"), "Kontrollü Hata"));
        }

        await using var verify = new MuhasebeBaglami(_fixture.Options);
        Assert.False(await verify.Faturalar.IgnoreQueryFilters().AnyAsync(x => x.FaturaNo == veri.FaturaNo));
        Assert.Empty(await verify.StokHareketleri.Where(x => x.BelgeNo == veri.FaturaNo).ToListAsync());
        Assert.Empty(await verify.CariHareketler.Where(x => x.BelgeNo == veri.FaturaNo).ToListAsync());
        Assert.Empty(await verify.KasaHareketleri.Where(x => x.Belge == veri.FaturaNo).ToListAsync());
        Assert.Empty(await verify.BankaHareketleri.Where(x => x.Aciklama.Contains(veri.FaturaNo)).ToListAsync());
        Assert.Empty(await verify.YevmiyeFisleri.Where(x => x.Aciklama.Contains(veri.FaturaNo)).ToListAsync());
        Assert.All(await verify.UrunLokasyonStoklari.Where(x => x.UrunId == veri.UrunId).ToListAsync(), x => Assert.Equal(20, x.MevcutMiktar));
    }

    private async Task<SeedData> SeedAsync()
    {
        _fixture.RequireDatabase();
        await using var context = new MuhasebeBaglami(_fixture.Options);
        if (!await context.Sirketler.AnyAsync(x => x.Id == MuhasebeBaglami.VarsayilanSirketId))
        {
            context.Sirketler.Add(new EtkiAlani.Modeller.Kurumsal.Sirket { Id = MuhasebeBaglami.VarsayilanSirketId, Unvan = "Varsayılan Şirket", SirketKodu = "DEF-01" });
            await context.SaveChangesAsync();
        }
        var cari = new CariKart { SirketId = MuhasebeBaglami.VarsayilanSirketId, Kod = $"POS-{Guid.NewGuid():N}", Unvan = "POS Test Cari", Aktif = true };
        var urun = new Urun { SirketId = MuhasebeBaglami.VarsayilanSirketId, Kod = $"POS-{Guid.NewGuid():N}", Barkod = $"869{Random.Shared.NextInt64(1000000000, 9999999999)}", Ad = "POS Test Ürün", Birim = "Adet", Aktif = true, StokMiktari = 60, PerakendeSatisFiyati = 100, KdvOrani = 20 };
        context.AddRange(cari, urun);
        await context.SaveChangesAsync();
        context.UrunLokasyonStoklari.AddRange(Enumerable.Range(1, 3).Select(depoId => new UrunLokasyonStogu { UrunId = urun.Id, DepoId = depoId, MevcutMiktar = 20 }));
        await context.SaveChangesAsync();
        return new SeedData(cari.Id, urun.Id, urun.Kod, $"POS-{Guid.NewGuid():N}");
    }

    private static Fatura FaturaOlustur(SeedData veri, string odemeYontemi) => new()
    {
        SirketId = MuhasebeBaglami.VarsayilanSirketId, CariKartId = veri.CariId, FaturaNo = veri.FaturaNo,
        Tarih = DateTime.UtcNow, Tur = FaturaTuru.Satis, Aciklama = $"{veri.FaturaNo} POS {odemeYontemi}",
        Kalemler = [new FaturaKalemi
        {
            UrunId = veri.UrunId, Miktar = 10, BirimFiyat = 100, KdvOrani = 20,
            StokDagilimlari =
            [
                new FaturaKalemiStokDagilimi { DepoId = 1, Miktar = 2 },
                new FaturaKalemiStokDagilimi { DepoId = 2, Miktar = 3 },
                new FaturaKalemiStokDagilimi { DepoId = 3, Miktar = 5 }
            ]
        }]
    };

    private sealed record SeedData(int CariId, int UrunId, string UrunKodu, string FaturaNo);

    private sealed class AuthorizedUserService : IKullaniciServisi
    {
        public Kullanici? AktifKullanici => new() { Yetkiler = AktifYetkiler };
        public KullaniciYetkileri? AktifYetkiler => new() { FaturaOlustur = true, FaturaOnayla = true, FaturaIptal = true };
        public Task<bool> GirisYapAsync(string a, string b, CancellationToken c = default) => Task.FromResult(false);
        public void CikisYap() { }
        public bool YetkiliMi(Func<KullaniciYetkileri, bool> sorgu) => sorgu(AktifYetkiler!);
        public void YetkiKontrolEt(Func<KullaniciYetkileri, bool> sorgu, string islem) { if (!YetkiliMi(sorgu)) throw new UnauthorizedAccessException(); }
        public Task<List<Kullanici>> TumunuGetirAsync() => throw new NotSupportedException();
        public Task<Kullanici?> GetirAsync(int id) => throw new NotSupportedException();
        public Task<Kullanici> EkleAsync(Kullanici k, string s) => throw new NotSupportedException();
        public Task GuncelleAsync(Kullanici k) => throw new NotSupportedException();
        public Task SifreDegistirAsync(int id, string s) => throw new NotSupportedException();
        public Task SilAsync(int id) => throw new NotSupportedException();
    }
}
