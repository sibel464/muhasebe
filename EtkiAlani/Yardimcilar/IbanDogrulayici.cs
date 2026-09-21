namespace EtkiAlani.Yardimcilar;

/// <summary>Türkiye IBAN'larını saklama öncesinde normalize eder ve MOD-97 kontrolünü yapar.</summary>
public static class IbanDogrulayici
{
    public static bool Gecerlidir(string? deger, out string normalizeEdilmisIban)
    {
        normalizeEdilmisIban = string.Concat((deger ?? string.Empty)
            .Where(character => !char.IsWhiteSpace(character)))
            .ToUpperInvariant();

        if (normalizeEdilmisIban.Length != 26 || !normalizeEdilmisIban.StartsWith("TR", StringComparison.Ordinal))
            return false;

        if (normalizeEdilmisIban.Any(character => !((character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9'))))
            return false;

        // IBAN kontrolü: ilk dört karakteri sona alıp harfleri A=10..Z=35 biçiminde MOD-97 hesapla.
        var yenidenSiralanmis = normalizeEdilmisIban[4..] + normalizeEdilmisIban[..4];
        var kalan = 0;
        foreach (var character in yenidenSiralanmis)
        {
            if (character is >= '0' and <= '9')
            {
                kalan = (kalan * 10 + character - '0') % 97;
            }
            else
            {
                var sayisalDeger = character - 'A' + 10;
                kalan = (kalan * 100 + sayisalDeger) % 97;
            }
        }

        return kalan == 1;
    }
}
