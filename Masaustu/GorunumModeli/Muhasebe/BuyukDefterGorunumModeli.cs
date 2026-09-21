using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class BuyukDefterGorunumModeli : TemelGorunumModeli
{
    private readonly IMuhasebeRaporServisi _raporServisi;
    private readonly IExcelExportServisi _excelServisi;
    private readonly IPdfRaporServisi _pdfServisi;

    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;
    private DateTime? _baslangicTarihi = DateTime.UtcNow.AddMonths(-1);
    private DateTime? _bitisTarihi = DateTime.UtcNow;

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

    public DateTime? BaslangicTarihi
    {
        get => _baslangicTarihi;
        set { if (OzellikAyarla(ref _baslangicTarihi, value)) _ = SorgulaAsync(); }
    }

    public DateTime? BitisTarihi
    {
        get => _bitisTarihi;
        set { if (OzellikAyarla(ref _bitisTarihi, value)) _ = SorgulaAsync(); }
    }

    public ObservableCollection<BuyukDefterSatirDto> HesapOzetleri { get; } = new();

    public ICommand YenileKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }
    public ICommand PdfAktarKomutu { get; }

    public BuyukDefterGorunumModeli(
        IMuhasebeRaporServisi raporServisi,
        IExcelExportServisi excelServisi,
        IPdfRaporServisi pdfServisi)
    {
        _raporServisi = raporServisi;
        _excelServisi = excelServisi;
        _pdfServisi = pdfServisi;

        YenileKomutu = new KomutSarici(async () => await SorgulaAsync());
        ExcelAktarKomutu = new KomutSarici(async () => await ExcelAktarAsync());
        PdfAktarKomutu = new KomutSarici(async () => await PdfAktarAsync());

        _ = SorgulaAsync();
    }

    public async Task SorgulaAsync()
    {
        try
        {
            Yukleniyor = true;
            DurumMesaji = "Büyük Defter (Defter-i Kebir) yükleniyor...";
            HesapOzetleri.Clear();

            var liste = await _raporServisi.BuyukDefterAlAsync(Guid.Empty, BaslangicTarihi, BitisTarihi);
            foreach (var h in liste)
            {
                HesapOzetleri.Add(h);
            }

            DurumMesaji = $"Büyük Defter: {HesapOzetleri.Count} hesap dökümü listelendi.";
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
                { "HesapKodu", "Hesap Kodu" },
                { "HesapAdi", "Hesap Adı" },
                { "DevirBorc", "Devir Borç" },
                { "DevirAlacak", "Devir Alacak" },
                { "DonemBorc", "Dönem Borç" },
                { "DonemAlacak", "Dönem Alacak" },
                { "ToplamBorc", "Toplam Borç" },
                { "ToplamAlacak", "Toplam Alacak" },
                { "Bakiye", "Bakiye" },
                { "BakiyeYonu", "Bakiye Yönü" },
                { "HareketSayisi", "Hareket Sayısı" }
            };

            var filtre = new Uygulama.Dto.Rapor.SatisRaporFiltreDto { PresetKey = "Büyük Defter" };
            
            var bytes = _excelServisi.ExportToExcel("Büyük Defter Raporu", filtre, HesapOzetleri.ToList(), kolonlar);

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Dosyası|*.xlsx",
                FileName = $"BuyukDefter_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
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
                { "DevirBorc", "Devir Borç" },
                { "DevirAlacak", "Devir Alacak" },
                { "DonemBorc", "Dönem Borç" },
                { "DonemAlacak", "Dönem Alacak" },
                { "ToplamBorc", "Toplam Borç" },
                { "ToplamAlacak", "Toplam Alacak" },
                { "Bakiye", "Bakiye" },
                { "BakiyeYonu", "Bakiye Yönü" },
                { "HareketSayisi", "Hareket Sayısı" }
            };

            var filtre = new Uygulama.Dto.Rapor.SatisRaporFiltreDto { PresetKey = "Büyük Defter" };
            
            var bytes = _pdfServisi.ExportToPdf("Büyük Defter Raporu", filtre, HesapOzetleri.ToList(), kolonlar);

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Dosyası|*.pdf",
                FileName = $"BuyukDefter_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
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
