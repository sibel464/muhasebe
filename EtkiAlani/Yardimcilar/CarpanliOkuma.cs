using System.Globalization;

namespace EtkiAlani.Yardimcilar;

// Barkod kutusuna "12*8690000000001" ya da "12x8690000000001" yazılırsa ürün 12 adet sayılır.
// Koli sayarken her birini tek tek okutmak gerekmesin diye.
public static class CarpanliOkuma
{
    public sealed record Sonuc(decimal Carpan, string Kod);

    public static Sonuc Coz(string? okunan)
    {
        var metin = (okunan ?? "").Trim();
        var ayrac = metin.IndexOfAny(['*', 'x', 'X']);
        if (ayrac > 0 && ayrac < metin.Length - 1)
        {
            var sol = metin[..ayrac].Trim().Replace(',', '.');
            var sag = metin[(ayrac + 1)..].Trim();
            if (sag.Length > 0
                && decimal.TryParse(sol, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var carpan)
                && carpan > 0)
                return new Sonuc(carpan, sag);
        }
        return new Sonuc(1, metin);
    }
}
