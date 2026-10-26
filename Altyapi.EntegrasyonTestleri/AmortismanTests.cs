using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Yardimcilar;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class AmortismanHesaplayiciTests
{
    private static SabitKiymet K(decimal maliyet, int omur, DateTime alis, AmortismanYontemi y = AmortismanYontemi.Normal, bool kist = false) =>
        new() { Maliyet = maliyet, FaydaliOmurYil = omur, AlisTarihi = alis, Yontem = y, KistAmortisman = kist };

    private static decimal[] Tutarlar(SabitKiymet k) => AmortismanHesaplayici.Plan(k).Where(x => !x.Devir).Select(x => x.Tutar).ToArray();

    [Fact]
    public void Normal_yontem_esit_tutar_ve_kalan_son_yila()
    {
        var plan = AmortismanHesaplayici.Plan(K(10000, 5, new DateTime(2024, 3, 15)));
        Assert.Equal(new[] { 2024, 2025, 2026, 2027, 2028 }, plan.Select(x => x.Yil));
        Assert.All(plan, x => Assert.Equal(2000m, x.Tutar));
        Assert.Equal(0m, plan[^1].NetDeger);
        Assert.Equal(new[] { 333.33m, 333.33m, 333.34m }, Tutarlar(K(1000, 3, new DateTime(2024, 1, 1))));
    }

    [Fact]
    public void Kist_amortisman_ilk_yil_ay_kesri_tam_ay_kalani_omur_sonrasi_yila()
    {
        var plan = AmortismanHesaplayici.Plan(K(120000, 5, new DateTime(2024, 4, 10), kist: true));
        Assert.Equal(new[] { 18000m, 24000m, 24000m, 24000m, 24000m, 6000m }, plan.Select(x => x.Tutar));
        Assert.Equal(2029, plan[^1].Yil);
        // Ocak alımında kıst fark yaratmaz
        Assert.Equal(5, AmortismanHesaplayici.Plan(K(120000, 5, new DateTime(2024, 1, 20), kist: true)).Count);
    }

    [Fact]
    public void Azalan_bakiyeler_iki_kat_oran_en_cok_yuzde_elli_son_yil_kalan()
    {
        Assert.Equal(new[] { 4000m, 2400m, 1440m, 864m, 1296m }, Tutarlar(K(10000, 5, new DateTime(2024, 6, 1), AmortismanYontemi.AzalanBakiyeler)));
        Assert.Equal(new[] { 5000m, 2500m, 2500m }, Tutarlar(K(10000, 3, new DateTime(2024, 6, 1), AmortismanYontemi.AzalanBakiyeler)));
    }

    [Fact]
    public void Devir_ve_elden_cikarma()
    {
        var k = K(10000, 5, new DateTime(2022, 5, 1));
        k.DevirBirikmisAmortisman = 4000;
        k.DevirSonYil = 2023;
        var plan = AmortismanHesaplayici.Plan(k);
        Assert.True(plan[0].Devir);
        Assert.Equal(new[] { 2024, 2025, 2026 }, plan.Where(x => !x.Devir).Select(x => x.Yil));
        Assert.Equal(0m, plan[^1].NetDeger);

        k.EldenCikarmaTarihi = new DateTime(2025, 3, 1);
        Assert.Equal(new[] { 2024 }, AmortismanHesaplayici.Plan(k).Where(x => !x.Devir).Select(x => x.Yil));
        Assert.Equal(0m, AmortismanHesaplayici.AyrilacakTutar(k, 2025));
    }
}

