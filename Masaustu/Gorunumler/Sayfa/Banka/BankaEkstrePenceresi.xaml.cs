using System.Windows;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa.Banka;

public partial class BankaEkstrePenceresi : Window
{
    public BankaEkstrePenceresi(BankaEkstreAktarimModeli model)
    {
        InitializeComponent();
        DataContext = model;
    }
}
