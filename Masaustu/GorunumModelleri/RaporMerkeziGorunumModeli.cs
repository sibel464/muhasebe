using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;
using Uygulama.Dto.Rapor;

namespace Masaustu.GorunumModelleri;

public class RaporMerkeziGorunumModeli : TemelGorunumModeli
{
    private readonly IRaporMerkeziServisi _raporServisi;
    private readonly IExcelExportServisi _excelServisi;
    private readonly IPdfRaporServisi _pdfServisi;
    private readonly IAktifFirmaBaglami? _firmaBaglami;
    private readonly IKullaniciServisi? _kullaniciServisi;

    private readonly SemaphoreSlim _raporKilidi = new(1, 1);
    private int _raporIstegi;
    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;
    private int _seciliSekmeIndeksi;

    private SatisRaporFiltreDto _filtre = new();
    private YoneticiOzetDto _yoneticiOzet = new();
    private DonemKarsilastirmaResultDto _donemKarsilastirma = new();
    private PagedResult<SatisRaporuKalemiDto> _satisRaporu = new();
    private PagedResult<AlisRaporuKalemiDto> _alisRaporu = new();
    private PagedResult<CariRaporuKalemiDto> _cariRaporu = new();
    private PagedResult<StokRaporuKalemiDto> _stokRaporu = new();
    private PagedResult<FinansRaporuKalemiDto> _finansRaporu = new();
    private NakitAkisRaporuDto _nakitAkisRaporu = new();

    private bool _maliyetGor = true;
    private bool _karlilikGor = true;

    public ObservableCollection<TopUrunRaporKalemiDto> TopSatanUrunler { get; } = new();
    public ObservableCollection<TopUrunRaporKalemiDto> TopKarliUrunler { get; } = new();
    public ObservableCollection<TopUrunRaporKalemiDto> EnDusukKarliUrunler { get; } = new();
    public ObservableCollection<CariYaslandirmaRaporuKalemiDto> CariYaslandirmaRaporu { get; } = new();
    public ObservableCollection<TahsilatPerformansiKalemiDto> TahsilatPerformansi { get; } = new();
    public ObservableCollection<HareketsizUrunKalemiDto> HareketsizUrunler { get; } = new();
    public ObservableCollection<StokDegerRaporuKalemiDto> StokDegerRaporu { get; } = new();
    public ObservableCollection<KasaBankaRaporKalemiDto> KasaBankaRaporu { get; } = new();
    public ObservableCollection<PosRaporuKalemiDto> PosRaporu { get; } = new();
    public ObservableCollection<CekSenetRaporuKalemiDto> CekSenetRaporu { get; } = new();
    public ObservableCollection<KdvRaporuKalemiDto> KdvRaporu { get; } = new();
    public ObservableCollection<BaBsKalemiDto> BaBsRaporu { get; } = new();

    public SatisRaporFiltreDto Filtre
    {
        get => _filtre;
        set => OzellikAyarla(ref _filtre, value);
    }

    public YoneticiOzetDto YoneticiOzet
    {
        get => _yoneticiOzet;
        set => OzellikAyarla(ref _yoneticiOzet, value);
    }

    public DonemKarsilastirmaResultDto DonemKarsilastirma
    {
        get => _donemKarsilastirma;
        set => OzellikAyarla(ref _donemKarsilastirma, value);
    }

    public PagedResult<SatisRaporuKalemiDto> SatisRaporu
    {
        get => _satisRaporu;
        set => OzellikAyarla(ref _satisRaporu, value);
    }

    public PagedResult<AlisRaporuKalemiDto> AlisRaporu
    {
        get => _alisRaporu;
        set => OzellikAyarla(ref _alisRaporu, value);
    }

    public PagedResult<CariRaporuKalemiDto> CariRaporu
    {
        get => _cariRaporu;
        set => OzellikAyarla(ref _cariRaporu, value);
    }

    public PagedResult<StokRaporuKalemiDto> StokRaporu
    {
        get => _stokRaporu;
        set => OzellikAyarla(ref _stokRaporu, value);
    }

