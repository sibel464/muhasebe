using System.Windows.Controls;
using System.Windows.Input;
using Masaustu.GorunumModeli.Muhasebe;

namespace Masaustu.Gorunumler.Sayfa.Muhasebe;

public partial class PersonelSayfasi : UserControl
{
    public PersonelSayfasi() => InitializeComponent();

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PersonelGorunumModeli vm && vm.DuzenleKomutu.CanExecute(null)) vm.DuzenleKomutu.Execute(null);
    }
}
