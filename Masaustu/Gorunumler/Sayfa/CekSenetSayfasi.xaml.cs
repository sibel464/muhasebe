using System.Windows.Controls;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa;

public partial class CekSenetSayfasi : UserControl
{
    public CekSenetSayfasi()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CekSenetGorunumModeli vm && vm.YukleCommand.CanExecute(null))
        {
            vm.YukleCommand.Execute(null);
        }
    }
}
