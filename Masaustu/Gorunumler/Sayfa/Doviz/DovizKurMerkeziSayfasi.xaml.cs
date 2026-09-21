using System.Windows.Controls;
using Masaustu.GorunumModeli.Doviz;

namespace Masaustu.Gorunumler.Sayfa.Doviz;

public partial class DovizKurMerkeziSayfasi : UserControl
{
    public DovizKurMerkeziSayfasi() { InitializeComponent(); }

    public DovizKurMerkeziSayfasi(DovizKurMerkeziGorunumModeli viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
