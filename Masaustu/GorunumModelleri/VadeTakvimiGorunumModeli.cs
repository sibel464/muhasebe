using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;
using Uygulama.Dto.Rapor;

namespace Masaustu.GorunumModelleri;

// Vade takvimi: seçilen gün sayısı içinde vadesi gelen (ve geçmiş) tahsilat ve ödemeler.
public class VadeTakvimiGorunumModeli : TemelGorunumModeli
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private readonly IRaporMerkeziServisi _servis;
    private List<VadeTakvimiKalemiDto> _tum = new();
    private int _gun = 30;
    private string _yon = "Tümü";
    private string _ozet = string.Empty, _durum = string.Empty;

    public VadeTakvimiGorunumModeli(IRaporMerkeziServisi servis)
    {
        _servis = servis;
        YenileKomutu = new KomutSarici(async _ => await YukleAsync());
        ExcelKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.Aktar(Kalemler.Select(k => new
        {
            k.Vade, k.Yon, k.Kaynak, k.BelgeNo, k.CariKod, k.CariUnvan, k.Tutar, k.GecikmeGun, k.Aciklama
        }).ToList(), $"VadeTakvimi_{DateTime.Today:yyyyMMdd}"), _ => Kalemler.Count > 0);
    }

    public static IReadOnlyList<SecenekOgesi<int>> Gunler { get; } = [new("7 gün", 7), new("15 gün", 15), new("30 gün", 30), new("60 gün", 60), new("90 gün", 90)];
    public static IReadOnlyList<string> Yonler { get; } = ["Tümü", "Tahsilat", "Ödeme"];

    public ObservableCollection<VadeTakvimiKalemiDto> Kalemler { get; } = new();
    public int Gun { get => _gun; set { if (OzellikAyarla(ref _gun, value)) _ = YukleAsync(); } }
    public string Yon { get => _yon; set { if (OzellikAyarla(ref _yon, value)) Suz(); } }
    public string Ozet { get => _ozet; private set => OzellikAyarla(ref _ozet, value); }
    public string Durum { get => _durum; private set => OzellikAyarla(ref _durum, value); }
    public ICommand YenileKomutu { get; }
    public ICommand ExcelKomutu { get; }

    public async Task YukleAsync()
    {
        try
        {
            Durum = "Hesaplanıyor...";
            _tum = await _servis.GetVadeTakvimiAsync(new SatisRaporFiltreDto { BitisTarihi = DateTime.Today.AddDays(Gun) });
            Durum = string.Empty;
        }
        catch (YetkisizErisimException)
        {
            _tum = new();
            Durum = "Vade takvimini görmek için rapor yetkiniz yok.";
        }
        catch (Exception ex)
        {
            _tum = new();
            Durum = "Liste alınamadı: " + ex.Message;
        }
        Suz();
    }

    private void Suz()
    {
        Kalemler.Clear();
        foreach (var k in _tum.Where(k => Yon == "Tümü" || k.Yon == Yon)) Kalemler.Add(k);
        decimal T(bool tahsilat, bool gecmis) => _tum.Where(k => k.Tahsilat == tahsilat && (k.GecikmeGun > 0) == gecmis).Sum(k => k.Tutar);
        var tahsilat = T(true, false) + T(true, true);
        var odeme = T(false, false) + T(false, true);
        string Gecmis(decimal t) => t > 0 ? $" (vadesi geçmiş {t.ToString("N2", Tr)})" : "";
        Ozet = $"Tahsil edilecek: {tahsilat.ToString("N2", Tr)} TL{Gecmis(T(true, true))}   ·   " +
               $"Ödenecek: {odeme.ToString("N2", Tr)} TL{Gecmis(T(false, true))}   ·   Fark: {(tahsilat - odeme).ToString("N2", Tr)} TL";
        CommandManager.InvalidateRequerySuggested();
    }
}
