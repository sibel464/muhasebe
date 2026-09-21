using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Ortak;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class YevmiyeDefteriGorunumModeli : TemelGorunumModeli
{
    private readonly IYevmiyeServisi _yevmiyeServisi;
    private readonly IExcelExportServisi _excelServisi;
    private readonly IPdfRaporServisi _pdfServisi;

    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;

    private YevmiyeFiltreDto _filtre = new();
    private SayfaliSonuc<YevmiyeSatiriDto>? _sayfaSonucu;

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

    public YevmiyeFiltreDto Filtre
    {
        get => _filtre;
        set => OzellikAyarla(ref _filtre, value);
    }

    public SayfaliSonuc<YevmiyeSatiriDto>? SayfaSonucu
    {
        get => _sayfaSonucu;
        private set => OzellikAyarla(ref _sayfaSonucu, value);
    }

    public ObservableCollection<YevmiyeSatiriDto> Satirlar { get; } = new();

    public ICommand YenileKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }
    public ICommand PdfAktarKomutu { get; }
    public ICommand SonrakiSayfaKomutu { get; }
    public ICommand ÖncekiSayfaKomutu { get; }

    public YevmiyeDefteriGorunumModeli(
        IYevmiyeServisi yevmiyeServisi,
        IExcelExportServisi excelServisi,
        IPdfRaporServisi pdfServisi)
    {
        _yevmiyeServisi = yevmiyeServisi;
        _excelServisi = excelServisi;
        _pdfServisi = pdfServisi;

        YenileKomutu = new KomutSarici(async () => await SorgulaAsync());
        ExcelAktarKomutu = new KomutSarici(async () => await ExcelAktarAsync());
        PdfAktarKomutu = new KomutSarici(async () => await PdfAktarAsync());
        SonrakiSayfaKomutu = new KomutSarici(async () => { Filtre.Sayfa++; await SorgulaAsync(); }, () => SayfaSonucu != null && SayfaSonucu.SonrakiSayfaVar);
        ÖncekiSayfaKomutu = new KomutSarici(async () => { Filtre.Sayfa--; await SorgulaAsync(); }, () => SayfaSonucu != null && SayfaSonucu.ÖncekiSayfaVar);

        _ = SorgulaAsync();
    }

    public async Task SorgulaAsync()
    {
        try
        {
            Yukleniyor = true;
            DurumMesaji = "Yevmiye defteri yükleniyor...";
            Satirlar.Clear();

            SayfaSonucu = await _yevmiyeServisi.YevmiyeDefteriSorgulaAsync(Filtre);
            foreach (var s in SayfaSonucu.Liste)
            {
                Satirlar.Add(s);
            }

            DurumMesaji = $"Yevmiye Defteri: Sayfa {SayfaSonucu.Sayfa}/{SayfaSonucu.ToplamSayfa} - Toplam {SayfaSonucu.ToplamKayit} yevmiye satırı.";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Hata: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task ExcelAktarAsync()
    {
        try
        {
            DurumMesaji = "Excel aktarılıyor...";
            
            var kolonlar = new Dictionary<string, string>
            {
                { "Tarih", "Tarih" },
                { "FisNo", "Fiş No" },
                { "HesapKodu", "Hesap Kodu" },
                { "HesapAdi", "Hesap Adı" },
                { "Borc", "Borç" },
                { "Alacak", "Alacak" },
                { "Aciklama", "Açıklama" },
                { "BelgeNo", "Belge No" },
                { "FisTuru", "Fiş Türü" }
            };

            var filtre = new Uygulama.Dto.Rapor.SatisRaporFiltreDto { PresetKey = "Yevmiye Defteri" };
            
            var bytes = _excelServisi.ExportToExcel("Yevmiye Defteri Raporu", filtre, Satirlar, kolonlar);

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Dosyası|*.xlsx",
                FileName = $"YevmiyeDefteri_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
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
                { "Tarih", "Tarih" },
                { "FisNo", "Fiş No" },
                { "HesapKodu", "Hesap Kodu" },
                { "HesapAdi", "Hesap Adı" },
                { "Borc", "Borç" },
                { "Alacak", "Alacak" },
                { "Aciklama", "Açıklama" },
                { "BelgeNo", "Belge No" },
                { "FisTuru", "Fiş Türü" }
            };

            var filtre = new Uygulama.Dto.Rapor.SatisRaporFiltreDto { PresetKey = "Yevmiye Defteri" };
            
            var bytes = _pdfServisi.ExportToPdf("Yevmiye Defteri Raporu", filtre, Satirlar, kolonlar);

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Dosyası|*.pdf",
                FileName = $"YevmiyeDefteri_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
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
