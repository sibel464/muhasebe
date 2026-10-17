using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EtkiAlani.Lisanslama;

// Lisans dosyası: imzalanan içerik (JSON) ve ECDSA P-256 imzası. İçerik değiştirilirse imza tutmaz;
// lisans yalnız üretildiği makine kodunda geçerlidir. Özel anahtar yalnız lisans üreticisinde (satıcıda) durur,
// programda yalnız doğrulama için açık anahtar bulunur.
public sealed record LisansBilgisi(
    string LisansNo,
    string Musteri,
    string? VergiNo,
    string MakineKodu,
    DateOnly Baslangic,
    DateOnly? Bitis,          // boş: süresiz lisans
    int Surum = 1);

public sealed record LisansDosyasi(string Veri, string Imza);

public enum LisansDurumu { Gecerli, ImzaGecersiz, BaskaMakine, SuresiDolmus, HenuzBaslamadi, Bozuk }

public sealed record LisansSonucu(LisansDurumu Durum, LisansBilgisi? Bilgi)
{
    public bool GecerliMi => Durum == LisansDurumu.Gecerli;

    public string Aciklama => Durum switch
    {
        LisansDurumu.Gecerli => Bilgi!.Bitis is DateOnly b ? $"Lisanslı: {Bilgi.Musteri} ({b:dd.MM.yyyy} tarihine kadar)" : $"Lisanslı: {Bilgi!.Musteri} (süresiz)",
        LisansDurumu.ImzaGecersiz => "Lisans dosyası geçersiz (imza doğrulanamadı; dosya değiştirilmiş olabilir).",
        LisansDurumu.BaskaMakine => "Bu lisans başka bir bilgisayar için üretilmiş.",
        LisansDurumu.SuresiDolmus => $"Lisans süresi {Bilgi!.Bitis:dd.MM.yyyy} tarihinde doldu.",
        LisansDurumu.HenuzBaslamadi => $"Lisans {Bilgi!.Baslangic:dd.MM.yyyy} tarihinde başlıyor.",
        _ => "Lisans dosyası okunamadı."
    };
}

public static class Lisans
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static LisansDosyasi Imzala(LisansBilgisi bilgi, ECDsa ozelAnahtar)
    {
        var veri = JsonSerializer.SerializeToUtf8Bytes(bilgi, Json);
        var imza = ozelAnahtar.SignData(veri, HashAlgorithmName.SHA256);
        return new LisansDosyasi(Convert.ToBase64String(veri), Convert.ToBase64String(imza));
    }

    public static string DosyaMetni(LisansDosyasi dosya) => JsonSerializer.Serialize(dosya, new JsonSerializerOptions { WriteIndented = true });

    public static LisansSonucu Dogrula(string dosyaMetni, string acikAnahtarBase64, string makineKodu, DateOnly bugun)
    {
        LisansDosyasi? dosya;
        byte[] veri, imza;
        try
        {
            dosya = JsonSerializer.Deserialize<LisansDosyasi>(dosyaMetni);
            if (dosya == null) return new(LisansDurumu.Bozuk, null);
            veri = Convert.FromBase64String(dosya.Veri);
            imza = Convert.FromBase64String(dosya.Imza);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentNullException)
        {
            return new(LisansDurumu.Bozuk, null);
        }

        using var anahtar = ECDsa.Create();
        anahtar.ImportSubjectPublicKeyInfo(Convert.FromBase64String(acikAnahtarBase64), out _);
        if (!anahtar.VerifyData(veri, imza, HashAlgorithmName.SHA256))
            return new(LisansDurumu.ImzaGecersiz, null);

        LisansBilgisi? bilgi;
        try { bilgi = JsonSerializer.Deserialize<LisansBilgisi>(veri); }
        catch (JsonException) { return new(LisansDurumu.Bozuk, null); }
        if (bilgi == null) return new(LisansDurumu.Bozuk, null);

        if (!string.Equals(Normalize(bilgi.MakineKodu), Normalize(makineKodu), StringComparison.Ordinal))
            return new(LisansDurumu.BaskaMakine, bilgi);
        if (bugun < bilgi.Baslangic) return new(LisansDurumu.HenuzBaslamadi, bilgi);
        if (bilgi.Bitis is DateOnly bitis && bugun > bitis) return new(LisansDurumu.SuresiDolmus, bilgi);
        return new(LisansDurumu.Gecerli, bilgi);
    }

    // Makine kodu: bilgisayara özgü kimlikten türetilen, okunması/yazdırılması kolay 16 karakter (XXXX-XXXX-XXXX-XXXX).
    public static string MakineKoduUret(string makineKimligi)
    {
        const string alfabe = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // karışan harfler (I, O, 0, 1) yok
        var ozet = SHA256.HashData(Encoding.UTF8.GetBytes("MuhasebeYonetim:" + makineKimligi.Trim().ToUpperInvariant()));
        var sb = new StringBuilder();
        for (var i = 0; i < 16; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append('-');
            sb.Append(alfabe[ozet[i] % alfabe.Length]);
        }
        return sb.ToString();
    }

    private static string Normalize(string kod) => kod.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
}
