using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class FinancialTransactionIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public FinancialTransactionIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(FaturaTuru.Satis, 20, "120", 12000, "600", 10000, "391", 2000)]
    [InlineData(FaturaTuru.Alis, 20, "153", 10000, "320", 12000, "191", 2000)]
    [InlineData(FaturaTuru.Satis, 0, "120", 10000, "600", 10000, "391", 0)]
    [InlineData(FaturaTuru.Alis, 0, "153", 10000, "320", 10000, "191", 0)]
    public async Task Fatura_onayi_beklenen_yevmiye_ve_hareketleri_olusturur(
        FaturaTuru tur, decimal kdv, string ilkHesap, decimal ilkTutar, string karsitHesap, decimal karsitTutar, string kdvHesap, decimal kdvTutar)
    {
        var invoice = await SeedAndCreateInvoiceAsync(tur, kdv);
        await using var context = new MuhasebeBaglami(_fixture.Options);
        var service = new FinancialTransactionService(context, new AuthorizedUserService());
        var fis = await service.CreateFromInvoiceAsync(invoice.Id);

        Assert.Equal(YevmiyeDurumu.Onaylandi, fis.Durum);
        Assert.Single(await context.CariHareketler.Where(x => x.IlgiliBelgeId == invoice.Id).ToListAsync());
        Assert.Single(await context.StokHareketleri.Where(x => x.IlgiliBelgeId == invoice.Id).ToListAsync());
        Assert.Equal(fis.Satirlar.Sum(x => x.Borc), fis.Satirlar.Sum(x => x.Alacak));
        var hesapKodlari = await context.HesapPlanlari.ToDictionaryAsync(x => x.Id, x => x.HesapKodu);
        Assert.Contains(fis.Satirlar, x => hesapKodlari[x.HesapId] == ilkHesap && (x.Borc == ilkTutar || x.Alacak == ilkTutar));
        Assert.Contains(fis.Satirlar, x => hesapKodlari[x.HesapId] == karsitHesap && (x.Borc == karsitTutar || x.Alacak == karsitTutar));
        Assert.Equal(kdvTutar > 0, fis.Satirlar.Any(x => hesapKodlari[x.HesapId] == kdvHesap));
    }

    [Fact]
    public async Task Ayni_faturanin_ikinci_onayi_yeni_finansal_kayit_uretmez()
    {
        var invoice = await SeedAndCreateInvoiceAsync(FaturaTuru.Satis, 20);
        await using var context = new MuhasebeBaglami(_fixture.Options);
        var service = new FinancialTransactionService(context, new AuthorizedUserService());
        await service.CreateFromInvoiceAsync(invoice.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFromInvoiceAsync(invoice.Id));
        Assert.Single(await context.YevmiyeFisleri.Where(x => x.BelgeId == invoice.Id && x.BelgeTipi == "Fatura").ToListAsync());
    }

    [Fact]
    public async Task Yetkisiz_veya_kullanici_servissiz_finansal_islem_reddedilir()
    {
        var invoice = await SeedAndCreateInvoiceAsync(FaturaTuru.Satis, 20);
        await using var context = new MuhasebeBaglami(_fixture.Options);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new FinancialTransactionService(context).CreateFromInvoiceAsync(invoice.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new FinancialTransactionService(context, new AuthorizedUserService(false)).CreateFromInvoiceAsync(invoice.Id));
    }

    [Theory]
    [InlineData(FaturaTuru.SatisIade)]
    [InlineData(FaturaTuru.AlisIade)]
    public async Task Iade_faturasi_ters_yonlu_yevmiye_ve_stok_hareketi_olusturur(FaturaTuru tur)
    {
        var invoice = await SeedAndCreateInvoiceAsync(tur, 20);
        await using var context = new MuhasebeBaglami(_fixture.Options);
        var fis = await new FinancialTransactionService(context, new AuthorizedUserService()).CreateFromInvoiceAsync(invoice.Id);
        var codes = await context.HesapPlanlari.ToDictionaryAsync(x => x.Id, x => x.HesapKodu);
        if (tur == FaturaTuru.SatisIade)
        {
            Assert.Contains(fis.Satirlar, x => codes[x.HesapId] == "600" && x.Borc == 10000);
            Assert.Contains(fis.Satirlar, x => codes[x.HesapId] == "391" && x.Borc == 2000);
            Assert.Contains(fis.Satirlar, x => codes[x.HesapId] == "120" && x.Alacak == 12000);
        }
        else
        {
            Assert.Contains(fis.Satirlar, x => codes[x.HesapId] == "320" && x.Borc == 12000);
            Assert.Contains(fis.Satirlar, x => codes[x.HesapId] == "153" && x.Alacak == 10000);
            Assert.Contains(fis.Satirlar, x => codes[x.HesapId] == "191" && x.Alacak == 2000);
        }
        Assert.Equal(fis.Satirlar.Sum(x => x.Borc), fis.Satirlar.Sum(x => x.Alacak));
    }

    [Theory]
    [InlineData(FaturaTuru.Satis)]
    [InlineData(FaturaTuru.Alis)]
    public async Task Onayli_faturanin_iptali_kaynaklari_korumak_ve_ters_fis_uretmek_zorundadir(FaturaTuru tur)
    {
        var invoice = await SeedAndCreateInvoiceAsync(tur, 20);
        await using var context = new MuhasebeBaglami(_fixture.Options);
        await new FinancialTransactionService(context, new AuthorizedUserService()).CreateFromInvoiceAsync(invoice.Id);
        var originalFis = await context.YevmiyeFisleri.SingleAsync(x => x.BelgeTipi == "Fatura" && x.BelgeId == invoice.Id);
        var originalCariCount = await context.CariHareketler.CountAsync(x => x.IlgiliBelgeId == invoice.Id);
        var originalStokCount = await context.StokHareketleri.CountAsync(x => x.IlgiliBelgeId == invoice.Id);
        await new FaturaServisi(context, new AuthorizedUserService()).IptalEtAsync(invoice.Id);
        Assert.Equal(BelgeDurumu.Iptal, (await context.Faturalar.SingleAsync(x => x.Id == invoice.Id)).Durum);
        Assert.NotNull(await context.YevmiyeFisleri.FindAsync(originalFis.Id));
        var reverse = await context.YevmiyeFisleri.Include(x => x.Satirlar).SingleAsync(x => x.BelgeTipi == "FaturaIptal" && x.BelgeId == invoice.Id);
        Assert.Equal(originalCariCount + 1, await context.CariHareketler.CountAsync(x => x.IlgiliBelgeId == invoice.Id));
        Assert.Equal(originalStokCount + 1, await context.StokHareketleri.CountAsync(x => x.IlgiliBelgeId == invoice.Id));
        Assert.Equal(reverse.Satirlar.Sum(x => x.Borc), reverse.Satirlar.Sum(x => x.Alacak));
        var hesapKodlari = await context.HesapPlanlari.ToDictionaryAsync(x => x.Id, x => x.HesapKodu);
        if (tur == FaturaTuru.Satis)
        {
            Assert.Contains(reverse.Satirlar, x => hesapKodlari[x.HesapId] == "120" && x.Borc == 0 && x.Alacak == 12000);
            Assert.Contains(reverse.Satirlar, x => hesapKodlari[x.HesapId] == "600" && x.Borc == 10000 && x.Alacak == 0);
            Assert.Contains(reverse.Satirlar, x => hesapKodlari[x.HesapId] == "391" && x.Borc == 2000 && x.Alacak == 0);
        }
        else
        {
            Assert.Contains(reverse.Satirlar, x => hesapKodlari[x.HesapId] == "153" && x.Borc == 0 && x.Alacak == 10000);
            Assert.Contains(reverse.Satirlar, x => hesapKodlari[x.HesapId] == "191" && x.Borc == 0 && x.Alacak == 2000);
            Assert.Contains(reverse.Satirlar, x => hesapKodlari[x.HesapId] == "320" && x.Borc == 12000 && x.Alacak == 0);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => new FaturaServisi(context, new AuthorizedUserService()).IptalEtAsync(invoice.Id));
        Assert.Single(await context.YevmiyeFisleri.Where(x => x.BelgeTipi == "FaturaIptal" && x.BelgeId == invoice.Id).ToListAsync());
    }

    [Fact]
    public async Task Eszamanli_onayda_tek_finansal_islem_kalir()
    {
        var invoice = await SeedAndCreateInvoiceAsync(FaturaTuru.Satis, 20);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Exception?> RunAsync()
        {
            await gate.Task;
            await using var context = new MuhasebeBaglami(_fixture.Options);
            try { await new FinancialTransactionService(context, new AuthorizedUserService()).CreateFromInvoiceAsync(invoice.Id); return null; }
            catch (Exception ex) { return ex; }
        }
        var first = RunAsync(); var second = RunAsync(); gate.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.Single(results, x => x is null);
        Assert.Single(results, x => x is InvalidOperationException);
        await using var verify = new MuhasebeBaglami(_fixture.Options);
        Assert.Equal(1, await verify.YevmiyeFisleri.CountAsync(x => x.BelgeTipi == "Fatura" && x.BelgeId == invoice.Id));
        Assert.Equal(1, await verify.CariHareketler.CountAsync(x => x.IlgiliBelgeId == invoice.Id));
        Assert.Equal(1, await verify.StokHareketleri.CountAsync(x => x.IlgiliBelgeId == invoice.Id));
    }

    [Fact]
    public async Task Yetersiz_stok_hatasi_finansal_transactioni_geri_alir()
    {
        var invoice = await SeedAndCreateInvoiceAsync(FaturaTuru.Satis, 20, 101);
        await using (var context = new MuhasebeBaglami(_fixture.Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new FinancialTransactionService(context, new AuthorizedUserService()).CreateFromInvoiceAsync(invoice.Id));
        await using var verify = new MuhasebeBaglami(_fixture.Options);
        Assert.Equal(BelgeDurumu.Taslak, (await verify.Faturalar.SingleAsync(x => x.Id == invoice.Id)).Durum);
        Assert.Empty(await verify.CariHareketler.Where(x => x.IlgiliBelgeId == invoice.Id).ToListAsync());
        Assert.Empty(await verify.StokHareketleri.Where(x => x.IlgiliBelgeId == invoice.Id).ToListAsync());
        Assert.Empty(await verify.YevmiyeFisleri.Where(x => x.BelgeId == invoice.Id).ToListAsync());
    }

    private async Task<Fatura> SeedAndCreateInvoiceAsync(FaturaTuru tur, decimal kdv, decimal miktar = 1)
    {
        _fixture.RequireDatabase();
        await using var context = new MuhasebeBaglami(_fixture.Options);
        var sirketId = MuhasebeBaglami.VarsayilanSirketId;
        if (!await context.Sirketler.AnyAsync(x => x.Id == sirketId))
        {
            context.Sirketler.Add(new EtkiAlani.Modeller.Kurumsal.Sirket { Id = sirketId, Unvan = "Varsayılan Şirket", SirketKodu = "DEF-01" });
            await context.SaveChangesAsync();
        }
        var cari = new CariKart { SirketId = sirketId, Kod = $"T-{Guid.NewGuid():N}", Unvan = "Test Cari", Aktif = true };
        var urun = new Urun { SirketId = sirketId, Kod = $"T-{Guid.NewGuid():N}", Ad = "Test Ürün", Birim = "Adet", Aktif = true, StokMiktari = 100 };
        context.AddRange(cari, urun);
        await context.SaveChangesAsync();
        context.UrunLokasyonStoklari.Add(new UrunLokasyonStogu
        {
            UrunId = urun.Id,
            DepoId = 1,
            MevcutMiktar = 100
        });
        await context.SaveChangesAsync();
        var invoice = new Fatura
        {
            SirketId = sirketId, CariKartId = cari.Id, FaturaNo = $"TEST-{Guid.NewGuid():N}", Tarih = DateTime.UtcNow, Tur = tur,
            GenelToplam = 10000m + (10000m * kdv / 100m), KdvToplam = 10000m * kdv / 100m,
            Kalemler = [new FaturaKalemi
            {
                UrunId = urun.Id, UrunKodu = urun.Kod, UrunAd = urun.Ad, Birim = urun.Birim,
                Miktar = miktar, BirimFiyat = 10000, KdvOrani = kdv,
                StokDagilimlari = [new FaturaKalemiStokDagilimi { DepoId = 1, Miktar = miktar }]
            }]
        };
        context.Faturalar.Add(invoice);
        await context.SaveChangesAsync();
        return invoice;
    }

    private sealed class AuthorizedUserService : IKullaniciServisi
    {
        private readonly bool _authorized;
        public AuthorizedUserService(bool authorized = true) => _authorized = authorized;
        public Kullanici? AktifKullanici => _authorized ? new Kullanici { Yetkiler = AktifYetkiler } : null;
        public KullaniciYetkileri? AktifYetkiler => _authorized ? new KullaniciYetkileri { FaturaOnayla = true, FaturaIptal = true } : null;
        public Task<bool> GirisYapAsync(string a, string b, CancellationToken c = default) => Task.FromResult(false);
        public void CikisYap() { }
        public bool YetkiliMi(Func<KullaniciYetkileri, bool> sorgu) => AktifYetkiler is not null && sorgu(AktifYetkiler);
        public void YetkiKontrolEt(Func<KullaniciYetkileri, bool> sorgu, string islem) { if (!YetkiliMi(sorgu)) throw new UnauthorizedAccessException(); }
        public Task<List<Kullanici>> TumunuGetirAsync() => throw new NotSupportedException();
        public Task<Kullanici?> GetirAsync(int id) => throw new NotSupportedException();
        public Task<Kullanici> EkleAsync(Kullanici k, string s) => throw new NotSupportedException();
        public Task GuncelleAsync(Kullanici k) => throw new NotSupportedException();
        public Task SifreDegistirAsync(int id, string s) => throw new NotSupportedException();
        public Task SilAsync(int id) => throw new NotSupportedException();
    }
}
