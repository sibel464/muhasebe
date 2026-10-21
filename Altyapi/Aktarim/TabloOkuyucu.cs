using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Altyapi.Aktarim;

// Toplu aktarım için Excel (.xlsx) ya da CSV dosyasını okur. İlk dolu satır başlıktır; her veri satırı
// "başlık → hücre metni" sözlüğü olarak döner. Excel için ek kütüphane gerekmez (xlsx bir zip + XML paketidir).
public static class TabloOkuyucu
{
    public sealed record Satir(int SatirNo, IReadOnlyDictionary<string, string> Hucreler);

    public static List<Satir> Oku(string dosyaYolu)
    {
        var uzanti = Path.GetExtension(dosyaYolu).ToLowerInvariant();
        var tablo = uzanti switch
        {
            ".xlsx" => XlsxOku(File.ReadAllBytes(dosyaYolu)),
            ".csv" or ".txt" => CsvOku(File.ReadAllBytes(dosyaYolu)),
            ".xls" => throw new InvalidOperationException("Eski Excel biçimi (.xls) okunamıyor. Dosyayı Excel'de açıp 'Farklı Kaydet → Excel Çalışma Kitabı (.xlsx)' ile kaydedin."),
            _ => throw new InvalidOperationException("Yalnız Excel (.xlsx) ya da CSV (.csv) dosyası aktarılabilir.")
        };
        return SatirlaraCevir(tablo);
    }

    private static List<Satir> SatirlaraCevir(List<(int SatirNo, List<string> Hucreler)> tablo)
    {
        var dolu = tablo.Where(s => s.Hucreler.Any(h => !string.IsNullOrWhiteSpace(h))).ToList();
        if (dolu.Count == 0)
            throw new InvalidOperationException("Dosya boş.");
        var basliklar = dolu[0].Hucreler.Select(h => h.Trim()).ToList();
        if (basliklar.All(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("İlk satırda sütun başlıkları bulunamadı.");

        var sonuc = new List<Satir>();
        foreach (var (no, hucreler) in dolu.Skip(1))
        {
            var sozluk = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < basliklar.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(basliklar[i]) || sozluk.ContainsKey(basliklar[i])) continue;
                sozluk[basliklar[i]] = i < hucreler.Count ? hucreler[i].Trim() : string.Empty;
            }
            sonuc.Add(new Satir(no, sozluk));
        }
        return sonuc;
    }

    // ---- CSV ----

