using System.Windows;
using Masaustu.GorunumModelleri;
using Masaustu.Servisler;

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
            var girisEkrani = App.HizmetSaglayici.GetService(typeof(Gorunumler.Giris.GirisEkrani)) as Gorunumler.Giris.GirisEkrani;
            girisEkrani?.Show();
            this.Close();
        };

        // Pencere sürükleme (üst bar ile)
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                try { DragMove(); } catch { }
        };
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
