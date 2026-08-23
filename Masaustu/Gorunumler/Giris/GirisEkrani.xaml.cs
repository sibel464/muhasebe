using System.Windows;
using Masaustu.GorunumModelleri;
using Masaustu.Gorunumler.Ana;

namespace Masaustu.Gorunumler.Giris;

public partial class GirisEkrani : Window
{
    private readonly GirisGorunumModeli _gorunumModeli;

    public GirisEkrani(GirisGorunumModeli gorunumModeli)
    {
        InitializeComponent();
        _gorunumModeli = gorunumModeli;
        DataContext = _gorunumModeli;

        // Şifreyi PasswordBox'dan al (binding sorunu nedeniyle)
        _gorunumModeli.SifreAl = () => SifreKutusu.Password;

        _gorunumModeli.GirisBasarili = () =>
        {
            var anaPencere = App.HizmetSaglayici.GetService(typeof(AnaPencere)) as AnaPencere;
            anaPencere?.Show();
            this.Close();
        };

        // Sürükle ile taşıma
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
    }

    private void KapatDugmeTiklandi(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
