using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EtkiAlani.Yardimcilar;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class DemirbasGorunumModeli : TemelGorunumModeli
{
    private readonly ISabitKiymetServisi _servis;
    private SabitKiymetListeDto? _secili;
    private bool _eldenCikanlar;
    private int _yil = DateTime.Today.Month == 12 ? DateTime.Today.Year : DateTime.Today.Year - 1;
    private AmortismanYilOnizlemeDto? _onizleme;
    private string _durumMesaji = string.Empty;
    private bool _mesgul;

    public DemirbasGorunumModeli(ISabitKiymetServisi servis)
    {
        _servis = servis;
        YenileKomutu = new KomutSarici(async () => await YukleAsync());
        YeniKomutu = new KomutSarici(async () => await FormAcAsync(null));
        DuzenleKomutu = new KomutSarici(async () => await FormAcAsync(Secili), () => Secili != null);
        SilKomutu = new KomutSarici(async () => await SilAsync(), () => Secili != null);
        EldenCikarKomutu = new KomutSarici(async () => await EldenCikarAsync(), () => Secili != null && !Secili.Durum.StartsWith("Elden"));
        AyirKomutu = new KomutSarici(async () => await AyirAsync(), () => !_mesgul && Onizleme?.AyrilabilirMi == true);
        GeriAlKomutu = new KomutSarici(async () => await GeriAlAsync(), () => !_mesgul && Onizleme?.AyrilmisToplam > 0);
        _ = YukleAsync();
    }

    public ObservableCollection<SabitKiymetListeDto> Demirbaslar { get; } = new();
    public ObservableCollection<AmortismanSatiri> Plan { get; } = new();
    public IReadOnlyList<int> Yillar { get; } = Enumerable.Range(DateTime.Today.Year - 8, 9).Reverse().ToList();

    public ICommand YenileKomutu { get; }
    public ICommand YeniKomutu { get; }
    public ICommand DuzenleKomutu { get; }
    public ICommand SilKomutu { get; }
    public ICommand EldenCikarKomutu { get; }
    public ICommand AyirKomutu { get; }
    public ICommand GeriAlKomutu { get; }

    public SabitKiymetListeDto? Secili
    {
        get => _secili;
        set { if (OzellikAyarla(ref _secili, value)) { _ = PlanYukleAsync(); CommandManager.InvalidateRequerySuggested(); } }
    }

    public bool EldenCikanlariGoster
    {
        get => _eldenCikanlar;
        set { if (OzellikAyarla(ref _eldenCikanlar, value)) _ = YukleAsync(); }
    }

    public int Yil
    {
        get => _yil;
        set { if (OzellikAyarla(ref _yil, value)) _ = OnizleAsync(); }
    }

    public AmortismanYilOnizlemeDto? Onizleme
    {
        get => _onizleme;
        private set { OzellikAyarla(ref _onizleme, value); BildirimGonder(nameof(OnizlemeMetni)); CommandManager.InvalidateRequerySuggested(); }
    }

    public string OnizlemeMetni => Onizleme == null ? "" :
        Onizleme.Engeller.Count > 0 ? string.Join(" ", Onizleme.Engeller) :
        Onizleme.Kalemler.Count > 0 ? $"{Onizleme.Kalemler.Count} demirbaş için {Onizleme.Toplam:N2} TL amortisman ayrılacak (31.12.{Yil} tarihli fiş)." +
            (Onizleme.AyrilmisToplam > 0 ? $" Bu yıl daha önce {Onizleme.AyrilmisToplam:N2} TL ayrıldı." : "") :
        Onizleme.AyrilmisToplam > 0 ? $"{Yil} yılı amortismanı ayrılmış: {Onizleme.AyrilmisToplam:N2} TL." : $"{Yil} yılı için ayrılacak amortisman yok.";

    public string DurumMesaji { get => _durumMesaji; private set => OzellikAyarla(ref _durumMesaji, value); }

    private async Task YukleAsync()
    {
        try
        {
            var seciliId = Secili?.Id;
            Demirbaslar.Clear();
            foreach (var d in await _servis.ListeAsync(EldenCikanlariGoster)) Demirbaslar.Add(d);
            Secili = Demirbaslar.FirstOrDefault(d => d.Id == seciliId) ?? Demirbaslar.FirstOrDefault();
            await OnizleAsync();
        }
        catch (Exception ex)
        {
            DurumMesaji = "Demirbaşlar yüklenemedi: " + ex.Message;
        }
    }

    private async Task PlanYukleAsync()
    {
        Plan.Clear();
        if (Secili == null) return;
        try { foreach (var s in await _servis.PlanAsync(Secili.Id)) Plan.Add(s); }
        catch (Exception ex) { DurumMesaji = ex.Message; }
    }

    private async Task OnizleAsync()
    {
        try { Onizleme = await _servis.YilOnizleAsync(Yil); }
        catch (Exception ex) { DurumMesaji = ex.Message; }
    }

    private async Task FormAcAsync(SabitKiymetListeDto? satir)
    {
        try
        {
            var kart = satir == null ? null : await _servis.GetirAsync(satir.Id);
            var pencere = new Masaustu.Gorunumler.Sayfa.Muhasebe.DemirbasPenceresi(new DemirbasFormuModeli(_servis, kart)) { Owner = Application.Current.MainWindow };
            if (pencere.ShowDialog() == true)
            {
                DurumMesaji = "Demirbaş kaydedildi.";
                await YukleAsync();
            }
        }
        catch (Exception ex)
        {
            DurumMesaji = ex.Message;
        }
    }

    private async Task SilAsync()
    {
        if (Secili == null) return;
        if (MessageBox.Show($"{Secili.Kod} {Secili.Ad} silinsin mi?", "Demirbaş Sil", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { await _servis.SilAsync(Secili.Id); DurumMesaji = "Demirbaş silindi."; await YukleAsync(); }
        catch (Exception ex) { DurumMesaji = ex.Message; }
    }

    private async Task AyirAsync()
    {
        if (Onizleme == null) return;
        if (MessageBox.Show(OnizlemeMetni + "\n\nDevam edilsin mi?", "Amortisman Ayır", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            _mesgul = true;
            var fisNo = await _servis.AmortismanAyirAsync(Yil);
            DurumMesaji = $"{Yil} yılı amortismanı ayrıldı. Fiş: {fisNo}";
        }
        catch (Exception ex) { DurumMesaji = "Amortisman ayrılamadı: " + ex.Message; }
        finally { _mesgul = false; }
        await YukleAsync();
    }

    private async Task GeriAlAsync()
    {
        if (MessageBox.Show($"{Yil} yılı amortisman fişleri aynı tarihli ters fişlerle geri alınacak. Devam edilsin mi?", "Amortismanı Geri Al", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            _mesgul = true;
            await _servis.AmortismanGeriAlAsync(Yil);
            DurumMesaji = $"{Yil} yılı amortismanı geri alındı.";
        }
        catch (Exception ex) { DurumMesaji = "Geri alınamadı: " + ex.Message; }
        finally { _mesgul = false; }
        await YukleAsync();
    }

    private async Task EldenCikarAsync()
    {
        if (Secili == null) return;
        var tarih = new DatePicker { SelectedDate = DateTime.Today, Margin = new Thickness(0, 4, 0, 10) };
        var aciklama = new TextBox { Height = 26 };
        var tamam = new Button { Content = "Elden Çıkar", IsDefault = true, Width = 120, Margin = new Thickness(0, 12, 8, 0) };
        var iptal = new Button { Content = "Vazgeç", IsCancel = true, Width = 100, Margin = new Thickness(0, 12, 0, 0) };
        var pencere = new Window
        {
            Title = $"Elden Çıkar: {Secili.Kod} {Secili.Ad}", Width = 440, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    new TextBlock { TextWrapping = TextWrapping.Wrap, Text = $"Net defter değeri ({Secili.NetDeger:N2} TL) 689 hesabına gider, birikmiş amortisman ve maliyet kapatılır. Satış yapıldıysa satış faturasını ayrıca kesin.", Margin = new Thickness(0, 0, 0, 10) },
                    new TextBlock { Text = "Çıkış tarihi" }, tarih,
                    new TextBlock { Text = "Açıklama (satış, hurda, kayıp...)" }, aciklama,
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { tamam, iptal } }
                }
            }
        };
        tamam.Click += (_, _) => pencere.DialogResult = true;
        if (pencere.ShowDialog() != true || tarih.SelectedDate == null) return;
        try
        {
            var fisNo = await _servis.EldenCikarAsync(Secili.Id, tarih.SelectedDate.Value, aciklama.Text);
            DurumMesaji = $"{Secili.Kod} elden çıkarıldı. Fiş: {fisNo}";
            await YukleAsync();
        }
        catch (Exception ex) { DurumMesaji = "Elden çıkarılamadı: " + ex.Message; }
    }
}
