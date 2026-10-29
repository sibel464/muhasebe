using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

// Maaş tahakkuku tablosunda düzenlenen satır; brüt ve maliyet sütunları tutar değiştikçe yenilenir.
public class TahakkukSatiriModeli : TemelGorunumModeli
{
    private bool _secili = true;
    private decimal _net, _sgkIsci, _issizlikIsci, _gv, _dv, _sgkIsveren, _issizlikIsveren, _avans;
    private readonly Action _degisti;

    public TahakkukSatiriModeli(MaasTahakkukSatiriDto d, Action degisti)
    {
        Kaynak = d;
        _degisti = degisti;
        (_net, _sgkIsci, _issizlikIsci, _gv, _dv, _sgkIsveren, _issizlikIsveren, _avans) =
            (d.NetUcret, d.SgkIsciPayi, d.IssizlikIsciPayi, d.GelirVergisi, d.DamgaVergisi, d.SgkIsverenPayi, d.IssizlikIsverenPayi, d.AvansKesintisi);
    }

    public MaasTahakkukSatiriDto Kaynak { get; }
    public string AdSoyad => Kaynak.AdSoyad;
    public decimal AvansBakiye => Kaynak.AvansBakiye;
    public bool Secili { get => _secili; set { if (OzellikAyarla(ref _secili, value)) _degisti(); } }
    public decimal NetUcret { get => _net; set => Ayarla(ref _net, value); }
    public decimal SgkIsciPayi { get => _sgkIsci; set => Ayarla(ref _sgkIsci, value); }
    public decimal IssizlikIsciPayi { get => _issizlikIsci; set => Ayarla(ref _issizlikIsci, value); }
    public decimal GelirVergisi { get => _gv; set => Ayarla(ref _gv, value); }
    public decimal DamgaVergisi { get => _dv; set => Ayarla(ref _dv, value); }
    public decimal SgkIsverenPayi { get => _sgkIsveren; set => Ayarla(ref _sgkIsveren, value); }
    public decimal IssizlikIsverenPayi { get => _issizlikIsveren; set => Ayarla(ref _issizlikIsveren, value); }
    public decimal AvansKesintisi { get => _avans; set => Ayarla(ref _avans, value); }
    public decimal BrutUcret => NetUcret + SgkIsciPayi + IssizlikIsciPayi + GelirVergisi + DamgaVergisi;
    public decimal IsverenMaliyeti => BrutUcret + SgkIsverenPayi + IssizlikIsverenPayi;
    public decimal OdenecekNet => NetUcret - AvansKesintisi;

    private void Ayarla(ref decimal alan, decimal deger)
    {
        if (!OzellikAyarla(ref alan, deger)) return;
        BildirimGonder(nameof(BrutUcret));
        BildirimGonder(nameof(IsverenMaliyeti));
        BildirimGonder(nameof(OdenecekNet));
        _degisti();
    }

    public MaasTahakkukSatiriDto Dto() => new()
    {
        PersonelId = Kaynak.PersonelId, Kod = Kaynak.Kod, AdSoyad = Kaynak.AdSoyad, GiderHesapKodu = Kaynak.GiderHesapKodu, AvansBakiye = Kaynak.AvansBakiye,
        NetUcret = NetUcret, SgkIsciPayi = SgkIsciPayi, IssizlikIsciPayi = IssizlikIsciPayi, GelirVergisi = GelirVergisi, DamgaVergisi = DamgaVergisi,
        SgkIsverenPayi = SgkIsverenPayi, IssizlikIsverenPayi = IssizlikIsverenPayi, AvansKesintisi = AvansKesintisi
    };
}

public class PersonelGorunumModeli : TemelGorunumModeli
{
    private readonly IPersonelServisi _servis;
    private PersonelListeDto? _secili;
    private PersonelEkstreSatiriDto? _seciliEkstre;
    private bool _ayrilanlar;
    private int _yil = DateTime.Today.Year;
    private int _ay = DateTime.Today.Month;
    private MaasDonemDto? _donem;
    private VergiSgkBakiyeDto? _vergiSgk;
    private OdemeHesabiDto? _odemeHesabi;
    private DateTime _odemeTarihi = DateTime.Today;
    private string _durumMesaji = string.Empty;
    private bool _mesgul;

