using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Dto.Muhasebe;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Ay sonu KDV mahsubu ayrı bir firmada geçmiş bir yılda (2025) denenir.
[Collection("PostgreSQL integration")]
public sealed class KdvMahsupTests(IsolatedPostgreSqlFixture fixture)
{
    private const int Yil = 2025;

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
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "KDV Mahsup Test", SirketKodu = $"KM-{Guid.NewGuid():N}"[..10] };
        c.Sirketler.Add(sirket);
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

    private async Task<decimal> BakiyeAsync(Guid sirketId, string kod)
    {
        await using var c = new MuhasebeBaglami(fixture.Options);
        return await c.YevmiyeSatirlari.Where(s => s.Fis!.SirketId == sirketId && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Hesap!.HesapKodu == kod)
            .SumAsync(s => (decimal?)(s.Borc - s.Alacak)) ?? 0m;
    }

    private KdvMahsupServisi Servis(MuhasebeBaglami c) => new(c, TamYetkili());

    [Fact]
    public async Task Indirilecek_fazla_ay_190a_devreder_sonraki_ay_devreden_kullanilip_kalani_360a_yazilir()
    {
        var f = await FirmaAsync();
        // Ocak: 1.000 satış KDV'si, 1.600 alış KDV'si → 600 devreder.
        await FisAsync(f, new(Yil, 1, 10), ("120", 6000, 0), ("600", 0, 5000), ("391", 0, 1000));
        await FisAsync(f, new(Yil, 1, 31, 23, 50, 0), ("153", 8000, 0), ("191", 1600, 0), ("320", 0, 9600)); // ayın son dakikası dahil
        // Şubat: 2.000 satış, 500 alış KDV'si → 1.500 - 600 devreden = 900 ödenecek.
        await FisAsync(f, new(Yil, 2, 5), ("120", 12000, 0), ("600", 0, 10000), ("391", 0, 2000));
        await FisAsync(f, new(Yil, 2, 20), ("770", 2500, 0), ("191", 500, 0), ("100", 0, 3000));

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var o = await Servis(c).OnizleAsync(Yil, 1, f);
            Assert.Empty(o.Engeller);
            Assert.Equal((1000m, 1600m, 0m, 0m, 600m), (o.HesaplananKdv, o.IndirilecekKdv, o.OncekiDevreden, o.OdenecekKdv, o.SonrakiDonemeDevreden));
            Assert.Equal(new[] { ("191", 0m, 1600m), ("391", 1000m, 0m), ("190", 600m, 0m) }, o.Satirlar.Select(s => (s.HesapKodu, s.Borc, s.Alacak)));
            Assert.NotEmpty(await Servis(c).MahsupEtAsync(Yil, 1, f));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Servis(c).MahsupEtAsync(Yil, 1, f));
        }
        Assert.Equal(600m, await BakiyeAsync(f, "190"));

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var o = await Servis(c).OnizleAsync(Yil, 2, f);
            Assert.Equal((2000m, 500m, 600m, 600m, 900m, 0m), (o.HesaplananKdv, o.IndirilecekKdv, o.OncekiDevreden, o.KullanilanDevreden, o.OdenecekKdv, o.SonrakiDonemeDevreden));
            await Servis(c).MahsupEtAsync(Yil, 2, f);
            var yil = await Servis(c).YilAsync(Yil, f);
            Assert.True(yil[0].Yapildi);
            Assert.Equal(600m, yil[0].SonrakiDonemeDevreden);
            Assert.Equal(900m, yil[1].OdenecekKdv);
            Assert.False(yil[2].Yapildi);
        }
        Assert.Equal(0m, await BakiyeAsync(f, "190"));
        Assert.Equal(0m, await BakiyeAsync(f, "191"));
        Assert.Equal(0m, await BakiyeAsync(f, "391"));
        Assert.Equal(-900m, await BakiyeAsync(f, "360"));
    }

    [Fact]
    public async Task Aylar_sirayla_geri_alinir_ve_yeniden_mahsup_edilebilir()
    {
        var f = await FirmaAsync();
        await FisAsync(f, new(Yil, 3, 10), ("120", 1200, 0), ("600", 0, 1000), ("391", 0, 200));
        await FisAsync(f, new(Yil, 4, 10), ("120", 2400, 0), ("600", 0, 2000), ("391", 0, 400));

        await using var c = new MuhasebeBaglami(fixture.Options);
        await Servis(c).MahsupEtAsync(Yil, 3, f);
        await Servis(c).MahsupEtAsync(Yil, 4, f);

        // Mart, nisan mahsubu dururken geri alınamaz ve yeniden yapılamaz.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Servis(c).GeriAlAsync(Yil, 3, f));
        Assert.Contains((await Servis(c).OnizleAsync(Yil, 3, f)).Engeller, e => e.Contains("sonraki"));

        await Servis(c).GeriAlAsync(Yil, 4, f);
        await Servis(c).GeriAlAsync(Yil, 3, f);
        Assert.Equal(-600m, await BakiyeAsync(f, "391"));
        Assert.Equal(0m, await BakiyeAsync(f, "360"));
        Assert.All(await Servis(c).YilAsync(Yil, f), a => Assert.False(a.Yapildi));

        // Mart atlanıp nisan mahsup edilirse martın bakiyesi de girer ve uyarılır.
        var nisan = await Servis(c).OnizleAsync(Yil, 4, f);
        Assert.Equal(600m, nisan.OdenecekKdv);
        Assert.Contains(nisan.Uyarilar, u => u.Contains("mahsup edilmemiş"));
        await Servis(c).MahsupEtAsync(Yil, 4, f);
        Assert.Equal(-600m, await BakiyeAsync(f, "360"));
    }

    [Fact]
    public async Task Bos_ay_ve_bitmemis_ay_mahsup_edilemez_kilitli_donem_engeller()
    {
        var f = await FirmaAsync();
        await using var c = new MuhasebeBaglami(fixture.Options);
        Assert.Contains((await Servis(c).OnizleAsync(Yil, 5, f)).Engeller, e => e.Contains("KDV yok"));
        var buAy = DateTime.Today;
        Assert.Contains((await Servis(c).OnizleAsync(buAy.AddMonths(1).Year, buAy.AddMonths(1).Month, f)).Engeller, e => e.Contains("bitmedi"));

        await FisAsync(f, new(Yil, 6, 10), ("120", 1200, 0), ("600", 0, 1000), ("391", 0, 200));
        c.MaliDonemler.Add(new MaliDonem { SirketId = f, DonemAdi = "2025-06", Baslangic = new DateTime(Yil, 6, 1, 0, 0, 0, DateTimeKind.Utc), Bitis = new DateTime(Yil, 6, 30, 23, 59, 59, DateTimeKind.Utc), KilitliMi = true });
        await c.SaveChangesAsync();
        Assert.Contains((await Servis(c).OnizleAsync(Yil, 6, f)).Engeller, e => e.Contains("kilitli"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Servis(c).MahsupEtAsync(Yil, 6, f));
    }
}
