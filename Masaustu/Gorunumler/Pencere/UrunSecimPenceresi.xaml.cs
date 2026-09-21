using System.Windows;
using System.Windows.Input;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Pencere;

public partial class UrunSecimPenceresi : Window
{
    private readonly IUrunServisi _servis;
    private List<UrunSecimSatiri> _satirlar = [];
    public Urun? SecilenUrun { get; private set; }

    public UrunSecimPenceresi(IUrunServisi servis)
    {
        InitializeComponent();
        _servis = servis;
        Loaded += async (_, _) => { await YukleAsync(); AramaKutusu.Focus(); };
    }

    private async Task YukleAsync()
    {
        try
        {
            var tumUrunler = await _servis.TumunuGetirAsync();
            var urunler = tumUrunler.Where(u => u != null && u.Aktif).ToList();
            var satirlar = new List<UrunSecimSatiri>();
            foreach (var urun in urunler)
            {
                decimal m1 = 0, m2 = 0, m3 = 0;
                try
                {
                    var stoklar = await _servis.LokasyonStoklariniGetirAsync(urun.Id);
                    m1 = stoklar?.FirstOrDefault(s => s.DepoId == 1)?.MevcutMiktar ?? 0;
                    m2 = stoklar?.FirstOrDefault(s => s.DepoId == 2)?.MevcutMiktar ?? 0;
                    m3 = stoklar?.FirstOrDefault(s => s.DepoId == 3)?.MevcutMiktar ?? 0;
                }
                catch
                {
                    m1 = urun.StokMiktari;
                }
                satirlar.Add(new UrunSecimSatiri(urun, m1, m2, m3));
            }
            _satirlar = satirlar;
            Filtrele();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ürün listesi yüklenirken hata oluştu: " + ex.Message, "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Filtrele()
    {
        var q = AramaKutusu.Text.Trim();
        UrunGrid.ItemsSource = _satirlar.Where(x => string.IsNullOrWhiteSpace(q) ||
            x.Urun.Kod.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            (x.Urun.Barkod?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
            x.Urun.Ad.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        if (UrunGrid.Items.Count > 0) UrunGrid.SelectedIndex = 0;
    }

    private void Sec() { if (UrunGrid.SelectedItem is UrunSecimSatiri s) { SecilenUrun = s.Urun; DialogResult = true; } }
    private void AramaKutusu_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => Filtrele();
    private void AramaKutusu_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Sec(); else if (e.Key == Key.Down) UrunGrid.Focus(); }
    private void UrunGrid_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { Sec(); e.Handled = true; } }
    private void UrunGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Sec();
    private void Sec_Click(object sender, RoutedEventArgs e) => Sec();
}

public sealed record UrunSecimSatiri(Urun Urun, decimal Magaza, decimal Depo1, decimal Depo2)
{
    public string Kategori => Urun.KategoriTanimi?.Ad ?? Urun.Kategori ?? "-";
    public string Marka => Urun.MarkaTanimi?.Ad ?? "-";
    public decimal Toplam => Magaza + Depo1 + Depo2;
}
