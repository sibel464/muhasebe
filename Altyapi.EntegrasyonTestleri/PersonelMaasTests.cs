using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Modeller.Ortak;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class PersonelMaasTests(IsolatedPostgreSqlFixture fixture)
{
    private static Cari360TestKullanici TamYetkili()
    {
        var y = new KullaniciYetkileri();
        foreach (var p in typeof(KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            p.SetValue(y, true);
        return new Cari360TestKullanici { AktifYetkiler = y };
    }

    private async Task<(Guid Firma, int KasaId)> FirmaAsync()
    {
        fixture.RequireDatabase();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var id = Guid.NewGuid();
        c.Sirketler.Add(new Sirket { Id = id, Unvan = "Personel Test", SirketKodu = $"PR-{Guid.NewGuid():N}"[..10] });
        var kasa = new Kasa { SirketId = id, Kod = "K1", Ad = "Merkez Kasa" };
        c.Kasalar.Add(kasa);
        await c.SaveChangesAsync();
        c.KasaHareketleri.Add(new KasaHareketi
        {
            SirketId = id, KasaId = kasa.Id, Tur = HareketTuru.Gelir, FisTuru = KasaFisTuru.AcilisDevir, Tutar = 200000,
            Tarih = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), Aciklama = "Açılış"
        });
        await c.SaveChangesAsync();
        return (id, kasa.Id);
    }

    private async Task<decimal> BakiyeAsync(Guid f, string kod)
    {
        await using var c = new MuhasebeBaglami(fixture.Options);
        return await c.YevmiyeSatirlari.Where(s => s.Fis!.SirketId == f && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Hesap!.HesapKodu == kod)
            .SumAsync(s => (decimal?)(s.Borc - s.Alacak)) ?? 0m;
    }

    private static DateTime T(int ay, int gun) => new(2025, ay, gun);

    [Fact]
    public async Task Tahakkuk_avans_mahsubu_odeme_iptal_geri_alma_ve_vergi_odemesi()
    {
        var (f, kasaId) = await FirmaAsync();
        int a, b;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new PersonelServisi(c, TamYetkili());
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.KaydetAsync(new Personel { AdSoyad = "Hatalı", TcKimlikNo = "12345678901", IseGirisTarihi = T(1, 1) }, f));
            a = (await s.KaydetAsync(new Personel
            {
                AdSoyad = "Ayşe Yılmaz", TcKimlikNo = "10000000146", IseGirisTarihi = T(1, 1), NetUcret = 20000, SgkIsciPayi = 3000, IssizlikIsciPayi = 200,
                GelirVergisi = 1500, DamgaVergisi = 150, SgkIsverenPayi = 4000, IssizlikIsverenPayi = 400, Iban = "TR33 0006 1005 1978 6457 8413 26"
            }, f)).Id;
            b = (await s.KaydetAsync(new Personel { AdSoyad = "Burak Demir", IseGirisTarihi = T(2, 1), NetUcret = 15000, GiderHesapKodu = "760" }, f)).Id;
            // Nisanda başlayan, mart tahakkukunda görünmez
            await s.KaydetAsync(new Personel { AdSoyad = "Can Öz", IseGirisTarihi = T(4, 1), NetUcret = 10000 }, f);

            await s.AvansVerAsync(a, 5000, T(3, 10), PersonelServisi.Kasa, kasaId, null, f);
            var d = await s.DonemAsync(2025, 3, f);
            Assert.Empty(d.Engeller);
            Assert.Equal(new[] { "Ayşe Yılmaz", "Burak Demir" }, d.Bekleyenler.Select(x => x.AdSoyad));
            Assert.Equal(5000m, d.Bekleyenler[0].AvansKesintisi);
            await s.TahakkukEtAsync(2025, 3, d.Bekleyenler, f);
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.TahakkukEtAsync(2025, 3, d.Bekleyenler.Take(1).ToList(), f));
        }
        Assert.Equal(29250m, await BakiyeAsync(f, "770"));
        Assert.Equal(15000m, await BakiyeAsync(f, "760"));
        Assert.Equal(-30000m, await BakiyeAsync(f, "335"));
        Assert.Equal(0m, await BakiyeAsync(f, "196"));
        Assert.Equal(-1650m, await BakiyeAsync(f, "360"));
        Assert.Equal(-7600m, await BakiyeAsync(f, "361"));

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new PersonelServisi(c, TamYetkili());
            var liste = await s.ListeAsync(false, f);
            Assert.Equal(15000m, liste.Single(x => x.Id == a).OdenecekBakiye);
            Assert.Equal(0m, liste.Single(x => x.Id == a).AvansBakiye);
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.OdemeYapAsync(a, 20000, T(4, 1), PersonelServisi.Kasa, kasaId, null, f));
            await s.OdemeYapAsync(a, 15000, T(4, 1), PersonelServisi.Kasa, kasaId, null, f);
            Assert.Equal(1, await s.TopluOdemeAsync(T(4, 1), PersonelServisi.Kasa, kasaId, f));
            Assert.Equal(0m, (await s.EkstreAsync(a, f))[^1].Bakiye);
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.TahakkukGeriAlAsync(2025, 3, f)); // ödenmiş maaş
        }
        Assert.Equal(0m, await BakiyeAsync(f, "335"));
        Assert.Equal(-35000m, await BakiyeAsync(f, "100"));

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new PersonelServisi(c, TamYetkili());
            // Kasa ekranından silinemez
            var odemeKasa = await c.KasaHareketleri.FirstAsync(h => h.SirketId == f && h.OzelKod == PersonelServisi.PersonelOzelKodu && h.Tutar == 15000);
            var kasaServisi = new KasaServisi(c, TamYetkili());
            await Assert.ThrowsAsync<InvalidOperationException>(() => kasaServisi.SilAsync(odemeKasa.Id));

            foreach (var p in new[] { a, b })
                foreach (var satir in (await s.EkstreAsync(p, f)).Where(x => x.Islem == "Maaş ödemesi"))
                    await s.HareketIptalAsync(satir.Id, "Yanlış girildi", f);
            var avansId = (await s.EkstreAsync(a, f)).Single(x => x.Islem == "Avans").Id;
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.HareketIptalAsync(avansId, "", f)); // maaştan düşülmüş avans
            await s.TahakkukGeriAlAsync(2025, 3, f);
            await s.HareketIptalAsync(avansId, "", f);
            Assert.Equal(200000m, (await s.OdemeHesaplariAsync(f)).Single(x => x.Id == kasaId && x.Kaynak == PersonelServisi.Kasa).Bakiye);
        }
        foreach (var kod in new[] { "770", "760", "335", "196", "360", "361", "100" })
            Assert.Equal(0m, await BakiyeAsync(f, kod));

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var s = new PersonelServisi(c, TamYetkili());
            var d = await s.DonemAsync(2025, 3, f);
            Assert.Equal(0m, d.Bekleyenler[0].AvansKesintisi);
            await s.TahakkukEtAsync(2025, 3, d.Bekleyenler, f); // geri alınan dönem yeniden tahakkuk edilir
            Assert.Single((await s.DonemAsync(2025, 3, f)).FisNolari);
            Assert.Equal(1650m, (await s.VergiSgkBakiyeAsync(f)).OdenecekVergi);
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.VergiSgkOdeAsync("360", 2000, T(4, 26), PersonelServisi.Kasa, kasaId, null, f));
            await s.VergiSgkOdeAsync("360", 1650, T(4, 26), PersonelServisi.Kasa, kasaId, null, f);
            await s.VergiSgkOdeAsync("361", 7600, T(4, 30), PersonelServisi.Kasa, kasaId, null, f);
            Assert.Equal(0m, (await s.VergiSgkBakiyeAsync(f)).OdenecekVergi);
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.SilAsync(a, f)); // hareketi olan personel silinmez
        }
        Assert.Equal(0m, await BakiyeAsync(f, "361"));
        Assert.Equal(-9250m, await BakiyeAsync(f, "100"));
    }

    [Fact]
    public async Task Yetkisiz_kullanici_maas_tahakkuk_edemez_ve_odeme_yapamaz()
    {
        var (f, kasaId) = await FirmaAsync();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var id = (await new PersonelServisi(c, TamYetkili()).KaydetAsync(new Personel { AdSoyad = "Deniz Ak", IseGirisTarihi = T(1, 1), NetUcret = 1000 }, f)).Id;
        var sinirli = new Cari360TestKullanici { AktifYetkiler = new KullaniciYetkileri { PersonelGoruntule = true } };
        var s = new PersonelServisi(c, sinirli);
        Assert.Single(await s.ListeAsync(false, f));
        var d = await s.DonemAsync(2025, 1, f);
        await Assert.ThrowsAsync<YetkisizErisimException>(() => s.TahakkukEtAsync(2025, 1, d.Bekleyenler, f));
        await Assert.ThrowsAsync<YetkisizErisimException>(() => s.AvansVerAsync(id, 100, T(1, 5), PersonelServisi.Kasa, kasaId, null, f));
    }

    [Fact]
    public async Task Ana_sayfa_uyarisi_ve_maas_pusulasi()
    {
        var (f, kasaId) = await FirmaAsync();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var s = new PersonelServisi(c, TamYetkili());
        // Hiç tahakkuk yapılmamış firmada geçen ay için uyarılmaz.
        var a = (await s.KaydetAsync(new Personel
        {
            AdSoyad = "Elif Şahin", TcKimlikNo = "10000000146", Gorev = "Muhasebe", IseGirisTarihi = T(1, 1), NetUcret = 20000, SgkIsciPayi = 3000,
            IssizlikIsciPayi = 200, GelirVergisi = 1500, DamgaVergisi = 150, SgkIsverenPayi = 4000, IssizlikIsverenPayi = 400
        }, f)).Id;
        await s.KaydetAsync(new Personel { AdSoyad = "Fatih Kaya", IseGirisTarihi = T(1, 1), NetUcret = 15000 }, f);
        var u = await s.UyariAsync(T(4, 10), f);
        Assert.Equal(2, u.CalisanSayisi);
        Assert.Equal(0, u.TahakkukBekleyen);
        Assert.Equal("", u.Metin(T(4, 10)));

        await s.AvansVerAsync(a, 2000, T(2, 10), PersonelServisi.Kasa, kasaId, null, f);
        var d = await s.DonemAsync(2025, 2, f);
        await s.TahakkukEtAsync(2025, 2, d.Bekleyenler.Where(x => x.PersonelId == a).ToList(), f);

        // Mart (geçen ay) hiç tahakkuk edilmedi; şubatın neti ödenmedi; 360/361 borcu var.
        u = await s.UyariAsync(T(4, 10), f);
        Assert.Equal((2025, 3, 2), (u.TahakkukYil, u.TahakkukAy, u.TahakkukBekleyen));
        Assert.Equal((1, 18000m), (u.OdenmemisKisi, u.OdenmemisNet));
        Assert.Equal((1650m, 7600m), (u.OdenecekVergi, u.OdenecekSgk));
        var metin = u.Metin(T(4, 10));
        Assert.Contains("03.2025 maaşı 2 personel", metin);
        Assert.Contains("26.04.2025", metin);

        // Mart başında geçen ay şubattır.
        u = await s.UyariAsync(new DateTime(2025, 3, 5), f);
        Assert.Equal(1, u.TahakkukBekleyen); // şubatta Fatih kaldı

        var yapilan = (await s.DonemAsync(2025, 2, f)).Yapilanlar;
        Assert.Equal("10000000146", yapilan.Single().TcKimlikNo);
        var html = System.Net.WebUtility.HtmlDecode(new BelgeYazdirmaServisi().MaasPusulasiHtmlUret(2025, 2, yapilan,
            new FirmaBilgisi { FirmaUnvani = "Deneme Ltd." }));
        Assert.Contains("Ücret Pusulası", html);
        Assert.Contains("Şubat 2025", html);
        Assert.Contains("Elif Şahin", html);
        Assert.Contains("24.850,00", html); // brüt
        Assert.Contains("-2.000,00", html); // avans mahsubu
        Assert.Contains("18.000,00", html); // ödenecek net
        Assert.Contains("Muhasebe", html);
    }
}
