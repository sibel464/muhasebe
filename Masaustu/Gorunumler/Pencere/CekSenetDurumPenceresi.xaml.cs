using System.Windows;
using System.Windows.Controls;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Pencere;

public partial class CekSenetDurumPenceresi : Window
{
    private readonly ICekSenetServisi _cekSenetServisi;
    private readonly IKasaServisi _kasaServisi;
    private readonly IBankaServisi _bankaServisi;
    private readonly ICariServisi _cariServisi;
    private readonly CekSenetOzet _kayit;
    private CariKart? _ciroCari;

    private List<HesapSecimOge> _hesaplar = new();

    public CekSenetDurumPenceresi(
        ICekSenetServisi cekSenetServisi, 
        IKasaServisi kasaServisi, 
        IBankaServisi bankaServisi, 
        ICariServisi cariServisi,
        CekSenetOzet kayit)
    {
        InitializeComponent();
        _cekSenetServisi = cekSenetServisi;
        _kasaServisi = kasaServisi;
        _bankaServisi = bankaServisi;
        _cariServisi = cariServisi;
        _kayit = kayit;

        BelgeOzetTxt.Text = $"{kayit.Tur} (No: {kayit.BelgeNo})";
        CariOzetTxt.Text = $"Cari: {kayit.CariUnvan} | Keşideci: {kayit.Kesideci}";
        TutarOzetTxt.Text = $"{kayit.Tutar:N2} ₺";

        DurumCombo.SelectedIndex = 0;
        HesaplariYukle();
    }

    private async void HesaplariYukle()
    {
        try
        {
            _hesaplar.Clear();
            var kasalar = await _kasaServisi.KasalariGetirAsync();
            foreach (var k in kasalar)
            {
                _hesaplar.Add(new HesapSecimOge { Id = k.Id, IsKasa = true, Ad = $"Kasa: {k.Ad}" });
            }

            var bankalar = await _bankaServisi.BankaHesaplariGetirAsync();
            foreach (var b in bankalar)
            {
                _hesaplar.Add(new HesapSecimOge { Id = b.Id, IsKasa = false, Ad = $"Banka: {b.Ad} ({b.HesapNo})" });
            }

            FinansHesapCombo.ItemsSource = _hesaplar;
            if (_hesaplar.Count > 0) FinansHesapCombo.SelectedIndex = 0;
        }
        catch { }
    }

    private void DurumCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FinansHesapPanel == null) return;

        // Tahsil Edildi (0) veya Ödendi (3) seçilirse Kasa/Banka panelini göster
        if (DurumCombo.SelectedIndex == 0 || DurumCombo.SelectedIndex == 3)
        {
            FinansHesapPanel.Visibility = Visibility.Visible;
        }
        else
        {
            FinansHesapPanel.Visibility = Visibility.Collapsed;
        }
        CiroCariPanel.Visibility = DurumCombo.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CiroCariSec_Click(object sender, RoutedEventArgs e)
    {
        var pencere = new CariSecimPenceresi(_cariServisi) { Owner = this };
        if (pencere.ShowDialog() == true && pencere.SecilenCari != null)
        {
            _ciroCari = pencere.SecilenCari;
            CiroCariTxt.Text = $"{_ciroCari.Kod} - {_ciroCari.Unvan}";
        }
    }

    private async void Kaydet_Click(object sender, RoutedEventArgs e)
    {
        var yeniDurum = DurumCombo.SelectedIndex switch
        {
            0 => CekSenetDurumu.TahsilEdildi,
            1 => CekSenetDurumu.CiroEdildi,
            2 => CekSenetDurumu.TahsileVerildi,
            3 => CekSenetDurumu.Odendi,
            4 => CekSenetDurumu.Karsiliksiz,
            _ => CekSenetDurumu.IadeEdildi
        };

        int? kasaId = null;
        int? bankaHesabiId = null;

        if (yeniDurum == CekSenetDurumu.TahsilEdildi || yeniDurum == CekSenetDurumu.Odendi)
        {
            if (FinansHesapCombo.SelectedItem is HesapSecimOge seciliHesap)
            {
                if (seciliHesap.IsKasa) kasaId = seciliHesap.Id;
                else bankaHesabiId = seciliHesap.Id;
            }
        }

        try
        {
            await _cekSenetServisi.DurumGuncelleAsync(_kayit.Id, yeniDurum, kasaId, bankaHesabiId, AciklamaTxt.Text.Trim(),
                yeniDurum == CekSenetDurumu.CiroEdildi ? _ciroCari?.Id : null);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Iptal_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class HesapSecimOge
{
    public int Id { get; set; }
    public bool IsKasa { get; set; }
    public string Ad { get; set; } = string.Empty;
    public override string ToString() => Ad;
}
