using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Uygulama.Arayuzler;
using Uygulama.Dto.Rapor;

namespace Altyapi.Servisler;

public class PdfRaporServisi : IPdfRaporServisi
{
    public string RaporHtmlOlustur<T>(string raporBasligi, SatisRaporFiltreDto filtre, IEnumerable<T> veriler, Dictionary<string, string> kolonBasliklari)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine($"  <title>{raporBasligi}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap');");
        sb.AppendLine("    body { font-family: 'Inter', sans-serif; margin: 20px; color: #1E293B; }");
        sb.AppendLine("    .header { border-bottom: 2px solid #2563EB; padding-bottom: 12px; margin-bottom: 20px; }");
        sb.AppendLine("    .title { font-size: 24px; font-weight: 700; color: #1D4ED8; margin: 0; }");
        sb.AppendLine("    .subtitle { font-size: 12px; color: #64748B; margin-top: 4px; }");
        sb.AppendLine("    table { width: 100%; border-collapse: collapse; margin-top: 15px; }");
        sb.AppendLine("    th { background: #F1F5F9; color: #475569; font-size: 11px; text-transform: uppercase; padding: 10px; border-bottom: 2px solid #CBD5E1; text-align: left; }");
        sb.AppendLine("    td { padding: 8px 10px; border-bottom: 1px solid #E2E8F0; font-size: 12px; }");
        sb.AppendLine("    tr:nth-child(even) td { background: #F8FAFC; }");
        sb.AppendLine("    .text-right { text-align: right; }");
        sb.AppendLine("    .footer { margin-top: 30px; font-size: 11px; color: #94A3B8; text-align: center; border-top: 1px solid #E2E8F0; padding-top: 10px; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine("  <div class=\"header\">");
        sb.AppendLine($"    <div class=\"title\">{EscapeHtml(raporBasligi)}</div>");
        sb.AppendLine($"    <div class=\"subtitle\">Oluşturulma Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm} | Preset: {EscapeHtml(filtre.PresetKey)}</div>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead>");
        sb.AppendLine("      <tr>");
        foreach (var header in kolonBasliklari.Values)
        {
            sb.AppendLine($"        <th>{EscapeHtml(header)}</th>");
        }
        sb.AppendLine("      </tr>");
        sb.AppendLine("    </thead>");
        sb.AppendLine("    <tbody>");

        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var propMap = kolonBasliklari.Keys.Select(k => props.FirstOrDefault(p => p.Name == k)).Where(p => p != null).ToList();

