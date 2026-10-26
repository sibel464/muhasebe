using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Muhasebe;
using Uygulama.Dto.Muhasebe;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class MaliTabloTests(IsolatedPostgreSqlFixture fixture)
{
    private const int Yil = 2023;

    private static Cari360TestKullanici TamYetkili()
    {
        var y = new KullaniciYetkileri();
        foreach (var p in typeof(KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            p.SetValue(y, true);
        return new Cari360TestKullanici { AktifYetkiler = y };
    }

    private async Task FisAsync(Guid sirketId, DateTime tarih, params (string Kod, decimal Borc, decimal Alacak)[] satirlar)
    {
        await using var c = new MuhasebeBaglami(fixture.Options);
        var dto = new YevmiyeFisDto { Tarih = DateTime.SpecifyKind(tarih, DateTimeKind.Utc), Aciklama = "test", Durum = YevmiyeDurumu.Onaylandi };
        foreach (var (kod, b, a) in satirlar)
            dto.Satirlar.Add(new YevmiyeSatiriDto { HesapId = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(c, sirketId, kod), Borc = b, Alacak = a });
        await new YevmiyeServisi(c, TamYetkili()).FisOlusturAsync(dto, sirketId, 0);
    }

    private async Task<Guid> OrnekAsync()
    {
        fixture.RequireDatabase();
        var f = Guid.NewGuid();
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            c.Sirketler.Add(new Sirket { Id = f, Unvan = "Mali Tablo Test", SirketKodu = $"MT-{Guid.NewGuid():N}"[..10] });
            await c.SaveChangesAsync();
        }
        await FisAsync(f, new(Yil, 1, 2), ("100", 10000, 0), ("500", 0, 10000));
        await FisAsync(f, new(Yil, 3, 1), ("153", 8000, 0), ("320", 0, 8000));
        await FisAsync(f, new(Yil, 4, 1), ("120", 12000, 0), ("600", 0, 10000), ("391", 0, 2000));
        await FisAsync(f, new(Yil, 4, 1), ("621", 6000, 0), ("153", 0, 6000));
        await FisAsync(f, new(Yil, 6, 1), ("770", 1000, 0), ("100", 0, 1000));
        await FisAsync(f, new(Yil, 6, 30), ("632", 300, 0), ("771", 0, 300));
        await FisAsync(f, new(Yil, 12, 31), ("691", 500, 0), ("370", 0, 500));
        return f;
    }

    private static decimal Satir(MaliTabloDto t, string ad) => t.Satirlar.Single(s => s.Ad == ad).Tutar;

    private async Task<(MaliTabloDto Bilanco, MaliTabloDto Gelir)> TablolarAsync(Guid f, DateTime bilancoTarihi)
    {
        await using var c = new MuhasebeBaglami(fixture.Options);
        var s = new MaliTabloServisi(c, TamYetkili());
        return (await s.BilancoAsync(bilancoTarihi, f), await s.GelirTablosuAsync(new DateTime(Yil, 1, 1), new DateTime(Yil, 12, 31), f));
    }

    private static void GelirTablosuDogrula(MaliTabloDto g)
    {
        Assert.Equal(10000m, Satir(g, "C. NET SATIŞLAR"));
        Assert.Equal(4000m, Satir(g, "BRÜT SATIŞ KÂRI VEYA ZARARI"));
        Assert.Equal(1000m, g.Satirlar.Single(s => s.Kod == "E" && s.Seviye == 1).Tutar); // 632: 300 elle + 700 yansıtılmamış 770
        Assert.Equal(3000m, Satir(g, "DÖNEM KÂRI VEYA ZARARI"));
        Assert.Equal(2500m, Satir(g, "DÖNEM NET KÂRI VEYA ZARARI"));
    }

    [Fact]
    public async Task Bilanco_dengelidir_donem_sonucunu_hesaplar_ve_kapanistan_once_sonra_ayni_kalir()
    {
        var f = await OrnekAsync();
        var (bilanco, gelir) = await TablolarAsync(f, new DateTime(Yil, 12, 31));
        Assert.Empty(bilanco.Uyarilar);
        Assert.Equal(23000m, Satir(bilanco, "AKTİF (VARLIKLAR) TOPLAMI"));
        Assert.Equal(23000m, Satir(bilanco, "PASİF (KAYNAKLAR) TOPLAMI"));
        Assert.Equal(2500m, Satir(bilanco, "Dönem sonucu (henüz aktarılmamış)"));
        GelirTablosuDogrula(gelir);

        await using (var c = new MuhasebeBaglami(fixture.Options))
            await new YilSonuServisi(c, TamYetkili()).KapatAsync(Yil, f);

        (bilanco, gelir) = await TablolarAsync(f, new DateTime(Yil, 12, 31));
        Assert.Equal(23000m, Satir(bilanco, "AKTİF (VARLIKLAR) TOPLAMI"));
        Assert.Equal(23000m, Satir(bilanco, "PASİF (KAYNAKLAR) TOPLAMI"));
        GelirTablosuDogrula(gelir);

        // Yeni yılda 590 açılıştan gelir; önceki dönem sütunu 31.12 bilançosudur.
        await FisAsync(f, new(Yil + 1, 2, 1), ("100", 400, 0), ("600", 0, 400));
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var yeni = await new MaliTabloServisi(c, TamYetkili()).BilancoAsync(new DateTime(Yil + 1, 3, 31), f);
            Assert.Empty(yeni.Uyarilar);
            Assert.Equal(23400m, Satir(yeni, "AKTİF (VARLIKLAR) TOPLAMI"));
            Assert.Equal(23000m, yeni.Satirlar.Single(s => s.Ad == "AKTİF (VARLIKLAR) TOPLAMI").OncekiTutar);
            Assert.Equal(2500m, yeni.Satirlar.Single(s => s.Kod == "590").Tutar);
            Assert.Equal(400m, Satir(yeni, "Dönem sonucu (henüz aktarılmamış)"));
            Assert.Equal(23400m, Satir(yeni, "PASİF (KAYNAKLAR) TOPLAMI"));
        }
    }

    [Fact]
    public async Task Kapatilmamis_onceki_yil_sonucu_gecmis_yillar_satirinda_gorunur()
    {
        var f = await OrnekAsync();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var b = await new MaliTabloServisi(c, TamYetkili()).BilancoAsync(new DateTime(Yil + 1, 6, 30), f);
        Assert.Equal(2500m, Satir(b, "Kapatılmamış önceki yıllar sonucu"));
        Assert.Equal(Satir(b, "AKTİF (VARLIKLAR) TOPLAMI"), Satir(b, "PASİF (KAYNAKLAR) TOPLAMI"));
        Assert.Contains(b.Uyarilar, u => u.Contains("kapatılmamış"));
    }
}
