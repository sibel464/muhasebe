namespace Masaustu.Gorunumler.Sayfa.Siparis;
public partial class SiparisSayfasi : System.Windows.Controls.UserControl
{
    public SiparisSayfasi()
    {
        InitializeComponent();
        System.Windows.DataObject.AddPastingHandler(TelefonKutusu, TelefonKutusu_Pasting);
    }
    private static bool TelefonGecerli(string metin) => metin.All(c => char.IsDigit(c) || c is ' ' or '+' or '(' or ')' or '-');
    private void TelefonKutusu_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e) => e.Handled = !TelefonGecerli(e.Text);
    private void TelefonKutusu_Pasting(object sender, System.Windows.DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText) ||
            e.SourceDataObject.GetData(System.Windows.DataFormats.UnicodeText) is not string metin || !TelefonGecerli(metin))
            e.CancelCommand();
    }
    private void KalemGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(() => (DataContext as Masaustu.GorunumModelleri.SiparisGorunumModeli)?.KalemleriHesapla());
    }
}
