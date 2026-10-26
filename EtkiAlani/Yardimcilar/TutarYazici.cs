namespace EtkiAlani.Yardimcilar;

/// <summary>Fatura ve makbuzlara basılan "Yalnız ... Türk Lirası ... Kuruş" satırı.</summary>
public static class TutarYazici
{
    private static readonly string[] Birler = ["", "Bir", "İki", "Üç", "Dört", "Beş", "Altı", "Yedi", "Sekiz", "Dokuz"];
    private static readonly string[] Onlar = ["", "On", "Yirmi", "Otuz", "Kırk", "Elli", "Altmış", "Yetmiş", "Seksen", "Doksan"];
    private static readonly string[] Basamaklar = ["", "Bin", "Milyon", "Milyar", "Trilyon"];

    // 1.250,50 → "Yalnız Bin İki Yüz Elli Türk Lirası Elli Kuruş". Kuruş, tutar 2 haneye yuvarlanarak alınır.
    public static string Yaziyla(decimal tutar)
    {
        tutar = Math.Round(Math.Abs(tutar), 2, MidpointRounding.AwayFromZero);
        var lira = (long)Math.Truncate(tutar);
        var kurus = (int)((tutar - lira) * 100);
        var parcalar = new List<string>();
        if (lira > 0 || kurus == 0) parcalar.Add($"{Sayi(lira)} Türk Lirası");
        if (kurus > 0) parcalar.Add($"{Sayi(kurus)} Kuruş");
        return "Yalnız " + string.Join(" ", parcalar);
    }

    public static string Sayi(long sayi)
    {
        if (sayi == 0) return "Sıfır";
        var parcalar = new List<string>();
        for (var basamak = 0; sayi > 0; basamak++, sayi /= 1000)
        {
            var uclu = (int)(sayi % 1000);
            if (uclu == 0) continue;
            // "Bir Bin" denmez: bin basamağında tek 1 yalnız "Bin" okunur.
            var okunus = basamak == 1 && uclu == 1 ? string.Empty : Uclu(uclu);
            parcalar.Insert(0, string.Join(" ", new[] { okunus, Basamaklar[basamak] }.Where(x => x.Length > 0)));
        }
        return string.Join(" ", parcalar);
    }

    private static string Uclu(int n)
    {
        var yuz = n / 100;
        var parcalar = new List<string>();
        if (yuz > 0) parcalar.Add(yuz == 1 ? "Yüz" : $"{Birler[yuz]} Yüz");
        if (Onlar[n / 10 % 10].Length > 0) parcalar.Add(Onlar[n / 10 % 10]);
        if (Birler[n % 10].Length > 0) parcalar.Add(Birler[n % 10]);
        return string.Join(" ", parcalar);
    }
}