[Collection("PostgreSQL integration")]
public sealed class AmortismanKayitTests(IsolatedPostgreSqlFixture fixture)
{
    private static Cari360TestKullanici TamYetkili()
    {
        var y = new KullaniciYetkileri();
        foreach (var p in typeof(KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            p.SetValue(y, true);
        return new Cari360TestKullanici { AktifYetkiler = y };
    }

    private async Task<Guid> FirmaAsync()
    {
        fixture.RequireDatabase();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var id = Guid.NewGuid();
        c.Sirketler.Add(new Sirket { Id = id, Unvan = "Amortisman Test", SirketKodu = $"AM-{Guid.NewGuid():N}"[..10] });
        await c.SaveChangesAsync();
        return id;
    }

    private async Task<decimal> BakiyeAsync(Guid f, string kod)
    {
        await using var c = new MuhasebeBaglami(fixture.Options);
        return await c.YevmiyeSatirlari.Where(s => s.Fis!.SirketId == f && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Hesap!.HesapKodu == kod)
            .SumAsync(s => (decimal?)(s.Borc - s.Alacak)) ?? 0m;
    }

    [Fact]
    public async Task Yillik_amortisman_fisi_kesilir_geri_alinir_yeniden_ayrilir_ve_elden_cikarma_varligi_kapatir()
    {
        var f = await FirmaAsync();
        int id;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new SabitKiymetServisi(c, TamYetkili());
            id = (await s.KaydetAsync(new SabitKiymet { Ad = "Dizüstü bilgisayar", Maliyet = 10000, FaydaliOmurYil = 5, AlisTarihi = new DateTime(2022, 3, 1) }, f)).Id;
            await s.KaydetAsync(new SabitKiymet { Ad = "Masa", Maliyet = 3000, FaydaliOmurYil = 3, AlisTarihi = new DateTime(2022, 7, 1), GiderHesapKodu = "760" }, f);

            var o = await s.YilOnizleAsync(2022, f);
            Assert.Empty(o.Engeller);
            Assert.Equal(3000m, o.Toplam);
            await s.AmortismanAyirAsync(2022, f);
        }
        Assert.Equal(2000m, await BakiyeAsync(f, "770"));
        Assert.Equal(1000m, await BakiyeAsync(f, "760"));
        Assert.Equal(-3000m, await BakiyeAsync(f, "257"));

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new SabitKiymetServisi(c, TamYetkili());
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.AmortismanAyirAsync(2022, f)); // tekrar ayrılacak yok
            var k = (await s.GetirAsync(id, f))!;
            k.Maliyet = 12000;
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.KaydetAsync(k, f)); // ayrılmış amortismanda maliyet değişmez
            Assert.Equal(new[] { "DMR-0001", "DMR-0002" }, (await s.ListeAsync(false, f)).Select(x => x.Kod));

            await s.AmortismanGeriAlAsync(2022, f);
        }
        Assert.Equal(0m, await BakiyeAsync(f, "770"));
        Assert.Equal(0m, await BakiyeAsync(f, "257"));

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new SabitKiymetServisi(c, TamYetkili());
            Assert.Equal(3000m, (await s.YilOnizleAsync(2022, f)).Toplam);
            await s.AmortismanAyirAsync(2022, f);
            await s.AmortismanAyirAsync(2023, f);
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.EldenCikarAsync(id, new DateTime(2023, 5, 1), "", f)); // çıkış yılı amortismanı ayrılmış
            await s.EldenCikarAsync(id, new DateTime(2024, 5, 1), "Satıldı", f);
            Assert.Equal(0m, (await s.YilOnizleAsync(2024, f)).Kalemler.Count(k => k.SabitKiymetId == id));
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.AmortismanGeriAlAsync(2023, f)); // sonradan çıkarılan demirbaşın yılı
        }
        // 257: masa 1000+1000, bilgisayar 2000+2000 ayrıldı; bilgisayarın 4000'i çıkışta kapandı
        Assert.Equal(-2000m, await BakiyeAsync(f, "257"));
        Assert.Equal(6000m, await BakiyeAsync(f, "689"));
        Assert.Equal(-10000m, await BakiyeAsync(f, "255"));
    }

    [Fact]
    public async Task Yil_sonu_kapanisi_ayrilmamis_amortismani_uyarir()
    {
        var f = await FirmaAsync();
        await using var c = new MuhasebeBaglami(fixture.Options);
        await new SabitKiymetServisi(c, TamYetkili()).KaydetAsync(new SabitKiymet { Ad = "Yazıcı", Maliyet = 5000, FaydaliOmurYil = 5, AlisTarihi = new DateTime(2022, 2, 1) }, f);
        var o = await new YilSonuServisi(c, TamYetkili()).OnizleAsync(2022, f);
        Assert.Contains(o.Uyarilar, u => u.Contains("amortismanı ayrılmamış"));
    }
}
