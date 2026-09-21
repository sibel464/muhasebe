using System.Windows;

namespace Masaustu.Gorunumler.Pencere;

public partial class FinansTransferPenceresi : Window
{
    public FinansTransferPenceresi()
    {
        InitializeComponent();
    }

    private void KapatClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
