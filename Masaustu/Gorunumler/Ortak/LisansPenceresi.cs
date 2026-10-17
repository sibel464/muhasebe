using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Masaustu.Servisler;

namespace Masaustu.Gorunumler.Ortak;

// Lisans durumu, makine kodu ve lisans dosyası yükleme. Deneme bittiğinde açılışta, istendiğinde Ayarlar'dan açılır.
// DialogResult true: program kullanılabilir durumda (geçerli lisans ya da süren deneme).
public sealed class LisansPenceresi : Window
{
    private readonly TextBlock _durum = new() { TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 0, 0, 14) };
    private readonly Button _devam = new() { Content = "Devam Et", Width = 120, Height = 34, Margin = new Thickness(8, 0, 0, 0), IsDefault = true };

    public LisansPenceresi(bool acilistaZorunlu)
    {
        Title = "Lisans";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.White;

        var makineKodu = LisansYoneticisi.MakineKodu();
        var kod = new TextBox { Text = makineKodu, IsReadOnly = true, FontSize = 18, FontFamily = new FontFamily("Consolas"), Padding = new Thickness(8, 4, 8, 4), Width = 300 };
        var kopyala = new Button { Content = "Kopyala", Height = 32, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(8, 0, 0, 0) };
        kopyala.Click += (_, _) => { try { Clipboard.SetText(makineKodu); kopyala.Content = "Kopyalandı"; } catch { } };

        var yukle = new Button { Content = "Lisans Dosyası Yükle...", Height = 34, Padding = new Thickness(14, 0, 14, 0) };
        yukle.Click += (_, _) => Yukle();
        var kapat = new Button { Content = acilistaZorunlu ? "Programı Kapat" : "Kapat", Height = 34, Padding = new Thickness(14, 0, 14, 0), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        kapat.Click += (_, _) => DialogResult = acilistaZorunlu ? false : LisansYoneticisi.DurumGetir().Kullanilabilir;
        _devam.Click += (_, _) => DialogResult = true;

        var kodSatiri = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 14) };
        kodSatiri.Children.Add(kod);
        kodSatiri.Children.Add(kopyala);

        var dugmeler = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        dugmeler.Children.Add(yukle);
        dugmeler.Children.Add(kapat);
        dugmeler.Children.Add(_devam);

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = "Muhasebe Yönetim Sistemi — Lisans", FontSize = 17, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });
        panel.Children.Add(_durum);
        panel.Children.Add(new TextBlock { Text = "Bu bilgisayarın makine kodu:", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(kodSatiri);
        panel.Children.Add(new TextBlock
        {
            Text = "Lisans almak için bu makine kodunu satıcınıza iletin. Size gönderilen lisans dosyasını \"Lisans Dosyası Yükle\" ile seçin. " +
                   "Lisans yalnız bu bilgisayarda geçerlidir. Verileriniz ve yedekleriniz lisans durumundan etkilenmez.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 16)
        });
        panel.Children.Add(dugmeler);
        Content = panel;
        DurumuGoster();
    }

    private void DurumuGoster()
    {
        var durum = LisansYoneticisi.DurumGetir();
        _durum.Text = durum.Aciklama;
        _durum.Foreground = durum.Lisans?.GecerliMi == true ? Brushes.DarkGreen : durum.Kullanilabilir ? Brushes.DarkGoldenrod : Brushes.DarkRed;
        _devam.IsEnabled = durum.Kullanilabilir;
    }

    private void Yukle()
    {
        var secim = new Microsoft.Win32.OpenFileDialog { Title = "Lisans dosyasını seçin", Filter = "Lisans dosyası (*.lic)|*.lic|Tüm dosyalar (*.*)|*.*" };
        if (secim.ShowDialog(this) != true) return;
        try
        {
            var sonuc = LisansYoneticisi.Yukle(secim.FileName);
            MessageBox.Show(this, sonuc.Aciklama, sonuc.GecerliMi ? "Lisans yüklendi" : "Lisans yüklenemedi",
                MessageBoxButton.OK, sonuc.GecerliMi ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Lisans dosyası okunamadı: {ex.Message}", "Lisans", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        DurumuGoster();
    }
}
