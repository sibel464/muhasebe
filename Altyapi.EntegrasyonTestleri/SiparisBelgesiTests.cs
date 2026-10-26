using Altyapi.Servisler;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Siparis;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class SiparisBelgesiTests
{
    private static Siparis Siparis(SiparisTuru tur, SiparisDurumu durum) => new()
    {
        SiparisNo = "SIP-2026-0042", SiparisTuru = tur, Durum = durum, SiparisTarihi = new DateTime(2026, 10, 26),
        CariKart = new CariKart { Unvan = "Örnek Müşteri <Ltd>", VergiNo = "1234567890" },
        BrutToplam = 1000, AraToplam = 1000, KdvToplami = 200, GenelToplam = 1200,
        Kalemler = [new SiparisKalemi { UrunAdi = "Montaj işçiliği", UrunKodu = "HZM-1", Birim = "Saat", Miktar = 10, BirimFiyat = 100, KdvOrani = 20, SatirToplami = 1200 }]
    };

    private static readonly FirmaBilgisi Firma = new() { FirmaUnvani = "Deneme Ticaret", VergiNo = "1111111111" };

    [Fact]
    public void Onaylanmamis_satis_siparisi_gecerlilik_tarihli_fiyat_teklifi_olarak_basilir()
    {
        var html = System.Net.WebUtility.HtmlDecode(new BelgeYazdirmaServisi().SiparisHtmlUret(Siparis(SiparisTuru.Satis, SiparisDurumu.Taslak), Firma));
        Assert.Contains("FİYAT TEKLİFİ", html);
        Assert.Contains("10.11.2026", html); // 15 gün geçerli
        Assert.Contains("Yalnız Bin İki Yüz Türk Lirası", html);
        Assert.Contains("Örnek Müşteri <Ltd>", html);
        Assert.Contains("Montaj işçiliği", html);
    }

    [Theory]
    [InlineData(SiparisTuru.Satis, SiparisDurumu.Onaylandi, "SİPARİŞ FORMU")]
    [InlineData(SiparisTuru.Alis, SiparisDurumu.Taslak, "SATIN ALMA SİPARİŞİ")]
    public void Onayli_siparis_ve_alis_siparisi_kendi_basligiyla_basilir(SiparisTuru tur, SiparisDurumu durum, string baslik)
    {
        var html = System.Net.WebUtility.HtmlDecode(new BelgeYazdirmaServisi().SiparisHtmlUret(Siparis(tur, durum), Firma));
        Assert.Contains(baslik, html);
        Assert.DoesNotContain("FİYAT TEKLİFİ", html);
    }
}
