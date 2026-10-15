namespace EtkiAlani.Yardimcilar;

/// <summary>Vergi Kimlik No (10 hane) ve T.C. Kimlik No (11 hane) sağlama hanesi kontrolleri.</summary>
public static class KimlikNoDogrulayici
{
    public static bool VergiNoGecerliMi(string? vkn)
    {
        vkn = vkn?.Trim();
        if (vkn is not { Length: 10 } || !vkn.All(char.IsAsciiDigit)) return false;
        var rakamlar = vkn.Select(c => c - '0').ToArray();
        var toplam = 0;
        for (var i = 0; i < 9; i++)
        {
            var tmp = (rakamlar[i] + (9 - i)) % 10;
            toplam += tmp == 9 ? tmp : (tmp * (1 << (9 - i))) % 9;
        }
        return rakamlar[9] == (10 - toplam % 10) % 10;
    }

    public static bool TcKimlikNoGecerliMi(string? tckn)
    {
        tckn = tckn?.Trim();
        if (tckn is not { Length: 11 } || !tckn.All(char.IsAsciiDigit) || tckn[0] == '0') return false;
        var r = tckn.Select(c => c - '0').ToArray();
        var tek = r[0] + r[2] + r[4] + r[6] + r[8];
        var cift = r[1] + r[3] + r[5] + r[7];
        var onuncu = ((tek * 7 - cift) % 10 + 10) % 10;
        return r[9] == onuncu && r[10] == (tek + cift + r[9]) % 10;
    }
}
