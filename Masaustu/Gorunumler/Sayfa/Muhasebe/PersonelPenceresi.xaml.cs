using System.Windows;
using Masaustu.GorunumModeli.Muhasebe;

namespace Masaustu.Gorunumler.Sayfa.Muhasebe;

public partial class PersonelPenceresi : Window
{
    public PersonelPenceresi(PersonelFormuModeli model)
    {
        InitializeComponent();
        DataContext = model;
        model.Kaydedildi = () => DialogResult = true;
    }
}
