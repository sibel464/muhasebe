using System.Windows;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Ortak;

public partial class EpostaGonderPenceresi : Window
{
    public EpostaGonderPenceresi(EpostaGonderModeli model)
    {
        InitializeComponent();
        DataContext = model;
        model.Gonderildi = () =>
        {
            MessageBox.Show(this, "E-posta gönderildi.", "E-posta", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        };
    }

    // Ekranlar pencereyi tek satırla açsın; servis yoksa (eski kurulum) sessizce çıkılmaz, kullanıcıya söylenir.
    public static void Ac(Window? sahip, string alicilar, string konu, string metin, params EpostaEkSecenegi[] ekler)
    {
        if (App.HizmetSaglayici?.GetService(typeof(Uygulama.Arayuzler.IEpostaServisi)) is not Uygulama.Arayuzler.IEpostaServisi servis)
        {
            MessageBox.Show("E-posta servisi bulunamadı.", "E-posta", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        new EpostaGonderPenceresi(new EpostaGonderModeli(servis, alicilar, konu, metin, ekler)) { Owner = sahip ?? Application.Current.MainWindow }.ShowDialog();
    }
}
