using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using EtkiAlani.Modeller.Fatura;
using Masaustu.Yardimcilar;

namespace Masaustu.Gorunumler.Sayfa;

// Fatura listesindeki "Detay": faturanın başlığı, kalemleri ve toplamları salt okunur gösterilir.
public static class FaturaDetayPenceresi
{
    private static readonly FaturaMetinDonusturucu Metin = new();

    public static void Goster(Fatura fatura)
    {
        string M(object deger) => (string)Metin.Convert(deger, typeof(string), null!, System.Globalization.CultureInfo.CurrentCulture);

        var kok = new DockPanel { Margin = new Thickness(20) };

        var baslik = new TextBlock
        {
            FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14),
            Text = $"{fatura.FaturaNo}  •  {M(fatura.Tur)} faturası  •  {M(fatura.Durum)}\n" +
                   $"Tarih: {fatura.Tarih:dd.MM.yyyy HH:mm}" + (fatura.VadeTarihi is DateTime v ? $"   Vade: {v:dd.MM.yyyy}" : string.Empty) +
                   $"   Ödeme: {M(fatura.OdemeSekli)}\n" +
                   $"Cari: {fatura.CariKart?.Kod} - {fatura.CariKart?.Unvan}" +
                   (string.IsNullOrWhiteSpace(fatura.Aciklama) ? string.Empty : $"\nAçıklama: {fatura.Aciklama}")
        };
        DockPanel.SetDock(baslik, Dock.Top);
        kok.Children.Add(baslik);

        var toplamlar = new TextBlock
        {
            FontSize = 14, HorizontalAlignment = HorizontalAlignment.Right, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 14, 0, 0),
            Text = $"Ara Toplam: {fatura.AraToplam:N2} ₺\n" +
                   $"Satır İskontoları: {fatura.SatirIskontosuToplam:N2} ₺\n" +
                   $"Genel İskonto (%{fatura.GenelIskontoOrani:N2}): {fatura.GenelIskontoTutari:N2} ₺\n" +
                   $"KDV: {fatura.KdvToplam:N2} ₺" +
                   (fatura.TevkifatTutari > 0
                       ? $"\nVergiler Dahil Toplam: {fatura.GenelToplam:N2} ₺\nKDV Tevkifatı ({fatura.TevkifatKodu}, {fatura.TevkifatOrani * 10:0}/10): -{fatura.TevkifatTutari:N2} ₺"
                       : string.Empty)
        };
        var genelToplam = new TextBlock
        {
            FontSize = 18, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right,
            Text = fatura.TevkifatTutari > 0 ? $"Ödenecek Tutar: {fatura.OdenecekTutar:N2} ₺" : $"Genel Toplam: {fatura.GenelToplam:N2} ₺"
        };
        var alt = new StackPanel();
        alt.Children.Add(toplamlar);
        alt.Children.Add(genelToplam);
        DockPanel.SetDock(alt, Dock.Bottom);
        kok.Children.Add(alt);

        var tablo = new DataGrid
        {
            ItemsSource = fatura.Kalemler.Where(k => !k.Silindi).ToList(), AutoGenerateColumns = false, IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column, CanUserAddRows = false, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Background = Brushes.White, RowHeight = 28
        };
        void Kolon(string ad, string yol, string? bicim = null, bool sag = false, double genislik = 100)
        {
            var kolon = new DataGridTextColumn { Header = ad, Binding = new Binding(yol) { StringFormat = bicim }, Width = genislik };
            if (sag) kolon.ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right) } };
            tablo.Columns.Add(kolon);
        }
        Kolon("Kod", nameof(FaturaKalemi.UrunKodu), genislik: 110);
        tablo.Columns.Add(new DataGridTextColumn { Header = "Ürün", Binding = new Binding(nameof(FaturaKalemi.UrunAd)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Kolon("Miktar", nameof(FaturaKalemi.Miktar), "N2", true, 80);
        Kolon("Birim", nameof(FaturaKalemi.Birim), genislik: 60);
        Kolon("Birim Fiyat", nameof(FaturaKalemi.BirimFiyat), "N2", true);
        Kolon("İsk. %", nameof(FaturaKalemi.IskontoOrani), "N2", true, 60);
        Kolon("KDV %", nameof(FaturaKalemi.KdvOrani), "N0", true, 60);
        Kolon("KDV", nameof(FaturaKalemi.KdvTutari), "N2", true, 90);
        Kolon("Satır Toplamı", nameof(FaturaKalemi.SatirToplami), "N2", true, 110);
        kok.Children.Add(tablo);

        var pencere = new Window
        {
            Title = $"Fatura Detayı - {fatura.FaturaNo}", Width = 960, Height = 580, MinWidth = 700, MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Background = Brushes.White, Content = kok,
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        };
        pencere.ShowDialog();
    }
}
