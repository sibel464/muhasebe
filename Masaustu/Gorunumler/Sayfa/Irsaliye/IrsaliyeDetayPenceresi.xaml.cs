using System.Windows;
using System.Windows.Input;
using EtkiAlani.Modeller.Irsaliye;
using Uygulama.Arayuzler;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa.Irsaliye;

public partial class IrsaliyeDetayPenceresi : Window
{
    private readonly EtkiAlani.Modeller.Irsaliye.Irsaliye _irsaliye;
    private readonly IIrsaliyeServisi _irsaliyeServisi;
    private readonly IrsaliyeGorunumModeli? _anaModel;

    public IrsaliyeDetayPenceresi(
        EtkiAlani.Modeller.Irsaliye.Irsaliye irsaliye, 
        IIrsaliyeServisi irsaliyeServisi,
        IrsaliyeGorunumModeli? anaModel = null)
    {
        InitializeComponent();
        _irsaliye = irsaliye;
        _irsaliyeServisi = irsaliyeServisi;
        _anaModel = anaModel;

        VerileriDoldur();

        MouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        };
    }

    private void VerileriDoldur()
    {
        TxtIrsaliyeNo.Text = _irsaliye.IrsaliyeNo;
        TxtCariUnvan.Text = _irsaliye.CariKart?.Unvan ?? "Bilinmeyen Cari";
        TxtCariKod.Text = $"Cari Kodu: {_irsaliye.CariKart?.Kod ?? "-"}";
        TxtCariVergi.Text = $"V.D / No: {_irsaliye.CariKart?.VergiDairesi ?? "-"} / {_irsaliye.CariKart?.VergiNo ?? _irsaliye.CariKart?.TcNo ?? "-"}";

        TxtTarih.Text = _irsaliye.Tarih.ToString("dd.MM.yyyy HH:mm");
        TxtTur.Text = _irsaliye.Tur == IrsaliyeTuru.Satis ? "Satış (Sevk) İrsaliyesi" : "Alış İrsaliyesi";
        TxtDurum.Text = _irsaliye.Durum.ToString();

        TxtAraToplam.Text = $"{_irsaliye.AraToplam:N2} ₺";
        TxtKdvToplam.Text = $"{_irsaliye.KdvToplam:N2} ₺";
        TxtGenelToplam.Text = $"{_irsaliye.GenelToplam:N2} ₺";

        DgKalemler.ItemsSource = _irsaliye.Kalemler;

        BtnFaturayaDonustur.Visibility = _irsaliye.Durum == IrsaliyeDurumu.Onaylandi ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void BtnFaturayaDonustur_Click(object sender, RoutedEventArgs e)
    {
        var sonuc = MessageBox.Show(
            $"'{_irsaliye.IrsaliyeNo}' numaralı irsaliye resmi faturaya dönüştürülecektir. Devam etmek istiyor musunuz?",
            "Faturaya Dönüştür",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (sonuc != MessageBoxResult.Yes) return;

        try
        {
            var fatura = await _irsaliyeServisi.FaturayaDonusturAsync(_irsaliye.Id);
            MessageBox.Show(
                $"İrsaliye başarıyla '{fatura.FaturaNo}' numaralı faturaya dönüştürüldü!",
                "Başarılı",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (_anaModel != null)
            {
                await _anaModel.ListeleAsync();
            }

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Faturaya dönüştürme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnKapat_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
