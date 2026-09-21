using System.Windows;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Pencere
{
    public partial class StokDagilimiPenceresi : Window
    {
        public StokDagilimiPenceresi(StokDagilimiGorunumModeli gorunumModeli)
        {
            InitializeComponent();
            DataContext = gorunumModeli;
            gorunumModeli.Kapat = Close;
        }
    }
}
