using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Masaustu.GorunumModelleri;
using Microsoft.Extensions.Configuration;
using Masaustu.Servisler;
using Microsoft.Extensions.DependencyInjection;
using Uygulama.Arayuzler;


namespace Masaustu.Gorunumler.Ana;

public partial class AnaPencere : Window
{
    private readonly AnaGorunumModeli _gorunumModeli;

    // Oturum zaman aşımı: belirtilen süre klavye/fare hareketi olmazsa ekran kilitlenir (0 = kapalı).
    private readonly int _zamanAsimiDakika;
    private readonly DispatcherTimer? _bostaKalmaZamanlayicisi;
    private DateTime _sonEtkinlik = DateTime.UtcNow;
    private bool _kilitli;

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

        var ayar = App.HizmetSaglayici.GetRequiredService<IConfiguration>()["UygulamaAyarlari:OturumZamanAsimiDakika"];
        _zamanAsimiDakika = int.TryParse(ayar, out var dakika) ? Math.Max(0, dakika) : 15;
        if (_zamanAsimiDakika > 0)
        {
            InputManager.Current.PreProcessInput += GirdiAlgilandi;
            _bostaKalmaZamanlayicisi = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _bostaKalmaZamanlayicisi.Tick += (_, _) => ZamanAsiminiDenetle();
            _bostaKalmaZamanlayicisi.Start();
        }

        // Pencere sürükleme (üst bar ile)
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                try { DragMove(); } catch { }
        };
    }

    private void GirdiAlgilandi(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is KeyboardEventArgs or MouseButtonEventArgs or MouseWheelEventArgs or TouchEventArgs ||
            e.StagingItem.Input.RoutedEvent == Mouse.MouseMoveEvent)
            _sonEtkinlik = DateTime.UtcNow;
    }

    private void ZamanAsiminiDenetle()
    {
        if (_kilitli || DateTime.UtcNow - _sonEtkinlik < TimeSpan.FromMinutes(_zamanAsimiDakika))
            return;
        OturumuKilitle();
    }

    private void OturumuKilitle()
    {
        _kilitli = true;
        // Kilit sırasında ekrandaki veriler okunmasın diye açık pencereler bulanıklaştırılır.
        var pencereler = Application.Current.Windows.OfType<Window>().Where(w => w.IsVisible).ToList();
        foreach (var w in pencereler)
            w.Effect = new BlurEffect { Radius = 25 };

        bool? sonuc;
        try
        {
            var kilit = new Gorunumler.Ortak.OturumKilidiPenceresi(App.HizmetSaglayici.GetRequiredService<IKullaniciServisi>(), _zamanAsimiDakika)
            {
                Owner = pencereler.LastOrDefault(w => w.IsActive) ?? this,
                Topmost = true
            };
            sonuc = kilit.ShowDialog();
        }
        finally
        {
            foreach (var w in pencereler)
                w.Effect = null;
            _sonEtkinlik = DateTime.UtcNow;
            _kilitli = false;
        }

        if (sonuc != true)
        {
            // Ana pencerenin üstünde açık kalan alt pencereler de oturumla birlikte kapanır.
            foreach (var w in pencereler.Where(w => w != this))
                w.Close();
            _gorunumModeli.CikisYapildi?.Invoke();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _bostaKalmaZamanlayicisi?.Stop();
        if (_zamanAsimiDakika > 0)
            InputManager.Current.PreProcessInput -= GirdiAlgilandi;
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