    // Türkçe Excel CSV'yi ";" ile, diğerleri "," ile ayırır; ayırıcı başlık satırından anlaşılır.
    // Kodlama: BOM varsa ona göre, yoksa geçerli UTF-8 değilse Windows-1254 (Türkçe) kabul edilir.
    public static List<(int SatirNo, List<string> Hucreler)> CsvOku(byte[] veri)
    {
        var metin = MetneCevir(veri);
        var ilkSatir = metin.Split('\n')[0];
        var ayirici = new[] { ';', ',', '\t' }.OrderByDescending(c => ilkSatir.Count(x => x == c)).First();

        var satirlar = new List<(int, List<string>)>();
        var hucre = new StringBuilder();
        var hucreler = new List<string>();
        var tirnakta = false;
        var satirNo = 1;
        for (var i = 0; i < metin.Length; i++)
        {
            var c = metin[i];
            if (tirnakta)
            {
                if (c == '"' && i + 1 < metin.Length && metin[i + 1] == '"') { hucre.Append('"'); i++; }
                else if (c == '"') tirnakta = false;
                else hucre.Append(c);
                continue;
            }
            if (c == '"') tirnakta = true;
            else if (c == ayirici) { hucreler.Add(hucre.ToString()); hucre.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < metin.Length && metin[i + 1] == '\n') i++;
                hucreler.Add(hucre.ToString()); hucre.Clear();
                satirlar.Add((satirNo++, hucreler));
                hucreler = new List<string>();
            }
            else hucre.Append(c);
        }
        if (hucre.Length > 0 || hucreler.Count > 0)
        {
            hucreler.Add(hucre.ToString());
            satirlar.Add((satirNo, hucreler));
        }
        return satirlar;
    }

    private static string MetneCevir(byte[] veri)
    {
        if (veri.Length >= 3 && veri[0] == 0xEF && veri[1] == 0xBB && veri[2] == 0xBF)
            return Encoding.UTF8.GetString(veri, 3, veri.Length - 3);
        if (veri.Length >= 2 && veri[0] == 0xFF && veri[1] == 0xFE)
            return Encoding.Unicode.GetString(veri, 2, veri.Length - 2);
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(veri);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1254).GetString(veri);
        }
    }

    // ---- XLSX ----

    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PaketR = "http://schemas.openxmlformats.org/package/2006/relationships";

    // İlk çalışma sayfası okunur. Sayılar ondalık virgüllü metin olarak döner;
    // tarih biçimli hücreler gg.aa.yyyy metnine çevrilir.
    public static List<(int SatirNo, List<string> Hucreler)> XlsxOku(byte[] veri)
    {
        ZipArchive zip;
        try { zip = new ZipArchive(new MemoryStream(veri), ZipArchiveMode.Read); }
        catch (InvalidDataException ex) { throw new InvalidOperationException("Dosya geçerli bir Excel (.xlsx) dosyası değil.", ex); }
        using (zip)
        {
            XDocument Yukle(string ad)
            {
                var giris = zip.GetEntry(ad) ?? throw new InvalidOperationException($"Excel dosyası bozuk: '{ad}' bulunamadı.");
                using var akis = giris.Open();
                using var okuyucu = XmlReader.Create(akis, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                return XDocument.Load(okuyucu);
            }

            var paylasilan = zip.GetEntry("xl/sharedStrings.xml") is null
                ? new List<string>()
                : Yukle("xl/sharedStrings.xml").Root!.Elements(S + "si")
                    .Select(si => string.Concat(si.Descendants(S + "t").Select(t => t.Value))).ToList();

            // İlk sayfanın dosyası workbook ilişkilerinden bulunur (her zaman sheet1.xml olmayabilir).
            var kitap = Yukle("xl/workbook.xml");
            var ilkSayfaId = kitap.Root!.Element(S + "sheets")?.Elements(S + "sheet").FirstOrDefault()?.Attribute(R + "id")?.Value;
            var sayfaYolu = "xl/worksheets/sheet1.xml";
            if (ilkSayfaId != null && zip.GetEntry("xl/_rels/workbook.xml.rels") != null)
            {
                var hedef = Yukle("xl/_rels/workbook.xml.rels").Root!.Elements(PaketR + "Relationship")
                    .FirstOrDefault(r => r.Attribute("Id")?.Value == ilkSayfaId)?.Attribute("Target")?.Value;
                if (!string.IsNullOrEmpty(hedef))
                    sayfaYolu = hedef.StartsWith('/') ? hedef.TrimStart('/') : "xl/" + hedef;
            }

            var tarihStilleri = TarihStilleri(zip.GetEntry("xl/styles.xml") is null ? null : Yukle("xl/styles.xml"));
            var sayfa = Yukle(sayfaYolu);
            var sonuc = new List<(int, List<string>)>();
            foreach (var row in sayfa.Descendants(S + "row"))
            {
                var satirNo = int.TryParse(row.Attribute("r")?.Value, out var rn) ? rn : sonuc.Count + 1;
                var hucreler = new List<string>();
                foreach (var c in row.Elements(S + "c"))
                {
                    var sutun = SutunNo(c.Attribute("r")?.Value) ?? hucreler.Count;
                    while (hucreler.Count < sutun) hucreler.Add(string.Empty);
                    var tip = c.Attribute("t")?.Value;
                    var deger = c.Element(S + "v")?.Value ?? string.Empty;
                    string metin = tip switch
                    {
                        "s" => int.TryParse(deger, out var idx) && idx < paylasilan.Count ? paylasilan[idx] : string.Empty,
                        "inlineStr" => string.Concat(c.Descendants(S + "t").Select(t => t.Value)),
                        "b" => deger == "1" ? "1" : "0",
                        _ => deger
                    };
                    if (tip is null or "n" && int.TryParse(c.Attribute("s")?.Value, out var stil) && tarihStilleri.Contains(stil)
                        && double.TryParse(deger, NumberStyles.Float, CultureInfo.InvariantCulture, out var seri))
                        metin = DateTime.FromOADate(seri).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                    // Sayı hücresi ondalık virgülle verilir: "0.125" binlik ayırıcılı "1.250" ile karışmasın,
                    // "8.69E+12" gibi bilimsel gösterim de düz rakama döner (barkod, vergi no).
                    else if (tip is null or "n" && deger.Length > 0
                        && decimal.TryParse(deger, NumberStyles.Float, CultureInfo.InvariantCulture, out var sayi))
                        metin = sayi.ToString(CultureInfo.InvariantCulture).Replace('.', ',');
                    hucreler.Add(metin);
                }
                sonuc.Add((satirNo, hucreler));
            }
            return sonuc;
        }
    }

    // "BC12" → 54 (0 tabanlı sütun sırası).
    private static int? SutunNo(string? hucreAdi)
    {
        if (string.IsNullOrEmpty(hucreAdi)) return null;
        var n = 0;
        foreach (var ch in hucreAdi)
        {
            if (!char.IsLetter(ch)) break;
            n = n * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }
        return n == 0 ? null : n - 1;
    }

    private static HashSet<int> TarihStilleri(XDocument? stiller)
    {
        var sonuc = new HashSet<int>();
        if (stiller?.Root == null) return sonuc;
        var ozelTarih = stiller.Root.Element(S + "numFmts")?.Elements(S + "numFmt")
            .Where(f => (f.Attribute("formatCode")?.Value ?? "").ToLowerInvariant() is var k && (k.Contains("yy") || k.Contains("dd")) && !k.Contains("0.0"))
            .Select(f => int.Parse(f.Attribute("numFmtId")!.Value, CultureInfo.InvariantCulture)).ToHashSet() ?? new HashSet<int>();
        var xfler = stiller.Root.Element(S + "cellXfs")?.Elements(S + "xf").ToList() ?? new List<XElement>();
        for (var i = 0; i < xfler.Count; i++)
        {
            var id = int.TryParse(xfler[i].Attribute("numFmtId")?.Value, out var v) ? v : 0;
            if (id is >= 14 and <= 22 || ozelTarih.Contains(id))
                sonuc.Add(i);
        }
        return sonuc;
    }
}
