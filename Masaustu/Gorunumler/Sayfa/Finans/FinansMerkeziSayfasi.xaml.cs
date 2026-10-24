using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Masaustu.Gorunumler.Pencere;
using Masaustu.GorunumModelleri;
using Microsoft.Extensions.DependencyInjection;

namespace Masaustu.Gorunumler.Sayfa.Finans;

public partial class FinansMerkeziSayfasi : UserControl
{
    public FinansMerkeziSayfasi()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not FinansMerkeziGorunumModeli vm) return;
            vm.TransferAcAction = tur => _ = PencereAcAsync(async () =>
            {
                var m = App.HizmetSaglayici.GetRequiredService<FinansTransferGorunumModeli>();
                await m.InitAsync(tur);
                var p = new FinansTransferPenceresi { DataContext = m };
                m.BasariliKayitAction = () => p.Close();
                return p;
            }, vm);
            vm.Kasa360AcAction = id => _ = PencereAcAsync(async () =>
            {
                var m = App.HizmetSaglayici.GetRequiredService<Kasa360GorunumModeli>();
                await m.YukleAsync(id);
                return new Kasa360Penceresi { DataContext = m };
            });
            vm.Banka360AcAction = id => _ = PencereAcAsync(async () =>
            {
                var m = App.HizmetSaglayici.GetRequiredService<Banka360GorunumModeli>();
                await m.YukleAsync(id);
                return new Banka360Penceresi { DataContext = m };
            });
            vm.MutabakatAcAction = () => _ = PencereAcAsync(async () =>
            {
                var m = App.HizmetSaglayici.GetRequiredService<MutabakatGorunumModeli>();
                await m.InitAsync();
                var p = new MutabakatPenceresi { DataContext = m };
                m.BasariliKayitAction = () => p.Close();
                return p;
            }, vm);
        };
    }

    // Pencereyi modal açar; verilmişse kapanınca Finans Merkezi özetini yeniler.
    private async Task PencereAcAsync(Func<Task<Window>> olustur, FinansMerkeziGorunumModeli? yenilenecek = null)
    {
        try
        {
            var pencere = await olustur();
            pencere.Owner = Window.GetWindow(this);
            pencere.ShowDialog();
            if (yenilenecek != null) await yenilenecek.YukleAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Pencere açılamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void KasaCiftTik(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is FinansMerkeziGorunumModeli vm && vm.Kasa360AcKomutu.CanExecute(null))
        {
            vm.Kasa360AcKomutu.Execute(null);
        }
    }

    private void BankaCiftTik(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is FinansMerkeziGorunumModeli vm && vm.Banka360AcKomutu.CanExecute(null))
        {
            vm.Banka360AcKomutu.Execute(null);
        }
    }
}
