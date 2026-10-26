using System.Globalization;
using System.Text;

namespace EtkiAlani.Yardimcilar;

/// <summary>
/// Etiketler için barkod çizimi (SVG). 13 haneli ve sağlama hanesi doğru barkod EAN-13, diğerleri Code 128-B olarak çizilir.
/// </summary>
public static class BarkodCizici
{
    // EAN-13 sol-tek (L) kodları; sağ (R) kodlar L'nin tümleyeni, sol-çift (G) kodlar R'nin tersidir.
    private static readonly string[] L = ["0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011"];
    private static readonly string[] Parite = ["LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG", "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"];

    private static string R(int d) => new(L[d].Select(c => c == '0' ? '1' : '0').ToArray());
    private static string G(int d) => new(R(d).Reverse().ToArray());

    public static bool Ean13GecerliMi(string? kod) =>
        kod is { Length: 13 } && kod.All(char.IsAsciiDigit) && kod[12] - '0' == Ean13SaglamaHanesi(kod[..12]);

    public static int Ean13SaglamaHanesi(string ilk12)
    {
        var toplam = ilk12.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        return (10 - toplam % 10) % 10;
    }

    // 95 modüllük EAN-13 bit dizisi ("1" çubuk, "0" boşluk).
    public static string Ean13Bitleri(string kod)
    {
        if (!Ean13GecerliMi(kod)) throw new ArgumentException("Geçerli bir EAN-13 barkodu değil.", nameof(kod));
        var d = kod.Select(c => c - '0').ToArray();
        var sb = new StringBuilder("101");
        for (var i = 1; i <= 6; i++)
            sb.Append(Parite[d[0]][i - 1] == 'L' ? L[d[i]] : G(d[i]));
        sb.Append("01010");
        for (var i = 7; i <= 12; i++)
            sb.Append(R(d[i]));
        sb.Append("101");
        return sb.ToString();
    }

    // Code 128 sembolleri: çubuk/boşluk genişlikleri (her biri 11 modül, durdurma 13 modül).
    public static readonly string[] Code128 =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
    ];

    // Code 128-B bit dizisi: başlat B (104), veriler (ASCII 32-127), sağlama (mod 103), durdur.
    public static string Code128Bitleri(string metin)
    {
        if (string.IsNullOrEmpty(metin) || metin.Any(c => c < 32 || c > 126))
            throw new ArgumentException("Barkod yalnız Türkçe karakter içermeyen harf, rakam ve işaretlerden oluşabilir.", nameof(metin));
        var degerler = new List<int> { 104 };
        degerler.AddRange(metin.Select(c => c - 32));
        var saglama = 104;
        for (var i = 1; i < degerler.Count; i++) saglama += i * degerler[i];
        degerler.Add(saglama % 103);
        degerler.Add(106);
        var sb = new StringBuilder();
        foreach (var desen in degerler.Select(v => Code128[v]))
            for (var i = 0; i < desen.Length; i++)
                sb.Append(i % 2 == 0 ? '1' : '0', desen[i] - '0');
        return sb.ToString();
    }

    public static string Bitler(string kod) => Ean13GecerliMi(kod) ? Ean13Bitleri(kod) : Code128Bitleri(kod);

    // Ölçeklenebilir SVG (genişlik/yükseklik CSS ile verilir); sessiz bölge her iki yanda 10 modül.
    public static string Svg(string kod, int yukseklik = 40)
    {
        var bitler = Bitler(kod.Trim());
        var genislik = bitler.Length + 20;
        var sb = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {genislik} {yukseklik}' preserveAspectRatio='none' shape-rendering='crispEdges'>");
        for (var i = 0; i < bitler.Length;)
        {
            if (bitler[i] == '0') { i++; continue; }
            var bas = i;
            while (i < bitler.Length && bitler[i] == '1') i++;
            sb.Append(CultureInfo.InvariantCulture, $"<rect x='{bas + 10}' y='0' width='{i - bas}' height='{yukseklik}'/>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }
}
