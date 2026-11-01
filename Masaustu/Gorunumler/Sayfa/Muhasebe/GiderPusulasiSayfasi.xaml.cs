using System.Windows.Controls;
using System.Windows.Input;
using Masaustu.GorunumModeli.Muhasebe;

namespace Masaustu.Gorunumler.Sayfa.Muhasebe;

public partial class GiderPusulasiSayfasi : UserControl
{
    public GiderPusulasiSayfasi() => InitializeComponent();

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is GiderPusulasiGorunumModeli vm && vm.YazdirKomutu.CanExecute(null)) vm.YazdirKomutu.Execute(null);
    }
}
