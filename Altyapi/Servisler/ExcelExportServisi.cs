using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Uygulama.Arayuzler;
using Uygulama.Dto.Rapor;

namespace Altyapi.Servisler;

public class ExcelExportServisi : IExcelExportServisi
{
    public byte[] ExportToExcel<T>(string raporBasligi, SatisRaporFiltreDto filtre, IEnumerable<T> veriler, Dictionary<string, string> kolonBasliklari)
        => ExcelOlustur(RaporTablosuOlusturucu.Olustur(raporBasligi, filtre, veriler, kolonBasliklari));

    public byte[] ExcelOlustur(RaporTablosu tablo)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true, Encoding.UTF8))
        {
            Yaz(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """);
            Yaz(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Yaz(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);
            Yaz(archive, "xl/workbook.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="{EscapeXml(SayfaAdi(tablo.Baslik))}" sheetId="1" r:id="rId1"/>
                  </sheets>
                </workbook>
                """);
            // Stiller: 0 normal, 1 başlık, 2 sütun başlığı, 3 ondalıklı sayı, 4 tam sayı, 5 alt başlık
            Yaz(archive, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <numFmts count="1">
                    <numFmt numFmtId="164" formatCode="#,##0.00"/>
                  </numFmts>
                  <fonts count="4">
                    <font><sz val="10"/><name val="Segoe UI"/><color rgb="FF000000"/></font>
                    <font><b/><sz val="14"/><name val="Segoe UI"/><color rgb="FF1E3A8A"/></font>
                    <font><b/><sz val="10"/><name val="Segoe UI"/><color rgb="FFFFFFFF"/></font>
                    <font><sz val="9"/><name val="Segoe UI"/><color rgb="FF64748B"/></font>
                  </fonts>
                  <fills count="3">
                    <fill><patternFill patternType="none"/></fill>
                    <fill><patternFill patternType="gray125"/></fill>
                    <fill><patternFill patternType="solid"><fgColor rgb="FF1D4ED8"/><bgColor indexed="64"/></patternFill></fill>
                  </fills>
                  <borders count="1">
                    <border><left/><right/><top/><bottom/><diagonal/></border>
                  </borders>
                  <cellStyleXfs count="1">
                    <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
                  </cellStyleXfs>
                  <cellXfs count="6">
                    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
                    <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>
                    <xf numFmtId="0" fontId="2" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
                    <xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"><alignment horizontal="right"/></xf>
                    <xf numFmtId="3" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"><alignment horizontal="right"/></xf>
                    <xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1"/>
                  </cellXfs>
                </styleSheet>
                """);

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            const int baslikSatiri = 4;
            sb.AppendLine($"  <sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"{baslikSatiri}\" topLeftCell=\"A{baslikSatiri + 1}\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            if (tablo.Kolonlar.Count > 0)
            {
                sb.AppendLine("  <cols>");
                for (int i = 0; i < tablo.Kolonlar.Count; i++)
                {
                    var enUzun = tablo.Satirlar.Select(s => i < s.Length ? s[i].Metin.Length : 0).DefaultIfEmpty(0).Max();
                    var genislik = Math.Clamp(Math.Max(enUzun, tablo.Kolonlar[i].Baslik.Length) + 3, 8, 60);
                    sb.AppendLine($"    <col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{genislik}\" customWidth=\"1\"/>");
                }
                sb.AppendLine("  </cols>");
            }
            sb.AppendLine("  <sheetData>");
            sb.AppendLine($"    <row r=\"1\">{MetinHucresi("A1", tablo.Baslik, 1)}</row>");
            var alt = string.Join(" | ", new[] { tablo.AltBaslik, $"Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}" }.Where(x => !string.IsNullOrWhiteSpace(x)));
            sb.AppendLine($"    <row r=\"2\">{MetinHucresi("A2", alt, 5)}</row>");

            sb.Append($"    <row r=\"{baslikSatiri}\">");
            for (int i = 0; i < tablo.Kolonlar.Count; i++)
                sb.Append(MetinHucresi($"{KolonHarfi(i + 1)}{baslikSatiri}", tablo.Kolonlar[i].Baslik, 2));
            sb.AppendLine("</row>");

            var satirNo = baslikSatiri + 1;
            foreach (var satir in tablo.Satirlar)
            {
                sb.Append($"    <row r=\"{satirNo}\">");
                for (int i = 0; i < satir.Length && i < tablo.Kolonlar.Count; i++)
                {
                    var adres = $"{KolonHarfi(i + 1)}{satirNo}";
                    var h = satir[i];
                    if (h.Sayi.HasValue)
                    {
                        var stil = tablo.Kolonlar[i].Ondalikli || h.Sayi.Value != decimal.Truncate(h.Sayi.Value) ? 3 : 4;
                        sb.Append($"<c r=\"{adres}\" s=\"{stil}\"><v>{h.Sayi.Value.ToString(CultureInfo.InvariantCulture)}</v></c>");
                    }
                    else
                        sb.Append(MetinHucresi(adres, h.Metin, 0));
                }
                sb.AppendLine("</row>");
                satirNo++;
            }
            sb.AppendLine("  </sheetData>");
            if (tablo.Kolonlar.Count > 0 && tablo.Satirlar.Count > 0)
                sb.AppendLine($"  <autoFilter ref=\"A{baslikSatiri}:{KolonHarfi(tablo.Kolonlar.Count)}{satirNo - 1}\"/>");
            sb.AppendLine("</worksheet>");
            Yaz(archive, "xl/worksheets/sheet1.xml", sb.ToString());
        }

        return ms.ToArray();
    }

    private static void Yaz(ZipArchive archive, string ad, string icerik)
    {
        using var writer = new StreamWriter(archive.CreateEntry(ad, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(icerik);
    }

    private static string MetinHucresi(string adres, string? metin, int stil) =>
        $"<c r=\"{adres}\"{(stil == 0 ? "" : $" s=\"{stil}\"")} t=\"inlineStr\"><is><t xml:space=\"preserve\">{EscapeXml(metin)}</t></is></c>";

    // Excel sayfa adı en çok 31 karakter olabilir ve : \ / ? * [ ] içeremez.
    private static string SayfaAdi(string baslik)
    {
        var temiz = new string(baslik.Where(c => !":\\/?*[]".Contains(c)).ToArray()).Trim();
        if (temiz.Length == 0) temiz = "Rapor";
        return temiz.Length > 31 ? temiz[..31] : temiz;
    }

    private static string KolonHarfi(int colIndex)
    {
        string refStr = "";
        while (colIndex > 0)
        {
            int rem = (colIndex - 1) % 26;
            refStr = (char)('A' + rem) + refStr;
            colIndex = (colIndex - 1) / 26;
        }
        return refStr;
    }

    // XML 1.0'da izin verilmeyen denetim karakterleri (metne yapıştırılmış sekme dışı kodlar) dosyayı açılmaz yapar.
    private static string EscapeXml(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var temiz = new string(input.Where(c => c == '\t' || c == '\n' || c == '\r' || c >= ' ').ToArray());
        return temiz.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }
}
