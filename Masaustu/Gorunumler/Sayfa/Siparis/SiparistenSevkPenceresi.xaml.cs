using System.Collections.ObjectModel;
using System.Windows;
using EtkiAlani.Modeller.Irsaliye;
using EtkiAlani.Modeller.Siparis;
using SiparisVarligi = EtkiAlani.Modeller.Siparis.Siparis;
using IrsaliyeVarligi = EtkiAlani.Modeller.Irsaliye.Irsaliye;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa.Siparis;

public partial class SiparistenSevkPenceresi : Window
{
    private readonly IIrsaliyeServisi _servis;
    public SiparisVarligi Siparis { get; }
    public ObservableCollection<SevkSatiri> Satirlar { get; } = new();
    public string Tasiyici { get; set; } = string.Empty; public string AracPlaka { get; set; } = string.Empty;
    public string SoforAdi { get; set; } = string.Empty; public string SoforTelefon { get; set; } = string.Empty;
    public SiparistenSevkPenceresi(SiparisVarligi siparis, IIrsaliyeServisi servis)
    {
        InitializeComponent(); Siparis = siparis; _servis = servis; DataContext = this; Loaded += Yukle;
    }
    private async void Yukle(object sender, RoutedEventArgs e)
    {
        try
        {
            // Sütunlar gerçek aktif stok yerlerine bağlanır: ilk mağaza ve ilk iki depo. Eskiden depo numarası
            // 1-2-3 sabitti; depolar yeniden tanımlanınca (ör. 4-5-6) sevk "depo bulunamadı veya pasif" hatası veriyordu.
            var yerler = await _servis.AktifStokYerleriniGetirAsync();
            var magaza = yerler.FirstOrDefault(d => d.DepoTuru == EtkiAlani.Modeller.Kurumsal.DepoTuru.Magaza);
            var depolar = yerler.Where(d => d.DepoTuru == EtkiAlani.Modeller.Kurumsal.DepoTuru.Depo).ToList();
            var sutunDepolari = new[] { magaza, depolar.ElementAtOrDefault(0), depolar.ElementAtOrDefault(1) };
            if (sutunDepolari.All(d => d == null)) throw new InvalidOperationException("Tanımlı aktif mağaza/depo yok. Önce Depo ekranından stok yeri tanımlayın.");
            var sutunlar = new[] { MagazaSutunu, Depo1Sutunu, Depo2Sutunu };
            for (var i = 0; i < 3; i++)
            {
                sutunlar[i].Header = sutunDepolari[i]?.DepoAdi.ToUpper(new System.Globalization.CultureInfo("tr-TR")) ?? "";
                sutunlar[i].Visibility = sutunDepolari[i] == null ? Visibility.Collapsed : Visibility.Visible;
            }
            var depoIdleri = sutunDepolari.Select(d => d?.Id).ToArray();

            var stoklar = await _servis.LokasyonStoklariniGetirAsync(Siparis.Kalemler.Select(x => x.UrunId));
            foreach (var k in Siparis.Kalemler.Where(x => x.Miktar > x.SevkEdilenMiktar))
                Satirlar.Add(new SevkSatiri(k, stoklar.Where(x => x.UrunId == k.UrunId).ToDictionary(x => x.DepoId, x => x.MevcutMiktar), depoIdleri));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Stoklar yüklenemedi", MessageBoxButton.OK, MessageBoxImage.Error); Close(); }
    }
    private async void Olustur_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var secilen = Satirlar.Where(x => x.BuSevk > 0).ToList();
            if (secilen.Count == 0) throw new InvalidOperationException("En az bir ürün için sevk miktarı girin.");
            foreach (var x in secilen)
            {
                if (x.BuSevk > x.Kalan) throw new InvalidOperationException($"{x.UrunAdi}: Bu sevk kalan miktardan fazla olamaz.");
                if (x.Magaza + x.Depo1 + x.Depo2 != x.BuSevk) throw new InvalidOperationException($"{x.UrunAdi}: Stok dağılımı ({x.Magaza + x.Depo1 + x.Depo2:N2}) sevk miktarına ({x.BuSevk:N2}) eşit olmalıdır.");
            }
            var irsaliye = new IrsaliyeVarligi { SiparisId = Siparis.Id, SevkTarihi = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc), Tasiyici = Tasiyici, AracPlaka = AracPlaka, SoforAdi = SoforAdi, SoforTelefon = SoforTelefon, Kalemler = secilen.Select(x => x.IrsaliyeKalemi()).ToList() };
            var sonuc = await _servis.SiparistenSevkEtAsync(irsaliye);
            MessageBox.Show($"{sonuc.IrsaliyeNo} numaralı irsaliye oluşturuldu.", "Sevkiyat tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information); DialogResult = true;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Sevkiyat oluşturulamadı", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}

public sealed class SevkSatiri : Masaustu.GorunumModelleri.TemelGorunumModeli
{
    private readonly SiparisKalemi _kalem;
    private decimal _buSevk;
    private decimal _magaza;
    private decimal _depo1;
    private decimal _depo2;