    public PersonelGorunumModeli(IPersonelServisi servis)
    {
        _servis = servis;
        YenileKomutu = new KomutSarici(async () => await YukleAsync());
        YeniKomutu = new KomutSarici(async () => await FormAcAsync(null));
        DuzenleKomutu = new KomutSarici(async () => await FormAcAsync(Secili), () => Secili != null);
        SilKomutu = new KomutSarici(async () => await SilAsync(), () => Secili != null);
        OdemeKomutu = new KomutSarici(async () => await ParaIslemiAsync(avans: false), () => Secili?.OdenecekBakiye > 0);
        AvansKomutu = new KomutSarici(async () => await ParaIslemiAsync(avans: true), () => Secili != null && Secili.IstenCikisTarihi == null);
        IptalKomutu = new KomutSarici(async () => await IptalAsync(), () => SeciliEkstre?.IptalEdilebilir == true);
        TahakkukKomutu = new KomutSarici(async () => await TahakkukAsync(), () => !_mesgul && Donem?.Engeller.Count == 0 && Satirlar.Any(s => s.Secili));
        GeriAlKomutu = new KomutSarici(async () => await GeriAlAsync(), () => !_mesgul && Donem?.FisNolari.Count > 0);
        TopluOdemeKomutu = new KomutSarici(async () => await TopluOdemeAsync(), () => !_mesgul && OdemeHesabi != null && ToplamOdenecek > 0);
        VergiOdeKomutu = new KomutSarici(async () => await VergiSgkOdeAsync("360"), () => !_mesgul && OdemeHesabi != null && VergiSgk?.OdenecekVergi > 0);
        SgkOdeKomutu = new KomutSarici(async () => await VergiSgkOdeAsync("361"), () => !_mesgul && OdemeHesabi != null && VergiSgk?.OdenecekSgk > 0);
        _ = YukleAsync();
    }

    public ObservableCollection<PersonelListeDto> Personeller { get; } = new();
    public ObservableCollection<PersonelEkstreSatiriDto> Ekstre { get; } = new();
    public ObservableCollection<TahakkukSatiriModeli> Satirlar { get; } = new();
    public ObservableCollection<MaasTahakkukSatiriDto> Yapilanlar { get; } = new();
    public ObservableCollection<OdemeHesabiDto> OdemeHesaplari { get; } = new();
    public IReadOnlyList<int> Yillar { get; } = Enumerable.Range(DateTime.Today.Year - 5, 6).Reverse().ToList();
    public IReadOnlyList<int> Aylar { get; } = Enumerable.Range(1, 12).ToList();

    public ICommand YenileKomutu { get; }
    public ICommand YeniKomutu { get; }
    public ICommand DuzenleKomutu { get; }
    public ICommand SilKomutu { get; }
    public ICommand OdemeKomutu { get; }
    public ICommand AvansKomutu { get; }
    public ICommand IptalKomutu { get; }
    public ICommand TahakkukKomutu { get; }
    public ICommand GeriAlKomutu { get; }
    public ICommand TopluOdemeKomutu { get; }
    public ICommand VergiOdeKomutu { get; }
    public ICommand SgkOdeKomutu { get; }

    public PersonelListeDto? Secili
    {
        get => _secili;
        set { if (OzellikAyarla(ref _secili, value)) { _ = EkstreYukleAsync(); CommandManager.InvalidateRequerySuggested(); } }
    }

