using System.Windows;

namespace Masaustu.Gorunumler.Pencere;

public partial class MutabakatPenceresi : Window
{
    public MutabakatPenceresi()
    {
        InitializeComponent();
    }

    private void KapatClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
