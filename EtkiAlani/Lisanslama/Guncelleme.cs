using System.Security.Cryptography;
using System.Text.Json;

namespace EtkiAlani.Lisanslama;

// Güncelleme bildirimi (guncelleme.json): yeni sürümün numarası, kurulum paketinin (MSI) adresi ve SHA-256 özeti.
// Lisanslarla aynı özel anahtarla imzalanır; program yalnız imzası tutan bildirime ve özeti tutan pakete güvenir.
// Böylece bildirimin yayınlandığı sunucu ele geçirilse bile müşteriye sahte paket kurdurulamaz.
public sealed record GuncellemeBilgisi(
    string Surum,
    string IndirmeAdresi,
    string Sha256,
    string Notlar,
    DateOnly YayinTarihi);

public static class Guncelleme
{
    public static string BildirimMetni(GuncellemeBilgisi bilgi, ECDsa ozelAnahtar)
    {
        var veri = JsonSerializer.SerializeToUtf8Bytes(bilgi);
        var imza = ozelAnahtar.SignData(veri, HashAlgorithmName.SHA256);
        return JsonSerializer.Serialize(new LisansDosyasi(Convert.ToBase64String(veri), Convert.ToBase64String(imza)), new JsonSerializerOptions { WriteIndented = true });
    }

    // İmza tutmazsa veya içerik bozuksa null.
    public static GuncellemeBilgisi? Dogrula(string bildirimMetni, string acikAnahtarBase64)
    {
        try
        {
            var dosya = JsonSerializer.Deserialize<LisansDosyasi>(bildirimMetni);
            if (dosya == null) return null;
            var veri = Convert.FromBase64String(dosya.Veri);
            using var anahtar = ECDsa.Create();
            anahtar.ImportSubjectPublicKeyInfo(Convert.FromBase64String(acikAnahtarBase64), out _);
            if (!anahtar.VerifyData(veri, Convert.FromBase64String(dosya.Imza), HashAlgorithmName.SHA256)) return null;
            var bilgi = JsonSerializer.Deserialize<GuncellemeBilgisi>(veri);
            return bilgi != null && Version.TryParse(bilgi.Surum, out _) && Uri.TryCreate(bilgi.IndirmeAdresi, UriKind.Absolute, out var adres)
                   && adres.Scheme == Uri.UriSchemeHttps && bilgi.Sha256.Length == 64 ? bilgi : null;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentNullException or CryptographicException)
        {
            return null;
        }
    }

    public static bool DahaYeniMi(GuncellemeBilgisi bilgi, Version mevcut) =>
        Version.TryParse(bilgi.Surum, out var yeni) && Normalize(yeni) > Normalize(mevcut);

    public static bool PaketDogruMu(Stream paket, string sha256Hex) =>
        string.Equals(Convert.ToHexString(SHA256.HashData(paket)), sha256Hex, StringComparison.OrdinalIgnoreCase);

    // 1.2 ile 1.2.0.0 eşit sayılır.
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
}
