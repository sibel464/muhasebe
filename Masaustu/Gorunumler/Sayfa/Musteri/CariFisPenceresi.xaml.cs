using System.Globalization;
using System.Windows;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa.Musteri;

// Elle cari fişi: borç dekontu, alacak dekontu veya açılış/devir. Kayıt servis tarafında
// cari hareketi ve muhasebe fişini (cari hesabı ↔ seçilen karşı hesap) birlikte oluşturur.
public partial class CariFisPenceresi : Window
{
    private sealed record HesapSecenegi(string Kod, string Ad)
    {
        public string Gorunen => $"{Kod} - {Ad}";
    }

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private readonly CariKart _cari;
    private readonly ICariHareketServisi _servis;
    private readonly string _cariHesap;
    private List<HesapSecenegi> _hesaplar = new();
    private bool _yuklendi;

    public CariFisPenceresi(CariKart cari, ICariHareketServisi servis, Guid sirketId)
    {
        InitializeComponent();
        _cari = cari;
        _servis = servis;
        _cariHesap = cari.Tur == CariTuru.Tedarikci ? "320" : "120";
        Title = "Cari Fiş • " + cari.Kod;
        CariBaslik.Text = $"{cari.Kod} - {cari.Unvan}";
        TarihKutusu.SelectedDate = DateTime.Today;
        Loaded += async (_, _) =>
        {
            try
            {
                _hesaplar = (await _servis.KarsiHesaplariGetirAsync(sirketId)).Select(h => new HesapSecenegi(h.Kod, h.Ad)).ToList();
                HesapKutusu.ItemsSource = _hesaplar;
            }
            catch (Exception ex)
            {
                HataMetni.Text = "Hesap planı okunamadı: " + ex.Message;
            }
            _yuklendi = true;
            TurKutusu.SelectedIndex = 0;
            TutarKutusu.Focus();
        };
    }

    private bool DevirMi => TurKutusu.SelectedIndex == 2;
    private bool CariBorclanir => TurKutusu.SelectedIndex == 0 || (DevirMi && YonKutusu.SelectedIndex == 0);

    private void Degisti(object sender, RoutedEventArgs e)
    {
        if (!_yuklendi) return;
        YonPaneli.Visibility = DevirMi ? Visibility.Visible : Visibility.Collapsed;

        // Tür değişince önerilen karşı hesap seçilir: borç dekontu → 602, alacak dekontu → 659; devirde seçim kullanıcıya bırakılır.
        if (ReferenceEquals(sender, TurKutusu))
        {
            var oneri = TurKutusu.SelectedIndex switch { 0 => "602", 1 => "659", _ => null };
            HesapKutusu.SelectedItem = oneri == null ? null : _hesaplar.FirstOrDefault(h => h.Kod == oneri);
        }
        OnizlemeyiGuncelle();
    }

    private void OnizlemeyiGuncelle()
    {
        var karsi = HesapKutusu.SelectedItem as HesapSecenegi;
        var tutar = decimal.TryParse(TutarKutusu.Text, NumberStyles.Number, Tr, out var t) && t > 0 ? t.ToString("N2", Tr) : "…";
        var cariSatir = $"{_cariHesap} {(_cariHesap == "320" ? "Satıcılar" : "Alıcılar")} ({_cari.Kod})";
        var karsiSatir = karsi?.Gorunen ?? "(karşı hesap seçin)";
        OnizlemeMetni.Text = CariBorclanir
            ? $"Muhasebe fişi:\nBORÇ    {cariSatir}  {tutar}\nALACAK  {karsiSatir}  {tutar}"
            : $"Muhasebe fişi:\nBORÇ    {karsiSatir}  {tutar}\nALACAK  {cariSatir}  {tutar}";
    }

    private async void Kaydet_Click(object sender, RoutedEventArgs e)
    {
        HataMetni.Text = string.Empty;
        if (!decimal.TryParse(TutarKutusu.Text, NumberStyles.Number, Tr, out var tutar) || tutar <= 0)
        { HataMetni.Text = "Geçerli bir tutar girin (ör. 1.250,50)."; return; }
        tutar = Math.Round(tutar, 2);
        if (TarihKutusu.SelectedDate is not DateTime tarih)
        { HataMetni.Text = "Tarih seçin."; return; }
        if (string.IsNullOrWhiteSpace(BelgeNoKutusu.Text))
        { HataMetni.Text = "Belge numarası girin."; return; }
        if (HesapKutusu.SelectedItem is not HesapSecenegi karsi)
        { HataMetni.Text = "Listeden bir karşı muhasebe hesabı seçin."; return; }

        var turMetni = DevirMi ? "Açılış / Devir" : TurKutusu.SelectedIndex == 0 ? "Borç Dekontu" : "Alacak Dekontu";
        var aciklama = string.IsNullOrWhiteSpace(AciklamaKutusu.Text) ? turMetni : $"{turMetni} • {AciklamaKutusu.Text.Trim()}";

        KaydetDugmesi.IsEnabled = false;
        try
        {
            await _servis.EkleAsync(new CariHareket
            {
                CariKartId = _cari.Id,
                // Seçilen gün korunur: o günün şu anki saatiyle UTC'ye çevrilir.
                Tarih = (tarih.Date + DateTime.Now.TimeOfDay).ToUniversalTime(),
                HareketTuru = DevirMi ? CariHareketTuru.Devir : CariHareketTuru.Diger,
                BelgeNo = BelgeNoKutusu.Text.Trim(),
                Aciklama = aciklama,
                Borc = CariBorclanir ? tutar : 0,
                Alacak = CariBorclanir ? 0 : tutar
            }, karsi.Kod);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            HataMetni.Text = ex.Message;
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "CariFis");
            HataMetni.Text = "Fiş kaydedilemedi. Ayrıntılar log dosyasına yazıldı.";
        }
        finally
        {
            KaydetDugmesi.IsEnabled = true;
        }
    }
}
