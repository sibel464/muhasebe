using System.Security.Cryptography;
using System.Text;
using EtkiAlani.Lisanslama;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class LisansTests
{
    private static readonly DateOnly Bugun = new(2026, 10, 18);

    private static (string Metin, string AcikAnahtar, string Makine) Uret(DateOnly? bitis = null, DateOnly? baslangic = null)
    {
        using var anahtar = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var makine = Lisans.MakineKoduUret("test-makine-guid");
        var dosya = Lisans.Imzala(new LisansBilgisi("LSN-1", "ABC Ltd.", "1234567890", makine, baslangic ?? Bugun, bitis), anahtar);
        return (Lisans.DosyaMetni(dosya), Convert.ToBase64String(anahtar.ExportSubjectPublicKeyInfo()), makine);
    }

    [Fact]
    public void Gecerli_lisans_dogrulanir_makine_kodu_tiresiz_de_kabul_edilir()
    {
        var (metin, acik, makine) = Uret(new DateOnly(2027, 10, 18));
        var sonuc = Lisans.Dogrula(metin, acik, makine.Replace("-", "").ToLowerInvariant(), Bugun);
        Assert.Equal(LisansDurumu.Gecerli, sonuc.Durum);
        Assert.Equal("ABC Ltd.", sonuc.Bilgi!.Musteri);
    }

    [Fact]
    public void Icerigi_degistirilmis_lisans_reddedilir()
    {
        var (metin, acik, makine) = Uret(new DateOnly(2026, 12, 31));
        var dosya = System.Text.Json.JsonSerializer.Deserialize<LisansDosyasi>(metin)!;
        var veri = Encoding.UTF8.GetString(Convert.FromBase64String(dosya.Veri)).Replace("2026-12-31", "2099-12-31");
        var sahte = Lisans.DosyaMetni(dosya with { Veri = Convert.ToBase64String(Encoding.UTF8.GetBytes(veri)) });
        Assert.Equal(LisansDurumu.ImzaGecersiz, Lisans.Dogrula(sahte, acik, makine, Bugun).Durum);
    }

    [Fact]
    public void Baska_anahtarla_uretilmis_lisans_reddedilir()
    {
        var (metin, _, makine) = Uret();
        var (_, baskaAcik, _) = Uret();
        Assert.Equal(LisansDurumu.ImzaGecersiz, Lisans.Dogrula(metin, baskaAcik, makine, Bugun).Durum);
    }

    [Fact]
    public void Baska_makinede_ve_suresi_dolunca_gecersiz()
    {
        var (metin, acik, _) = Uret(new DateOnly(2026, 11, 1));
        Assert.Equal(LisansDurumu.BaskaMakine, Lisans.Dogrula(metin, acik, Lisans.MakineKoduUret("baska-bilgisayar"), Bugun).Durum);
        var (metin2, acik2, makine2) = Uret(new DateOnly(2026, 11, 1));
        Assert.Equal(LisansDurumu.Gecerli, Lisans.Dogrula(metin2, acik2, makine2, new DateOnly(2026, 11, 1)).Durum);
        Assert.Equal(LisansDurumu.SuresiDolmus, Lisans.Dogrula(metin2, acik2, makine2, new DateOnly(2026, 11, 2)).Durum);
    }

    [Fact]
    public void Bozuk_dosya_hata_firlatmadan_bozuk_doner()
    {
        var (_, acik, makine) = Uret();
        Assert.Equal(LisansDurumu.Bozuk, Lisans.Dogrula("bu bir lisans değil", acik, makine, Bugun).Durum);
        Assert.Equal(LisansDurumu.Bozuk, Lisans.Dogrula("{\"Veri\":\"%%%\",\"Imza\":\"x\"}", acik, makine, Bugun).Durum);
    }

    [Fact]
    public void Makine_kodu_kararli_ve_okunakli()
    {
        var a = Lisans.MakineKoduUret("ABC-123");
        Assert.Equal(a, Lisans.MakineKoduUret("abc-123 "));
        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}$", a);
        Assert.NotEqual(a, Lisans.MakineKoduUret("ABC-124"));
    }
}
