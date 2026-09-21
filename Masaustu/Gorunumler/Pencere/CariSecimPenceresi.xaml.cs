using System.Windows;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Pencere;

public partial class CariSecimPenceresi : Window
{
    private readonly ICariServisi _servis;
    private List<CariKart> _cariler = [];
    private readonly CariTuru? _tur;
    public CariKart? SecilenCari { get; private set; }
    public CariSecimPenceresi(ICariServisi servis, CariTuru? tur = null) { InitializeComponent(); _servis = servis; _tur = tur; Loaded += async (_, _) => { _cariler = (await _servis.TumunuGetirAsync()).Where(c => c.Aktif && (!_tur.HasValue || c.Tur == _tur || c.Tur == CariTuru.MusteriTedarikci)).ToList(); Filtrele(); AramaKutusu.Focus(); }; }
    private void Filtrele() { var q = AramaKutusu.Text.Trim(); CariGrid.ItemsSource = _cariler.Where(c => string.IsNullOrWhiteSpace(q) || c.Kod.Contains(q, StringComparison.OrdinalIgnoreCase) || c.Unvan.Contains(q, StringComparison.OrdinalIgnoreCase) || (c.Telefon?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) || (c.CepTelefonu?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToList(); if (CariGrid.Items.Count > 0) CariGrid.SelectedIndex = 0; }
    private void Sec() { if (CariGrid.SelectedItem is CariKart c) { SecilenCari = c; DialogResult = true; } }
    private void AramaKutusu_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => Filtrele();
    private void AramaKutusu_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Sec(); else if (e.Key == Key.Down) CariGrid.Focus(); }
    private void CariGrid_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { Sec(); e.Handled = true; } }
    private void CariGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Sec();
    private void Sec_Click(object sender, RoutedEventArgs e) => Sec();
}
