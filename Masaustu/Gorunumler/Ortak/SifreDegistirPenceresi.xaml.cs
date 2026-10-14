using System.Windows;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Ortak;

public partial class SifreDegistirPenceresi : Window
{
    private readonly IKullaniciServisi _kullaniciServisi;

    public SifreDegistirPenceresi(IKullaniciServisi kullaniciServisi, bool zorunlu)
    {
        InitializeComponent();
        _kullaniciServisi = kullaniciServisi;

        if (zorunlu)
        {
            BaslikMetni.Text = "Şifrenizi değiştirmeniz gerekiyor";
            AciklamaMetni.Text = "Geçici veya varsayılan bir şifreyle giriş yaptınız. Devam etmek için kendi şifrenizi belirleyin. " +
                                 "Yeni şifre en az 8 karakter olmalı, harf ve rakam içermeli ve kullanıcı adınızı içermemelidir.";
            IptalDugmesi.Content = "Çıkış";
        }

        Loaded += (_, _) => MevcutSifreKutusu.Focus();
    }

    private async void KaydetTiklandi(object sender, RoutedEventArgs e)
    {
        HataMetni.Visibility = Visibility.Collapsed;

        if (YeniSifreKutusu.Password != YeniSifreTekrarKutusu.Password)
        {
            HataGoster("Yeni şifre ile tekrarı aynı değil.");
            return;
        }

        KaydetDugmesi.IsEnabled = false;
        try
        {
            await _kullaniciServisi.KendiSifreniDegistirAsync(MevcutSifreKutusu.Password, YeniSifreKutusu.Password);
            MessageBox.Show(this, "Şifreniz değiştirildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or YetkisizErisimException)
        {
            HataGoster(ex.Message);
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "SifreDegistir");
            HataGoster("Şifre değiştirilemedi. Lütfen veritabanı bağlantınızı kontrol edin.");
        }
        finally
        {
            KaydetDugmesi.IsEnabled = true;
        }
    }

    private void HataGoster(string mesaj)
    {
        HataMetni.Text = mesaj;
        HataMetni.Visibility = Visibility.Visible;
    }

    private void IptalTiklandi(object sender, RoutedEventArgs e) => DialogResult = false;
}
