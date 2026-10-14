using System.Windows;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Ortak;

// Boşta kalma süresi dolunca açılır; oturum sahibi şifresiyle devam eder ya da çıkış yapar.
// DialogResult true: kilit açıldı, false: çıkış yapılmalı.
public partial class OturumKilidiPenceresi : Window
{
    private readonly IKullaniciServisi _kullaniciServisi;
    private readonly string _kullaniciAdi;

    public OturumKilidiPenceresi(IKullaniciServisi kullaniciServisi, int dakika)
    {
        InitializeComponent();
        _kullaniciServisi = kullaniciServisi;
        _kullaniciAdi = kullaniciServisi.AktifKullanici?.KullaniciAdi ?? string.Empty;
        AciklamaMetni.Text = $"Program {dakika} dakikadır kullanılmadığı için güvenlik amacıyla kilitlendi. " +
                             $"Devam etmek için '{_kullaniciAdi}' kullanıcısının şifresini girin.";
        Loaded += (_, _) => SifreKutusu.Focus();
    }

    private async void KilidiAcTiklandi(object sender, RoutedEventArgs e)
    {
        HataMetni.Visibility = Visibility.Collapsed;
        if (string.IsNullOrEmpty(SifreKutusu.Password))
        {
            HataGoster("Şifrenizi girin.");
            return;
        }

        KilidiAcDugmesi.IsEnabled = false;
        try
        {
            var sonuc = await _kullaniciServisi.GirisDeneAsync(_kullaniciAdi, SifreKutusu.Password);
            if (sonuc.Basarili)
            {
                DialogResult = true;
                return;
            }

            if (sonuc.Durum == GirisDurumu.HesapKilitli)
            {
                MessageBox.Show(this, "Çok sayıda hatalı deneme nedeniyle hesap kilitlendi. Oturum kapatılıyor.",
                    "Hesap Kilitlendi", MessageBoxButton.OK, MessageBoxImage.Warning);
                DialogResult = false;
                return;
            }

            SifreKutusu.Clear();
            HataGoster(sonuc.KalanDeneme is > 0 and <= 2
                ? $"Şifre hatalı. Hesap kilitlenmeden önce {sonuc.KalanDeneme} deneme hakkınız kaldı."
                : "Şifre hatalı.");
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "OturumKilidi");
            HataGoster("Şifre doğrulanamadı. Lütfen veritabanı bağlantınızı kontrol edin.");
        }
        finally
        {
            KilidiAcDugmesi.IsEnabled = true;
        }
    }

    private void HataGoster(string mesaj)
    {
        HataMetni.Text = mesaj;
        HataMetni.Visibility = Visibility.Visible;
    }

    private void CikisTiklandi(object sender, RoutedEventArgs e) => DialogResult = false;
}
