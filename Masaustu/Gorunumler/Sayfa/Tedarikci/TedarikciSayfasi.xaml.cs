using System.Windows;
using System.Windows.Controls;
using EtkiAlani.Modeller.Cari;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa;

public partial class TedarikciSayfasi : UserControl
{
    public TedarikciSayfasi()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is TedarikciGorunumModeli vm)
            {
                // Data bağlamındaki komutu tetikle
                if (vm.YenileKomutu.CanExecute(null))
                {
                    vm.YenileKomutu.Execute(null);
                }

                vm.TedarikciDuzenleFormAc = async (cari) =>
                {
                    var cariServisi = App.HizmetSaglayici.GetService(typeof(ICariServisi)) as ICariServisi;
                    var cariTipiServisi = App.HizmetSaglayici.GetService(typeof(ICariTipiTanimiServisi)) as ICariTipiTanimiServisi;
                    if (cariServisi == null || cariTipiServisi == null) return false;

                    var pencere = new Tedarikci.TedarikciDuzenlePenceresi(cariServisi, cariTipiServisi, cari)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    pencere.ShowDialog();
                    return pencere.Kaydedildi;
                };

                vm.CariEkstreAc = async (cari) =>
                {
                    var hareketServisi = App.HizmetSaglayici.GetService(typeof(ICariHareketServisi)) as ICariHareketServisi;
                    var cariServisi = App.HizmetSaglayici.GetService(typeof(ICariServisi)) as ICariServisi;
                    if (hareketServisi == null || cariServisi == null) return;

                    var pencere = new Musteri.CariHareketlerPenceresi(hareketServisi, cariServisi, cari)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    pencere.ShowDialog();
                    if (vm.YenileKomutu.CanExecute(null))
                        vm.YenileKomutu.Execute(null);
                };
            }
        };
    }

    private void DataGrid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is TedarikciGorunumModeli vm && vm.SeciliTedarikci != null)
        {
            if (vm.DuzenleKomutu.CanExecute(null))
                vm.DuzenleKomutu.Execute(null);
        }
    }
}

