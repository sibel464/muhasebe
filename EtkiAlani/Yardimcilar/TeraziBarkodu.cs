namespace EtkiAlani.Yardimcilar;

// Terazi (tartılı ürün) etiketi: EAN-13, 27/28/29 ile başlar. Türkiye'de yaygın düzen:
// PP CCCCC WWWWW K  →  PP önek, CCCCC terazideki ürün (PLU) kodu, WWWWW gram cinsinden ağırlık, K kontrol hanesi.
public static class TeraziBarkodu
{
    public static readonly string[] Onekler = ["27", "28", "29"];

    public sealed record Sonuc(string Onek, string PluKodu, decimal Kilogram)
    {
        // Ürün kartına terazi kodu olarak yazılabilecek biçimler: "27" + PLU (7 hane) ya da yalnız PLU.
        public string YediHane => Onek + PluKodu;
    }

    public static bool TeraziBarkoduMu(string? barkod) =>
        barkod is { Length: 13 } && barkod.All(char.IsDigit) && Onekler.Contains(barkod[..2]) && KontrolHanesiDogru(barkod);

    public static Sonuc? Coz(string? barkod)
    {
        if (!TeraziBarkoduMu(barkod)) return null;
        var gram = int.Parse(barkod![7..12]);
        if (gram <= 0) return null;
        return new Sonuc(barkod[..2], barkod[2..7], gram / 1000m);
    }

    private static bool KontrolHanesiDogru(string b)
    {
        var toplam = 0;
        for (var i = 0; i < 12; i++) toplam += (b[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (10 - toplam % 10) % 10 == b[12] - '0';
    }
}
