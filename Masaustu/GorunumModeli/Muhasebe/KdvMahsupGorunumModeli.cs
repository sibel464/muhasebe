using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

// Ay sonu KDV mahsubu: yılın 12 ayı listelenir; seçilen ayın mahsup fişi önizlenir, kesilir ya da geri alınır.
public class KdvMahsupGorunumModeli : TemelGorunumModeli
{
    private readonly IKdvMahsupServisi _servis;
    private int _yil;
    private KdvMahsupAyDto? _seciliAy;
    private KdvMahsupOnizlemeDto? _onizleme;
    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;

    public KdvMahsupGorunumModeli(IKdvMahsupServisi servis)
    {
        _servis = servis;
        var gecenAy = DateTime.Today.AddMonths(-1);
        _yil = gecenAy.Year;
        YenileKomutu = new KomutSarici(async () => await YukleAsync(Onizleme?.Ay ?? gecenAy.Month));
        MahsupKomutu = new KomutSarici(async () => await MahsupEtAsync(), () => !Yukleniyor && Onizleme?.MahsupEdilebilirMi == true);
        GeriAlKomutu = new KomutSarici(async () => await GeriAlAsync(), () => !Yukleniyor && Onizleme?.Yapildi == true);
        _ = YukleAsync(gecenAy.Month);
    }

    public ICommand YenileKomutu { get; }
    public ICommand MahsupKomutu { get; }
    public ICommand GeriAlKomutu { get; }

    public IReadOnlyList<int> Yillar { get; } = Enumerable.Range(DateTime.Today.Year - 6, 7).Reverse().ToList();
    public ObservableCollection<KdvMahsupAyDto> Aylar { get; } = new();
    public ObservableCollection<string> Engeller { get; } = new();
    public ObservableCollection<string> Uyarilar { get; } = new();
    public bool EngelVar => Engeller.Count > 0;
    public bool UyariVar => Uyarilar.Count > 0;

    public int Yil
    {
        get => _yil;
        set { if (OzellikAyarla(ref _yil, value)) _ = YukleAsync(Onizleme?.Ay ?? 1); }
    }

    public KdvMahsupAyDto? SeciliAy
    {
        get => _seciliAy;
        set { if (OzellikAyarla(ref _seciliAy, value) && value != null && value.Ay != Onizleme?.Ay) _ = OnizleAsync(value.Ay); }
    }

    public KdvMahsupOnizlemeDto? Onizleme
    {
        get => _onizleme;
        private set
        {
            OzellikAyarla(ref _onizleme, value);
            Engeller.Clear();
            Uyarilar.Clear();
            foreach (var e in value?.Engeller ?? []) Engeller.Add(e);
            foreach (var u in value?.Uyarilar ?? []) Uyarilar.Add(u);
            foreach (var p in new[] { nameof(DurumBasligi), nameof(SonucMetni), nameof(EngelVar), nameof(UyariVar) }) BildirimGonder(p);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private string AyMetni => Onizleme == null ? "" : $"{Onizleme.FisTarihi:MMMM yyyy}";

    public string DurumBasligi => Onizleme == null ? "" :
        Onizleme.Yapildi ? $"{AyMetni} KDV mahsubu yapılmış ({Onizleme.FisNo})." :
        Onizleme.MahsupEdilebilirMi ? $"{AyMetni} KDV mahsubuna hazır. Fiş {Onizleme.FisTarihi:dd.MM.yyyy} tarihiyle kesilecek." :
        $"{AyMetni} şu an mahsup edilemiyor; aşağıdaki maddelere bakın.";

    public string SonucMetni => Onizleme == null ? "" :
        Onizleme.Yapildi ? "Fiş kesildi; tutarlar defterde." :
        Onizleme.OdenecekKdv > 0 ? $"{Onizleme.OdenecekKdv:N2} TL ödenecek KDV → 360" :
        Onizleme.SonrakiDonemeDevreden > 0 ? $"Ödenecek KDV yok; {Onizleme.SonrakiDonemeDevreden:N2} TL sonraki aya devreder → 190" : "Ödenecek ve devreden KDV yok";

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

    private async Task YukleAsync(int ay)
    {
        try
        {
            Yukleniyor = true;
            var aylar = await _servis.YilAsync(Yil);
            Aylar.Clear();
            foreach (var a in aylar) Aylar.Add(a);
            _seciliAy = Aylar.FirstOrDefault(a => a.Ay == ay);
            BildirimGonder(nameof(SeciliAy));
        }
        catch (Exception ex)
        {
            DurumMesaji = "Liste alınamadı: " + ex.Message;
        }
        finally
        {
            Yukleniyor = false;
        }
        await OnizleAsync(ay);
    }

    private async Task OnizleAsync(int ay)
    {
        try
        {
            Yukleniyor = true;
            Onizleme = await _servis.OnizleAsync(Yil, ay);
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

    private async Task MahsupEtAsync()
    {
        if (Onizleme == null) return;
        var ay = Onizleme.Ay;
        if (MessageBox.Show($"{AyMetni} KDV mahsup fişi {Onizleme.FisTarihi:dd.MM.yyyy} tarihiyle kesilecek:\n\n• Hesaplanan KDV (391): {Onizleme.HesaplananKdv:N2} TL\n• İndirilecek KDV (191): {Onizleme.IndirilecekKdv:N2} TL\n• Önceki aydan devreden (190): {Onizleme.OncekiDevreden:N2} TL\n• {SonucMetni}\n\nDevam edilsin mi?",
                "KDV Mahsubu", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        string mesaj;
        try
        {
            Yukleniyor = true;
            var fisNo = await _servis.MahsupEtAsync(Yil, ay);
            mesaj = $"{AyMetni} KDV mahsup fişi kesildi: {fisNo}.";
        }
        catch (Exception ex)
        {
            mesaj = "Mahsup yapılamadı: " + ex.Message;
        }
        finally
        {
            Yukleniyor = false;
        }
        await YukleAsync(ay);
        DurumMesaji = mesaj;
    }

    private async Task GeriAlAsync()
    {
        if (Onizleme == null) return;
        var ay = Onizleme.Ay;
        if (MessageBox.Show($"{AyMetni} KDV mahsup fişi aynı tarihli ters fişle geri alınacak. Devam edilsin mi?", "KDV Mahsubunu Geri Al",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        string mesaj;
        try
        {
            Yukleniyor = true;
            await _servis.GeriAlAsync(Yil, ay);
            mesaj = $"{AyMetni} KDV mahsubu geri alındı.";
        }
        catch (Exception ex)
        {
            mesaj = "Geri alınamadı: " + ex.Message;
        }
        finally
        {
            Yukleniyor = false;
        }
        await YukleAsync(ay);
        DurumMesaji = mesaj;
    }
}
