using System.Windows.Controls;
using System.Windows.Input;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa.Finans;

public partial class FinansMerkeziSayfasi : UserControl
{
    public FinansMerkeziSayfasi()
    {
        InitializeComponent();
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
