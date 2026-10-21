using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa.Stok;

public partial class StokOperasyonMerkeziSayfasi : UserControl
{
    public StokOperasyonMerkeziSayfasi()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is StokOperasyonMerkeziGorunumModeli vm)
            {
                var parentWindow = Window.GetWindow(this);
                vm.Stok360AcAction = id => {
                    new UrunStok360Penceresi(id) { Owner = parentWindow }.ShowDialog();
                };
                vm.StokIslemAcAction = (id, isGiris) => {
                    new StokIslemPenceresi(id, isGiris) { Owner = parentWindow }.ShowDialog();
                    _ = vm.YukleAsync();
                };
                vm.TransferAcAction = id => {
                    new StokTransferPenceresi(id) { Owner = parentWindow }.ShowDialog();
                    _ = vm.YukleAsync();
                };
                vm.SayimAcAction = () => {
                    new StokSayimPenceresi() { Owner = parentWindow }.ShowDialog();
                    _ = vm.YukleAsync();
                };

                await vm.InitAsync();
            }
        };
    }

    private void TabloCiftTik(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is StokOperasyonMerkeziGorunumModeli vm && vm.SeciliSatir != null)
        {
            vm.Stok360AcKomutu.Execute(vm.SeciliSatir.UrunId);
        }
    }

    private async void ExceldenAktar_Click(object sender, RoutedEventArgs e)
    {
        if (await Masaustu.Gorunumler.Ortak.TopluAktarimPenceresi.GosterAsync(Uygulama.Arayuzler.AktarimTuru.Urun) > 0
            && DataContext is StokOperasyonMerkeziGorunumModeli vm && vm.YenileKomutu.CanExecute(null))
            vm.YenileKomutu.Execute(null);
    }
}
