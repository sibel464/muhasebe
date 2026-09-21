using System.Windows.Controls;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using Masaustu.GorunumModelleri;
using Masaustu.Gorunumler.Pencere;

namespace Masaustu.Gorunumler.Sayfa;

public partial class SatisSayfasi : UserControl
{
    public SatisSayfasi()
    {
        InitializeComponent();
        
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Kasiyerin anında ürün okutabilmesi için barkod kutusuna odaklan
        txtBarkod.Focus();
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is SatisGorunumModeli vm)
        {
            vm.UrunSeciciAc = () =>
            {
                var pencere = new UrunSecimPenceresi(vm.UrunServisi) { Owner = System.Windows.Window.GetWindow(this) };
                return Task.FromResult(pencere.ShowDialog() == true ? pencere.SecilenUrun : null);
            };
            vm.CariSeciciAc = () =>
            {
                var pencere = new CariSecimPenceresi(vm.CariServisi) { Owner = System.Windows.Window.GetWindow(this) };
                return Task.FromResult(pencere.ShowDialog() == true ? pencere.SecilenCari : null);
            };
            vm.BekleyenSatisSeciciAc = () =>
            {
                var pencere = new BekleyenSatislarPenceresi(vm.FaturaServisi) { Owner = System.Windows.Window.GetWindow(this) };
                return Task.FromResult(pencere.ShowDialog() == true ? pencere.SecilenSatis : null);
            };
        }
    }

    // Kullanıcı ekranda herhangi bir yere tıklasa da, 
    // tuşa bastığı an eğer TextBox'ta değilse odaklan. (Barkod okuyucu garantisi)
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        
        if (!txtBarkod.IsFocused)
        {
            // Eğer basılan tuş harf veya rakamsa barkoda odaklan
            if ((e.Key >= Key.A && e.Key <= Key.Z) || (e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                txtBarkod.Focus();
            }
        }
    }
}
