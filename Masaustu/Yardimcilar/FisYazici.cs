using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Ortak;

namespace Masaustu.Yardimcilar;

// Hızlı satış sonrası 80 mm termal yazıcıya bilgi fişi. Mali belge değildir (yasal fiş yazar kasa/ÖKC ya da e-Arşiv faturadır).
public static class FisYazici
{
    private static readonly CultureInfo Tr = new("tr-TR");
    private const double Genislik = 280; // ≈ 74 mm yazdırılabilir alan (96 dpi)

    // Bu bilgisayarda satıştan sonra fiş yazdırılsın mı (kasa başına tercih).
    private static string TercihDosyasi => Path.Combine(Altyapi.Veri.UygulamaKlasorleri.VeriKoku, "pos-fis-yazdir.txt");
    public static bool OtomatikYazdir
    {
        get { try { return File.Exists(TercihDosyasi) && File.ReadAllText(TercihDosyasi).Trim() == "1"; } catch { return false; } }
        set { try { Directory.CreateDirectory(Path.GetDirectoryName(TercihDosyasi)!); File.WriteAllText(TercihDosyasi, value ? "1" : "0"); } catch { } }
    }

    public static FlowDocument Belge(Fatura fatura, FirmaBilgisi firma, string odemeYontemi)
    {
        var belge = new FlowDocument { PageWidth = Genislik + 20, PagePadding = new Thickness(10), ColumnWidth = Genislik, FontFamily = new FontFamily("Consolas, Courier New"), FontSize = 11 };
        Paragraph P(string metin, double boyut = 11, FontWeight? kalinlik = null, TextAlignment hiza = TextAlignment.Left) =>
            new(new Run(metin)) { FontSize = boyut, FontWeight = kalinlik ?? FontWeights.Normal, TextAlignment = hiza, Margin = new Thickness(0) };
        Paragraph Cizgi() => P(new string('-', 40), 9, hiza: TextAlignment.Center);

        if (!string.IsNullOrWhiteSpace(firma.FirmaUnvani)) belge.Blocks.Add(P(firma.FirmaUnvani, 13, FontWeights.Bold, TextAlignment.Center));
        var adres = string.Join(" ", new[] { firma.Adres, firma.Ilce, firma.Il }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (adres.Length > 0) belge.Blocks.Add(P(adres, 9, hiza: TextAlignment.Center));
        if (!string.IsNullOrWhiteSpace(firma.VergiNo)) belge.Blocks.Add(P($"{firma.VergiDairesi} V.D. {firma.VergiNo}", 9, hiza: TextAlignment.Center));
        if (!string.IsNullOrWhiteSpace(firma.Telefon)) belge.Blocks.Add(P("Tel: " + firma.Telefon, 9, hiza: TextAlignment.Center));
        belge.Blocks.Add(Cizgi());
        belge.Blocks.Add(P($"Tarih: {fatura.Tarih:dd.MM.yyyy HH:mm}", 10));
        belge.Blocks.Add(P($"Belge: {fatura.FaturaNo}", 10));
        if (fatura.CariKart is { } c && !c.Unvan.Contains("PERAKENDE", StringComparison.OrdinalIgnoreCase)) belge.Blocks.Add(P("Müşteri: " + c.Unvan, 10));
        belge.Blocks.Add(Cizgi());

        var tablo = new Table { CellSpacing = 0, Margin = new Thickness(0) };
        tablo.Columns.Add(new TableColumn { Width = new GridLength(Genislik * 0.62) });
        tablo.Columns.Add(new TableColumn { Width = new GridLength(Genislik * 0.38) });
        var grup = new TableRowGroup();
        foreach (var k in fatura.Kalemler)
        {
            grup.Rows.Add(new TableRow { Cells = { new TableCell(P(k.UrunAd, 10)) { ColumnSpan = 2 } } });
            grup.Rows.Add(new TableRow
            {
                Cells =
                {
                    new TableCell(P($"  {k.Miktar.ToString("0.###", Tr)} {k.Birim} x {k.BirimFiyat.ToString("N2", Tr)}", 9)),
                    new TableCell(P(k.SatirToplami.ToString("N2", Tr), 10, hiza: TextAlignment.Right))
                }
            });
        }
        tablo.RowGroups.Add(grup);
        belge.Blocks.Add(tablo);
        belge.Blocks.Add(Cizgi());

        void Toplam(string ad, decimal tutar, bool kalin = false) => belge.Blocks.Add(new Paragraph
        {
            Margin = new Thickness(0), FontWeight = kalin ? FontWeights.Bold : FontWeights.Normal, FontSize = kalin ? 14 : 10,
            Inlines = { new Run(ad), new Run(" "), new Run(tutar.ToString("N2", Tr) + " ₺") }, TextAlignment = TextAlignment.Right
        });
        if (fatura.SatirIskontosuToplam + fatura.GenelIskontoTutari > 0) Toplam("İskonto:", -(fatura.SatirIskontosuToplam + fatura.GenelIskontoTutari));
        Toplam("KDV:", fatura.KdvToplam);
        Toplam("TOPLAM:", fatura.OdenecekTutar, kalin: true);
        belge.Blocks.Add(P("Ödeme: " + odemeYontemi, 10, hiza: TextAlignment.Right));
        belge.Blocks.Add(Cizgi());
        if (!string.IsNullOrWhiteSpace(firma.BelgeNotu)) belge.Blocks.Add(P(firma.BelgeNotu, 10, hiza: TextAlignment.Center));
        belge.Blocks.Add(P("Bu belge mali değeri olmayan bilgi fişidir.", 8, hiza: TextAlignment.Center));
        return belge;
    }

    // yaziciSor: yazıcı seçim penceresi açılır; değilse varsayılan yazıcıya doğrudan basılır.
    public static void Yazdir(Fatura fatura, FirmaBilgisi firma, string odemeYontemi, bool yaziciSor)
    {
        var dialog = new PrintDialog();
        if (yaziciSor && dialog.ShowDialog() != true) return;
        var belge = Belge(fatura, firma, odemeYontemi);
        dialog.PrintDocument(((IDocumentPaginatorSource)belge).DocumentPaginator, "Satış Fişi " + fatura.FaturaNo);
    }
}
