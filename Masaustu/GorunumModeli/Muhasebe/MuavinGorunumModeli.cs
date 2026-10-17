using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class MuavinGorunumModeli : TemelGorunumModeli
{
    private readonly IMuhasebeRaporServisi _raporServisi;
    private readonly IHesapPlaniServisi _hesapServisi;
    private readonly IExcelExportServisi _excelServisi;
    private readonly IPdfRaporServisi _pdfServisi;

    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;

    private HesapPlaniDto? _seciliHesap;
    private DateTime? _baslangicTarihi = DateTime.Today.AddMonths(-1);
    private DateTime? _bitisTarihi = DateTime.Today;
    private MuavinRaporDto? _muavin;

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

    public ObservableCollection<HesapPlaniDto> Hesaplar { get; } = new();

    public HesapPlaniDto? SeciliHesap
    {
        get => _seciliHesap;
        set { if (OzellikAyarla(ref _seciliHesap, value)) _ = SorgulaAsync(); }
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

    public MuavinRaporDto? Muavin
    {
        get => _muavin;
        private set => OzellikAyarla(ref _muavin, value);
    }

    public ICommand YenileKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }
    public ICommand PdfAktarKomutu { get; }

    public MuavinGorunumModeli(
        IMuhasebeRaporServisi raporServisi,
        IHesapPlaniServisi hesapServisi,
        IExcelExportServisi excelServisi,
        IPdfRaporServisi pdfServisi)
    {
        _raporServisi = raporServisi;
        _hesapServisi = hesapServisi;
        _excelServisi = excelServisi;
        _pdfServisi = pdfServisi;

        YenileKomutu = new KomutSarici(async () => await SorgulaAsync());
        ExcelAktarKomutu = new KomutSarici(async () => await ExcelAktarAsync());
        PdfAktarKomutu = new KomutSarici(async () => await PdfAktarAsync());

        _ = HesaplariYukleAsync();
    }

    private async Task HesaplariYukleAsync()
    {
        var liste = await _hesapServisi.HesapPlaniGetirAsync("", true);
        Hesaplar.Clear();
        foreach (var h in liste)
        {
            Hesaplar.Add(h);
        }
        SeciliHesap = Hesaplar.FirstOrDefault(h => h.HesapKodu == "100.01" || h.HesapKodu == "100") ?? Hesaplar.FirstOrDefault();
    }

    public async Task SorgulaAsync()
    {
        if (SeciliHesap == null) return;
        try
        {
            Yukleniyor = true;
            DurumMesaji = $"[{SeciliHesap.HesapKodu}] Muavin Defter yükleniyor...";
            Muavin = await _raporServisi.MuavinAlAsync(SeciliHesap.Id, Guid.Empty, BaslangicTarihi, BitisTarihi);
            DurumMesaji = $"Muavin Defter: {Muavin.Satirlar.Count} hareket listelendi. Son Bakiye: {Muavin.SonBakiye:N2} TL ({Muavin.SonBakiyeYonu})";
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
                { "BelgeTipi", "Belge Tipi" },
                { "BelgeNo", "Belge No" },
                { "Aciklama", "Açıklama" },
                { "Borc", "Borç" },
                { "Alacak", "Alacak" },
                { "YuruyenBakiye", "Bakiye" },
                { "BakiyeYonu", "Bakiye Yönü" }
            };

            var filtre = new Uygulama.Dto.Rapor.SatisRaporFiltreDto { PresetKey = "Muavin" };
            
            var bytes = _excelServisi.ExportToExcel("Muavin Raporu", filtre, Muavin?.Satirlar ?? new System.Collections.Generic.List<MuavinSatirDto>(), kolonlar);

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Dosyası|*.xlsx",
                FileName = $"Muavin_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
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
                { "BelgeTipi", "Belge Tipi" },
                { "BelgeNo", "Belge No" },
                { "Aciklama", "Açıklama" },
                { "Borc", "Borç" },
                { "Alacak", "Alacak" },
                { "YuruyenBakiye", "Bakiye" },
                { "BakiyeYonu", "Bakiye Yönü" }
            };

            var filtre = new Uygulama.Dto.Rapor.SatisRaporFiltreDto { PresetKey = "Muavin" };
            
            var bytes = _pdfServisi.ExportToPdf("Muavin Raporu", filtre, Muavin?.Satirlar ?? new System.Collections.Generic.List<MuavinSatirDto>(), kolonlar);

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Dosyası|*.pdf",
                FileName = $"Muavin_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
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
