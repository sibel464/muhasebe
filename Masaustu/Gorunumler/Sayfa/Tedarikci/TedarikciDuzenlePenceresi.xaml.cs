using System.Windows;
using EtkiAlani.Modeller.Cari;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa.Tedarikci;

public partial class TedarikciDuzenlePenceresi : Window
{
    public bool Kaydedildi { get; private set; }

    public TedarikciDuzenlePenceresi(ICariServisi cariServisi, ICariTipiTanimiServisi cariTipiServisi, CariKart? mevcutCari = null)
    {
        InitializeComponent();
        var vm = new TedarikciDuzenleGorunumModeli(cariServisi, cariTipiServisi, mevcutCari);
        vm.PencereKapat = kaydedildi =>
        {
            Kaydedildi = kaydedildi;
            this.Close();
        };
        DataContext = vm;

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                try { DragMove(); } catch { }
        };
    }

    private async void CariTipiYonet_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TedarikciDuzenleGorunumModeli vm) return;
        var servis = App.HizmetSaglayici.GetService(typeof(ICariTipiTanimiServisi)) as ICariTipiTanimiServisi;
        if (servis == null) return;
        var pencere = new Masaustu.Gorunumler.Sayfa.CariTipiYonetimPenceresi(servis) { Owner = this };
        pencere.ShowDialog();
        if (pencere.GorunumModeli.KaydedilenId is int id) await vm.CariTipiniSecAsync(id); else vm.CariTipleriniYenileKomutu.Execute(null);
    }
}

