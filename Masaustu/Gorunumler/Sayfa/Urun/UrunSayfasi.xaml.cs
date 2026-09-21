using System.Windows;
using System.Windows.Controls;
using EtkiAlani.Modeller.Stok;
using Masaustu.GorunumModelleri;
using Masaustu.Gorunumler.Sayfa.Urun;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa;

public partial class UrunSayfasi : UserControl
{
    public UrunSayfasi()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is UrunGorunumModeli vm)
            {
                vm.UrunDuzenleFormAc = async (mevcutUrun) =>
                {
                    var urunServisi = App.HizmetSaglayici.GetService(typeof(IUrunServisi)) as IUrunServisi;
                    if (urunServisi == null) return false;

                    var pencere = new UrunDuzenlePenceresi(urunServisi, mevcutUrun)
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
        if (DataContext is UrunGorunumModeli vm && vm.SeciliUrun != null)
        {
            if (vm.DuzenleKomutu.CanExecute(null))
                vm.DuzenleKomutu.Execute(null);
        }
    }
}
