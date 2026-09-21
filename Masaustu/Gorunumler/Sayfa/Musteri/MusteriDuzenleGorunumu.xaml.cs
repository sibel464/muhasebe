using System.Windows.Controls;

namespace Masaustu.Gorunumler.Sayfa.Musteri;

using Masaustu.GorunumModelleri;

public partial class MusteriDuzenleGorunumu : UserControl
{
    public MusteriDuzenleGorunumu()
    {
        InitializeComponent();
    }

    private async void CariTipiYonet_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var servis = App.HizmetSaglayici.GetService(typeof(Uygulama.Arayuzler.ICariTipiTanimiServisi)) as Uygulama.Arayuzler.ICariTipiTanimiServisi;
        if (servis == null) return;
        var pencere = new Masaustu.Gorunumler.Sayfa.CariTipiYonetimPenceresi(servis) { Owner = System.Windows.Window.GetWindow(this) };
        pencere.ShowDialog();
        if (DataContext is MusteriDuzenleGorunumModeli vm)
        {
            if (pencere.GorunumModeli.KaydedilenId is int id) await vm.CariTipiniSecAsync(id); else vm.CariTipleriniYenileKomutu.Execute(null);
        }
    }
}
