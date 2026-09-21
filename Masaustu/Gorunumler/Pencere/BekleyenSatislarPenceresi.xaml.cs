using System.Windows;
using System.Windows.Input;
using EtkiAlani.Modeller.Fatura;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Pencere;

public partial class BekleyenSatislarPenceresi : Window
{
    private readonly IFaturaServisi _servis;
    public Fatura? SecilenSatis { get; private set; }
    public BekleyenSatislarPenceresi(IFaturaServisi servis) { InitializeComponent(); _servis = servis; Loaded += async (_, _) => await YukleAsync(); }
    private async Task YukleAsync() { var liste = (await _servis.TumunuGetirAsync(FaturaTuru.Satis)).Where(f => f.Durum == BelgeDurumu.Beklemede).ToList(); SatisGrid.ItemsSource = liste; Durum.Text = $"{liste.Count} bekleyen satış"; if (liste.Count > 0) SatisGrid.SelectedIndex = 0; }
    private void Ac() { if (SatisGrid.SelectedItem is Fatura f) { SecilenSatis = f; DialogResult = true; } }
    private void Ac_Click(object sender, RoutedEventArgs e) => Ac();
    private void SatisGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Ac();
    private void SatisGrid_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { Ac(); e.Handled = true; } }
    private async void Sil_Click(object sender, RoutedEventArgs e)
    {
        if (SatisGrid.SelectedItem is not Fatura f) return;
        if (MessageBox.Show($"{f.FaturaNo} numaralı bekleyen satış silinsin mi?", "Bekleyen Satışı Sil", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await _servis.SilAsync(f.Id);
        await YukleAsync();
    }
}
