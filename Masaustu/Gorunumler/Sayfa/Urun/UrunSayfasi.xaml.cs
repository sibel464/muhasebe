using System.Windows;
using System.Windows.Controls;
using EtkiAlani.Modeller.Stok;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;
using UrunModel = EtkiAlani.Modeller.Stok.Urun;

namespace Masaustu.Gorunumler.Sayfa;

public partial class UrunSayfasi : UserControl
{
    public UrunSayfasi()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (DataContext is UrunGorunumModeli vm)
            {
                vm.DuzenleFormAc = async (urun) =>
                {
                    var urunServisi = App.HizmetSaglayici.GetService(typeof(IUrunServisi)) as IUrunServisi;
                    if (urunServisi == null) return false;

                    var pencere = new Urun.UrunDuzenlePenceresi(urunServisi, urun)
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