    public PagedResult<FinansRaporuKalemiDto> FinansRaporu
    {
        get => _finansRaporu;
        set => OzellikAyarla(ref _finansRaporu, value);
    }

    public NakitAkisRaporuDto NakitAkisRaporu
    {
        get => _nakitAkisRaporu;
        set => OzellikAyarla(ref _nakitAkisRaporu, value);
    }

    public bool Yukleniyor
    {
        get => _yukleniyor;
        set => OzellikAyarla(ref _yukleniyor, value);
    }

    public string DurumMesaji
    {
        get => _durumMesaji;
        set => OzellikAyarla(ref _durumMesaji, value);
    }

    public int SeciliSekmeIndeksi
    {
        get => _seciliSekmeIndeksi;
        set
        {
            if (value >= 0 && value <= 15 && OzellikAyarla(ref _seciliSekmeIndeksi, value))
            {
                Filtre.PageNumber = 1;
                _ = RaporYukleAsync();
            }
        }
    }

    public bool MaliyetGor
    {
        get => _maliyetGor;
        set => OzellikAyarla(ref _maliyetGor, value);
    }

    public bool KarlilikGor
    {
        get => _karlilikGor;
        set => OzellikAyarla(ref _karlilikGor, value);
    }

    public ICommand YenileKomutu { get; }
    public ICommand PresetDegistirKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }
    public ICommand PdfAktarKomutu { get; }
    public ICommand YazdirKomutu { get; }
    public ICommand SonrakiSayfaKomutu { get; }
    public ICommand ÖncekiSayfaKomutu { get; }

    public ICommand DrillDownKomutu { get; }

    public RaporMerkeziGorunumModeli(
        IRaporMerkeziServisi raporServisi,
        IExcelExportServisi excelServisi,
        IPdfRaporServisi pdfServisi,
        IAktifFirmaBaglami? firmaBaglami = null,
        IKullaniciServisi? kullaniciServisi = null)
    {
        _raporServisi = raporServisi;
        _excelServisi = excelServisi;
        _pdfServisi = pdfServisi;
        _firmaBaglami = firmaBaglami;
        _kullaniciServisi = kullaniciServisi;

        YenileKomutu = new KomutSarici(async _ => await RaporYukleAsync());
        PresetDegistirKomutu = new KomutSarici(async p => await PresetDegistirAsync(p?.ToString()));
        ExcelAktarKomutu = new KomutSarici(async p => await CiktiAlAsync(p, "excel"));
        PdfAktarKomutu = new KomutSarici(async p => await CiktiAlAsync(p, "pdf"));
        YazdirKomutu = new KomutSarici(async p => await CiktiAlAsync(p, "yazdir"));
        SonrakiSayfaKomutu = new KomutSarici(async _ => await SayfaDegistirAsync(1));
        ÖncekiSayfaKomutu = new KomutSarici(async _ => await SayfaDegistirAsync(-1));
        DrillDownKomutu = new KomutSarici(p => DrillDown(p));

        YetkileriYukle();
        _ = PresetDegistirAsync("BuAy");
    }

    private void DrillDown(object? p)
    {
        if (p == null) return;
        MessageBox.Show($"Drill-down detayı açılıyor: {p}", "Drill-Down Yönlendirme", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void YetkileriYukle()
    {
        if (_kullaniciServisi != null)
        {
            MaliyetGor = _kullaniciServisi.YetkiliMi(y => y.RaporGoruntule);
            KarlilikGor = _kullaniciServisi.YetkiliMi(y => y.RaporGoruntule);
        }
    }

    private async Task PresetDegistirAsync(string? key)
    {
        if (string.IsNullOrEmpty(key)) return;
        Filtre.PresetKey = key;

        var bugün = DateTime.Today;
        switch (key)
        {
            case "Bugun":
                Filtre.BaslangicTarihi = bugün;
                Filtre.BitisTarihi = bugün.AddDays(1).AddTicks(-1);
                break;
            case "Dun":
                Filtre.BaslangicTarihi = bugün.AddDays(-1);
                Filtre.BitisTarihi = bugün.AddTicks(-1);
                break;
            case "BuHafta":
                int diff = (int)bugün.DayOfWeek - (int)DayOfWeek.Monday;
                if (diff < 0) diff += 7;
                Filtre.BaslangicTarihi = bugün.AddDays(-diff);
                Filtre.BitisTarihi = bugün.AddDays(7 - diff).AddTicks(-1);
                break;
            case "BuAy":
                Filtre.BaslangicTarihi = new DateTime(bugün.Year, bugün.Month, 1);
                Filtre.BitisTarihi = Filtre.BaslangicTarihi.Value.AddMonths(1).AddTicks(-1);
                break;
            case "GecenAy":
                Filtre.BaslangicTarihi = new DateTime(bugün.Year, bugün.Month, 1).AddMonths(-1);
                Filtre.BitisTarihi = new DateTime(bugün.Year, bugün.Month, 1).AddTicks(-1);
                break;
            case "BuYil":
                Filtre.BaslangicTarihi = new DateTime(bugün.Year, 1, 1);
                Filtre.BitisTarihi = new DateTime(bugün.Year, 12, 31, 23, 59, 59);
                break;
            case "GecenYil":
                Filtre.BaslangicTarihi = new DateTime(bugün.Year - 1, 1, 1);
                Filtre.BitisTarihi = new DateTime(bugün.Year - 1, 12, 31, 23, 59, 59);
                break;
        }

        Filtre.PageNumber = 1;
        BildirimGonder(nameof(Filtre));
        await RaporYukleAsync();
    }

    public async Task RaporYukleAsync()
    {
        var istek = ++_raporIstegi;
        Yukleniyor = true;
        await _raporKilidi.WaitAsync();
        DurumMesaji = "Rapor verileri sorgulanıyor...";

        try
        {
            if (istek != _raporIstegi) return;
            var filtre = System.Text.Json.JsonSerializer.Deserialize<SatisRaporFiltreDto>(
                System.Text.Json.JsonSerializer.Serialize(Filtre))!;
            switch (SeciliSekmeIndeksi)
            {
                case 0: // Yönetici Özeti
                    YoneticiOzet = await _raporServisi.GetYoneticiOzetiAsync(filtre);
                    DonemKarsilastirma = await _raporServisi.GetDonemKarsilastirmaAsync(filtre);
                    break;
                case 1: // Satış Raporları
                    SatisRaporu = await _raporServisi.GetSatisRaporuAsync(filtre);
                    var topSatis = await _raporServisi.GetEnCokSatanUrunlerAsync(filtre, 10);
                    TopSatanUrunler.Clear();
                    foreach (var item in topSatis) TopSatanUrunler.Add(item);
                    break;
                case 2: // Alış Raporları
                    AlisRaporu = await _raporServisi.GetAlisRaporuAsync(filtre);
                    break;
                case 3: // Cari Raporları
                    CariRaporu = await _raporServisi.GetCariRaporuAsync(filtre);
                    break;
                case 4: // Cari Yaşlandırma
                    var yaslandirma = await _raporServisi.GetCariYaslandirmaAsync(filtre);
                    CariYaslandirmaRaporu.Clear();
                    foreach (var item in yaslandirma) CariYaslandirmaRaporu.Add(item);
                    break;
                case 5: // Stok Raporları
                    StokRaporu = await _raporServisi.GetStokRaporuAsync(filtre);
                    break;
                case 6: // Stok Devir / Hareketsiz
                    var hareketsiz = await _raporServisi.GetHareketsizUrunlerAsync(filtre, 30);
                    HareketsizUrunler.Clear();
                    foreach (var item in hareketsiz) HareketsizUrunler.Add(item);
                    break;
                case 7: // Stok Değeri
                    var stokDeger = await _raporServisi.GetStokDegerRaporuAsync(filtre);
                    StokDegerRaporu.Clear();
                    foreach (var item in stokDeger) StokDegerRaporu.Add(item);
                    break;
                case 8: // Finans Raporları
                    FinansRaporu = await _raporServisi.GetFinansRaporuAsync(filtre);
                    break;
                case 9: // Nakit Akışı
                    NakitAkisRaporu = await _raporServisi.GetNakitAkisRaporuAsync(filtre);
                    break;
                case 10: // Kasa / Banka
                    var kasaBanka = await _raporServisi.GetKasaBankaRaporuAsync(filtre);
                    KasaBankaRaporu.Clear();
                    foreach (var item in kasaBanka) KasaBankaRaporu.Add(item);
                    break;
                case 11: // POS Raporu
                    var posList = await _raporServisi.GetPosRaporuAsync(filtre);
                    PosRaporu.Clear();
                    foreach (var item in posList) PosRaporu.Add(item);
                    break;
                case 12: // Çek / Senet
                    var cekList = await _raporServisi.GetCekSenetRaporuAsync(filtre);
                    CekSenetRaporu.Clear();
                    foreach (var item in cekList) CekSenetRaporu.Add(item);
                    break;
                case 13: // KDV Raporu
                    var kdvList = await _raporServisi.GetKdvRaporuAsync(filtre);
                    KdvRaporu.Clear();
                    foreach (var item in kdvList) KdvRaporu.Add(item);
                    break;
                case 14: // Tahsilat Performansı
                    var tahsilat = await _raporServisi.GetTahsilatPerformansiAsync(filtre);
                    TahsilatPerformansi.Clear();
                    foreach (var item in tahsilat) TahsilatPerformansi.Add(item);
                    break;
                case 15: // Ba-Bs
                    var babs = await _raporServisi.GetBaBsRaporuAsync(filtre);
                    BaBsRaporu.Clear();
                    foreach (var item in babs) BaBsRaporu.Add(item);
                    break;
            }
            DurumMesaji = "Rapor güncellendi.";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Rapor oluşturulamadı: {ex.Message}";
        }
        finally
        {
            _raporKilidi.Release();
            if (istek == _raporIstegi) Yukleniyor = false;
        }
    }

    private async Task SayfaDegistirAsync(int delta)
    {
        Filtre.PageNumber += delta;
        if (Filtre.PageNumber < 1) Filtre.PageNumber = 1;
        await RaporYukleAsync();
    }

    private static readonly string[] SekmeAdlari =
    [
        "Dönem Karşılaştırması", "Satış Raporu", "Alış Raporu", "Cari Raporu", "Cari Yaşlandırma", "Stok Raporu",
        "Hareketsiz Ürünler", "Stok Değer Raporu", "Finans Raporu", "Nakit Akışı", "Kasa ve Banka Raporu", "POS Raporu",
        "Çek ve Senet Raporu", "KDV Raporu", "Tahsilat Performansı", "Ba-Bs Formları"
    ];

    private string SekmeAdi => SeciliSekmeIndeksi >= 0 && SeciliSekmeIndeksi < SekmeAdlari.Length ? SekmeAdlari[SeciliSekmeIndeksi] : "Rapor";

    private SatisRaporFiltreDto FiltreKopyasi() =>
        System.Text.Json.JsonSerializer.Deserialize<SatisRaporFiltreDto>(System.Text.Json.JsonSerializer.Serialize(Filtre))!;

    // Sayfalı raporlarda çıktı yalnız ekrandaki sayfayı değil dönemin tüm kayıtlarını içerir.
    private async Task<List<object>> TumSayfalarAsync<T>(Func<SatisRaporFiltreDto, Task<PagedResult<T>>> getir)
    {
        var f = FiltreKopyasi();
        f.PageSize = 500;
        var liste = new List<object>();
        for (f.PageNumber = 1; f.PageNumber <= 400; f.PageNumber++)
        {
            var sayfa = await getir(f);
            liste.AddRange(sayfa.Items.Cast<object>());
            if (sayfa.Items.Count == 0 || liste.Count >= sayfa.TotalCount) break;
        }
        return liste;
    }

    // Seçili sekmenin çıktı tablosu: ekrandaki sütunlar ve biçimler, sayfalı raporlarda tüm kayıtlar.
    private async Task<RaporTablosu?> SekmeTablosuAsync(object? sekmeIcerigi)
    {
        var donem = string.Join(" | ", new[]
        {
            _firmaBaglami?.SirketAdi,
            Filtre.BaslangicTarihi.HasValue || Filtre.BitisTarihi.HasValue
                ? $"Dönem: {Filtre.BaslangicTarihi:dd.MM.yyyy} - {Filtre.BitisTarihi:dd.MM.yyyy}" : null
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        if (SeciliSekmeIndeksi == 9)
        {
            var n = NakitAkisRaporu;
            var tablo = new RaporTablosu { Baslik = SekmeAdi, AltBaslik = donem, Kolonlar = [new("Kalem"), new("Tutar", true)] };
            foreach (var (ad, tutar) in new[] { ("Dönem başı nakit", n.DonemBasiNakit), ("Tahsilatlar", n.Tahsilatlar), ("Diğer girişler", n.DigerGirisler),
                         ("Ödemeler", n.Odemeler), ("Diğer çıkışlar", n.DigerCikislar), ("Net nakit akışı", n.NetNakitAkisi), ("Dönem sonu nakit", n.DonemSonuNakit) })
                tablo.Satirlar.Add([new RaporHucresi(ad), new RaporHucresi($"{tutar:N2} TL", tutar)]);
            return tablo;
        }

        var grid = EkranTablosu.IlkDataGrid(sekmeIcerigi);
        if (grid == null) return null;
        System.Collections.IEnumerable satirlar = SeciliSekmeIndeksi switch
        {
            1 => await TumSayfalarAsync(_raporServisi.GetSatisRaporuAsync),
            2 => await TumSayfalarAsync(_raporServisi.GetAlisRaporuAsync),
            3 => await TumSayfalarAsync(_raporServisi.GetCariRaporuAsync),
            5 => await TumSayfalarAsync(_raporServisi.GetStokRaporuAsync),
            8 => await TumSayfalarAsync(_raporServisi.GetFinansRaporuAsync),
            _ => grid.Items.Cast<object>().ToList()
        };
        return EkranTablosu.Olustur(grid, satirlar, SekmeAdi, donem);
    }

    private string? KayitYeriSor(string filtre, string uzanti)
    {
        var ad = new string($"{SekmeAdi} {DateTime.Now:yyyy-MM-dd HHmm}".Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = filtre, FileName = ad + uzanti, Title = $"{SekmeAdi} - Kaydet" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static void KaydedildiBildir(string yol)
    {
        if (MessageBox.Show($"Dosya kaydedildi:\n{yol}\n\nŞimdi açılsın mı?", "Rapor kaydedildi", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(yol) { UseShellExecute = true });
    }

    private async Task CiktiAlAsync(object? sekmeIcerigi, string tur)
    {
        try
        {
            DurumMesaji = "Çıktı hazırlanıyor...";
            var tablo = await SekmeTablosuAsync(sekmeIcerigi);
            if (tablo == null)
            {
                MessageBox.Show("Bu sekmede çıktısı alınacak tablo yok.", "Rapor çıktısı", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            switch (tur)
            {
                case "excel":
                    if (KayitYeriSor("Excel Dosyası (*.xlsx)|*.xlsx", ".xlsx") is { } excelYolu)
                    {
                        await File.WriteAllBytesAsync(excelYolu, _excelServisi.ExcelOlustur(tablo));
                        KaydedildiBildir(excelYolu);
                    }
                    break;
                case "pdf":
                    if (KayitYeriSor("PDF Dosyası (*.pdf)|*.pdf", ".pdf") is { } pdfYolu)
                    {
                        await File.WriteAllBytesAsync(pdfYolu, _pdfServisi.PdfOlustur(tablo));
                        KaydedildiBildir(pdfYolu);
                    }
                    break;
                default:
                    var geciciYol = Path.Combine(Path.GetTempPath(), $"Rapor_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                    await File.WriteAllTextAsync(geciciYol, _pdfServisi.HtmlOlustur(tablo), System.Text.Encoding.UTF8);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(geciciYol) { UseShellExecute = true });
                    break;
            }
            DurumMesaji = $"{SekmeAdi}: {tablo.Satirlar.Count} kayıt çıktıya hazırlandı.";
        }
        catch (Exception ex)
        {
            DurumMesaji = "Çıktı alınamadı.";
            MessageBox.Show($"Çıktı alınamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
