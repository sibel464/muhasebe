using System.Windows;
using Masaustu.GorunumModeli.Muhasebe;

namespace Masaustu.Gorunumler.Sayfa.Muhasebe;

public partial class DemirbasPenceresi : Window
{
    public DemirbasPenceresi(DemirbasFormuModeli model)
    {
        InitializeComponent();
        DataContext = model;
        model.Kaydedildi = () => DialogResult = true;
    }
}
