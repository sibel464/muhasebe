using System.Windows.Controls;
using System.Windows;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa;

public partial class CariEkleSayfasi : UserControl
{
    public CariEkleSayfasi()
    {
        InitializeComponent();
    }

    private async void CariTipiYonet_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var servis = App.HizmetSaglayici.GetService(typeof(Uygulama.Arayuzler.ICariTipiTanimiServisi)) as Uygulama.Arayuzler.ICariTipiTanimiServisi;
        if (servis == null) return;
        var pencere = new CariTipiYonetimPenceresi(servis) { Owner = System.Windows.Window.GetWindow(this) };
        pencere.ShowDialog();
        if (DataContext is CariEkleGorunumModeli vm)
            await vm.CariTipleriniYukleAsync(pencere.GorunumModeli.KaydedilenId);
    }

    private void SadeceRakamGirisi(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]+$");
    }

    private void CariKoduEkle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CariEkleGorunumModeli vm) return;
        var deger = KodPenceresiAc("Cari Kodu Seç / Ekle", "Yeni cari kodunu yazabilir veya mevcut kodlardan seçebilirsiniz.", vm.CariKodlari, vm.KodManuel ? vm.Kod : "");
        if (deger == null) return;
        vm.Kodlama = "Manuel";
        vm.Kod = deger;
    }

    private void GrupKoduEkle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CariEkleGorunumModeli vm) return;
        var deger = KodPenceresiAc("Grup Kodu Seç / Ekle", "Yeni grup kodunu yazabilir veya mevcut gruplardan seçebilirsiniz.", vm.GrupKodlari, vm.GrupKoduManuel ? vm.GrupKodu : "");
        if (deger == null) return;
        vm.GrupKodlama = "Manuel";
        vm.GrupKodu = deger;
    }

    private string? KodPenceresiAc(string baslik, string aciklama, IEnumerable<string> secenekler, string mevcut)
    {
        var kutu = new ComboBox { IsEditable = true, IsTextSearchEnabled = true, ItemsSource = secenekler.ToList(), Text = mevcut, Height = 34, Margin = new Thickness(0, 10, 0, 14) };
        var tamam = new Button { Content = "Kullan", Width = 90, Height = 34, IsDefault = true, Margin = new Thickness(8, 0, 0, 0) };
        var vazgec = new Button { Content = "Vazgeç", Width = 90, Height = 34, IsCancel = true };
        var dugmeler = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        dugmeler.Children.Add(vazgec); dugmeler.Children.Add(tamam);
        var govde = new StackPanel { Margin = new Thickness(22) };
        govde.Children.Add(new TextBlock { Text = aciklama, TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.SlateGray });
        govde.Children.Add(kutu); govde.Children.Add(dugmeler);
        var pencere = new Window { Title = baslik, Width = 440, Height = 190, Content = govde, Owner = Window.GetWindow(this), WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
        tamam.Click += (_, _) => { if (string.IsNullOrWhiteSpace(kutu.Text)) { MessageBox.Show(pencere, "Kod boş bırakılamaz.", "Eksik bilgi", MessageBoxButton.OK, MessageBoxImage.Warning); return; } pencere.DialogResult = true; };
        pencere.Loaded += (_, _) => { kutu.Focus(); };
        return pencere.ShowDialog() == true ? kutu.Text.Trim() : null;
    }
}
