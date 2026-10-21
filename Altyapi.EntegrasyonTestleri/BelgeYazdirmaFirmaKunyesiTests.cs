using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Ayar;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kurumsal;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class BelgeYazdirmaFirmaKunyesiTests
{
    private static readonly SistemAyarlari Ayar = new()
    {
        FirmaUnvani = "Ana Firma A.Ş.", VergiDairesi = "Merkez", VergiNo = "1111111111", MersisNo = "0123456789000015",
        FaturaNotu = "Fatura notu", FisFooter = "Makbuz notu"
    };

    [Fact]
    public void Varsayilan_sirkette_kunye_ayarlardan_bos_alan_sirket_kartindan_gelir()
    {
        var sirket = new Sirket { Id = MuhasebeBaglami.VarsayilanSirketId, Unvan = "Kart Unvanı", Adres = "Kart Adresi", VergiNo = "9999999999" };
        var f = BelgeYazdirmaServisi.FirmaBilgisiOlustur(Ayar, sirket, true);

        Assert.Equal("Ana Firma A.Ş.", f.FirmaUnvani);
        Assert.Equal("1111111111", f.VergiNo);
        Assert.Equal("Kart Adresi", f.Adres);
        Assert.Equal("0123456789000015", f.MersisNo);
        Assert.Equal("Fatura notu", f.FaturaNotu);
    }

    [Fact]
    public void Diger_sirkette_baska_sirketin_vergi_numarasi_basilmaz()
    {
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "İkinci Firma Ltd.", VergiNo = "2222222222" };
        var f = BelgeYazdirmaServisi.FirmaBilgisiOlustur(Ayar, sirket, false);

        Assert.Equal("İkinci Firma Ltd.", f.FirmaUnvani);
        Assert.Equal("2222222222", f.VergiNo);
        Assert.Equal(string.Empty, f.VergiDairesi);
        Assert.Equal(string.Empty, f.MersisNo);
        Assert.Equal("Makbuz notu", f.BelgeNotu);
    }

    [Fact]
    public void Ayar_yoksa_ornek_firma_basilmaz_ve_metinler_html_olarak_korunur()
    {
        var bos = BelgeYazdirmaServisi.FirmaBilgisiOlustur(null, null, true);
        Assert.Equal(string.Empty, bos.FirmaUnvani);
        Assert.Equal(string.Empty, bos.VergiNo);

        var servis = new BelgeYazdirmaServisi();
        var fatura = new Fatura
        {
            FaturaNo = "F-1", Tarih = DateTime.Today, Tur = FaturaTuru.Satis,
            CariKart = new CariKart { Unvan = "A & B <Ticaret>" },
            Kalemler = [new FaturaKalemi { UrunAd = "Vida <M8>", Miktar = 1, BirimFiyat = 10 }]
        };
        var html = servis.FaturaHtmlUret(fatura, BelgeYazdirmaServisi.FirmaBilgisiOlustur(Ayar, null, true));

        Assert.Contains("A &amp; B &lt;Ticaret&gt;", html);
        Assert.Contains("Vida &lt;M8&gt;", html);
        Assert.DoesNotContain("Örnek Ticaret", html);
        Assert.Contains("Mersis No: 0123456789000015", html);
        Assert.Contains("Fatura notu", html);
    }
}
