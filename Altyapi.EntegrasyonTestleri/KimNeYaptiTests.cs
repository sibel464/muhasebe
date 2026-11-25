using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Guvenlik;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Servisler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Yönetici paneli: kimin neyi yapabileceği (ürün fiyatı ayrı yetki) ve "Kim Ne Yaptı?" ekranının okunur Türkçe dökümü.
[Collection("PostgreSQL integration")]
public sealed class KimNeYaptiTests(IsolatedPostgreSqlFixture fixture)
{
    private static Cari360TestKullanici Yetkili(Action<KullaniciYetkileri>? kisit = null)
    {
        var y = new KullaniciYetkileri();
        foreach (var p in typeof(KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            p.SetValue(y, true);
        kisit?.Invoke(y);
        return new Cari360TestKullanici { AktifYetkiler = y };
    }

    [Fact]
    public void Guncelleme_ozeti_alan_alan_eski_ve_yeni_degeri_turkce_gosterir()
    {
        var log = new AuditLog
        {
            TabloAdi = "CariKart", KayitId = "7", IslemTuru = AuditIslemTuru.Guncelleme, KullaniciAdi = "ayse",
            EskiDegerJson = "{\"Unvan\":\"Eski Ticaret\",\"Telefon\":null,\"GuncellemeTarihi\":\"2026-11-01T10:00:00Z\",\"Aktif\":true}",
            YeniDegerJson = "{\"Unvan\":\"Yeni Ticaret\",\"Telefon\":\"0212\",\"GuncellemeTarihi\":\"2026-11-25T10:00:00Z\",\"Aktif\":true}"
        };

        Assert.Equal("Cari Hesap", DenetimMetinleri.ModulAdi(log.TabloAdi));
        Assert.Equal("Değiştirdi", DenetimMetinleri.IslemAdi(log.IslemTuru, log.TabloAdi));
        var d = DenetimMetinleri.Degisiklikler(log);
        Assert.Equal(2, d.Count); // teknik alan (güncelleme tarihi) ve değişmeyen alan (aktif) gösterilmez
        Assert.Contains(d, x => x.Alan == "Ünvan" && x.Eski == "Eski Ticaret" && x.Yeni == "Yeni Ticaret");
        Assert.Contains("Ünvan: Eski Ticaret → Yeni Ticaret", DenetimMetinleri.Ozet(log));
        Assert.Contains("Telefon: (boş) → 0212", DenetimMetinleri.Ozet(log));

        var ekleme = new AuditLog
        {
            TabloAdi = "Fatura", IslemTuru = AuditIslemTuru.Ekleme,
            YeniDegerJson = "{\"Id\":5,\"FaturaNo\":\"FAT-2026-000123\",\"GenelToplam\":1200.5,\"Tarih\":\"2026-11-25T00:00:00Z\"}"
        };
        var ozet = DenetimMetinleri.Ozet(ekleme);
        Assert.StartsWith("Fatura no: FAT-2026-000123", ozet);
        Assert.Contains("Genel toplam: 1.200,50", ozet);
        Assert.Contains("Tarih: 25.11.2026", ozet);
        Assert.Equal("Ekledi", DenetimMetinleri.IslemAdi(ekleme.IslemTuru, ekleme.TabloAdi));
    }

    [Fact]
    public async Task Islem_gecmisi_turkce_harfle_kullaniciya_ve_modul_adina_gore_aranir_ayrinti_satirlari_gizlenebilir()
    {
        fixture.RequireDatabase();
        var ad = "Şahinoğlu Tipi " + Guid.NewGuid().ToString("N")[..6];
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            await c.Database.MigrateAsync();
            await new CariTipiTanimiServisi(c, Yetkili()).KaydetAsync(new CariTipiTanimi { Kod = "T" + Guid.NewGuid().ToString("N")[..6], Ad = ad });
            c.AuditLoglar.Add(new AuditLog { TabloAdi = "FaturaKalemi", KayitId = "1", IslemTuru = AuditIslemTuru.Ekleme, YeniDegerJson = $"{{\"Aciklama\":\"{ad}\"}}" });
            await c.SaveChangesAsync();
        }

        await using var b = new MuhasebeBaglami(fixture.Options);
        var servis = new AuditLogServisi(b, Yetkili());
        var bulunan = await servis.LoglariGetirAsync(limit: 0, arama: "Şahinoğlu Tipi " + ad[^6..]);
        Assert.Contains(bulunan, l => l.TabloAdi == "CariTipiTanimi");
        Assert.Contains(bulunan, l => l.TabloAdi == "FaturaKalemi");

        var ayrintisiz = await servis.LoglariGetirAsync(limit: 0, arama: ad, ayrintiSatirlari: false);
        Assert.Contains(ayrintisiz, l => l.TabloAdi == "CariTipiTanimi");
        Assert.DoesNotContain(ayrintisiz, l => l.TabloAdi == "FaturaKalemi");

        // Türkçe modül adıyla arama
        Assert.Contains(await servis.LoglariGetirAsync(limit: 0, arama: "cari tipi"), l => l.TabloAdi == "CariTipiTanimi");

        // Yetkisi olmayan göremez
        await Assert.ThrowsAsync<YetkisizErisimException>(() =>
            new AuditLogServisi(b, Yetkili(y => y.AuditLogGoruntule = false)).LoglariGetirAsync(limit: 0));
    }

    [Fact]
    public async Task Fiyat_degistirme_yetkisi_olmayan_kart_duzenleyebilir_ama_fiyati_degistiremez()
    {
        fixture.RequireDatabase();
        int urunId;
        await using (var c = new MuhasebeBaglami(fixture.Options))
            urunId = (await new UrunServisi(c, Yetkili()).GuvenliEkleAsync(
                new Urun { Ad = "Fiyat Yetki Ürünü", Birim = "Adet", KdvOrani = 20, Aktif = true, PerakendeSatisFiyati = 100, AlisFiyati = 60 }, true, true)).Id;

        var depocu = Yetkili(y => y.StokFiyatDegistir = false);
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var urun = await c.Urunler.AsNoTracking().SingleAsync(u => u.Id == urunId);
            urun.Ad = "Fiyat Yetki Ürünü (raf 3)";
            await new UrunServisi(c, depocu).GuncelleAsync(urun); // fiyat değişmedi: izin var
        }
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var urun = await c.Urunler.AsNoTracking().SingleAsync(u => u.Id == urunId);
            urun.PerakendeSatisFiyati = 80;
            await Assert.ThrowsAsync<YetkisizErisimException>(() => new UrunServisi(c, depocu).GuncelleAsync(urun));
            await Assert.ThrowsAsync<YetkisizErisimException>(() => new UrunServisi(c, depocu).FiyatlariKaydetAsync(urunId,
                [new UrunSatisFiyati { TaksitSayisi = 1, Fiyat = 80, AktifMi = true }]));
        }
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var urun = await c.Urunler.AsNoTracking().SingleAsync(u => u.Id == urunId);
            Assert.Equal(100, urun.PerakendeSatisFiyati);
            Assert.Equal("Fiyat Yetki Ürünü (raf 3)", urun.Ad);
            urun.PerakendeSatisFiyati = 80;
            await new UrunServisi(c, Yetkili()).GuncelleAsync(urun); // fiyat yetkisi olan değiştirebilir
        }
    }
}
