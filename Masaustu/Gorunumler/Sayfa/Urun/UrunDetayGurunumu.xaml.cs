using System.Windows.Controls;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa.Urun;

public partial class UrunDetayGurunumu : UserControl
{
    public UrunDetayGurunumu()
    {
        InitializeComponent();
    }
    private async void TanimYonet_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if(sender is not Button b || b.Tag is not string grup) return;
        var servis=App.HizmetSaglayici.GetService(typeof(IMerkeziTanimServisi)) as IMerkeziTanimServisi;
        if(servis==null)return;
        var baslik=grup=="MARKA"?"Markalar":grup=="BIRIM"?"Birimler":"Ürün Kategorileri";
        var p=new Masaustu.Gorunumler.Sayfa.MerkeziTanimYonetimPenceresi(servis,grup,baslik){Owner=System.Windows.Window.GetWindow(this)};
        p.ShowDialog();
        if(DataContext is UrunDuzenleGorunumModeli vm) await vm.TanimlariYukleAsync(grup,p.GorunumModeli.KaydedilenId);
    }
}
