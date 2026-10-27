using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Dto.Muhasebe;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Yıl sonu kapanışı ayrı bir firmada geçmiş bir yıl (2024) üzerinde denenir.
[Collection("PostgreSQL integration")]
public sealed class YilSonuKapanisTests(IsolatedPostgreSqlFixture fixture)
{
    private const int Yil = 2024;

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
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Yıl Sonu Test", SirketKodu = $"YS-{Guid.NewGuid():N}"[..10] };
        c.Sirketler.Add(sirket);
        foreach (var (kod, ad, tip) in new[] { ("691", "DÖNEM KARI VERGİ VE DİĞER YASAL YÜKÜMLÜLÜK KARŞILIKLARI (-)", HesapTipi.Gider), ("370", "DÖNEM KARI VERGİ KARŞILIKLARI", HesapTipi.Pasif) })
            c.HesapPlanlari.Add(new HesapPlani { SirketId = sirket.Id, HesapKodu = kod, HesapAdi = ad, Tip = tip, AktifMi = true });
        await c.SaveChangesAsync();
        return sirket.Id;
    }

    private async Task FisAsync(Guid sirketId, DateTime tarih, params (string Kod, decimal Borc, decimal Alacak)[] satirlar)
    {
        await using var c = new MuhasebeBaglami(fixture.Options);
        var dto = new YevmiyeFisDto { Tarih = DateTime.SpecifyKind(tarih, DateTimeKind.Utc), Aciklama = "test", Durum = YevmiyeDurumu.Onaylandi };
        foreach (var (kod, b, a) in satirlar)
            dto.Satirlar.Add(new YevmiyeSatiriDto { HesapId = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(c, sirketId, kod), Borc = b, Alacak = a });
        await new YevmiyeServisi(c, TamYetkili()).FisOlusturAsync(dto, sirketId, 0);
    }

    private async Task<decimal> BakiyeAsync(Guid sirketId, string kod, DateTime once)
    {
        await using var c = new MuhasebeBaglami(fixture.Options);
        var sinir = DateTime.SpecifyKind(once, DateTimeKind.Utc);
        return await c.YevmiyeSatirlari.Where(s => s.Fis!.SirketId == sirketId && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih < sinir && s.Hesap!.HesapKodu == kod)
            .SumAsync(s => (decimal?)(s.Borc - s.Alacak)) ?? 0m;
    }

    private async Task<Guid> OrnekYilAsync()
    {
        var f = await FirmaAsync();
        await FisAsync(f, new(Yil, 1, 2), ("100", 10000, 0), ("500", 0, 10000));
        await FisAsync(f, new(Yil, 3, 1), ("153", 8000, 0), ("320", 0, 8000));
        await FisAsync(f, new(Yil, 4, 1), ("120", 12000, 0), ("600", 0, 10000), ("391", 0, 2000));
        await FisAsync(f, new(Yil, 4, 1), ("621", 6000, 0), ("153", 0, 6000));
        await FisAsync(f, new(Yil, 6, 1), ("770", 1000, 0), ("100", 0, 1000));
        await FisAsync(f, new(Yil, 6, 30), ("632", 300, 0), ("771", 0, 300));   // yıl içinde elle yapılmış kısmi yansıtma
        await FisAsync(f, new(Yil, 12, 31), ("691", 500, 0), ("370", 0, 500));
        return f;
    }

    [Fact]
    public async Task Onizleme_yansitmayi_cift_saymaz_kar_ve_vergiyi_dogru_hesaplar()
    {
        var f = await OrnekYilAsync();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var o = await new YilSonuServisi(c, TamYetkili()).OnizleAsync(Yil, f);

        Assert.Empty(o.Engeller);
        Assert.False(o.Kapatildi);
        var y = Assert.Single(o.Yansitmalar);
        Assert.Equal(("770", "771", "632", 700m), (y.GiderHesabi, y.YansitmaHesabi, y.HedefHesap, y.Tutar));
        Assert.Equal(10000m, o.GelirToplami);
        Assert.Equal(7000m, o.GiderToplami);   // 621 6.000 + 632 (300 + 700)
        Assert.Equal(3000m, o.DonemKari);
        Assert.Equal(500m, o.VergiKarsiligi);
        Assert.Equal(2500m, o.NetKar);
    }

    [Fact]
    public async Task Kapanis_gelir_gider_hesaplarini_sifirlar_kari_590a_aktarir_bilancoyu_yeni_yila_devreder_ve_geri_alinir()
    {
        var f = await OrnekYilAsync();
        var yilSonu = new DateTime(Yil, 12, 31, 23, 59, 59);
        var yeniYil = new DateTime(Yil + 1, 1, 2);
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var sonuc = await new YilSonuServisi(c, TamYetkili()).KapatAsync(Yil, f);
            Assert.Equal(5, sonuc.FisNolari.Count);
            Assert.Equal(2500m, sonuc.NetKar);
        }

        foreach (var kod in new[] { "600", "621", "632", "690", "691", "692", "770", "771" })
            Assert.Equal(0m, await BakiyeAsync(f, kod, yeniYil));
        // 31.12 kapanışından sonra bilanço hesapları sıfır, 1 Ocak açılışıyla geri gelir.
        Assert.Equal(0m, await BakiyeAsync(f, "100", yilSonu.AddSeconds(1)));
        Assert.Equal(9000m, await BakiyeAsync(f, "100", yeniYil));
        Assert.Equal(-2500m, await BakiyeAsync(f, "590", yeniYil));
        Assert.Equal(-500m, await BakiyeAsync(f, "370", yeniYil));
        Assert.Equal(2000m, await BakiyeAsync(f, "153", yeniYil));

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var fisler = await c.YevmiyeFisleri.Include(x => x.Satirlar).Where(x => x.SirketId == f && x.BelgeTipi.StartsWith("YILSONU_")).ToListAsync();
            Assert.All(fisler, x => Assert.Equal(x.Satirlar.Sum(s => s.Borc), x.Satirlar.Sum(s => s.Alacak)));
            Assert.Contains(fisler, x => x.FisTuru == FisTuru.Kapanis);
            Assert.Contains(fisler, x => x.FisTuru == FisTuru.Acilis && x.Tarih.Year == Yil + 1);

            var tekrar = await new YilSonuServisi(c, TamYetkili()).OnizleAsync(Yil, f);
            Assert.True(tekrar.Kapatildi);
            await Assert.ThrowsAsync<InvalidOperationException>(() => new YilSonuServisi(c, TamYetkili()).KapatAsync(Yil, f));
        }

        await using (var c = new MuhasebeBaglami(fixture.Options))
            await new YilSonuServisi(c, TamYetkili()).GeriAlAsync(Yil, f);

        Assert.Equal(1000m, await BakiyeAsync(f, "770", yeniYil));
        Assert.Equal(-10000m, await BakiyeAsync(f, "600", yeniYil));
        Assert.Equal(0m, await BakiyeAsync(f, "590", yeniYil));
        Assert.Equal(9000m, await BakiyeAsync(f, "100", yilSonu.AddSeconds(1)));
        await using (var c = new MuhasebeBaglami(fixture.Options))
            Assert.False((await new YilSonuServisi(c, TamYetkili()).OnizleAsync(Yil, f)).Kapatildi);

        // Geri alınan yıl yeniden kapatılabilmeli (aynı belge tipli fişler benzersiz indekse takılmamalı).
        await using (var c = new MuhasebeBaglami(fixture.Options))
            await new YilSonuServisi(c, TamYetkili()).KapatAsync(Yil, f);
        Assert.Equal(0m, await BakiyeAsync(f, "770", yeniYil));
        Assert.Equal(-2500m, await BakiyeAsync(f, "590", yeniYil.AddDays(1)));
    }

    [Fact]
    public async Task Zarar_591e_aktarilir()
    {
        var f = await FirmaAsync();
        await FisAsync(f, new(Yil, 1, 2), ("100", 1000, 0), ("500", 0, 1000));
        await FisAsync(f, new(Yil, 5, 2), ("770", 400, 0), ("100", 0, 400));
        await using (var c = new MuhasebeBaglami(fixture.Options))
            await new YilSonuServisi(c, TamYetkili()).KapatAsync(Yil, f);
        Assert.Equal(400m, await BakiyeAsync(f, "591", new DateTime(Yil + 1, 1, 2)));
        Assert.Equal(0m, await BakiyeAsync(f, "590", new DateTime(Yil + 1, 1, 2)));
    }

    [Fact]
    public async Task Taslak_fis_kilitli_donem_ve_bitmemis_yil_kapanisi_durdurur()
    {
        var f = await FirmaAsync();
        await FisAsync(f, new(Yil, 1, 2), ("100", 1000, 0), ("600", 0, 1000));
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var dto = new YevmiyeFisDto { Tarih = DateTime.SpecifyKind(new DateTime(Yil, 2, 1), DateTimeKind.Utc), Durum = YevmiyeDurumu.Taslak, Aciklama = "taslak" };
            dto.Satirlar.Add(new YevmiyeSatiriDto { HesapId = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(c, f, "100"), Borc = 5 });
            dto.Satirlar.Add(new YevmiyeSatiriDto { HesapId = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(c, f, "600"), Alacak = 5 });
            await new YevmiyeServisi(c, TamYetkili()).FisOlusturAsync(dto, f, 0);
            c.MaliDonemler.Add(new MaliDonem { SirketId = f, DonemAdi = "2025", Baslangic = new DateTime(Yil + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc), Bitis = new DateTime(Yil + 1, 12, 31, 23, 59, 59, DateTimeKind.Utc), KilitliMi = true });
            await c.SaveChangesAsync();
        }
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var servis = new YilSonuServisi(c, TamYetkili());
            var o = await servis.OnizleAsync(Yil, f);
            Assert.Contains(o.Engeller, e => e.Contains("taslak"));
            Assert.Contains(o.Engeller, e => e.Contains("kilitli"));
            Assert.False(o.KapatilabilirMi);
            await Assert.ThrowsAsync<InvalidOperationException>(() => servis.KapatAsync(Yil, f));

            Assert.Contains((await servis.OnizleAsync(DateTime.Today.Year + 1, f)).Engeller, e => e.Contains("henüz bitmedi"));
        }
    }
}
