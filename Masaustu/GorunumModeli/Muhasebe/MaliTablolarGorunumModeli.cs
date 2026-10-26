using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Uygulama.Dto.Rapor;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class MaliTablolarGorunumModeli : TemelGorunumModeli
{
    private readonly IMaliTabloServisi _servis;
    private readonly IExcelExportServisi _excel;
    private readonly IPdfRaporServisi _pdf;
    private readonly IAktifFirmaBaglami? _firma;
    private DateTime _bilancoTarihi = DateTime.Today;
    private DateTime _gelirBaslangic = new(DateTime.Today.Year, 1, 1);
    private DateTime _gelirBitis = DateTime.Today;
    private int _seciliSekme;
    private MaliTabloDto? _bilanco;
    private MaliTabloDto? _gelirTablosu;
    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;

    public MaliTablolarGorunumModeli(IMaliTabloServisi servis, IExcelExportServisi excel, IPdfRaporServisi pdf, IAktifFirmaBaglami? firma = null)
    {
        _servis = servis;
        _excel = excel;
        _pdf = pdf;
        _firma = firma;
        YenileKomutu = new KomutSarici(async () => await YukleAsync());
        ExcelKomutu = new KomutSarici(async () => await CiktiAsync("excel"));
        PdfKomutu = new KomutSarici(async () => await CiktiAsync("pdf"));
        YazdirKomutu = new KomutSarici(async () => await CiktiAsync("yazdir"));
        _ = YukleAsync();
    }

    public ICommand YenileKomutu { get; }
    public ICommand ExcelKomutu { get; }
    public ICommand PdfKomutu { get; }
    public ICommand YazdirKomutu { get; }

    public ObservableCollection<MaliTabloSatiriDto> BilancoSatirlari { get; } = new();
    public ObservableCollection<MaliTabloSatiriDto> GelirSatirlari { get; } = new();
    public ObservableCollection<string> Uyarilar { get; } = new();
    public bool UyariVar => Uyarilar.Count > 0;

    public DateTime BilancoTarihi { get => _bilancoTarihi; set { if (OzellikAyarla(ref _bilancoTarihi, value)) _ = YukleAsync(); } }
    public DateTime GelirBaslangic { get => _gelirBaslangic; set { if (OzellikAyarla(ref _gelirBaslangic, value)) _ = YukleAsync(); } }
    public DateTime GelirBitis { get => _gelirBitis; set { if (OzellikAyarla(ref _gelirBitis, value)) _ = YukleAsync(); } }

    public int SeciliSekme
    {
        get => _seciliSekme;
        set { if (OzellikAyarla(ref _seciliSekme, value)) UyarilariGoster(); }
    }

    public string BilancoCariBaslik => _bilanco?.DonemMetni ?? "";
    public string BilancoOncekiBaslik => _bilanco?.OncekiDonemMetni ?? "";
    public string GelirCariBaslik => _gelirTablosu?.DonemMetni ?? "";
    public string GelirOncekiBaslik => _gelirTablosu?.OncekiDonemMetni ?? "";

    public bool Yukleniyor { get => _yukleniyor; private set => OzellikAyarla(ref _yukleniyor, value); }
    public string DurumMesaji { get => _durumMesaji; private set => OzellikAyarla(ref _durumMesaji, value); }

    private async Task YukleAsync()
    {
        if (GelirBitis < GelirBaslangic)
        {
            DurumMesaji = "Gelir tablosu bitiş tarihi başlangıçtan önce olamaz.";
            return;
        }
        try
        {
            Yukleniyor = true;
            DurumMesaji = "Hesaplanıyor...";
            _bilanco = await _servis.BilancoAsync(BilancoTarihi);
            _gelirTablosu = await _servis.GelirTablosuAsync(GelirBaslangic, GelirBitis);
            Doldur(BilancoSatirlari, _bilanco);
            Doldur(GelirSatirlari, _gelirTablosu);
            foreach (var p in new[] { nameof(BilancoCariBaslik), nameof(BilancoOncekiBaslik), nameof(GelirCariBaslik), nameof(GelirOncekiBaslik) }) BildirimGonder(p);
            UyarilariGoster();
            DurumMesaji = string.Empty;
        }
        catch (Exception ex)
        {
            DurumMesaji = "Mali tablolar hesaplanamadı: " + ex.Message;
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private static void Doldur(ObservableCollection<MaliTabloSatiriDto> hedef, MaliTabloDto tablo)
    {
        hedef.Clear();
        foreach (var s in tablo.Satirlar) hedef.Add(s);
    }

    private void UyarilariGoster()
    {
        Uyarilar.Clear();
        foreach (var u in (SeciliSekme == 0 ? _bilanco : _gelirTablosu)?.Uyarilar ?? []) Uyarilar.Add(u);
        BildirimGonder(nameof(UyariVar));
    }

    private RaporTablosu? Tablo()
    {
        var t = SeciliSekme == 0 ? _bilanco : _gelirTablosu;
        if (t == null) return null;
        var tablo = new RaporTablosu
        {
            Baslik = t.Baslik,
            AltBaslik = string.Join(" | ", new[] { _firma?.SirketAdi, SeciliSekme == 0 ? $"Tarih: {t.DonemMetni}" : $"Dönem: {t.DonemMetni}" }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Kolonlar = [new("Hesap / Kalem"), new(t.DonemMetni, true), new(t.OncekiDonemMetni, true)]
        };
        foreach (var s in t.Satirlar)
        {
            var bos = s.Seviye == 0 && s.Tutar == 0 && s.OncekiTutar == 0;
            tablo.Satirlar.Add([new RaporHucresi(s.Baslik), bos ? new RaporHucresi("") : new RaporHucresi(s.Tutar.ToString("N2"), s.Tutar), bos ? new RaporHucresi("") : new RaporHucresi(s.OncekiTutar.ToString("N2"), s.OncekiTutar)]);
        }
        return tablo;
    }

    private async Task CiktiAsync(string tur)
    {
        try
        {
            var tablo = Tablo();
            if (tablo == null) return;
            var ad = (SeciliSekme == 0 ? "Bilanco" : "Gelir_Tablosu") + $"_{DateTime.Now:yyyyMMdd_HHmm}";
            if (tur == "yazdir")
            {
                var yol = Path.Combine(Path.GetTempPath(), ad + ".html");
                await File.WriteAllTextAsync(yol, _pdf.HtmlOlustur(tablo), System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(yol) { UseShellExecute = true });
                return;
            }
            var excel = tur == "excel";
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = excel ? "Excel Dosyası (*.xlsx)|*.xlsx" : "PDF Dosyası (*.pdf)|*.pdf", FileName = ad + (excel ? ".xlsx" : ".pdf") };
            if (dialog.ShowDialog() != true) return;
            await File.WriteAllBytesAsync(dialog.FileName, excel ? _excel.ExcelOlustur(tablo) : _pdf.PdfOlustur(tablo));
            if (MessageBox.Show($"Dosya kaydedildi:\n{dialog.FileName}\n\nŞimdi açılsın mı?", "Mali Tablolar", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DurumMesaji = "Çıktı alınamadı: " + ex.Message;
        }
    }
}
