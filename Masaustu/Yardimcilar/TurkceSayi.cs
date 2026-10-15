using System.Globalization;
using System.Text.RegularExpressions;

namespace Masaustu.Yardimcilar;

// Kullanıcının yazdığı tutar/miktarı Türkçe alışkanlığa göre okur:
// "1.250" = bin iki yüz elli, "1.250,50" = 1250,50, "12,5" = 12,5. Yalnız tek noktalı ve binlik
// biçimine uymayan "12.5" gibi girişler ondalık kabul edilir (barkod okuyucu/klavye alışkanlığı).
public static partial class TurkceSayi
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    [GeneratedRegex(@"^-?\d{1,3}(\.\d{3})+$")]
    private static partial Regex BinlikNoktali();

    public static bool TryCoz(string? metin, out decimal deger)
    {
        deger = 0;
        var s = (metin ?? string.Empty).Trim().Replace(" ", string.Empty).Replace("₺", string.Empty);
        if (s.Length == 0) return false;

        if (s.Contains(',') || BinlikNoktali().IsMatch(s))
            return decimal.TryParse(s, NumberStyles.Number, Tr, out deger);

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out deger);
    }

    // Geçersiz veya negatif giriş 0 kabul edilir (satır alanları için).
    public static decimal PozitifVeyaSifir(string? metin) => TryCoz(metin, out var d) ? Math.Max(0, d) : 0;
}