    public string UrunAdi => _kalem.UrunAdi;
    public decimal SiparisMiktari => _kalem.Miktar;
    public decimal OncekiSevk => _kalem.SevkEdilenMiktar;
    public decimal Kalan => _kalem.Miktar - _kalem.SevkEdilenMiktar;

    public decimal BuSevk { get => _buSevk; set { if (OzellikAyarla(ref _buSevk, value)) BildirimGonder(nameof(BuSevkMetni)); } }
    public decimal Magaza { get => _magaza; set { if (OzellikAyarla(ref _magaza, value)) BildirimGonder(nameof(MagazaMetni)); } }
    public decimal Depo1 { get => _depo1; set { if (OzellikAyarla(ref _depo1, value)) BildirimGonder(nameof(Depo1Metni)); } }
    public decimal Depo2 { get => _depo2; set { if (OzellikAyarla(ref _depo2, value)) BildirimGonder(nameof(Depo2Metni)); } }

    public string BuSevkMetni { get => _buSevk == 0 ? string.Empty : _buSevk.ToString("0.####"); set => BuSevk = ParseDecimal(value); }
    public string MagazaMetni { get => _magaza == 0 ? string.Empty : _magaza.ToString("0.####"); set => Magaza = ParseDecimal(value); }
    public string Depo1Metni { get => _depo1 == 0 ? string.Empty : _depo1.ToString("0.####"); set => Depo1 = ParseDecimal(value); }
    public string Depo2Metni { get => _depo2 == 0 ? string.Empty : _depo2.ToString("0.####"); set => Depo2 = ParseDecimal(value); }

    public decimal MagazaMevcut { get; }
    public decimal Depo1Mevcut { get; }
    public decimal Depo2Mevcut { get; }
    public string MevcutBilgisi => $"{MagazaMevcut:N2} / {Depo1Mevcut:N2} / {Depo2Mevcut:N2}";

    private readonly int?[] _depoIdleri;

    // depoIdleri: [mağaza, depo 1, depo 2]; olmayan stok yeri null.
    public SevkSatiri(SiparisKalemi kalem, Dictionary<int, decimal> stok, int?[] depoIdleri)
    {
        _kalem = kalem;
        _depoIdleri = depoIdleri;
        _buSevk = Kalan;
        decimal Mevcut(int? id) => id is int d ? stok.GetValueOrDefault(d) : 0;
        MagazaMevcut = Mevcut(depoIdleri[0]);
        Depo1Mevcut = Mevcut(depoIdleri[1]);
        Depo2Mevcut = Mevcut(depoIdleri[2]);
        // Varsayılan dağılım: ilk mevcut stok yeri (mağaza yoksa depo 1, o da yoksa depo 2).
        if (depoIdleri[0] != null) _magaza = _buSevk;
        else if (depoIdleri[1] != null) _depo1 = _buSevk;
        else _depo2 = _buSevk;
    }

    private static decimal ParseDecimal(string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0;
        if (decimal.TryParse(val.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d1)) return Math.Max(0, d1);
        if (decimal.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), out var d2)) return Math.Max(0, d2);
        return 0;
    }

    public IrsaliyeKalemi IrsaliyeKalemi() => new()
    {
        SiparisKalemiId = _kalem.Id,
        Miktar = BuSevk,
        StokDagilimlari = new[] { (_depoIdleri[0], Magaza), (_depoIdleri[1], Depo1), (_depoIdleri[2], Depo2) }
            .Where(x => x.Item1 != null && x.Item2 > 0)
            .Select(x => new IrsaliyeKalemiStokDagilimi { DepoId = x.Item1!.Value, Miktar = x.Item2 })
            .ToList()
    };
}
