using System.Windows.Controls;
using System.Windows.Input;
using Masaustu.GorunumModelleri;
using Masaustu.Gorunumler.Pencere;

namespace Masaustu.Gorunumler.Sayfa;

public partial class ProfesyonelSatisSayfasi : UserControl
{
    public ProfesyonelSatisSayfasi() { InitializeComponent(); DataContextChanged += Bagla; Loaded += (_, _) => Dispatcher.BeginInvoke(() => { Bagla(this, default); txtBarkod.Focus(); }); }
    private void Bagla(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is not SatisGorunumModeli vm) return;
        vm.UrunSeciciAc = () => { var p = new UrunSecimPenceresi(vm.UrunServisi) { Owner = System.Windows.Window.GetWindow(this) }; return Task.FromResult(p.ShowDialog() == true ? p.SecilenUrun : null); };
        vm.CariSeciciAc = () => { var p = new CariSecimPenceresi(vm.CariServisi) { Owner = System.Windows.Window.GetWindow(this) }; return Task.FromResult(p.ShowDialog() == true ? p.SecilenCari : null); };
        vm.BekleyenSatisSeciciAc = () => { var p = new BekleyenSatislarPenceresi(vm.FaturaServisi) { Owner = System.Windows.Window.GetWindow(this) }; return Task.FromResult(p.ShowDialog() == true ? p.SecilenSatis : null); };
    }
    private void Barkod_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && DataContext is SatisGorunumModeli vm && vm.BarkodOkunduKomutu.CanExecute(null)) { vm.BarkodOkunduKomutu.Execute(null); e.Handled = true; } }
    private void HizliSatis_Checked(object sender, System.Windows.RoutedEventArgs e) { if (DataContext is SatisGorunumModeli vm) vm.CariSatisMi = false; }
    private void CariSatis_Checked(object sender, System.Windows.RoutedEventArgs e) { if (DataContext is SatisGorunumModeli vm) vm.CariSatisMi = true; }
    protected override void OnPreviewKeyDown(KeyEventArgs e) { base.OnPreviewKeyDown(e); if (!txtBarkod.IsFocused && ((e.Key >= Key.A && e.Key <= Key.Z) || (e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))) txtBarkod.Focus(); }
}
