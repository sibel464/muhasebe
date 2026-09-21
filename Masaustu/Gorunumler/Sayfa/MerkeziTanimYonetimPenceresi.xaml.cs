using System.Windows;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa;
public partial class MerkeziTanimYonetimPenceresi : Window
{
    public MerkeziTanimYonetimGorunumModeli GorunumModeli { get; }
    public MerkeziTanimYonetimPenceresi(IMerkeziTanimServisi servis,string grup,string baslik){InitializeComponent();DataContext=GorunumModeli=new(servis,grup,baslik);}
}