        foreach (var item in veriler)
        {
            sb.AppendLine("      <tr>");
            foreach (var prop in propMap)
            {
                if (prop == null) continue;
                var val = prop.GetValue(item);
                if (val is decimal decVal)
                {
                    sb.AppendLine($"        <td class=\"text-right\">{decVal:N2} TL</td>");
                }
                else if (val is DateTime dtVal)
                {
                    sb.AppendLine($"        <td>{dtVal:dd.MM.yyyy}</td>");
                }
                else
                {
                    sb.AppendLine($"        <td>{EscapeHtml(val?.ToString() ?? "")}</td>");
                }
            }
            sb.AppendLine("      </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine("  <div class=\"footer\">");
        sb.AppendLine("    Ticari Muhasebe Rapor Merkezi - Resmî Beyanname Değildir.");
        sb.AppendLine("  </div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    public byte[] ExportToPdf<T>(string raporBasligi, SatisRaporFiltreDto filtre, IEnumerable<T> veriler, Dictionary<string, string> kolonBasliklari)
    {
        var itemList = veriler.ToList();
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var propMap = kolonBasliklari.Keys.Select(k => props.FirstOrDefault(p => p.Name == k)).Where(p => p != null).ToList();

        int itemsPerPage = 35;
        int totalPages = Math.Max(1, (int)Math.Ceiling((double)itemList.Count / itemsPerPage));

        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.Latin1);
        var offsets = new List<long>();

        // Header
        writer.Write("%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");

        // Obj 1: Catalog
        writer.Flush();
        offsets.Add(ms.Position);
        writer.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Obj 2: Pages placeholder - reserved
        writer.Flush();
        long pagesObjOffsetPos = ms.Position;
        offsets.Add(pagesObjOffsetPos);
        // We will update object 2 later or calculate page object IDs.
        // Page Object IDs: 5, 6, ..., 5 + totalPages - 1
        // Content Object IDs: 5 + totalPages, ...

        // Font Objects: 3 and 4
        writer.Flush();
        offsets.Add(ms.Position); // Obj 3
        writer.Write("3 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        writer.Flush();
        offsets.Add(ms.Position); // Obj 4
        writer.Write("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        // Write Pages Array (Obj 2)
        var pageObjIds = new List<int>();
        for (int i = 0; i < totalPages; i++) pageObjIds.Add(5 + i * 2);

        var pagesObjSb = new StringBuilder();
        pagesObjSb.Append("2 0 obj\n<< /Type /Pages /Kids [");
        foreach (var id in pageObjIds) pagesObjSb.Append($"{id} 0 R ");
        pagesObjSb.Append($"] /Count {totalPages} >>\nendobj\n");

        // Write Obj 2 at pagesObjOffsetPos by rewriting stream
        // Instead of seeking, let's build objects logically
        // To be completely stream-friendly, let's write all page and content objects in sequence!

        // Let's generate page content streams first
        var pageStreams = new List<string>();

        for (int p = 0; p < totalPages; p++)
        {
            var pageItems = itemList.Skip(p * itemsPerPage).Take(itemsPerPage).ToList();
            var csb = new StringBuilder();

            // Page Background & Header Bar
            csb.AppendLine("0.11 0.30 0.85 rg");
            csb.AppendLine("15 800 565 26 re f");
            csb.AppendLine("BT /F2 13 Tf 1 1 1 rg 25 808 Td (" + PdfPdfEscape(raporBasligi) + ") Tj ET");

            // Subtitle
            csb.AppendLine("BT /F1 9 Tf 0.4 0.4 0.4 rg 25 782 Td (Tarih: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + " | Preset: " + PdfPdfEscape(filtre.PresetKey) + ") Tj ET");

            // Table Header Bar
            csb.AppendLine("0.94 0.96 0.98 rg");
            csb.AppendLine("15 755 565 20 re f");
            csb.AppendLine("0.80 0.84 0.88 RG 0.5 w");
            csb.AppendLine("15 755 565 20 re S");

            // Column Headers
            int colWidth = 565 / Math.Max(1, kolonBasliklari.Count);
            int xPos = 20;
            foreach (var h in kolonBasliklari.Values)
            {
                csb.AppendLine("BT /F2 9 Tf 0.2 0.3 0.4 rg " + xPos + " 761 Td (" + PdfPdfEscape(h) + ") Tj ET");
                xPos += colWidth;
            }

            // Data Rows
            int yPos = 735;
            foreach (var item in pageItems)
            {
                csb.AppendLine("0.90 0.92 0.95 RG 0.3 w");
                csb.AppendLine($"15 {yPos} 565 18 re S");

                xPos = 20;
                foreach (var prop in propMap)
                {
                    if (prop != null)
                    {
                        var val = prop.GetValue(item);
                        string cellStr = "";
                        if (val is decimal decVal) cellStr = decVal.ToString("N2") + " TL";
                        else if (val is DateTime dtVal) cellStr = dtVal.ToString("dd.MM.yyyy");
                        else cellStr = val?.ToString() ?? "";

                        csb.AppendLine($"BT /F1 8 Tf 0.1 0.1 0.1 rg {xPos} {yPos + 5} Td (" + PdfPdfEscape(cellStr) + ") Tj ET");
                    }
                    xPos += colWidth;
                }
                yPos -= 18;
            }

            // Footer
            csb.AppendLine("0.8 0.8 0.8 RG 0.5 w 15 30 565 0 m 580 30 565 0 l S");
            csb.AppendLine($"BT /F1 8 Tf 0.5 0.5 0.5 rg 20 18 Td (Ticari Muhasebe Rapor Merkezi - Sayfa {p + 1} / {totalPages}) Tj ET");

            pageStreams.Add(csb.ToString());
        }

        // Now write all Objects sequentially with accurate offsets
        using var finalMs = new MemoryStream();
        using var finalWriter = new StreamWriter(finalMs, Encoding.Latin1);
        var finalOffsets = new List<long>();

        // Header
        finalWriter.Write("%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");

        // Obj 1: Catalog
        finalWriter.Flush();
        finalOffsets.Add(finalMs.Position);
        finalWriter.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Obj 2: Pages
        finalWriter.Flush();
        finalOffsets.Add(finalMs.Position);
        finalWriter.Write(pagesObjSb.ToString());

        // Obj 3: Font Regular
        finalWriter.Flush();
        finalOffsets.Add(finalMs.Position);
        finalWriter.Write("3 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        // Obj 4: Font Bold
        finalWriter.Flush();
        finalOffsets.Add(finalMs.Position);
        finalWriter.Write("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        int currentObjId = 5;
        for (int i = 0; i < totalPages; i++)
        {
            int pageObjId = currentObjId++;
            int contentObjId = currentObjId++;

            // Page Object
            finalWriter.Flush();
            finalOffsets.Add(finalMs.Position);
            finalWriter.Write($"{pageObjId} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObjId} 0 R >>\nendobj\n");

            // Content Object
            var contentBytes = Encoding.Latin1.GetBytes(pageStreams[i]);
            finalWriter.Flush();
            finalOffsets.Add(finalMs.Position);
            finalWriter.Write($"{contentObjId} 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
            finalWriter.Flush();
            finalMs.Write(contentBytes, 0, contentBytes.Length);
            finalWriter.Write("\nendstream\nendobj\n");
        }

        // XRef Table
        finalWriter.Flush();
        long startXRef = finalMs.Position;
        int totalObjects = finalOffsets.Count + 1;

        finalWriter.Write($"xref\n0 {totalObjects}\n0000000000 65535 f \n");
        foreach (var offset in finalOffsets)
        {
            finalWriter.Write($"{offset:D10} 00000 n \n");
        }

        finalWriter.Write($"trailer\n<< /Size {totalObjects} /Root 1 0 R >>\nstartxref\n{startXRef}\n%%EOF\n");
        finalWriter.Flush();

        return finalMs.ToArray();
    }

    private static string EscapeHtml(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }

    private static string PdfPdfEscape(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        // Transliterate Turkish characters for WinAnsiEncoding Helvetica compatibility
        string clean = input
            .Replace("Ğ", "G").Replace("ğ", "g")
            .Replace("Ü", "U").Replace("ü", "u")
            .Replace("Ş", "S").Replace("ş", "s")
            .Replace("İ", "I").Replace("ı", "i")
            .Replace("Ö", "O").Replace("ö", "o")
            .Replace("Ç", "C").Replace("ç", "c")
            .Replace("(", "\\(").Replace(")", "\\)").Replace("\\", "\\\\");
        return clean;
    }
}
