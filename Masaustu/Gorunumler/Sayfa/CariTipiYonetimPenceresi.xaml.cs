using System.Windows;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa;

public partial class CariTipiYonetimPenceresi : Window
{
    public CariTipiYonetimGorunumModeli GorunumModeli { get; }
    public CariTipiYonetimPenceresi(ICariTipiTanimiServisi servis)
    {
        InitializeComponent();
        DataContext = GorunumModeli = new CariTipiYonetimGorunumModeli(servis);
    }
}
