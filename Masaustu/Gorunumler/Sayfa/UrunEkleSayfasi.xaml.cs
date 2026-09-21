using System.Windows.Controls;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa;

public partial class UrunEkleSayfasi : UserControl
{
    public UrunEkleSayfasi()
    {
        InitializeComponent();
    }
    private async void BarkodArama_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && DataContext is UrunEkleGorunumModeli vm)
        { e.Handled = true; await vm.BarkodlaAraAsync(); }
    }
    private void KodEkle_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not UrunEkleGorunumModeli vm) return;
        var deger = KodPenceresiAc(
            "Ürün Kodu Seç / Ekle",
            "Yeni ürün kodunu yazabilir veya daha önce kullanılan kodlardan seçebilirsiniz.",
            vm.KullanilanKodlar,
            vm.KodOtomatik ? string.Empty : vm.Kod,
            "Kod boş bırakılamaz.");
        if (deger == null) return;
        vm.Kodlama = "Manuel";
        vm.Kod = deger;
    }

    private void BarkodEkle_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not UrunEkleGorunumModeli vm) return;
        var deger = KodPenceresiAc(
            "Barkod Seç / Ekle",
            "Yeni barkodu okutabilir, yazabilir veya daha önce kullanılan barkodlardan seçebilirsiniz.",
            vm.KullanilanBarkodlar,
            vm.BarkodOtomatik ? string.Empty : vm.Barkod,
            "Barkod boş bırakılamaz.");
        if (deger == null) return;
        vm.BarkodKodlama = "Manuel";
        vm.Barkod = deger;
    }

    private string? KodPenceresiAc(string baslik, string aciklama, IEnumerable<string> secenekler, string mevcut, string bosUyarisi)
    {
        var kutu = new ComboBox
        {
            IsEditable = true,
            IsTextSearchEnabled = true,
            ItemsSource = secenekler.ToList(),
            Text = mevcut,
            Height = 36,
            Margin = new System.Windows.Thickness(0, 12, 0, 16)
        };
        var kullan = new Button { Content = "Kullan", Width = 96, Height = 36, IsDefault = true, Margin = new System.Windows.Thickness(8, 0, 0, 0) };
        var vazgec = new Button { Content = "Vazgeç", Width = 96, Height = 36, IsCancel = true };
        var dugmeler = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        dugmeler.Children.Add(vazgec);
        dugmeler.Children.Add(kullan);
        var govde = new StackPanel { Margin = new System.Windows.Thickness(24) };
        govde.Children.Add(new TextBlock { Text = aciklama, TextWrapping = System.Windows.TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.SlateGray });
        govde.Children.Add(kutu);
        govde.Children.Add(dugmeler);
        var pencere = new System.Windows.Window
        {
            Title = baslik,
            Width = 470,
            Height = 205,
            Content = govde,
            Owner = System.Windows.Window.GetWindow(this),
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            ResizeMode = System.Windows.ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        kullan.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(kutu.Text))
            {
                System.Windows.MessageBox.Show(pencere, bosUyarisi, "Eksik bilgi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            pencere.DialogResult = true;
        };
        pencere.Loaded += (_, _) => { kutu.Focus(); kutu.IsDropDownOpen = kutu.Items.Count > 0; };
        return pencere.ShowDialog() == true ? kutu.Text.Trim() : null;
    }
    private async void TanimYonet_Click(object sender,System.Windows.RoutedEventArgs e)
    {
        if(sender is not Button b||b.Tag is not string grup)return;
        var s=App.HizmetSaglayici.GetService(typeof(IMerkeziTanimServisi)) as IMerkeziTanimServisi;if(s==null)return;
        var baslik=grup=="MARKA"?"Markalar":grup=="BIRIM"?"Birimler":"Ürün Kategorileri";
        var p=new MerkeziTanimYonetimPenceresi(s,grup,baslik){Owner=System.Windows.Window.GetWindow(this)};p.ShowDialog();
        if(DataContext is UrunEkleGorunumModeli vm)await vm.TanimlariYukleAsync(grup,p.GorunumModeli.KaydedilenId);
    }
}
