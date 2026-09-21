using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Uygulama.Arayuzler;
using Uygulama.Dto.Rapor;

namespace Altyapi.Servisler;

public class ExcelExportServisi : IExcelExportServisi
{
    public byte[] ExportToExcel<T>(string raporBasligi, SatisRaporFiltreDto filtre, IEnumerable<T> veriler, Dictionary<string, string> kolonBasliklari)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true, Encoding.UTF8))
        {
            // 1. [Content_Types].xml
            var contentTypes = archive.CreateEntry("[Content_Types].xml", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(contentTypes.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                      <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                      <Default Extension="xml" ContentType="application/xml"/>
                      <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                      <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                      <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                    </Types>
                    """);
            }

            // 2. _rels/.rels
            var mainRels = archive.CreateEntry("_rels/.rels", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(mainRels.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                    </Relationships>
                    """);
            }

            // 3. xl/_rels/workbook.xml.rels
            var wbRels = archive.CreateEntry("xl/_rels/workbook.xml.rels", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(wbRels.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                      <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                    </Relationships>
                    """);
            }

            // 4. xl/workbook.xml
            var wb = archive.CreateEntry("xl/workbook.xml", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(wb.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                      <sheets>
                        <sheet name="Rapor" sheetId="1" r:id="rId1"/>
                      </sheets>
                    </workbook>
                    """);
            }

            // 5. xl/styles.xml
            var styles = archive.CreateEntry("xl/styles.xml", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(styles.Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                      <numFmts count="2">
                        <numFmt numFmtId="164" formatCode="#,##0.00"/>
                        <numFmt numFmtId="165" formatCode="yyyy-mm-dd hh:mm"/>
                      </numFmts>
                      <fonts count="3">
                        <font><sz val="10"/><name val="Segoe UI"/><color rgb="FF000000"/></font>
                        <font><b/><sz val="14"/><name val="Segoe UI"/><color rgb="FF1E3A8A"/></font>
                        <font><b/><sz val="10"/><name val="Segoe UI"/><color rgb="FFFFFFFF"/></font>
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
                      <cellXfs count="4">
                        <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
                        <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0"/>
                        <xf numFmtId="0" fontId="2" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
                        <xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"><alignment horizontal="right"/></xf>
                      </cellXfs>
                    </styleSheet>
                    """);
            }

            // 6. xl/worksheets/sheet1.xml
            var sheet = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(sheet.Open(), Encoding.UTF8))
            {
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                writer.WriteLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
                writer.WriteLine("  <sheetData>");

                int rowIndex = 1;

                // Title row
                writer.WriteLine($"    <row r=\"{rowIndex}\">");
                writer.WriteLine($"      <c r=\"A{rowIndex}\" s=\"1\" t=\"inlineStr\"><is><t>{EscapeXml(raporBasligi)}</t></is></c>");
                writer.WriteLine("    </row>");
                rowIndex++;

                // Subtitle
                writer.WriteLine($"    <row r=\"{rowIndex}\">");
                writer.WriteLine($"      <c r=\"A{rowIndex}\" t=\"inlineStr\"><is><t>Oluşturulma Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm} | Preset: {EscapeXml(filtre.PresetKey)}</t></is></c>");
                writer.WriteLine("    </row>");
                rowIndex++;

                // Blank row
                rowIndex++;

                // Header row
                writer.WriteLine($"    <row r=\"{rowIndex}\">");
                int colIndex = 1;
                foreach (var header in kolonBasliklari.Values)
                {
                    var colRef = GetColumnRef(colIndex);
                    writer.WriteLine($"      <c r=\"{colRef}{rowIndex}\" s=\"2\" t=\"inlineStr\"><is><t>{EscapeXml(header)}</t></is></c>");
                    colIndex++;
                }
                writer.WriteLine("    </row>");
                rowIndex++;

                // Data Rows
                var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var propMap = kolonBasliklari.Keys.Select(k => props.FirstOrDefault(p => p.Name == k)).Where(p => p != null).ToList();

                foreach (var item in veriler)
                {
                    writer.WriteLine($"    <row r=\"{rowIndex}\">");
                    colIndex = 1;
                    foreach (var prop in propMap)
                    {
                        var colRef = GetColumnRef(colIndex);
                        if (prop == null)
                        {
                            colIndex++;
                            continue;
                        }

                        var val = prop.GetValue(item);
                        if (val == null)
                        {
                            writer.WriteLine($"      <c r=\"{colRef}{rowIndex}\" t=\"inlineStr\"><is><t></t></is></c>");
                        }
                        else if (val is decimal decVal)
                        {
                            writer.WriteLine($"      <c r=\"{colRef}{rowIndex}\" s=\"3\"><v>{decVal.ToString(System.Globalization.CultureInfo.InvariantCulture)}</v></c>");
                        }
                        else if (val is double dblVal)
                        {
                            writer.WriteLine($"      <c r=\"{colRef}{rowIndex}\" s=\"3\"><v>{dblVal.ToString(System.Globalization.CultureInfo.InvariantCulture)}</v></c>");
                        }
                        else if (val is int intVal)
                        {
                            writer.WriteLine($"      <c r=\"{colRef}{rowIndex}\"><v>{intVal}</v></c>");
                        }
                        else if (val is DateTime dtVal)
                        {
                            writer.WriteLine($"      <c r=\"{colRef}{rowIndex}\" t=\"inlineStr\"><is><t>{dtVal:dd.MM.yyyy HH:mm}</t></is></c>");
                        }
                        else
                        {
                            writer.WriteLine($"      <c r=\"{colRef}{rowIndex}\" t=\"inlineStr\"><is><t>{EscapeXml(val.ToString())}</t></is></c>");
                        }
                        colIndex++;
                    }
                    writer.WriteLine("    </row>");
                    rowIndex++;
                }

                writer.WriteLine("  </sheetData>");
                writer.WriteLine("</worksheet>");
            }
        }

        return ms.ToArray();
    }

    private static string GetColumnRef(int colIndex)
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

    private static string EscapeXml(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }
}
