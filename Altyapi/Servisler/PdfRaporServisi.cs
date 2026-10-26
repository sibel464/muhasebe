using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Uygulama.Arayuzler;
using Uygulama.Dto.Rapor;

namespace Altyapi.Servisler;

public class PdfRaporServisi : IPdfRaporServisi
{
    public string RaporHtmlOlustur<T>(string raporBasligi, SatisRaporFiltreDto filtre, IEnumerable<T> veriler, Dictionary<string, string> kolonBasliklari)
        => HtmlOlustur(RaporTablosuOlusturucu.Olustur(raporBasligi, filtre, veriler, kolonBasliklari));

    public byte[] ExportToPdf<T>(string raporBasligi, SatisRaporFiltreDto filtre, IEnumerable<T> veriler, Dictionary<string, string> kolonBasliklari)
        => PdfOlustur(RaporTablosuOlusturucu.Olustur(raporBasligi, filtre, veriler, kolonBasliklari));

    public string HtmlOlustur(RaporTablosu tablo)
    {
        var yatay = tablo.Kolonlar.Count > 6;
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"tr\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine($"  <title>{EscapeHtml(tablo.Baslik)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine($"    @page {{ size: A4 {(yatay ? "landscape" : "portrait")}; margin: 12mm; }}");
        sb.AppendLine("    body { font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; color: #1E293B; }");
        sb.AppendLine("    .header { border-bottom: 2px solid #2563EB; padding-bottom: 12px; margin-bottom: 16px; }");
        sb.AppendLine("    .title { font-size: 22px; font-weight: 700; color: #1D4ED8; margin: 0; }");
        sb.AppendLine("    .subtitle { font-size: 12px; color: #64748B; margin-top: 4px; }");
        sb.AppendLine("    table { width: 100%; border-collapse: collapse; }");
        sb.AppendLine("    thead { display: table-header-group; }");
        sb.AppendLine("    tr { page-break-inside: avoid; }");
        sb.AppendLine("    th { background: #F1F5F9; color: #475569; font-size: 11px; padding: 8px; border-bottom: 2px solid #CBD5E1; text-align: left; }");
        sb.AppendLine("    td { padding: 6px 8px; border-bottom: 1px solid #E2E8F0; font-size: 12px; }");
        sb.AppendLine("    tr:nth-child(even) td { background: #F8FAFC; }");
        sb.AppendLine("    .sag { text-align: right; white-space: nowrap; }");
        sb.AppendLine("    .footer { margin-top: 24px; font-size: 11px; color: #94A3B8; text-align: center; border-top: 1px solid #E2E8F0; padding-top: 8px; }");
        sb.AppendLine("    @media print { body { margin: 0; } tr:nth-child(even) td { -webkit-print-color-adjust: exact; print-color-adjust: exact; } }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body onload=\"window.print()\">");
        sb.AppendLine("  <div class=\"header\">");
        sb.AppendLine($"    <div class=\"title\">{EscapeHtml(tablo.Baslik)}</div>");
        sb.AppendLine($"    <div class=\"subtitle\">{EscapeHtml(AltBaslik(tablo))}</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr>");
        foreach (var k in tablo.Kolonlar)
            sb.AppendLine($"      <th{(k.Sayisal ? " class=\"sag\"" : "")}>{EscapeHtml(k.Baslik)}</th>");
        sb.AppendLine("    </tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var satir in tablo.Satirlar)
        {
            sb.Append("      <tr>");
            for (int i = 0; i < tablo.Kolonlar.Count; i++)
            {
                var h = i < satir.Length ? satir[i] : new RaporHucresi("");
                sb.Append($"<td{(tablo.Kolonlar[i].Sayisal ? " class=\"sag\"" : "")}>{EscapeHtml(h.Metin)}</td>");
            }
            sb.AppendLine("</tr>");
        }
        if (tablo.Satirlar.Count == 0)
            sb.AppendLine($"      <tr><td colspan=\"{Math.Max(1, tablo.Kolonlar.Count)}\">Bu dönem için kayıt yok.</td></tr>");
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine($"  <div class=\"footer\">{tablo.Satirlar.Count} kayıt - Bilgi amaçlıdır, resmî beyanname değildir.</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    public byte[] PdfOlustur(RaporTablosu tablo)
    {
        const float yaziBoyu = 8f, baslikBoyu = 8f, satirYuksekligi = 16f, kenar = 20f, hucreBosluk = 4f;
        var kolonSayisi = Math.Max(1, tablo.Kolonlar.Count);

        // Sütunların doğal genişliği (en uzun metin); çok sütunlu ya da geniş raporlar yatay sayfaya basılır.
        var dogal = new float[kolonSayisi];
        for (int i = 0; i < tablo.Kolonlar.Count; i++)
        {
            var enGenis = Genislik(tablo.Kolonlar[i].Baslik, baslikBoyu, true);
            foreach (var s in tablo.Satirlar.Take(2000))
                if (i < s.Length) enGenis = Math.Max(enGenis, Genislik(s[i].Metin, yaziBoyu, false));
            dogal[i] = Math.Min(enGenis, 260f) + 2 * hucreBosluk;
        }
        var yatay = tablo.Kolonlar.Count > 6 || dogal.Sum() > 595f - 2 * kenar;
        float sayfaG = yatay ? 842f : 595f, sayfaY = yatay ? 595f : 842f;
        var kullanilabilir = sayfaG - 2 * kenar;
        var genislikler = KolonGenislikleri(dogal, kullanilabilir);

        var ustBaslikY = sayfaY - 40f;
        var tabloBasi = sayfaY - 92f;
        var satirPerSayfa = Math.Max(1, (int)((tabloBasi - satirYuksekligi - 40f) / satirYuksekligi));
        var sayfaSayisi = Math.Max(1, (int)Math.Ceiling(tablo.Satirlar.Count / (double)satirPerSayfa));

        var sayfalar = new List<string>();
        for (int p = 0; p < sayfaSayisi; p++)
        {
            var c = new StringBuilder();
            c.AppendLine("0.11 0.30 0.85 rg");
            c.AppendLine(Fmt($"{kenar} {ustBaslikY} {kullanilabilir} 26 re f"));
            c.AppendLine(Fmt($"BT /F2 13 Tf 1 1 1 rg {kenar + 8} {ustBaslikY + 8} Td ({Kacis(Sigdir(tablo.Baslik, 13, true, kullanilabilir - 16))}) Tj ET"));
            c.AppendLine(Fmt($"BT /F1 9 Tf 0.4 0.4 0.4 rg {kenar + 4} {ustBaslikY - 18} Td ({Kacis(Sigdir(AltBaslik(tablo), 9, false, kullanilabilir - 8))}) Tj ET"));

            // Sütun başlıkları her sayfada tekrarlanır.
            var y = tabloBasi;
            c.AppendLine("0.94 0.96 0.98 rg");
            c.AppendLine(Fmt($"{kenar} {y} {kullanilabilir} {satirYuksekligi + 4} re f"));
            c.AppendLine("0.80 0.84 0.88 RG 0.5 w");
            c.AppendLine(Fmt($"{kenar} {y} {kullanilabilir} {satirYuksekligi + 4} re S"));
            HucreleriYaz(c, tablo.Kolonlar.Select(k => k.Baslik).ToArray(), tablo.Kolonlar.Select(k => k.Sayisal).ToArray(), genislikler, kenar, hucreBosluk, y + 7, baslikBoyu, true, "0.2 0.3 0.4");

            var satirlar = tablo.Satirlar.Skip(p * satirPerSayfa).Take(satirPerSayfa).ToList();
            if (tablo.Satirlar.Count == 0)
                c.AppendLine(Fmt($"BT /F1 9 Tf 0.4 0.4 0.4 rg {kenar + hucreBosluk} {y - satirYuksekligi + 5} Td ({Kacis("Bu dönem için kayıt yok.")}) Tj ET"));
            for (int r = 0; r < satirlar.Count; r++)
            {
                y -= satirYuksekligi;
                if (r % 2 == 1)
                {
                    c.AppendLine("0.973 0.980 0.988 rg");
                    c.AppendLine(Fmt($"{kenar} {y} {kullanilabilir} {satirYuksekligi} re f"));
                }
                c.AppendLine("0.90 0.92 0.95 RG 0.3 w");
                c.AppendLine(Fmt($"{kenar} {y} m {kenar + kullanilabilir} {y} l S"));
                var metinler = Enumerable.Range(0, tablo.Kolonlar.Count).Select(i => i < satirlar[r].Length ? satirlar[r][i].Metin : "").ToArray();
                HucreleriYaz(c, metinler, tablo.Kolonlar.Select(k => k.Sayisal).ToArray(), genislikler, kenar, hucreBosluk, y + 5, yaziBoyu, false, "0.1 0.1 0.1");
            }

            c.AppendLine(Fmt($"0.8 0.8 0.8 RG 0.5 w {kenar} 30 m {kenar + kullanilabilir} 30 l S"));
            c.AppendLine(Fmt($"BT /F1 8 Tf 0.5 0.5 0.5 rg {kenar} 18 Td ({Kacis($"{tablo.Satirlar.Count} kayıt - Bilgi amaçlıdır, resmî beyanname değildir.")}) Tj ET"));
            var sayfaMetni = $"Sayfa {p + 1} / {sayfaSayisi}";
            c.AppendLine(Fmt($"BT /F1 8 Tf 0.5 0.5 0.5 rg {kenar + kullanilabilir - Genislik(sayfaMetni, 8, false)} 18 Td ({Kacis(sayfaMetni)}) Tj ET"));
            sayfalar.Add(c.ToString());
        }

        return PdfDosyasi(sayfalar, sayfaG, sayfaY);
    }

    private static void HucreleriYaz(StringBuilder c, string[] metinler, bool[] sagaYasli, float[] genislikler, float kenar, float bosluk, float y, float boyut, bool kalin, string renk)
    {
        var x = kenar;
        for (int i = 0; i < genislikler.Length && i < metinler.Length; i++)
        {
            var sigan = Sigdir(metinler[i], boyut, kalin, genislikler[i] - 2 * bosluk);
            var tx = sagaYasli[i] ? x + genislikler[i] - bosluk - Genislik(sigan, boyut, kalin) : x + bosluk;
            c.AppendLine(Fmt($"BT /{(kalin ? "F2" : "F1")} {boyut} Tf {renk} rg {tx} {y} Td ({Kacis(sigan)}) Tj ET"));
            x += genislikler[i];
        }
    }

    // Sayfaya sığmayan raporda geniş sütunlar daraltılır; dar sütunlar (tarih, tutar) olduğu gibi kalır.
    private static float[] KolonGenislikleri(float[] dogal, float kullanilabilir)
    {
        var g = (float[])dogal.Clone();
        if (g.Sum() <= kullanilabilir)
        {
            var artis = (kullanilabilir - g.Sum()) / g.Length;
            return g.Select(x => x + artis).ToArray();
        }
        for (int tur = 0; tur < 20 && g.Sum() > kullanilabilir + 0.5f; tur++)
        {
            var esik = kullanilabilir / g.Length;
            var genisler = g.Select((x, i) => (x, i)).Where(t => t.x > esik).ToList();
            if (genisler.Count == 0) break;
            var fazla = g.Sum() - kullanilabilir;
            var genisToplam = genisler.Sum(t => t.x - esik);
            foreach (var (x, i) in genisler)
                g[i] = Math.Max(esik, x - fazla * (x - esik) / genisToplam);
        }
        if (g.Sum() > kullanilabilir)
        {
            var oran = kullanilabilir / g.Sum();
            g = g.Select(x => x * oran).ToArray();
        }
        return g;
    }

    private static string Sigdir(string metin, float boyut, bool kalin, float azami)
    {
        metin = Temizle(metin);
        if (Genislik(metin, boyut, kalin) <= azami) return metin;
        var sonuc = metin;
        while (sonuc.Length > 0 && Genislik(sonuc + "...", boyut, kalin) > azami) sonuc = sonuc[..^1];
        return sonuc.TrimEnd() + "...";
    }

    // Helvetica karakter genişlikleri (1/1000 em). Kalın yazı yaklaşık %6 geniştir.
    private static readonly int[] HelveticaAscii =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584
    ];

    private static float Genislik(string metin, float boyut, bool kalin)
    {
        float toplam = 0;
        foreach (var ch in Temizle(metin))
        {
            var taban = ch switch { 'Ç' => 'C', 'ç' => 'c', 'Ö' => 'O', 'ö' => 'o', 'Ü' => 'U', 'ü' => 'u', 'Â' => 'A', 'â' => 'a', 'Î' => 'I', 'î' => 'i', 'Û' => 'U', 'û' => 'u', _ => ch };
            toplam += taban >= ' ' && taban <= '~' ? HelveticaAscii[taban - ' '] : 556;
        }
        return toplam * boyut / 1000f * (kalin ? 1.06f : 1f);
    }

    // Standart PDF yazı tipleri (WinAnsi) Ç, Ö, Ü'yü gösterir; Ğ, Ş, İ, ı gösteremediği için en yakın harfe çevrilir.
    private static string Temizle(string? metin)
    {
        if (string.IsNullOrEmpty(metin)) return "";
        var sb = new StringBuilder(metin.Length);
        foreach (var ch in metin)
        {
            switch (ch)
            {
                case 'Ğ': sb.Append('G'); break;
                case 'ğ': sb.Append('g'); break;
                case 'Ş': sb.Append('S'); break;
                case 'ş': sb.Append('s'); break;
                case 'İ': sb.Append('I'); break;
                case 'ı': sb.Append('i'); break;
                case '€': sb.Append("EUR"); break;
                case '₺': sb.Append("TL"); break;
                case '–' or '—': sb.Append('-'); break;
                case '\r' or '\n' or '\t': sb.Append(' '); break;
                default: sb.Append(ch <= 0xFF && ch >= ' ' ? ch : '?'); break;
            }
        }
        return sb.ToString();
    }

    // PDF metninde önce ters bölü, sonra parantezler kaçırılır.
    private static string Kacis(string metin) => Temizle(metin).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static string Fmt(FormattableString s) => s.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string AltBaslik(RaporTablosu tablo) =>
        string.Join(" | ", new[] { tablo.AltBaslik, $"Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}" }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static byte[] PdfDosyasi(List<string> sayfalar, float sayfaG, float sayfaY)
    {
        using var ms = new MemoryStream();
        var offsets = new List<long>();
        void Yaz(string s) { var b = Encoding.Latin1.GetBytes(s); ms.Write(b, 0, b.Length); }
        void Nesne(string s) { offsets.Add(ms.Position); Yaz(s); }

        Yaz("%PDF-1.4\n%âãÏÓ\n");
        Nesne("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        var kids = string.Join(" ", Enumerable.Range(0, sayfalar.Count).Select(i => $"{5 + i * 2} 0 R"));
        Nesne($"2 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {sayfalar.Count} >>\nendobj\n");
        Nesne("3 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");
        Nesne("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");
        var boyut = Fmt($"[0 0 {sayfaG} {sayfaY}]");
        for (int i = 0; i < sayfalar.Count; i++)
        {
            int sayfaId = 5 + i * 2, icerikId = sayfaId + 1;
            Nesne($"{sayfaId} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox {boyut} /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {icerikId} 0 R >>\nendobj\n");
            var icerik = Encoding.Latin1.GetBytes(sayfalar[i]);
            Nesne($"{icerikId} 0 obj\n<< /Length {icerik.Length} >>\nstream\n");
            ms.Write(icerik, 0, icerik.Length);
            Yaz("\nendstream\nendobj\n");
        }
        var xref = ms.Position;
        Yaz($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) Yaz($"{o:D10} 00000 n \n");
        Yaz($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    private static string EscapeHtml(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
    }
}
