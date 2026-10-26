using Altyapi.Servisler;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Ortak;
using Uygulama.Dto.Rapor;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class MutabakatMektubuTests
{
    private static readonly FirmaBilgisi Firma = new() { FirmaUnvani = "Deneme Ticaret", VergiNo = "1111111111", Telefon = "0212 000 00 00" };
    private static readonly CariKart Cari = new() { Kod = "C-001", Unvan = "Örnek Müşteri <Ltd>", VergiNo = "1234567890", VergiDairesi = "Kadıköy", Il = "İstanbul" };

    private static string Uret(decimal bakiye, IReadOnlyList<BaBsKalemiDto>? baBs = null) =>
        System.Net.WebUtility.HtmlDecode(new BelgeYazdirmaServisi().MutabakatMektubuHtmlUret(Cari, new DateTime(2026, 9, 30), bakiye, baBs, Firma));

    [Fact]
    public void Borclu_cariye_tarih_itibariyla_borc_bakiyesi_yaziyla_ve_cevap_bolumuyle_basilir()
    {
        var html = Uret(12500.5m);
        Assert.Contains("30.09.2026", html);
        Assert.Contains("12.500,50 TL", html);
        Assert.Contains("BORÇ", html);
        Assert.Contains("Yalnız On İki Bin Beş Yüz Türk Lirası Elli Kuruş", html);
        Assert.Contains("mutabıkız", html);
        Assert.Contains("Örnek Müşteri <Ltd>", html);
        Assert.DoesNotContain("Ba-Bs mutabakatı", html);
    }

    [Fact]
    public void Alacakli_cari_ve_babs_bolumu_karsi_tarafin_formunu_ters_gosterir()
    {
        var html = Uret(-300m, [new BaBsKalemiDto { Form = "Bs", Yil = 2026, Ay = 9, BelgeSayisi = 2, Tutar = 7000m }]);
        Assert.Contains("ALACAK", html);
        Assert.Contains("Ba-Bs mutabakatı", html);
        Assert.Contains("<td>09/2026</td><td>Size düzenlediğimiz faturalar</td><td>Bs</td><td>Ba</td>", html);
        Assert.Contains("7.000", html);
    }

    [Fact]
    public void Sifir_bakiyede_bakiye_yok_yazar()
    {
        Assert.Contains("(bakiye yoktur)", Uret(0m));
    }
}