    public PersonelEkstreSatiriDto? SeciliEkstre
    {
        get => _seciliEkstre;
        set { if (OzellikAyarla(ref _seciliEkstre, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public bool AyrilanlariGoster
    {
        get => _ayrilanlar;
        set { if (OzellikAyarla(ref _ayrilanlar, value)) _ = YukleAsync(); }
    }

    public int Yil { get => _yil; set { if (OzellikAyarla(ref _yil, value)) _ = DonemYukleAsync(); } }
    public int Ay { get => _ay; set { if (OzellikAyarla(ref _ay, value)) _ = DonemYukleAsync(); } }

    public MaasDonemDto? Donem
    {
        get => _donem;
        private set { OzellikAyarla(ref _donem, value); BildirimGonder(nameof(DonemMetni)); CommandManager.InvalidateRequerySuggested(); }
    }

    public string DonemMetni => Donem == null ? "" :
        Donem.Engeller.Count > 0 ? string.Join(" ", Donem.Engeller) :
        (Satirlar.Count > 0 ? $"Tahakkuk edilecek: {Satirlar.Count(s => s.Secili)} personel, net {Satirlar.Where(s => s.Secili).Sum(s => s.NetUcret):N2} TL, işveren maliyeti {Satirlar.Where(s => s.Secili).Sum(s => s.IsverenMaliyeti):N2} TL. "
            : "Bu dönemde tahakkuk bekleyen personel yok. ") +
        (Donem.FisNolari.Count > 0 ? $"Tahakkuk edilmiş: {Yapilanlar.Count} personel (fiş {string.Join(", ", Donem.FisNolari)})." : "");

    public VergiSgkBakiyeDto? VergiSgk
    {
        get => _vergiSgk;
        private set { OzellikAyarla(ref _vergiSgk, value); CommandManager.InvalidateRequerySuggested(); }
    }

    public OdemeHesabiDto? OdemeHesabi { get => _odemeHesabi; set { if (OzellikAyarla(ref _odemeHesabi, value)) CommandManager.InvalidateRequerySuggested(); } }
    public DateTime OdemeTarihi { get => _odemeTarihi; set => OzellikAyarla(ref _odemeTarihi, value); }
    public decimal ToplamOdenecek => Personeller.Where(p => p.OdenecekBakiye > 0).Sum(p => p.OdenecekBakiye);
    public string TopluOdemeMetni => ToplamOdenecek > 0
        ? $"{Personeller.Count(p => p.OdenecekBakiye > 0)} personele toplam {ToplamOdenecek:N2} TL maaş ödenecek."
        : "Ödenecek maaş yok. Önce Maaş Tahakkuku sekmesinden maaşları tahakkuk edin.";

    public string DurumMesaji { get => _durumMesaji; private set => OzellikAyarla(ref _durumMesaji, value); }

    private async Task YukleAsync()
    {
        try
        {
            var seciliId = Secili?.Id;
            Personeller.Clear();
            foreach (var p in await _servis.ListeAsync(AyrilanlariGoster)) Personeller.Add(p);
            Secili = Personeller.FirstOrDefault(p => p.Id == seciliId) ?? Personeller.FirstOrDefault();
            BildirimGonder(nameof(ToplamOdenecek));
            BildirimGonder(nameof(TopluOdemeMetni));
            var hesapKaynak = OdemeHesabi?.Kaynak;
            var hesapId = OdemeHesabi?.Id;
            OdemeHesaplari.Clear();
            foreach (var h in await _servis.OdemeHesaplariAsync()) OdemeHesaplari.Add(h);
            OdemeHesabi = OdemeHesaplari.FirstOrDefault(h => h.Kaynak == hesapKaynak && h.Id == hesapId) ?? OdemeHesaplari.FirstOrDefault();
            VergiSgk = await _servis.VergiSgkBakiyeAsync();
            await DonemYukleAsync();
            await EkstreYukleAsync();
        }
        catch (Exception ex)
        {
            DurumMesaji = "Personel bilgileri yüklenemedi: " + ex.Message;
        }
    }

    private async Task EkstreYukleAsync()
    {
        Ekstre.Clear();
        if (Secili == null) return;
        try { foreach (var s in (await _servis.EkstreAsync(Secili.Id)).AsEnumerable().Reverse()) Ekstre.Add(s); }
        catch (Exception ex) { DurumMesaji = ex.Message; }
    }

    private async Task DonemYukleAsync()
    {
        try
        {
            var d = await _servis.DonemAsync(Yil, Ay);
            Satirlar.Clear();
            foreach (var s in d.Bekleyenler) Satirlar.Add(new TahakkukSatiriModeli(s, () => { BildirimGonder(nameof(DonemMetni)); CommandManager.InvalidateRequerySuggested(); }));
            Yapilanlar.Clear();
            foreach (var s in d.Yapilanlar) Yapilanlar.Add(s);
            Donem = d;
        }
        catch (Exception ex) { DurumMesaji = ex.Message; }
    }

    private async Task FormAcAsync(PersonelListeDto? satir)
    {
        try
        {
            var kart = satir == null ? null : await _servis.GetirAsync(satir.Id);
            var pencere = new Masaustu.Gorunumler.Sayfa.Muhasebe.PersonelPenceresi(new PersonelFormuModeli(_servis, kart)) { Owner = Application.Current.MainWindow };
            if (pencere.ShowDialog() == true)
            {
                DurumMesaji = "Personel kaydedildi.";
                await YukleAsync();
            }
        }
        catch (Exception ex) { DurumMesaji = ex.Message; }
    }

    private async Task SilAsync()
    {
        if (Secili == null) return;
        if (MessageBox.Show($"{Secili.AdSoyad} silinsin mi?", "Personel Sil", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { await _servis.SilAsync(Secili.Id); DurumMesaji = "Personel silindi."; await YukleAsync(); }
        catch (Exception ex) { DurumMesaji = ex.Message; }
    }

    private async Task ParaIslemiAsync(bool avans)
    {
        if (Secili == null) return;
        if (OdemeHesaplari.Count == 0) { DurumMesaji = "Önce Kasa ya da Banka ekranından TL bir kasa veya vadesiz hesap açın."; return; }
        var tutar = new TextBox { Height = 28, Text = avans ? "" : Secili.OdenecekBakiye.ToString("N2"), Margin = new Thickness(0, 4, 0, 10) };
        var tarih = new DatePicker { SelectedDate = DateTime.Today, Margin = new Thickness(0, 4, 0, 10) };
        var hesap = new ComboBox { ItemsSource = OdemeHesaplari, SelectedItem = OdemeHesabi ?? OdemeHesaplari[0], Height = 28, Margin = new Thickness(0, 4, 0, 10) };
        var aciklama = new TextBox { Height = 28, Margin = new Thickness(0, 4, 0, 0) };
        var tamam = new Button { Content = avans ? "Avans Ver" : "Öde", IsDefault = true, Width = 110, Margin = new Thickness(0, 14, 8, 0) };
        var iptal = new Button { Content = "Vazgeç", IsCancel = true, Width = 100, Margin = new Thickness(0, 14, 0, 0) };
        var bilgi = avans
            ? $"Avans 196 Personel Avansları hesabına yazılır ve sonraki maaş tahakkukunda maaştan düşülmesi önerilir. Mahsup edilmemiş avans: {Secili.AvansBakiye:N2} TL."
            : $"Tahakkuk edilmiş, ödenmemiş maaş: {Secili.OdenecekBakiye:N2} TL. Ödeme 335 Personele Borçlar hesabını kapatır.";
        var pencere = new Window
        {
            Title = (avans ? "Avans Ver: " : "Maaş Öde: ") + Secili.AdSoyad, Width = 440, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    new TextBlock { TextWrapping = TextWrapping.Wrap, Text = bilgi, Margin = new Thickness(0, 0, 0, 10) },
                    new TextBlock { Text = "Tutar (TL)" }, tutar,
                    new TextBlock { Text = "Tarih" }, tarih,
                    new TextBlock { Text = "Ödendiği kasa / banka" }, hesap,
                    new TextBlock { Text = "Açıklama (boş bırakılabilir)" }, aciklama,
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { tamam, iptal } }
                }
            }
        };
        tamam.Click += (_, _) => pencere.DialogResult = true;
        if (pencere.ShowDialog() != true) return;
        if (!decimal.TryParse(tutar.Text, out var t) || t <= 0) { DurumMesaji = "Geçerli bir tutar yazın."; return; }
        if (hesap.SelectedItem is not OdemeHesabiDto h || tarih.SelectedDate is not DateTime gun) return;
        try
        {
            if (avans) await _servis.AvansVerAsync(Secili.Id, t, gun, h.Kaynak, h.Id, aciklama.Text);
            else await _servis.OdemeYapAsync(Secili.Id, t, gun, h.Kaynak, h.Id, aciklama.Text);
            DurumMesaji = $"{Secili.AdSoyad}: {t:N2} TL {(avans ? "avans verildi" : "maaş ödendi")} ({h.Ad}).";
            await YukleAsync();
        }
        catch (Exception ex) { DurumMesaji = (avans ? "Avans verilemedi: " : "Ödeme yapılamadı: ") + ex.Message; }
    }

    private async Task IptalAsync()
    {
        if (SeciliEkstre == null) return;
        if (MessageBox.Show($"{SeciliEkstre.Tarih:dd.MM.yyyy} tarihli {SeciliEkstre.Islem.ToLowerInvariant()} ({SeciliEkstre.Odenen:N2} TL) iptal edilsin mi?\n\nKasa/banka hareketi bugün tarihli ters kayıtla kapatılır.",
                "İptal", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { await _servis.HareketIptalAsync(SeciliEkstre.Id, "Personel ekranından iptal"); DurumMesaji = "Kayıt iptal edildi."; await YukleAsync(); }
        catch (Exception ex) { DurumMesaji = "İptal edilemedi: " + ex.Message; }
    }

    private async Task TahakkukAsync()
    {
        var secilenler = Satirlar.Where(s => s.Secili).Select(s => s.Dto()).ToList();
        if (MessageBox.Show(DonemMetni + $"\n\n{DateTime.DaysInMonth(Yil, Ay)}.{Ay:00}.{Yil} tarihli maaş tahakkuk fişi kesilecek. Devam edilsin mi?", "Maaş Tahakkuku",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            _mesgul = true;
            var fisNo = await _servis.TahakkukEtAsync(Yil, Ay, secilenler);
            DurumMesaji = $"{Ay:00}.{Yil} maaşları tahakkuk edildi. Fiş: {fisNo}";
        }
        catch (Exception ex) { DurumMesaji = "Tahakkuk edilemedi: " + ex.Message; }
        finally { _mesgul = false; }
        await YukleAsync();
    }

    private async Task GeriAlAsync()
    {
        if (MessageBox.Show($"{Ay:00}.{Yil} maaş tahakkuku aynı tarihli ters fişle geri alınacak. Devam edilsin mi?", "Tahakkuku Geri Al",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            _mesgul = true;
            await _servis.TahakkukGeriAlAsync(Yil, Ay);
            DurumMesaji = $"{Ay:00}.{Yil} maaş tahakkuku geri alındı.";
        }
        catch (Exception ex) { DurumMesaji = "Geri alınamadı: " + ex.Message; }
        finally { _mesgul = false; }
        await YukleAsync();
    }

    private async Task TopluOdemeAsync()
    {
        if (OdemeHesabi == null) return;
        if (MessageBox.Show($"{TopluOdemeMetni}\n\nÖdeme {OdemeHesabi.Kaynak.ToLowerInvariant()} hesabı '{OdemeHesabi.Ad}' üzerinden {OdemeTarihi:dd.MM.yyyy} tarihiyle yapılacak. Devam edilsin mi?",
                "Toplu Maaş Ödemesi", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            _mesgul = true;
            var adet = await _servis.TopluOdemeAsync(OdemeTarihi, OdemeHesabi.Kaynak, OdemeHesabi.Id);
            DurumMesaji = $"{adet} personele maaş ödendi.";
        }
        catch (Exception ex) { DurumMesaji = "Toplu ödeme yapılamadı: " + ex.Message; }
        finally { _mesgul = false; }
        await YukleAsync();
    }

    private async Task VergiSgkOdeAsync(string hesapKodu)
    {
        if (OdemeHesabi == null || VergiSgk == null) return;
        var borc = hesapKodu == "360" ? VergiSgk.OdenecekVergi : VergiSgk.OdenecekSgk;
        var ad = hesapKodu == "360" ? "Vergi (muhtasar / KDV / damga)" : "SGK primi";
        var tutar = new TextBox { Height = 28, Text = borc.ToString("N2"), Margin = new Thickness(0, 4, 0, 10) };
        var tamam = new Button { Content = "Öde", IsDefault = true, Width = 110, Margin = new Thickness(0, 14, 8, 0) };
        var iptal = new Button { Content = "Vazgeç", IsCancel = true, Width = 100, Margin = new Thickness(0, 14, 0, 0) };
        var pencere = new Window
        {
            Title = ad + " Ödemesi", Width = 420, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
                        Text = $"Ödenecek {ad.ToLowerInvariant()} borcu: {borc:N2} TL. Ödeme '{OdemeHesabi.Ad}' üzerinden {OdemeTarihi:dd.MM.yyyy} tarihiyle yapılır." },
                    new TextBlock { Text = "Ödenen tutar (TL)" }, tutar,
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { tamam, iptal } }
                }
            }
        };
        tamam.Click += (_, _) => pencere.DialogResult = true;
        if (pencere.ShowDialog() != true) return;
        if (!decimal.TryParse(tutar.Text, out var t) || t <= 0) { DurumMesaji = "Geçerli bir tutar yazın."; return; }
        try
        {
            _mesgul = true;
            await _servis.VergiSgkOdeAsync(hesapKodu, t, OdemeTarihi, OdemeHesabi.Kaynak, OdemeHesabi.Id);
            DurumMesaji = $"{ad} ödemesi kaydedildi: {t:N2} TL.";
        }
        catch (Exception ex) { DurumMesaji = "Ödeme yapılamadı: " + ex.Message; }
        finally { _mesgul = false; }
        await YukleAsync();
    }
}
