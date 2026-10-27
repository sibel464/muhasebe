using System.ComponentModel;
using System.Windows;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa.Musteri;

public partial class VadeHatirlatmaPenceresi : Window
{
    public VadeHatirlatmaPenceresi(VadeHatirlatmaGorunumModeli model)
    {
        InitializeComponent();
        DataContext = model;
        Loaded += async (_, _) => await model.ListeleAsync();
    }

    // Gönderim sürerken pencere kapanırsa yarıda kalan iletinin durumu bilinmez; önce durdurulmalı.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is VadeHatirlatmaGorunumModeli { Calisiyor: true })
        {
            MessageBox.Show(this, "Gönderim sürüyor. Önce 'Durdur'a basın.", "Vade Hatırlatma", MessageBoxButton.OK, MessageBoxImage.Information);
            e.Cancel = true;
        }
        base.OnClosing(e);
    }

    public static void Ac(Window? sahip)
    {
        var sp = App.HizmetSaglayici;
        if (sp?.GetService(typeof(ICariServisi)) is not ICariServisi cari
            || sp.GetService(typeof(ICariHareketServisi)) is not ICariHareketServisi hareket
            || sp.GetService(typeof(IBelgeYazdirmaServisi)) is not IBelgeYazdirmaServisi yazdirma
            || sp.GetService(typeof(IEpostaServisi)) is not IEpostaServisi eposta
            || sp.GetService(typeof(IRaporMerkeziServisi)) is not IRaporMerkeziServisi rapor)
        {
            MessageBox.Show("Gerekli servisler bulunamadı.", "Vade Hatırlatma", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var model = new VadeHatirlatmaGorunumModeli(rapor, cari, hareket, yazdirma, eposta);
        new VadeHatirlatmaPenceresi(model) { Owner = sahip ?? Application.Current.MainWindow }.ShowDialog();
    }
}
