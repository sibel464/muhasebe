using System.Text;
using Altyapi.Aktarim;
using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kullanici;
using Uygulama.Dto.Finans360;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class BankaEkstreOkuyucuTests
{
    private static List<(int, List<string>)> T(params string[][] satirlar) => satirlar.Select((s, i) => (i + 1, s.ToList())).ToList();

    [Theory]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("-1.234,56", -1234.56)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("(250,00)", -250)]
    [InlineData("1.500.000", 1500000)]
    [InlineData("12,6 TL", 12.6)]
    [InlineData("75,00-", -75)]
    public void Tutar_bicimleri(string metin, double beklenen)
    {
        Assert.True(BankaEkstreOkuyucu.TutarCoz(metin, out var t));
        Assert.Equal((decimal)beklenen, t);
    }

    [Fact]
    public void Ust_bilgi_satirlarini_atlar_tek_isaretli_tutar_sutununu_okur()
    {
        var s = BankaEkstreOkuyucu.Coz(T(
            ["Hesap Hareketleri", "", ""],
            ["IBAN: TR00 0000", "", ""],
            ["İşlem Tarihi", "Açıklama", "Tutar", "Bakiye", "Dekont No"],
            ["01.10.2026 14:22", "EFT GELEN ALİ", "1.500,00", "1.500,00", "D1"],
            ["02/10/2026", "MASRAF", "-12,60", "1.487,40", ""],
            ["", "Toplam", "1.487,40", "", ""]));
        Assert.Equal(2, s.Satirlar.Count);
        Assert.Equal(new DateTime(2026, 10, 1), s.Satirlar[0].Tarih);
        Assert.Equal(1500m, s.Satirlar[0].Tutar);
        Assert.Equal("D1", s.Satirlar[0].DekontNo);
        Assert.Equal(-12.6m, s.Satirlar[1].Tutar);
        Assert.Single(s.Uyarilar); // toplam satırı atlandı
    }

    [Fact]
    public void Borc_alacak_ve_yon_sutunlu_duzenler()
    {
        var ba = BankaEkstreOkuyucu.Coz(T(["Tarih", "Açıklama", "Borç", "Alacak"], ["03.10.2026", "Kira", "5.000,00", ""], ["04.10.2026", "Tahsilat", "", "750,00"]));
        Assert.Equal(new[] { -5000m, 750m }, ba.Satirlar.Select(x => x.Tutar));

        var yon = BankaEkstreOkuyucu.Coz(T(["Tarih", "Açıklama", "Tutar", "B/A"], ["05.10.2026", "Ödeme", "300,00", "B"], ["06.10.2026", "Gelen", "200,00", "A"]));
        Assert.Equal(new[] { -300m, 200m }, yon.Satirlar.Select(x => x.Tutar));
    }

    [Fact]
    public void Tarih_tutar_basligi_yoksa_anlasilir_hata()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => BankaEkstreOkuyucu.Coz(T(["Ad", "Soyad"], ["a", "b"])));
        Assert.Contains("Tarih", ex.Message);
    }
}

