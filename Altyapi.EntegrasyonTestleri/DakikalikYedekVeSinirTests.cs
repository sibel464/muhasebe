using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Yardimcilar;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class DakikalikYedekVeSinirTests(IsolatedPostgreSqlFixture fixture)
{
    private string BaglantiDizesi()
    {
        using var db = new MuhasebeBaglami(fixture.Options);
        return db.Database.GetConnectionString()!;
    }

    [Fact]
    public async Task Dakikalik_yedek_yalniz_veri_degisince_alinir_ve_ayri_klasorde_durur()
    {
        fixture.RequireDatabase();
        var klasor = Path.Combine(Path.GetTempPath(), "muhasebe-dk-yedek-" + Guid.NewGuid().ToString("N"));
        var servis = new VeritabaniYedekServisi(BaglantiDizesi(), klasor) { DakikalikEnKisaAralik = TimeSpan.Zero };
        try
        {
            var ilk = await servis.GerekirseDakikalikYedekAlAsync();
            Assert.NotNull(ilk);
            Assert.Contains(Path.Combine(klasor, VeritabaniYedekServisi.DakikalikKlasorAdi), ilk!.TamYol);
            Assert.NotNull(servis.SonDakikalikYedek);

            // Veri değişince (istatistikler birkaç saniye gecikebilir) yeni yedek alınır.
            await using (var bag = new NpgsqlConnection(BaglantiDizesi()))
            {
                await bag.OpenAsync();
                await using var k = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS dk_yedek_deneme (id serial primary key, x int); INSERT INTO dk_yedek_deneme (x) VALUES (1); SELECT pg_stat_force_next_flush();", bag);
                await k.ExecuteNonQueryAsync();
                await using var k2 = new NpgsqlCommand("SELECT 1", bag);
                await k2.ExecuteScalarAsync();
            }
            Uygulama.Arayuzler.YedekDosyasi? ikinci = null;
            for (var i = 0; i < 30 && ikinci == null; i++)
            {
                await Task.Delay(1000);
                ikinci = await servis.GerekirseDakikalikYedekAlAsync();
            }
            Assert.NotNull(ikinci);
            Assert.Equal(2, servis.DakikalikYedekleriListele().Count);
            // Günlük yedek dakikalıklara bakmaz: henüz günlük yedek olmadığı için alınır.
            Assert.NotNull(await servis.GerekirseGunlukYedekAlAsync());
            Assert.Equal(3, servis.YedekleriListele().Count);
        }
        finally
        {
            await using (var bag = new NpgsqlConnection(BaglantiDizesi()))
            {
                await bag.OpenAsync();
                await using var k = new NpgsqlCommand("DROP TABLE IF EXISTS dk_yedek_deneme", bag);
                await k.ExecuteNonQueryAsync();
            }
            if (Directory.Exists(klasor)) Directory.Delete(klasor, recursive: true);
        }
    }

    [Fact]
    public void Dakikalik_saklama_son_iki_saati_tutar_sonra_saatte_bir_iki_gunden_eskisini_siler()
    {
        var klasor = Path.Combine(Path.GetTempPath(), "muhasebe-dk-temizle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var simdi = DateTime.Now;
            void Dosya(string ad, DateTime zaman) { var y = Path.Combine(klasor, $"Db_{ad}.dump"); File.WriteAllText(y, "x"); File.SetLastWriteTime(y, zaman); }
            Dosya("a", simdi.AddMinutes(-5)); Dosya("b", simdi.AddMinutes(-6)); Dosya("c", simdi.AddMinutes(-90));
            var eskiSaat = new DateTime(simdi.Year, simdi.Month, simdi.Day, simdi.Hour, 0, 0).AddHours(-5);
            Dosya("d", eskiSaat.AddMinutes(10)); Dosya("e", eskiSaat.AddMinutes(20)); // aynı saat: biri kalır
            Dosya("f", simdi.AddDays(-3));
            VeritabaniYedekServisi.DakikalikTemizle(klasor, "Db_");
            var kalan = Directory.GetFiles(klasor).Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).ToList();
            Assert.Equal(new[] { "Db_a", "Db_b", "Db_c", "Db_e" }, kalan);
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public async Task Otomatik_cari_kodu_999999_dan_sonra_da_devam_eder()
    {
        fixture.RequireDatabase();
        var y = new EtkiAlani.Modeller.Kullanici.KullaniciYetkileri();
        foreach (var p in typeof(EtkiAlani.Modeller.Kullanici.KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite)) p.SetValue(y, true);
        var k = new Cari360TestKullanici { AktifYetkiler = y };
        await using var c = new MuhasebeBaglami(fixture.Options);
        var s = new CariServisi(c, k);
        if (!await c.CariKartlar.IgnoreQueryFilters().AnyAsync(x => x.Kod == "C-999999"))
            await s.OtomatikKodlaEkleAsync(new EtkiAlani.Modeller.Cari.CariKart { Kod = "C-999999", Unvan = "Sınır " + Guid.NewGuid().ToString("N")[..6], Aktif = true }, otomatikCariKodu: false);
        var yeni = await s.OtomatikKodlaEkleAsync(new EtkiAlani.Modeller.Cari.CariKart { Unvan = "Sınır sonrası " + Guid.NewGuid().ToString("N")[..6], Aktif = true });
        Assert.Matches(@"^C-\d{7,}$", yeni.Kod);
    }

    [Fact]
    public void Terazi_barkodu_cozulur()
    {
        // 27 + PLU 00123 + 01250 g + kontrol hanesi
        var govde = "270012301250";
        var tam = govde + UrunKodBarkodUreteciKontrol(govde);
        var s = TeraziBarkodu.Coz(tam);
        Assert.NotNull(s);
        Assert.Equal("00123", s!.PluKodu);
        Assert.Equal(1.25m, s.Kilogram);
        Assert.Null(TeraziBarkodu.Coz("8690000000000"));
        Assert.Null(TeraziBarkodu.Coz(govde + ((UrunKodBarkodUreteciKontrol(govde) + 1) % 10)));
    }

    private static int UrunKodBarkodUreteciKontrol(string g) => Altyapi.Yardimcilar.UrunKodBarkodUreteci.Ean13KontrolBasamagiHesapla(g);

    [Fact]
    public async Task Barkodla_bulma_terazi_etiketini_ve_ürün_kodunu_tanir()
    {
        fixture.RequireDatabase();
        await using var c = new MuhasebeBaglami(fixture.Options);
        var ek = Guid.NewGuid().ToString("N")[..6];
        var plu = (Random.Shared.Next(10000, 99999)).ToString();
        c.Urunler.Add(new Urun { Kod = "TRZ-" + ek, Ad = "Dana kıyma " + ek, Barkod = "27" + plu, Birim = "Kg", Aktif = true });
        c.Urunler.Add(new Urun { Kod = "NRM-" + ek, Ad = "Normal " + ek, Aktif = true });
        await c.SaveChangesAsync();
        var s = new UrunServisi(c);
        var govde = "27" + plu + "00750";
        var sonuc = await s.BarkodlaBulAsync(govde + UrunKodBarkodUreteciKontrol(govde));
        Assert.NotNull(sonuc);
        Assert.True(sonuc!.TeraziEtiketi);
        Assert.Equal(0.75m, sonuc.Miktar);
        Assert.Equal("TRZ-" + ek, sonuc.Urun.Kod);
        var kodla = await s.BarkodlaBulAsync("NRM-" + ek);
        Assert.Equal(1m, kodla!.Miktar);
        Assert.Null(await s.BarkodlaBulAsync("0000000000000"));
    }
}
