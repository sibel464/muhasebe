namespace Masaustu.Gorunumler.Sayfa;
public partial class AyarlarSayfasi : System.Windows.Controls.UserControl
{
    public AyarlarSayfasi()
    {
        InitializeComponent();
        LisansDurumunuGoster();
        AgDurumMetni.Text = App.AgModu switch
        {
            Altyapi.Veri.AgModu.Sunucu => "Bu bilgisayar ağ sunucusu: veriler burada, diğer bilgisayarlar bağlantı koduyla bağlanır.",
            Altyapi.Veri.AgModu.Istemci => "Bu bilgisayar ağ istemcisi: veriler sunucu bilgisayarda.",
            _ => "Tek bilgisayar: veriler bu bilgisayarda, ağdan erişilemez."
        };
    }

    private void AgAyarlari_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var oturum = App.HizmetSaglayici.GetService(typeof(Uygulama.Arayuzler.IKullaniciServisi)) as Uygulama.Arayuzler.IKullaniciServisi;
        if (oturum?.YetkiliMi(y => y.AyarlariYonet) != true)
        {
            System.Windows.MessageBox.Show("Ağ ayarlarını yalnız ayarları yönetme yetkisi olan kullanıcı değiştirebilir.", "Yetki", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        new Masaustu.Gorunumler.Ortak.AgAyarlariPenceresi { Owner = System.Windows.Window.GetWindow(this) }.ShowDialog();
    }

    private void LisansDurumunuGoster() => LisansDurumMetni.Text = Masaustu.Servisler.LisansYoneticisi.DenetimGerekli
        ? Masaustu.Servisler.LisansYoneticisi.DurumGetir().Aciklama
        : "Geliştirme sürümü: lisans denetimi yapılmaz.";

    private void Lisans_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        new Masaustu.Gorunumler.Ortak.LisansPenceresi(acilistaZorunlu: false) { Owner = System.Windows.Window.GetWindow(this) }.ShowDialog();
        LisansDurumunuGoster();
    }
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