[Collection("PostgreSQL integration")]
public sealed class BankaEkstreAktarimTests(IsolatedPostgreSqlFixture fixture)
{
    private static Cari360TestKullanici TamYetkili()
    {
        var y = new KullaniciYetkileri();
        foreach (var p in typeof(KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            p.SetValue(y, true);
        return new Cari360TestKullanici { AktifYetkiler = y };
    }

    // Rastgele ama doğrulama hanesi doğru TR IBAN (TR + 2 kontrol + 5 banka + 1 rezerv + 16 hesap).
    private static string Iban()
    {
        var bban = "00062" + "0" + string.Concat(Enumerable.Range(0, 16).Select(_ => Random.Shared.Next(10)));
        var sayi = System.Numerics.BigInteger.Parse(bban + "2927" + "00");
        return $"TR{98 - (int)(sayi % 97):00}{bban}";
    }

    [Fact]
    public async Task Ekstre_cariyi_bulur_masraf_faizi_tanir_kaydeder_ve_ikinci_aktarimda_tekrar_kaydetmez()
    {
        fixture.RequireDatabase();
        var k = TamYetkili();
        var anahtar = "KARDELEN" + new string(Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(6).ToArray()).ToUpperInvariant();
        var vkn = "9" + string.Concat(Guid.NewGuid().ToString("N").Where(char.IsDigit).Concat("123456789")).Substring(0, 9);
        int hesapId, aId, bId;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var cari = new CariServisi(c, k);
            aId = (await cari.OtomatikKodlaEkleAsync(new CariKart { Unvan = $"{anahtar} Gıda Ltd. Şti.", Aktif = true })).Id;
            bId = (await cari.OtomatikKodlaEkleAsync(new CariKart { Unvan = "Ekstre Tedarikçi " + Guid.NewGuid().ToString("N")[..5], VergiNo = vkn, Aktif = true })).Id;
            hesapId = (await new BankaServisi(c, k).BankaHesabiEkleAsync(new BankaHesabi { Kod = "EKS-" + Guid.NewGuid().ToString("N")[..6], Ad = "Ekstre Test Bankası", Iban = Iban(), AktifMi = true })).Id;
        }

        var dosya = Path.Combine(Path.GetTempPath(), $"ekstre_{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(dosya, string.Join("\n",
            "Hesap Hareketleri;;;;",
            "İşlem Tarihi;Açıklama;Tutar;Bakiye;Dekont No",
            $"01.10.2026;EFT GELEN {anahtar} GIDA LTD STI;1.500,00;1.500,00;D1",
            $"02.10.2026;HAVALE GIDEN VKN {vkn} FATURA ODEMESI;-1.000,50;499,50;D2",
            "03.10.2026;EFT UCRETI BSMV;-12,60;486,90;D3",
            "04.10.2026;MEVDUAT FAIZ TAHAKKUKU;3,25;490,15;D4",
            "05.10.2026;TANIMSIZ ISLEM;-50,00;440,15;D5"), new UTF8Encoding(true));

        List<EkstreEslesmeDto> eslesme;
        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var servis = new BankaEkstreServisi(c, new BankaServisi(c, k), k);
            var okuma = servis.Oku(dosya);
            Assert.Equal(5, okuma.Satirlar.Count);
            eslesme = await servis.EslestirAsync(hesapId, okuma.Satirlar);
        }
        Assert.Equal((EkstreIslemTuru.Tahsilat, (int?)aId), (eslesme[0].Islem, eslesme[0].CariId));
        Assert.Equal((EkstreIslemTuru.Odeme, (int?)bId), (eslesme[1].Islem, eslesme[1].CariId));
        Assert.Equal(EkstreIslemTuru.BankaMasrafi, eslesme[2].Islem);
        Assert.Equal(EkstreIslemTuru.FaizGeliri, eslesme[3].Islem);
        Assert.Equal(EkstreIslemTuru.Atla, eslesme[4].Islem);
        Assert.False(eslesme[4].Secili);

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            var sonuc = await new BankaEkstreServisi(c, new BankaServisi(c, k), k).KaydetAsync(hesapId, eslesme);
            Assert.Equal(4, sonuc.Kaydedilen);
            Assert.Equal(0, sonuc.Hatali);
        }

        await using (var c = new MuhasebeBaglami(fixture.Options))
        {
            Assert.Equal(490.15m, await new BankaServisi(c, k).HesapBakiyeHesaplaAsync(hesapId));
            Assert.Equal(-1500m, c.CariKartlar.Single(x => x.Id == aId).Bakiye);
            Assert.Equal(1000.5m, c.CariKartlar.Single(x => x.Id == bId).Bakiye);

            var servis = new BankaEkstreServisi(c, new BankaServisi(c, k), k);
            var ikinci = await servis.EslestirAsync(hesapId, servis.Oku(dosya).Satirlar);
            Assert.Equal(4, ikinci.Count(x => x.ZatenKayitli));
            Assert.False(ikinci[4].ZatenKayitli);
        }
        File.Delete(dosya);
    }
}
