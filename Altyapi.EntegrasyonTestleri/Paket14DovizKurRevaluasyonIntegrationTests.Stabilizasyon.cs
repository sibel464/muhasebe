using Altyapi.Veri;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed partial class Paket14DovizKurRevaluasyonIntegrationTests
{
    [Fact]
    public async Task Stabilizasyon_BaskaFirma_Revaluasyon_OkumaVeIptal_Reddedilir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var env = await SetupTestEnvironmentAsync(db);
        var baskaFirma = Guid.NewGuid();
        await Assert.ThrowsAsync<YetkisizErisimException>(() => env.RevaluasyonServisi.GetirRevaluasyonGecmisiAsync(baskaFirma));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => env.RevaluasyonServisi.RevaluasyonIptalEtAsync(1, "Denetim", baskaFirma));
    }

    [Fact]
    public async Task Stabilizasyon_IstemciTutarVeHesapManipulasyonu_KaliciKaydiDegistirmez()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var env = await SetupTestEnvironmentAsync(db);
        await DenetimBankaBakiyesiAsync(db, env.SirketId);
        var onizleme = await env.RevaluasyonServisi.RevaluasyonOnizleAsync(env.SirketId, "USD");
        Assert.Equal(200m, onizleme.ToplamKurFarki);
        onizleme.ToplamKurFarki = 999999m;
        onizleme.Kalemler[0].KurFarki = 999999m;
        onizleme.Kalemler[0].HesapKodu = "646";
        var sonuc = await env.RevaluasyonServisi.RevaluasyonUygulaAsync(onizleme, 999999);
        Assert.Equal(200m, sonuc.ToplamKurFarki);
        var kalici = await db.RevaluasyonKayitlari.AsNoTracking().SingleAsync(r => r.Id == sonuc.Id);
        Assert.Equal(env.Kullanici.Id, kalici.KullaniciId);
        var bankaHesabi = await db.HesapPlanlari.SingleAsync(h => h.SirketId == env.SirketId && h.HesapKodu == "102");
        var bankaBorc = await db.YevmiyeSatirlari.Where(s => s.FisId == kalici.YevmiyeFisId && s.HesapId == bankaHesabi.Id).SumAsync(s => s.Borc);
        Assert.Equal(200m, bankaBorc);
    }

    [Fact]
    public async Task Stabilizasyon_YevmiyeBasarisizsa_RevaluasyonBasligiDaGeriAlinir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var env = await SetupTestEnvironmentAsync(db);
        await DenetimBankaBakiyesiAsync(db, env.SirketId);
        var onizleme = await env.RevaluasyonServisi.RevaluasyonOnizleAsync(env.SirketId, "USD");
        var yetkiler = await db.KullaniciYetkileri.SingleAsync(y => y.KullaniciId == env.Kullanici.Id);
        yetkiler.MuhasebeFisEkle = false;
        await Assert.ThrowsAsync<YetkisizErisimException>(() => env.RevaluasyonServisi.RevaluasyonUygulaAsync(onizleme));
        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        Assert.False(await kontrol.RevaluasyonKayitlari.AnyAsync(r => r.SirketId == env.SirketId));
        Assert.False(await kontrol.YevmiyeFisleri.AnyAsync(f => f.SirketId == env.SirketId));
    }

    private static async Task DenetimBankaBakiyesiAsync(MuhasebeBaglami db, Guid sirketId)
    {
        var banka = new BankaHesabi { MuhasebeKodu = "102", SirketId = sirketId, Kod = "AUD-" + Guid.NewGuid().ToString("N"), Ad = "Denetim Banka", ParaBirimi = "USD", AktifMi = true };
        db.BankaHesaplari.Add(banka);
        await db.SaveChangesAsync();
        db.BankaHareketleri.Add(new BankaHareketi { SirketId = sirketId, BankaHesabiId = banka.Id, Tur = HareketTuru.Gelir, FisTuru = BankaFisTuru.GelenHavale, Tutar = 4000m, DovizTutar = 100m, Kur = 40m,
            ParaBirimiId = await ParaBirimiIdAsync(db, "USD"), Tarih = DateTime.UtcNow.AddDays(-2) });
        db.DovizKurlari.Add(new DovizKuru { ParaBirimiKodu = "USD", Tarih = DateTime.UtcNow.Date, KurTarihi = DateTime.UtcNow.Date, DovizAlis = 42m, DovizSatis = 42m, Kaynak = "MANUEL" });
        await db.SaveChangesAsync();
    }
}
