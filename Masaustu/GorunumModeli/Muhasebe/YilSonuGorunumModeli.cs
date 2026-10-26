using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class YilSonuGorunumModeli : TemelGorunumModeli
{
    private readonly IYilSonuServisi _servis;
    private int _yil = DateTime.Today.Month == 12 && DateTime.Today.Day == 31 ? DateTime.Today.Year : DateTime.Today.Year - 1;
    private YilSonuOnizlemeDto? _onizleme;
    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;

    public YilSonuGorunumModeli(IYilSonuServisi servis)
    {
        _servis = servis;
        YenileKomutu = new KomutSarici(async () => await YukleAsync());
        KapatKomutu = new KomutSarici(async () => await KapatAsync(), () => !Yukleniyor && Onizleme?.KapatilabilirMi == true);
        GeriAlKomutu = new KomutSarici(async () => await GeriAlAsync(), () => !Yukleniyor && Onizleme?.Kapatildi == true);
        _ = YukleAsync();
    }

    public ICommand YenileKomutu { get; }
    public ICommand KapatKomutu { get; }
    public ICommand GeriAlKomutu { get; }

    public IReadOnlyList<int> Yillar { get; } = Enumerable.Range(DateTime.Today.Year - 6, 7).Reverse().ToList();

    public int Yil
    {
        get => _yil;
        set { if (OzellikAyarla(ref _yil, value)) _ = YukleAsync(); }
    }

    public YilSonuOnizlemeDto? Onizleme
    {
        get => _onizleme;
        private set
        {
            OzellikAyarla(ref _onizleme, value);
            Engeller.Clear();
            Uyarilar.Clear();
            foreach (var e in value?.Engeller ?? []) Engeller.Add(e);
            foreach (var u in value?.Uyarilar ?? []) Uyarilar.Add(u);
            foreach (var p in new[] { nameof(DurumBasligi), nameof(KarMetni), nameof(EngelVar), nameof(UyariVar) }) BildirimGonder(p);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ObservableCollection<string> Engeller { get; } = new();
    public ObservableCollection<string> Uyarilar { get; } = new();
    public bool EngelVar => Engeller.Count > 0;
    public bool UyariVar => Uyarilar.Count > 0;

    public string DurumBasligi => Onizleme == null ? "" :
        Onizleme.Kapatildi ? $"{Yil} yılı kapatılmış. Açılış fişi {Yil + 1} yılına devredildi." :
        Onizleme.KapatilabilirMi ? $"{Yil} yılı kapanışa hazır." : $"{Yil} yılı şu an kapatılamıyor; aşağıdaki maddeleri giderin.";

    public string KarMetni => Onizleme == null ? "" :
        Onizleme.NetKar >= 0 ? $"{Onizleme.NetKar:N2} TL net kâr → 590 Dönem Net Kârı" : $"{-Onizleme.NetKar:N2} TL net zarar → 591 Dönem Net Zararı";

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

    private async Task YukleAsync()
    {
        try
        {
            Yukleniyor = true;
            DurumMesaji = $"{Yil} yılı bakiyeleri hesaplanıyor...";
            Onizleme = await _servis.OnizleAsync(Yil);
            DurumMesaji = string.Empty;
        }
        catch (Exception ex)
        {
            DurumMesaji = "Önizleme alınamadı: " + ex.Message;
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task KapatAsync()
    {
        if (Onizleme == null) return;
        var onay = MessageBox.Show(
            $"{Yil} yılı kapatılacak:\n\n• Gider hesapları yansıtılacak\n• Gelir tablosu hesapları 690'a kapatılacak\n• {KarMetni}\n• 31.12.{Yil} kapanış ve 01.01.{Yil + 1} açılış fişleri kesilecek\n\nKapanıştan önce yedek almanız önerilir. Devam edilsin mi?",
            "Yıl Sonu Kapanışı", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (onay != MessageBoxResult.Yes) return;
        try
        {
            Yukleniyor = true;
            var sonuc = await _servis.KapatAsync(Yil);
            DurumMesaji = $"{Yil} yılı kapatıldı. Kesilen fişler: {string.Join(", ", sonuc.FisNolari)}. {Yil} dönemini Firma İşlemleri'nden kilitlemeniz önerilir.";
        }
        catch (Exception ex)
        {
            DurumMesaji = "Kapanış yapılamadı: " + ex.Message;
        }
        finally
        {
            Yukleniyor = false;
        }
        await YukleAsync(false);
    }

    private async Task YukleAsync(bool mesajiTemizle)
    {
        var mesaj = DurumMesaji;
        await YukleAsync();
        if (!mesajiTemizle && string.IsNullOrEmpty(DurumMesaji)) DurumMesaji = mesaj;
    }

    private async Task GeriAlAsync()
    {
        if (MessageBox.Show($"{Yil} yılının kapanış fişleri aynı tarihli ters fişlerle geri alınacak. Devam edilsin mi?", "Kapanışı Geri Al",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            Yukleniyor = true;
            await _servis.GeriAlAsync(Yil);
            DurumMesaji = $"{Yil} yılı kapanışı geri alındı.";
        }
        catch (Exception ex)
        {
            DurumMesaji = "Geri alınamadı: " + ex.Message;
        }
        finally
        {
            Yukleniyor = false;
        }
        await YukleAsync(false);
    }
}
