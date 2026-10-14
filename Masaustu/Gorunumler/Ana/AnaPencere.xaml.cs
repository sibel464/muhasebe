using System.Windows;
using Masaustu.GorunumModelleri;
using Masaustu.Servisler;
using Microsoft.Extensions.DependencyInjection;
using Uygulama.Arayuzler;


namespace Masaustu.Gorunumler.Ana;

public partial class AnaPencere : Window
{
    private readonly AnaGorunumModeli _gorunumModeli;

    public AnaPencere(AnaGorunumModeli gorunumModeli)
    {
        InitializeComponent();
        _gorunumModeli = gorunumModeli;
        DataContext = _gorunumModeli;

        _gorunumModeli.CikisYapildi = () =>
        {
            try
            {
                var kapsam = App.HizmetSaglayici.CreateScope();
                var girisEkrani = kapsam.ServiceProvider.GetRequiredService<Gorunumler.Giris.GirisEkrani>();
                girisEkrani.Closed += (_, _) => kapsam.Dispose();

                App.HizmetSaglayici.GetRequiredService<IKullaniciServisi>().CikisYap();
                var aktifBaglam = App.HizmetSaglayici.GetRequiredService<IAktifFirmaBaglami>();
                aktifBaglam.Degistir(aktifBaglam.SirketId, aktifBaglam.SubeId, aktifBaglam.MaliDonemId, null);

                Application.Current.MainWindow = girisEkrani;
                girisEkrani.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Giriş ekranı açılamadı:\n{ex.Message}",
                    "Hata", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        };

        // Pencere sürükleme (üst bar ile)
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                try { DragMove(); } catch { }
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // Ana pencere kapatıldığında ve başka pencere yoksa uygulamayı sonlandır
        if (Application.Current.MainWindow == this || Application.Current.Windows.Count == 0)
        {
            Application.Current.Shutdown();
        }
    }

    private void SifreDegistirTiklandi(object sender, RoutedEventArgs e)
    {
        var pencere = new Gorunumler.Ortak.SifreDegistirPenceresi(
            App.HizmetSaglayici.GetRequiredService<IKullaniciServisi>(), zorunlu: false) { Owner = this };
        pencere.ShowDialog();
    }

    private void KapatDugmeTiklandi(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();

    private void KucultDugmeTiklandi(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void BuyutDugmeTiklandi(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
}
