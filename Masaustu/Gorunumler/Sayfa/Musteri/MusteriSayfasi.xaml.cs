using System.Windows;
using System.Windows.Controls;
using EtkiAlani.Modeller.Cari;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa;

public partial class MusteriSayfasi : UserControl
{
    public MusteriSayfasi()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (DataContext is MusteriGorunumModeli vm)
            {
                vm.MusteriDuzenleFormAc = async (cari) =>
                {
                    var cariServisi = App.HizmetSaglayici.GetService(typeof(ICariServisi)) as ICariServisi;
                    if (cariServisi == null) return false;

                    var pencere = new Musteri.MusteriDuzenlePenceresi(cariServisi, cari)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    pencere.ShowDialog();
                    return pencere.Kaydedildi;
                };
            }
        };
    }

    private void DataGrid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MusteriGorunumModeli vm && vm.SeciliMusteri != null)
        {
            if (vm.DuzenleKomutu.CanExecute(null))
                vm.DuzenleKomutu.Execute(null);
        }
    }
}
