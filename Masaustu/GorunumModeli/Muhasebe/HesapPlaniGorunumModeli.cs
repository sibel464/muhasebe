using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class HesapPlaniGorunumModeli : TemelGorunumModeli
{
    private readonly IHesapPlaniServisi _hesapServisi;
    private readonly IExcelExportServisi _excelServisi;
    private readonly IPdfRaporServisi _pdfServisi;

    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;
    private string _aramaMetni = string.Empty;
    
    // Filters
    private string _seciliDurumFiltresi = "Tümü";
    private string _seciliTipFiltresi = "Tümü";
    private bool _sadeceHareketGorenler = false;

    private HesapPlaniDto? _seciliHesap;

    public ObservableCollection<HesapPlaniDto> Hesaplar { get; } = new();
    public ObservableCollection<HesapPlaniDto> GosterilenHesaplar { get; } = new();

    public List<string> DurumFiltreleri { get; } = new() { "Tümü", "Aktif Hesaplar", "Pasif Hesaplar" };
    public List<string> TipFiltreleri { get; } = new() { "Tümü", "Aktif (Varlık)", "Pasif (Kaynak)", "Gelir", "Gider" };

    public bool Yukleniyor
    {
        get => _yukleniyor;
        private set { OzellikAyarla(ref _yukleniyor, value); CommandManager.InvalidateRequerySuggested(); }
    }

    public string DurumMesaji
    {
        get => _durumMesaji;
        private set => OzellikAyarla(ref _durumMesaji, value);
    }

    public string AramaMetni
    {
        get => _aramaMetni;
        set
        {
            if (OzellikAyarla(ref _aramaMetni, value))
            {
                UygulaFiltre();
            }
        }
    }

    public string SeciliDurumFiltresi
    {
        get => _seciliDurumFiltresi;
        set
        {
            if (OzellikAyarla(ref _seciliDurumFiltresi, value))
                UygulaFiltre();
        }
    }

    public string SeciliTipFiltresi
    {
        get => _seciliTipFiltresi;
        set
        {
            if (OzellikAyarla(ref _seciliTipFiltresi, value))
                UygulaFiltre();
        }
    }

    public bool SadeceHareketGorenler
    {
        get => _sadeceHareketGorenler;
        set
        {
            if (OzellikAyarla(ref _sadeceHareketGorenler, value))
                UygulaFiltre();
        }
    }

    public HesapPlaniDto? SeciliHesap
    {
        get => _seciliHesap;
        set => OzellikAyarla(ref _seciliHesap, value);
    }

    public ICommand YenileKomutu { get; }
    public ICommand HesapEkleKomutu { get; }
    public ICommand HesapDuzenleKomutu { get; }
    public ICommand Hesap360AcKomutu { get; }
    public ICommand HesapDurumDegistirKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }
    public ICommand PdfAktarKomutu { get; }

    public HesapPlaniGorunumModeli(
        IHesapPlaniServisi hesapServisi,
        IExcelExportServisi excelServisi,
        IPdfRaporServisi pdfServisi)
    {
        _hesapServisi = hesapServisi;
        _excelServisi = excelServisi;
        _pdfServisi = pdfServisi;

        YenileKomutu = new KomutSarici(async () => await ListeyiYenileAsync());
        HesapEkleKomutu = new KomutSarici(HesapEkle);
        HesapDuzenleKomutu = new KomutSarici(HesapDuzenle, () => SeciliHesap != null);
        Hesap360AcKomutu = new KomutSarici(Hesap360Ac, () => SeciliHesap != null);
        HesapDurumDegistirKomutu = new KomutSarici(async () => await PasifeAlAsync(), () => SeciliHesap != null);
        ExcelAktarKomutu = new KomutSarici(async () => await ExcelAktarAsync());
        PdfAktarKomutu = new KomutSarici(async () => await PdfAktarAsync());

        _ = ListeyiYenileAsync();
    }

    public async Task ListeyiYenileAsync()
    {
        try
        {
            Yukleniyor = true;
            DurumMesaji = "Hesap planı yükleniyor...";
            Hesaplar.Clear();

            // Fetch all initially, we do client side filtering for speed
            var liste = await _hesapServisi.HesapPlaniGetirAsync("", false); 
            foreach (var h in liste)
            {
                Hesaplar.Add(h);
            }

            UygulaFiltre();
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Hata: {ex.Message}";
            MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private void UygulaFiltre()
    {
        GosterilenHesaplar.Clear();
        var q = (AramaMetni ?? "").Trim().ToLower();

        foreach (var h in Hesaplar)
        {
            if (!string.IsNullOrEmpty(q) && !h.HesapKodu.ToLower().Contains(q) && !h.HesapAdi.ToLower().Contains(q))
                continue;

            if (SeciliDurumFiltresi == "Aktif Hesaplar" && !h.AktifMi) continue;
            if (SeciliDurumFiltresi == "Pasif Hesaplar" && h.AktifMi) continue;

            if (SeciliTipFiltresi != "Tümü")
            {
                if (SeciliTipFiltresi == "Aktif (Varlık)" && h.Tip != EtkiAlani.Modeller.Muhasebe.HesapTipi.Aktif) continue;
                if (SeciliTipFiltresi == "Pasif (Kaynak)" && h.Tip != EtkiAlani.Modeller.Muhasebe.HesapTipi.Pasif) continue;
                if (SeciliTipFiltresi == "Gelir" && h.Tip != EtkiAlani.Modeller.Muhasebe.HesapTipi.Gelir) continue;
                if (SeciliTipFiltresi == "Gider" && h.Tip != EtkiAlani.Modeller.Muhasebe.HesapTipi.Gider) continue;
            }

            if (SadeceHareketGorenler && !h.HareketGordu) continue;

            GosterilenHesaplar.Add(h);
        }
        
        DurumMesaji = $"{GosterilenHesaplar.Count} hesap listelendi.";
    }

    private void HesapEkle()
    {
        var gm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<HesapEkleDuzenleGorunumModeli>(App.HizmetSaglayici);
        var pencere = new Masaustu.Gorunumler.Sayfa.Muhasebe.HesapEkleDuzenlePenceresi(gm);
        pencere.Owner = Application.Current.MainWindow;
        pencere.ShowDialog();
        if (pencere.Kaydedildi)
        {
            _ = ListeyiYenileAsync();
        }
    }

    private void HesapDuzenle()
    {
        if (SeciliHesap == null) return;
        var gm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<HesapEkleDuzenleGorunumModeli>(App.HizmetSaglayici);
        gm.Yukle(SeciliHesap.Id);
        var pencere = new Masaustu.Gorunumler.Sayfa.Muhasebe.HesapEkleDuzenlePenceresi(gm);
        pencere.Owner = Application.Current.MainWindow;
        pencere.ShowDialog();
        if (pencere.Kaydedildi)
        {
            _ = ListeyiYenileAsync();
        }
    }

    private void Hesap360Ac()
    {
        if (SeciliHesap == null) return;
        var pencere = new Masaustu.Gorunumler.Sayfa.Muhasebe.Hesap360Penceresi(SeciliHesap.Id);
        pencere.Owner = Application.Current.MainWindow;
        pencere.ShowDialog();
    }

    private async Task PasifeAlAsync()
    {
        if (SeciliHesap == null) return;

        string eylem = SeciliHesap.AktifMi ? "Pasife almak" : "Aktifleştirmek";

        var sonuc = MessageBox.Show(
            $"[{SeciliHesap.HesapKodu}] {SeciliHesap.HesapAdi} hesabının durumunu değiştirmek ({eylem}) istediğinize emin misiniz?",
            "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (sonuc == MessageBoxResult.Yes)
        {
            try
            {
                var res = await _hesapServisi.HesapDurumDegistirAsync(SeciliHesap.Id, !SeciliHesap.AktifMi);
                if (res)
                {
                    DurumMesaji = $"Hesap başarıyla {(SeciliHesap.AktifMi ? "pasife alındı" : "aktifleştirildi")}.";
                    await ListeyiYenileAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "İşlem Başarısız", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private async Task ExcelAktarAsync()
    {
        try
        {
            DurumMesaji = "Excel aktarılıyor...";
            
            var kolonlar = new Dictionary<string, string>
            {
                { "HesapKodu", "Hesap Kodu" },
                { "HesapAdi", "Hesap Adı" },
                { "Tip", "Tip" },
                { "BakiyeYonuStr", "Bakiye Yönü" },
                { "ParaBirimiKodu", "Para Birimi" },
                { "Bakiye", "Bakiye" },
                { "DurumStr", "Durum" }
            };

            var filtre = new Uygulama.Dto.Rapor.SatisRaporFiltreDto { PresetKey = "Hesap Planı" };
            
            var bytes = _excelServisi.ExportToExcel("Hesap Planı Raporu", filtre, GosterilenHesaplar, kolonlar);

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Dosyası|*.xlsx",
                FileName = $"HesapPlani_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (sfd.ShowDialog() == true)
            {
                await System.IO.File.WriteAllBytesAsync(sfd.FileName, bytes);
                DurumMesaji = "Excel dosyası başarıyla kaydedildi.";
            }
            else
            {
                DurumMesaji = "Excel aktarma iptal edildi.";
            }
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Excel aktarma hatası: {ex.Message}";
        }
    }

    private async Task PdfAktarAsync()
    {
        try
        {
            DurumMesaji = "PDF aktarılıyor...";
            
            var kolonlar = new Dictionary<string, string>
            {
                { "HesapKodu", "Hesap Kodu" },
                { "HesapAdi", "Hesap Adı" },
                { "Tip", "Tip" },
                { "BakiyeYonuStr", "Bakiye Yönü" },
                { "ParaBirimiKodu", "Para Birimi" },
                { "Bakiye", "Bakiye" },
                { "DurumStr", "Durum" }
            };

            var filtre = new Uygulama.Dto.Rapor.SatisRaporFiltreDto { PresetKey = "Hesap Planı" };
            
            var bytes = _pdfServisi.ExportToPdf("Hesap Planı Raporu", filtre, GosterilenHesaplar, kolonlar);

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Dosyası|*.pdf",
                FileName = $"HesapPlani_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                await System.IO.File.WriteAllBytesAsync(sfd.FileName, bytes);
                DurumMesaji = "PDF dosyası başarıyla kaydedildi.";
            }
            else
            {
                DurumMesaji = "PDF aktarma iptal edildi.";
            }
        }
        catch (Exception ex)
        {
            DurumMesaji = $"PDF aktarma hatası: {ex.Message}";
        }
    }
}
