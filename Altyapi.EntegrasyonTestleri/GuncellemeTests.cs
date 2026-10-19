using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EtkiAlani.Lisanslama;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class GuncellemeTests
{
    private static readonly byte[] Paket = Encoding.UTF8.GetBytes("sahte msi içeriği");

    private static (string Metin, string AcikAnahtar) Bildirim(string surum = "1.2.0", string adres = "https://ornek.com/MuhasebeYonetim-1.2.0.msi")
    {
        using var anahtar = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var bilgi = new GuncellemeBilgisi(surum, adres, Convert.ToHexString(SHA256.HashData(Paket)), "Hata düzeltmeleri", new DateOnly(2026, 10, 19));
        return (Guncelleme.BildirimMetni(bilgi, anahtar), Convert.ToBase64String(anahtar.ExportSubjectPublicKeyInfo()));
    }

    [Fact]
    public void Imzali_bildirim_dogrulanir_ve_yeni_surum_algilanir()
    {
        var (metin, acik) = Bildirim();
        var bilgi = Guncelleme.Dogrula(metin, acik);
        Assert.NotNull(bilgi);
        Assert.True(Guncelleme.DahaYeniMi(bilgi, new Version(1, 1, 9)));
        Assert.False(Guncelleme.DahaYeniMi(bilgi, new Version(1, 2, 0, 0)));
        Assert.False(Guncelleme.DahaYeniMi(bilgi, new Version(1, 3)));
    }

    [Fact]
    public void Degistirilmis_baska_anahtarli_veya_http_bildirim_reddedilir()
    {
        var (metin, acik) = Bildirim();
        var dosya = JsonSerializer.Deserialize<LisansDosyasi>(metin)!;
        var veri = Encoding.UTF8.GetString(Convert.FromBase64String(dosya.Veri)).Replace("ornek.com", "saldirgan.com");
        var bozuk = JsonSerializer.Serialize(dosya with { Veri = Convert.ToBase64String(Encoding.UTF8.GetBytes(veri)) });
        Assert.Null(Guncelleme.Dogrula(bozuk, acik));

        var (_, baskaAcik) = Bildirim();
        Assert.Null(Guncelleme.Dogrula(metin, baskaAcik));

        var (httpMetin, httpAcik) = Bildirim(adres: "http://ornek.com/paket.msi");
        Assert.Null(Guncelleme.Dogrula(httpMetin, httpAcik));
        Assert.Null(Guncelleme.Dogrula("çöp", acik));
    }

    [Fact]
    public void Paket_ozeti_kontrol_edilir()
    {
        var (metin, acik) = Bildirim();
        var bilgi = Guncelleme.Dogrula(metin, acik)!;
        Assert.True(Guncelleme.PaketDogruMu(new MemoryStream(Paket), bilgi.Sha256));
        Assert.False(Guncelleme.PaketDogruMu(new MemoryStream(Encoding.UTF8.GetBytes("değiştirilmiş")), bilgi.Sha256));
    }
}
