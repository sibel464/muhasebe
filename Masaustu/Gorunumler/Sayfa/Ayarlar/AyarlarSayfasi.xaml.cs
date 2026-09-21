namespace Masaustu.Gorunumler.Sayfa;
public partial class AyarlarSayfasi : System.Windows.Controls.UserControl
{
    public AyarlarSayfasi() => InitializeComponent();
    private void MerkeziTanim_Click(object sender,System.Windows.RoutedEventArgs e)
    {
        if(sender is not System.Windows.Controls.Button b||b.Tag is not string grup)return;
        var s=App.HizmetSaglayici.GetService(typeof(Uygulama.Arayuzler.IMerkeziTanimServisi)) as Uygulama.Arayuzler.IMerkeziTanimServisi;if(s==null)return;
        var baslik=grup=="MARKA"?"Markalar":grup=="BIRIM"?"Birimler":"Ürün Kategorileri";
        new MerkeziTanimYonetimPenceresi(s,grup,baslik){Owner=System.Windows.Window.GetWindow(this)}.ShowDialog();
    }
    private void CariTipleri_Click(object sender,System.Windows.RoutedEventArgs e)
    {
        var s=App.HizmetSaglayici.GetService(typeof(Uygulama.Arayuzler.ICariTipiTanimiServisi)) as Uygulama.Arayuzler.ICariTipiTanimiServisi;if(s==null)return;
        new CariTipiYonetimPenceresi(s){Owner=System.Windows.Window.GetWindow(this)}.ShowDialog();
    }
}
