using System.Windows;
using Masaustu.GorunumModeli.Muhasebe;

namespace Masaustu.Gorunumler.Sayfa.Muhasebe;

public partial class GiderPusulasiPenceresi : Window
{
    public GiderPusulasiPenceresi(GiderPusulasiFormuModeli model)
    {
        InitializeComponent();
        DataContext = model;
        model.Kaydedildi = () => DialogResult = true;
    }
}
