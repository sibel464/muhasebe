using System.Windows;
using System.Windows.Controls;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa;

public partial class DashboardSayfasi : UserControl
{
    public DashboardSayfasi()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is DashboardGorunumModeli vm)
            {
                vm.MusteriDuzenleFormAc = async () =>
                {
                    var cariServisi = App.HizmetSaglayici.GetService(typeof(ICariServisi)) as ICariServisi;
                    if (cariServisi == null) return false;

                    var cariTipiServisi = App.HizmetSaglayici.GetService(typeof(ICariTipiTanimiServisi)) as ICariTipiTanimiServisi;
                    var pencere = new Musteri.MusteriDuzenlePenceresi(cariServisi, cariTipiServisi, null)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    pencere.ShowDialog();
                    return pencere.Kaydedildi;
                };
            }
        };
    }
}
