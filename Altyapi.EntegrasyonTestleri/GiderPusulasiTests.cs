using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Modeller.Ortak;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class GiderPusulasiTests(IsolatedPostgreSqlFixture fixture)
{
    private static Cari360TestKullanici TamYetkili()
    {
        var y = new KullaniciYetkileri();
        foreach (var p in typeof(KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            p.SetValue(y, true);
        return new Cari360TestKullanici { AktifYetkiler = y };
    }

    private async Task<(Guid Firma, int KasaId, int BankaId)> FirmaAsync()
    {
        fixture.RequireDatabase();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var id = Guid.NewGuid();
        c.Sirketler.Add(new Sirket { Id = id, Unvan = "Gider Pusulası Test", SirketKodu = $"GP-{Guid.NewGuid():N}"[..10] });
        var kasa = new Kasa { SirketId = id, Kod = "K1", Ad = "Merkez Kasa" };
        c.Kasalar.Add(kasa);
        var banka = new BankaHesabi { SirketId = id, Kod = "B1", Ad = "Vadesiz", HesapNo = "1", HesapTuru = BankaHesapTuru.VadesizTL };
        c.BankaHesaplari.Add(banka);
        await c.SaveChangesAsync();
        var acilis = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        c.KasaHareketleri.Add(new KasaHareketi { SirketId = id, KasaId = kasa.Id, Tur = HareketTuru.Gelir, FisTuru = KasaFisTuru.AcilisDevir, Tutar = 50000, Tarih = acilis, Aciklama = "Açılış" });
        c.BankaHareketleri.Add(new BankaHareketi { SirketId = id, BankaHesabiId = banka.Id, BankaAdi = "Vadesiz", HesapNo = "1", Tur = HareketTuru.Gelir, FisTuru = BankaFisTuru.GelenHavale, Tutar = 50000, Tarih = acilis, Aciklama = "Açılış" });
        await c.SaveChangesAsync();
        return (id, kasa.Id, banka.Id);
    }

    private async Task<decimal> BakiyeAsync(Guid f, string kod)
    {
        await using var c = new MuhasebeBaglami(fixture.Options);
        return await c.YevmiyeSatirlari.Where(s => s.Fis!.SirketId == f && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Hesap!.HesapKodu == kod)
            .SumAsync(s => (decimal?)(s.Borc - s.Alacak)) ?? 0m;
    }

    [Fact]
    public async Task Gider_pusulasi_stopajla_fislenir_odenir_ve_iptal_edilir()
    {
        var (f, kasaId, bankaId) = await FirmaAsync();
        var tarih = DateTime.Today.AddDays(-3);
        GiderPusulasi gp, mm;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new GiderPusulasiServisi(c, TamYetkili());
            GiderPusulasiGirisDto Giris() => new()
            {
                Tarih = tarih, SaticiAdSoyad = "Ali Usta", SaticiTcKimlikNo = "10000000146", GiderHesapKodu = "770", StopajOrani = 10,
                OdemeKaynagi = GiderPusulasiServisi.Kasa, OdemeHesabiId = kasaId,
                Kalemler = { new() { Cinsi = "Dükkân boyası işçiliği", Miktar = 1, BirimFiyat = 4000 }, new() { Cinsi = "Kapı tamiri", Miktar = 2, BirimFiyat = 500 } }
            };
            var hatali = Giris(); hatali.SaticiTcKimlikNo = "12345678901";
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.KaydetAsync(hatali, f));
            var bos = Giris(); bos.Kalemler.Clear();
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.KaydetAsync(bos, f));
            var fazla = Giris(); fazla.Kalemler[0].BirimFiyat = 100000; // kasada 50.000 var
            await Assert.ThrowsAnyAsync<Exception>(() => s.KaydetAsync(fazla, f));

            gp = await s.KaydetAsync(Giris(), f);
            Assert.Equal($"GP-{tarih.Year}-00001", gp.BelgeNo);
            Assert.Equal(5000m, gp.BrutTutar);
            Assert.Equal(500m, gp.StopajTutari);
            Assert.Equal(4500m, gp.NetTutar);

            mm = await s.KaydetAsync(new GiderPusulasiGirisDto
            {
                Tur = GiderPusulasiTuru.MustahsilMakbuzu, Tarih = tarih, SaticiAdSoyad = "Hasan Çiftçi", GiderHesapKodu = "153", StopajOrani = 2,
                OdemeKaynagi = GiderPusulasiServisi.Banka, OdemeHesabiId = bankaId,
                Kalemler = { new() { Cinsi = "Buğday", Miktar = 1000, Birim = "Kg", BirimFiyat = 12.5m } }
            }, f);
            Assert.Equal($"MM-{tarih.Year}-00001", mm.BelgeNo);
            Assert.Equal(250m, mm.StopajTutari);
        }
        Assert.Equal(5000m, await BakiyeAsync(f, "770"));
        Assert.Equal(12500m, await BakiyeAsync(f, "153"));
        Assert.Equal(-750m, await BakiyeAsync(f, "360"));
        Assert.Equal(0m, await BakiyeAsync(f, "336"));
        Assert.Equal(-4500m, await BakiyeAsync(f, "100"));
        Assert.Equal(-12250m, await BakiyeAsync(f, "102"));

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            // Kasa ve banka ekranından silinemez
            var kasaHareketi = await c.KasaHareketleri.SingleAsync(h => h.Id == gp.KasaHareketiId);
            Assert.Equal(GiderPusulasiServisi.OzelKod, kasaHareketi.OzelKod);
            await Assert.ThrowsAsync<InvalidOperationException>(() => new KasaServisi(c, TamYetkili()).SilAsync(kasaHareketi.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(() => new BankaServisi(c, TamYetkili()).SilAsync(mm.BankaHareketiId!.Value));

            var s = new GiderPusulasiServisi(c, TamYetkili());
            var liste = await s.ListeAsync(tarih.AddDays(-1), DateTime.Today, true, f);
            Assert.Equal(2, liste.Count);
            Assert.Equal("Kasa: Merkez Kasa", liste.Single(x => x.Id == gp.Id).OdemeHesabi);
            await s.IptalAsync(gp.Id, "Yanlış girildi", f);
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.IptalAsync(gp.Id, "tekrar", f));
            Assert.True((await s.ListeAsync(tarih.AddDays(-1), DateTime.Today, true, f)).Single(x => x.Id == gp.Id).IptalEdildi);
            Assert.Single(await s.ListeAsync(tarih.AddDays(-1), DateTime.Today, false, f));

            // İptal sonrası numara yeniden verilmez
            var yeni = await s.KaydetAsync(new GiderPusulasiGirisDto
            {
                Tarih = tarih, SaticiAdSoyad = "Veli", StopajOrani = 0, OdemeKaynagi = GiderPusulasiServisi.Kasa, OdemeHesabiId = kasaId,
                Kalemler = { new() { Cinsi = "Hurda", Miktar = 1, BirimFiyat = 100 } }
            }, f);
            Assert.Equal($"GP-{tarih.Year}-00002", yeni.BelgeNo);

            var tam = await s.GetirAsync(gp.Id, f);
            var html = System.Net.WebUtility.HtmlDecode(new BelgeYazdirmaServisi().GiderPusulasiHtmlUret(tam!, new FirmaBilgisi { FirmaUnvani = "Test A.Ş." }));
            Assert.Contains("Gider Pusulası", html);
            Assert.Contains("Dükkân boyası işçiliği", html);
            Assert.Contains("İPTAL EDİLMİŞTİR", html);
            Assert.Contains("4.500,00", html);
        }
        Assert.Equal(100m, await BakiyeAsync(f, "770"));
        Assert.Equal(-250m, await BakiyeAsync(f, "360"));
        Assert.Equal(0m, await BakiyeAsync(f, "336"));
        Assert.Equal(-100m, await BakiyeAsync(f, "100"));
    }

    [Fact]
    public async Task Kira_ve_serbest_meslek_stopaj_ve_kdv_ile_fislenir_kvkk_anonimlestirir()
    {
        var (f, kasaId, bankaId) = await FirmaAsync();
        var tarih = DateTime.Today.AddDays(-1);
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new GiderPusulasiServisi(c, TamYetkili());
            var kira = await s.KaydetAsync(new GiderPusulasiGirisDto
            {
                Tur = GiderPusulasiTuru.KiraOdemesi, Tarih = tarih, SaticiAdSoyad = "Fatma Ev Sahibi", StopajOrani = 20,
                OdemeKaynagi = GiderPusulasiServisi.Banka, OdemeHesabiId = bankaId, Kalemler = { new() { Cinsi = "Ekim kirası", Birim = "Ay", BirimFiyat = 10000 } }
            }, f);
            Assert.StartsWith($"KR-{tarih.Year}-", kira.BelgeNo);
            Assert.Equal(8000m, kira.NetTutar);

            // KDV yalnız serbest meslekte; makbuz no zorunlu
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.KaydetAsync(new GiderPusulasiGirisDto
            {
                Tur = GiderPusulasiTuru.KiraOdemesi, Tarih = tarih, SaticiAdSoyad = "X", KdvOrani = 20, OdemeKaynagi = GiderPusulasiServisi.Kasa, OdemeHesabiId = kasaId,
                Kalemler = { new() { Cinsi = "a", BirimFiyat = 100 } }
            }, f));
            var smmGiris = new GiderPusulasiGirisDto
            {
                Tur = GiderPusulasiTuru.SerbestMeslekMakbuzu, Tarih = tarih, SaticiAdSoyad = "Av. Kemal Hukuk", StopajOrani = 20, KdvOrani = 20,
                OdemeKaynagi = GiderPusulasiServisi.Kasa, OdemeHesabiId = kasaId, Kalemler = { new() { Cinsi = "Danışmanlık ücreti", Birim = "Hizmet", BirimFiyat = 5000 } }
            };
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.KaydetAsync(smmGiris, f));
            smmGiris.KarsiBelgeNo = "SMM2026000000123";
            var smm = await s.KaydetAsync(smmGiris, f);
            Assert.Equal(1000m, smm.StopajTutari);
            Assert.Equal(1000m, smm.KdvTutari);
            Assert.Equal(5000m, smm.NetTutar);

            var tam = await s.GetirAsync(smm.Id, f);
            var html = System.Net.WebUtility.HtmlDecode(new BelgeYazdirmaServisi().GiderPusulasiHtmlUret(tam!, new FirmaBilgisi { FirmaUnvani = "Test" }));
            Assert.Contains("SMM2026000000123", html);
            Assert.Contains("KDV %20", html);

            // 11 yıl önceki belge anonimleştirilir, bugünkü kalır
            var eski = new GiderPusulasi
            {
                SirketId = f, BelgeNo = "GP-ESKI-1", Tarih = new DateTime(DateTime.Today.Year - 11, 6, 1, 0, 0, 0, DateTimeKind.Utc), SaticiAdSoyad = "Eski Satıcı",
                SaticiTcKimlikNo = "10000000146", SaticiAdres = "Adres", BrutTutar = 10, NetTutar = 10
            };
            c.GiderPusulalari.Add(eski);
            await c.SaveChangesAsync();
            Assert.Equal(1, await s.SuresiDolanlariAnonimlestirAsync(DateTime.Today, f));
            Assert.Equal(0, await s.SuresiDolanlariAnonimlestirAsync(DateTime.Today, f));
            var anonim = await s.GetirAsync(eski.Id, f);
            Assert.Equal(GiderPusulasiServisi.AnonimAd, anonim!.SaticiAdSoyad);
            Assert.Null(anonim.SaticiTcKimlikNo);
            Assert.Equal("Fatma Ev Sahibi", (await s.GetirAsync(kira.Id, f))!.SaticiAdSoyad);
        }
        Assert.Equal(15000m, await BakiyeAsync(f, "770"));
        Assert.Equal(1000m, await BakiyeAsync(f, "191"));
        Assert.Equal(-3000m, await BakiyeAsync(f, "360"));
        Assert.Equal(0m, await BakiyeAsync(f, "336"));
        Assert.Equal(-5000m, await BakiyeAsync(f, "100"));
        Assert.Equal(-8000m, await BakiyeAsync(f, "102"));
    }

    [Fact]
    public async Task Yetkisiz_kullanici_gider_pusulasi_kesemez()
    {
        var (f, kasaId, _) = await FirmaAsync();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var s = new GiderPusulasiServisi(c, new Cari360TestKullanici { AktifYetkiler = new KullaniciYetkileri { FaturaGoruntule = true, FaturaOlustur = true } });
        Assert.Empty(await s.ListeAsync(DateTime.Today.AddYears(-1), DateTime.Today, true, f));
        await Assert.ThrowsAnyAsync<Exception>(() => s.KaydetAsync(new GiderPusulasiGirisDto
        {
            SaticiAdSoyad = "X", OdemeKaynagi = GiderPusulasiServisi.Kasa, OdemeHesabiId = kasaId, Kalemler = { new() { Cinsi = "a", BirimFiyat = 10 } }
        }, f));
    }
}
