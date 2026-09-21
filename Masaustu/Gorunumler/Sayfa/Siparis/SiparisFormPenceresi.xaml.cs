using System.ComponentModel;
namespace Masaustu.Gorunumler.Sayfa.Siparis;
public partial class SiparisFormPenceresi : System.Windows.Window
{
    public SiparisFormPenceresi()
    {
        InitializeComponent();
        System.Windows.DataObject.AddPastingHandler(TelefonKutusu, TelefonKutusu_Pasting);
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged eski) eski.PropertyChanged -= ModelDegisti;
            if (e.NewValue is INotifyPropertyChanged yeni) yeni.PropertyChanged += ModelDegisti;
        };
        Closed += (_, _) =>
        {
            if (DataContext is Masaustu.GorunumModelleri.SiparisGorunumModeli vm && vm.FormAcik)
                vm.FormaGeriDonKomutu.Execute(null);
        };
    }
    private void ModelDegisti(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "FormAcik" && DataContext is Masaustu.GorunumModelleri.SiparisGorunumModeli vm && !vm.FormAcik) Close();
    }
    private static bool TelefonGecerli(string metin) => metin.All(c => char.IsDigit(c) || c is ' ' or '+' or '(' or ')' or '-');
    private void TelefonKutusu_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e) => e.Handled = !TelefonGecerli(e.Text);
    private void TelefonKutusu_Pasting(object sender, System.Windows.DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText) || e.SourceDataObject.GetData(System.Windows.DataFormats.UnicodeText) is not string metin || !TelefonGecerli(metin)) e.CancelCommand();
    }
    private void KalemGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e) => Dispatcher.BeginInvoke(() => (DataContext as Masaustu.GorunumModelleri.SiparisGorunumModeli)?.KalemleriHesapla());
}
