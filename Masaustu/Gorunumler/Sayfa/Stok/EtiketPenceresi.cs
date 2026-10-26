using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa;

// Ürün listesindeki "Etiket Yazdır": hangi ürünler, kaçar adet ve hangi etiket kâğıdı soruluyor.
public static class EtiketPenceresi
{
    public sealed record Secim(bool Tumu, int Adet, EtiketDuzeni Duzen);

    public static Secim? Goster(string? seciliUrunAdi, int listedekiSayi)
    {
        var secili = new RadioButton { Content = $"Seçili ürün: {seciliUrunAdi}", IsChecked = seciliUrunAdi != null, IsEnabled = seciliUrunAdi != null, Margin = new Thickness(0, 0, 0, 6) };
        var tumu = new RadioButton { Content = $"Listede görünen tüm ürünler ({listedekiSayi} ürün)", IsChecked = seciliUrunAdi == null, Margin = new Thickness(0, 0, 0, 12) };
        var adet = new TextBox { Text = "1", Width = 60 };
        var duzen = new ComboBox
        {
            Width = 320, SelectedIndex = 0,
            ItemsSource = new[] { "A4 etiket kâğıdı - 21'li (63,5 × 38,1 mm)", "A4 etiket kâğıdı - 40'lı (48,5 × 25,4 mm)", "Etiket yazıcısı - rulo (50 × 30 mm)" }
        };
        var hata = new TextBlock { Foreground = Brushes.Firebrick, Margin = new Thickness(0, 8, 0, 0) };
        var tamam = new Button { Content = "🏷️ Etiketleri Oluştur", Padding = new Thickness(12, 6, 12, 6), IsDefault = true, Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        var vazgec = new Button { Content = "Vazgeç", Padding = new Thickness(12, 6, 12, 6), IsCancel = true };

        var kok = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                new TextBlock { Text = "Etiketi basılacak ürünler", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) },
                secili, tumu,
                new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8), Children = { new TextBlock { Text = "Her üründen kaç etiket:", Width = 160, VerticalAlignment = VerticalAlignment.Center }, adet } },
                new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = "Etiket kâğıdı:", Width = 160, VerticalAlignment = VerticalAlignment.Center }, duzen } },
                new TextBlock { Text = "Fiyat KDV dahil basılır. Barkodu olmayan ürüne yalnız ad ve fiyat yazılır.", Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap },
                hata,
                new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0), Children = { tamam, vazgec } }
            }
        };
        var pencere = new Window
        {
            Title = "Ürün Etiketi Yazdır", SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Background = Brushes.White, Content = kok,
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        };
        Secim? sonuc = null;
        tamam.Click += (_, _) =>
        {
            if (!int.TryParse(adet.Text.Trim(), out var n) || n is < 1 or > 500) { hata.Text = "Etiket sayısı 1 ile 500 arasında olmalıdır."; return; }
            sonuc = new Secim(tumu.IsChecked == true, n, (EtiketDuzeni)duzen.SelectedIndex);
            pencere.Close();
        };
        pencere.ShowDialog();
        return sonuc;
    }
}
