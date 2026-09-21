using System.Windows;

namespace Masaustu.Gorunumler.Ortak;

public partial class MetinGirisPenceresi : Window
{
    public string GirilenMetin { get; private set; } = string.Empty;

    public MetinGirisPenceresi(string pencereBasligi, string aciklamaMetni)
    {
        InitializeComponent();
        DataContext = new { PencereBasligi = pencereBasligi, AciklamaMetni = aciklamaMetni };
        TxtGiris.Focus();
    }

    private void BtnOnay_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtGiris.Text))
        {
            MessageBox.Show("Lütfen bir açıklama giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        GirilenMetin = TxtGiris.Text.Trim();
        DialogResult = true;
    }

    private void BtnIptal_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
