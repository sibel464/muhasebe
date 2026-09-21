using System.Windows;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Pencere;

public partial class CekSenetEklePenceresi : Window
{
    private readonly ICekSenetServisi _cekSenetServisi;
    private readonly ICariServisi _cariServisi;
    private CariKart? _seciliCari;

    public CekSenetEklePenceresi(ICekSenetServisi cekSenetServisi, ICariServisi cariServisi)
    {
        InitializeComponent();
        _cekSenetServisi = cekSenetServisi;
        _cariServisi = cariServisi;
        VadeTarihiPicker.SelectedDate = DateTime.Today.AddDays(30);
    }

    private void CariSec_Click(object sender, RoutedEventArgs e)
    {
        var pencere = new CariSecimPenceresi(_cariServisi) { Owner = this };
        if (pencere.ShowDialog() == true && pencere.SecilenCari != null)
        {
            _seciliCari = pencere.SecilenCari;
            CariUnvanTxt.Text = $"{_seciliCari.Kod} - {_seciliCari.Unvan}";
            if (string.IsNullOrWhiteSpace(KesideciTxt.Text)) KesideciTxt.Text = _seciliCari.Unvan;
        }
    }

    private async void Kaydet_Click(object sender, RoutedEventArgs e)
    {
        if (_seciliCari == null)
        {
            MessageBox.Show("Lütfen bir cari kart seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(BelgeNoTxt.Text))
        {
            MessageBox.Show("Belge numarası boş olamaz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TutarTxt.Text, out var tutar) || tutar <= 0)
        {
            MessageBox.Show("Geçerli bir tutar giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var tur = TurCombo.SelectedIndex switch
        {
            0 => CekSenetTuru.AlinanCek,
            1 => CekSenetTuru.VerilenCek,
            2 => CekSenetTuru.AlinanSenet,
            _ => CekSenetTuru.VerilenSenet
        };

        try
        {
            await _cekSenetServisi.EkleAsync(new CekSenetEkleTalebi(
                Tur: tur,
                BelgeNo: BelgeNoTxt.Text.Trim(),
                VadeTarihi: VadeTarihiPicker.SelectedDate ?? DateTime.Today,
                DuzenlemeTarihi: DateTime.Today,
                Tutar: tutar,
                CariKartId: _seciliCari.Id,
                Kesideci: KesideciTxt.Text.Trim(),
                BankaAdi: BankaAdiTxt.Text.Trim(),
                SubeAdi: SubeAdiTxt.Text.Trim(),
                Aciklama: AciklamaTxt.Text.Trim()
            ));

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
