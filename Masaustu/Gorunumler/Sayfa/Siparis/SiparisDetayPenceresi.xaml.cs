namespace Masaustu.Gorunumler.Sayfa.Siparis;
public partial class SiparisDetayPenceresi : System.Windows.Window
{
    private EtkiAlani.Modeller.Siparis.Siparis _siparis;
    public SiparisDetayPenceresi(EtkiAlani.Modeller.Siparis.Siparis siparis) { InitializeComponent(); _siparis = siparis; DataContext = siparis; SevkEtDugmesi.Visibility = SevkEdilebilir(siparis) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed; }
    private async void SevkEt_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var siparisServisi = App.HizmetSaglayici.GetService(typeof(Uygulama.Arayuzler.ISiparisServisi)) as Uygulama.Arayuzler.ISiparisServisi;
        var irsaliyeServisi = App.HizmetSaglayici.GetService(typeof(Uygulama.Arayuzler.IIrsaliyeServisi)) as Uygulama.Arayuzler.IIrsaliyeServisi;
        if (siparisServisi == null || irsaliyeServisi == null) return;
        _siparis = await siparisServisi.GetirAsync(_siparis.Id) ?? _siparis;
        var pencere = new SiparistenSevkPenceresi(_siparis, irsaliyeServisi) { Owner = this };
        if (pencere.ShowDialog() == true)
        {
            _siparis = await siparisServisi.GetirAsync(_siparis.Id) ?? _siparis; DataContext = _siparis;
            SevkEtDugmesi.Visibility = SevkEdilebilir(_siparis) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
    }
    private static bool SevkEdilebilir(EtkiAlani.Modeller.Siparis.Siparis s) => s.Durum is EtkiAlani.Modeller.Siparis.SiparisDurumu.Beklemede or EtkiAlani.Modeller.Siparis.SiparisDurumu.Onaylandi or EtkiAlani.Modeller.Siparis.SiparisDurumu.KismenKarsilandi;
}
