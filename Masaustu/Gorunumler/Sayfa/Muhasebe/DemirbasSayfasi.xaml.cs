using System.Windows.Controls;
using System.Windows.Input;
using Masaustu.GorunumModeli.Muhasebe;

namespace Masaustu.Gorunumler.Sayfa.Muhasebe;

public partial class DemirbasSayfasi : UserControl
{
    public DemirbasSayfasi() => InitializeComponent();

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DemirbasGorunumModeli vm && vm.DuzenleKomutu.CanExecute(null)) vm.DuzenleKomutu.Execute(null);
    }
}
