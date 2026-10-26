using System.IO.Compression;
using System.Text;
using Altyapi.Servisler;
using Uygulama.Dto.Rapor;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Excel / PDF / yazdırma çıktıları: parantezli metin PDF'i bozmamalı, çok sütunlu rapor yatay basılmalı,
// sayılar Excel'e sayı olarak gitmeli, miktara "TL" eklenmemeli.
public sealed class RaporCiktisiTests
{
    private sealed class Satir
    {
        public string Ad { get; set; } = "";
        public decimal Miktar { get; set; }
        public decimal Tutar { get; set; }
        public int BelgeSayisi { get; set; }
    }

    private static RaporTablosu GenisTablo(int satirSayisi) => new()
    {
        Baslik = "Ba-Bs Formları (Ekim)",
        AltBaslik = "Dönem: 01.10.2026 - 31.10.2026",
        Kolonlar = Enumerable.Range(1, 9).Select(i => new RaporKolonu($"Sütun {i}", i > 6)).ToList(),
        Satirlar = Enumerable.Range(1, satirSayisi).Select(r => Enumerable.Range(1, 9)
            .Select(i => i > 6 ? new RaporHucresi($"{r * 1000m:N2} TL", r * 1000m) : new RaporHucresi($"Çok uzun bir ünvan (Şirket) \\ Ltd. Şti. satır {r}"))
            .ToArray()).ToList()
    };

    [Fact]
    public void Pdf_parantez_ve_ters_boluyu_kacirir_yatay_sayfaya_basar_ve_sayfalara_boler()
    {
        var bytes = new PdfRaporServisi().PdfOlustur(GenisTablo(120));
        var metin = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", metin);
        Assert.Contains("/MediaBox [0 0 842 595]", metin);
        Assert.Contains("Ba-Bs Formlari \\(Ekim\\)", metin);
        Assert.DoesNotContain("\\\\(", metin);
        Assert.Contains("Sayfa 1 / ", metin);
        Assert.True(metin.Split("/Type /Page ").Length - 1 > 1, "120 satır tek sayfaya sığmaz");
        Assert.Contains("Ç", metin); // WinAnsi Ç'yi gösterir, harf korunur

        var klasor = Environment.GetEnvironmentVariable("RAPOR_CIKTI_KLASORU");
        if (!string.IsNullOrEmpty(klasor))
        {
            File.WriteAllBytes(Path.Combine(klasor, "genis.pdf"), bytes);
            File.WriteAllBytes(Path.Combine(klasor, "dar.pdf"), new PdfRaporServisi().ExportToPdf("Mizan Raporu", new SatisRaporFiltreDto(),
                new[] { new Satir { Ad = "100 Kasa (Merkez)", Miktar = 5, Tutar = 1234.5m, BelgeSayisi = 3 } },
                new Dictionary<string, string> { ["Ad"] = "Hesap", ["Miktar"] = "Miktar", ["Tutar"] = "Tutar", ["BelgeSayisi"] = "Belge" }));
            File.WriteAllBytes(Path.Combine(klasor, "rapor.xlsx"), new ExcelExportServisi().ExcelOlustur(GenisTablo(30)));
            File.WriteAllText(Path.Combine(klasor, "rapor.html"), new PdfRaporServisi().HtmlOlustur(GenisTablo(30)), Encoding.UTF8);
        }
    }

    [Fact]
    public void Az_sutunlu_rapor_dikey_basilir_ve_miktara_TL_eklenmez()
    {
        var veri = new[] { new Satir { Ad = "Kalem", Miktar = 5, Tutar = 1234.5m, BelgeSayisi = 3 } };
        var kolonlar = new Dictionary<string, string> { ["Ad"] = "Ad", ["Miktar"] = "Miktar", ["Tutar"] = "Tutar", ["BelgeSayisi"] = "Belge" };

        var tablo = RaporTablosuOlusturucu.Olustur("Test", new SatisRaporFiltreDto(), veri, kolonlar);
        Assert.Equal("5,00", tablo.Satirlar[0][1].Metin);
        Assert.Equal("1.234,50 TL", tablo.Satirlar[0][2].Metin);
        Assert.Equal(3m, tablo.Satirlar[0][3].Sayi);

        // Liste object olarak verilse de (ekrandan gelen satırlar) gerçek türün alanları bulunur.
        var nesneTablo = RaporTablosuOlusturucu.Olustur("Test", null, veri.Cast<object>(), kolonlar);
        Assert.Equal(4, nesneTablo.Kolonlar.Count);

        var pdf = Encoding.Latin1.GetString(new PdfRaporServisi().PdfOlustur(tablo));
        Assert.Contains("/MediaBox [0 0 595 842]", pdf);
    }

    [Fact]
    public void Excel_sayilari_sayi_olarak_yazar_tam_sayiyi_ondaliksiz_bicimler_sayfa_adini_temizler()
    {
        var tablo = new RaporTablosu
        {
            Baslik = "KDV Raporu: Ekim/2026 [taslak]",
            Kolonlar = [new("Ad"), new("Adet", true, false), new("Tutar", true)],
            Satirlar = [[new("A & B <Ltd>"), new("3", 3m), new("1.500,25 TL", 1500.25m)]]
        };
        var bytes = new ExcelExportServisi().ExcelOlustur(tablo);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        string Oku(string ad) { using var r = new StreamReader(zip.GetEntry(ad)!.Open()); return r.ReadToEnd(); }

        var sayfa = Oku("xl/worksheets/sheet1.xml");
        Assert.Contains("s=\"4\"><v>3</v>", sayfa);
        Assert.Contains("s=\"3\"><v>1500.25</v>", sayfa);
        Assert.Contains("A &amp; B &lt;Ltd&gt;", sayfa);
        Assert.Contains("<autoFilter", sayfa);
        Assert.Contains("name=\"KDV Raporu Ekim2026 taslak\"", Oku("xl/workbook.xml"));
    }
}
