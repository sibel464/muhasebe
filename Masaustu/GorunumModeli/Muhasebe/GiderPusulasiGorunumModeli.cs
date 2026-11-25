using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Altyapi.Servisler;
using EtkiAlani.Modeller.Muhasebe;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class GiderPusulasiGorunumModeli : TemelGorunumModeli
{
    // Sayfanın servisleri tek veritabanı bağlamını paylaşır; çağrılar sıraya konur (bkz. SiraliServis).
    private readonly SemaphoreSlim _servisKilidi = new(1, 1);

    private readonly IGiderPusulasiServisi _servis;
    private readonly IBelgeYazdirmaServisi _yazdirma;
    private GiderPusulasiListeDto? _secili;
    private DateTime _baslangic = new(DateTime.Today.Year, 1, 1);
    private DateTime _bitis = DateTime.Today;
    private bool _iptaller = true;
    private string _durumMesaji = string.Empty;

    public GiderPusulasiGorunumModeli(IGiderPusulasiServisi servis, IBelgeYazdirmaServisi yazdirma)
    {
        _servis = SiraliServis<IGiderPusulasiServisi>.Sar(servis, _servisKilidi);
        _yazdirma = yazdirma;
        YenileKomutu = new KomutSarici(async () => await YukleAsync());
        YeniKomutu = new KomutSarici(async () => await YeniAsync());
        YazdirKomutu = new KomutSarici(async () => await YazdirAsync(Secili), () => Secili != null);
        IptalKomutu = new KomutSarici(async () => await IptalAsync(), () => Secili is { IptalEdildi: false });
        AnonimlestirKomutu = new KomutSarici(async () => await AnonimlestirAsync());
        _ = YukleAsync();
    }

    public TopluKoleksiyon<GiderPusulasiListeDto> Belgeler { get; } = new();
    public ICommand YenileKomutu { get; }
    public ICommand YeniKomutu { get; }
    public ICommand YazdirKomutu { get; }
    public ICommand IptalKomutu { get; }
    public ICommand AnonimlestirKomutu { get; }

    private async Task AnonimlestirAsync()
    {
        if (MessageBox.Show("Yasal saklama süresi (belge yılını izleyen yıldan itibaren 10 yıl) dolmuş belgelerde satıcının adı, T.C. kimlik numarası ve adresi kalıcı olarak silinecek; tutarlar ve muhasebe kayıtları kalır. Devam edilsin mi?",
                "KVKK - Kişisel Verileri Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            var sayi = await _servis.SuresiDolanlariAnonimlestirAsync(DateTime.Today);
            DurumMesaji = sayi == 0 ? "Saklama süresi dolmuş belge yok." : $"{sayi} belgede satıcının kişisel verileri silindi.";
            await YukleAsync();
        }
        catch (Exception ex) { DurumMesaji = ex.Message; }
    }

    public GiderPusulasiListeDto? Secili
    {
        get => _secili;
        set { if (OzellikAyarla(ref _secili, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public DateTime Baslangic { get => _baslangic; set { if (OzellikAyarla(ref _baslangic, value)) _ = YukleAsync(); } }
    public DateTime Bitis { get => _bitis; set { if (OzellikAyarla(ref _bitis, value)) _ = YukleAsync(); } }
    public bool IptalleriGoster { get => _iptaller; set { if (OzellikAyarla(ref _iptaller, value)) _ = YukleAsync(); } }

    public string Ozet
    {
        get
        {
            var gecerli = Belgeler.Where(b => !b.IptalEdildi).ToList();
            return gecerli.Count == 0 ? "Bu tarih aralığında belge yok."
                : $"{gecerli.Count} belge · Brüt {gecerli.Sum(b => b.BrutTutar):N2} TL · Kesilen stopaj {gecerli.Sum(b => b.StopajTutari):N2} TL · Ödenen {gecerli.Sum(b => b.NetTutar):N2} TL";
        }
    }

    public string DurumMesaji { get => _durumMesaji; private set => OzellikAyarla(ref _durumMesaji, value); }

    private async Task YukleAsync()
    {
        try
        {
            var seciliId = Secili?.Id;
            Belgeler.HepsiniDegistir(await _servis.ListeAsync(Baslangic, Bitis, IptalleriGoster));
            Secili = Belgeler.FirstOrDefault(b => b.Id == seciliId) ?? Belgeler.FirstOrDefault();
        }
        catch (Exception ex)
        {
            DurumMesaji = "Belgeler yüklenemedi: " + ex.Message;
        }
        BildirimGonder(nameof(Ozet));
    }

    private async Task YeniAsync()
    {
        try
        {
            var hesaplar = await _servis.OdemeHesaplariAsync();
            if (hesaplar.Count == 0) { DurumMesaji = "Önce Kasa ya da Banka ekranından TL bir kasa veya vadesiz hesap açın."; return; }
            var model = new GiderPusulasiFormuModeli(_servis, hesaplar);
            var pencere = new Masaustu.Gorunumler.Sayfa.Muhasebe.GiderPusulasiPenceresi(model) { Owner = Application.Current.MainWindow };
            if (pencere.ShowDialog() != true || model.Kaydedilen == null) return;
            DurumMesaji = $"{model.Kaydedilen.BelgeNo} kaydedildi; {model.Kaydedilen.NetTutar:N2} TL ödendi" +
                (model.Kaydedilen.StopajTutari > 0 ? $", {model.Kaydedilen.StopajTutari:N2} TL stopaj 360 hesabına alındı." : ".");
            await YukleAsync();
            Secili = Belgeler.FirstOrDefault(b => b.Id == model.Kaydedilen.Id) ?? Secili;
            if (MessageBox.Show("Belge yazdırılsın mı? (Asıl satıcıya verilir, suret işletmede kalır; ikisi de imzalanır.)", "Yazdır",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                await YazdirAsync(Secili);
        }
        catch (Exception ex)
        {
            DurumMesaji = ex.Message;
        }
    }

    private async Task YazdirAsync(GiderPusulasiListeDto? satir)
    {
        if (satir == null) return;
        try
        {
            var belge = await _servis.GetirAsync(satir.Id) ?? throw new KeyNotFoundException("Belge bulunamadı.");
            await _yazdirma.GiderPusulasiYazdirAsync(belge);
        }
        catch (Exception ex) { DurumMesaji = "Yazdırılamadı: " + ex.Message; }
    }

    private async Task IptalAsync()
    {
        if (Secili == null) return;
        var neden = new TextBox { Height = 26, Margin = new Thickness(0, 4, 0, 0) };
        var tamam = new Button { Content = "İptal Et", IsDefault = true, Width = 110, Margin = new Thickness(0, 12, 8, 0) };
        var vazgec = new Button { Content = "Vazgeç", IsCancel = true, Width = 100, Margin = new Thickness(0, 12, 0, 0) };
        var pencere = new Window
        {
            Title = $"{Secili.TurAdi} İptali: {Secili.BelgeNo}", Width = 460, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Children =
                {
                    new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
                        Text = $"{Secili.SaticiAdSoyad} adına {Secili.Tarih:dd.MM.yyyy} tarihli {Secili.BrutTutar:N2} TL'lik belge iptal edilecek. Muhasebe fişi ters kayıtla kapanır, ödenen {Secili.NetTutar:N2} TL bugünkü tarihle {Secili.OdemeHesabi} hesabına geri girer. Belge listede 'İptal' olarak kalır." },
                    new TextBlock { Text = "İptal nedeni" }, neden,
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { tamam, vazgec } }
                }
            }
        };
        tamam.Click += (_, _) => pencere.DialogResult = true;
        if (pencere.ShowDialog() != true) return;
        try
        {
            await _servis.IptalAsync(Secili.Id, neden.Text);
            DurumMesaji = $"{Secili.BelgeNo} iptal edildi.";
            await YukleAsync();
        }
        catch (Exception ex) { DurumMesaji = "İptal edilemedi: " + ex.Message; }
    }
}

public class GiderPusulasiKalemSatiri : TemelGorunumModeli
{
    private string _cinsi = "";
    private decimal _miktar = 1;
    private string _birim = "Adet";
    private decimal _birimFiyat;

    public string Cinsi { get => _cinsi; set => OzellikAyarla(ref _cinsi, value); }
    public decimal Miktar { get => _miktar; set { if (OzellikAyarla(ref _miktar, value)) BildirimGonder(nameof(Tutar)); } }
    public string Birim { get => _birim; set => OzellikAyarla(ref _birim, value); }
    public decimal BirimFiyat { get => _birimFiyat; set { if (OzellikAyarla(ref _birimFiyat, value)) BildirimGonder(nameof(Tutar)); } }
    public decimal Tutar => decimal.Round(Miktar * BirimFiyat, 2, MidpointRounding.AwayFromZero);
}

public class GiderPusulasiFormuModeli : TemelGorunumModeli
{
    public sealed record Secenek(string Kod, string Ad) { public override string ToString() => $"{Kod} - {Ad}"; }
    public sealed record TurSecenegi(GiderPusulasiTuru Tur, string Ad) { public override string ToString() => Ad; }

    public IReadOnlyList<TurSecenegi> Turler { get; } =
    [
        new(GiderPusulasiTuru.GiderPusulasi, "Gider Pusulası (esnaf muaflığındaki kişi, birey)"),
        new(GiderPusulasiTuru.MustahsilMakbuzu, "Müstahsil Makbuzu (çiftçiden zirai ürün)"),
        new(GiderPusulasiTuru.KiraOdemesi, "Kira Ödemesi (şahıs mal sahibine işyeri kirası)"),
        new(GiderPusulasiTuru.SerbestMeslekMakbuzu, "Serbest Meslek Makbuzu (avukat, müşavir, doktor)")
    ];
    public IReadOnlyList<decimal> KdvOranlari { get; } = [20m, 10m, 1m, 0m];
    public IReadOnlyList<Secenek> GiderHesaplari { get; } =
    [
        new("770", "Genel yönetim giderleri"), new("760", "Pazarlama satış dağıtım giderleri"), new("740", "Hizmet üretim maliyeti"),
        new("730", "Genel üretim giderleri"), new("150", "İlk madde ve malzeme"), new("153", "Ticari mallar")
    ];
    public IReadOnlyList<StopajSecenegi> StopajSecenekleri => GiderPusulasiServisi.StopajSecenekleri;
    public IReadOnlyList<string> Birimler { get; } = ["Adet", "Kg", "Ton", "Lt", "Metre", "M²", "Saat", "Gün", "Ay", "Hizmet"];

    private readonly IGiderPusulasiServisi _servis;
    private TurSecenegi _tur;
    private StopajSecenegi? _stopajSecenegi;
    private decimal _stopajOrani;
    private Secenek _giderHesabi;
    private string _hata = string.Empty;
    private bool _mesgul;

    public GiderPusulasiFormuModeli(IGiderPusulasiServisi servis, IReadOnlyList<OdemeHesabiDto> hesaplar)
    {
        _servis = servis;
        OdemeHesaplari = hesaplar;
        OdemeHesabi = hesaplar[0];
        _tur = Turler[0];
        _giderHesabi = GiderHesaplari[0];
        StopajSecenegi = StopajSecenekleri[0];
        Kalemler.CollectionChanged += (_, e) =>
        {
            foreach (INotifyPropertyChanged k in e.NewItems ?? Array.Empty<object>()) k.PropertyChanged += (_, _) => ToplamlarDegisti();
            ToplamlarDegisti();
        };
        Kalemler.Add(new GiderPusulasiKalemSatiri());
        SatirEkleKomutu = new KomutSarici(() => Kalemler.Add(new GiderPusulasiKalemSatiri()));
        SatirSilKomutu = new KomutSarici(() => { if (SeciliKalem != null && Kalemler.Count > 1) Kalemler.Remove(SeciliKalem); });
        KaydetKomutu = new KomutSarici(async () => await KaydetAsync(), () => !_mesgul);
    }

    public ObservableCollection<GiderPusulasiKalemSatiri> Kalemler { get; } = new();
    public GiderPusulasiKalemSatiri? SeciliKalem { get; set; }
    public IReadOnlyList<OdemeHesabiDto> OdemeHesaplari { get; }
    public OdemeHesabiDto OdemeHesabi { get; set; }

    public TurSecenegi Tur
    {
        get => _tur;
        set
        {
            if (!OzellikAyarla(ref _tur, value)) return;
            // Türe göre varsayılan stopaj ve hesap; kullanıcı değiştirebilir.
            switch (value.Tur)
            {
                case GiderPusulasiTuru.MustahsilMakbuzu:
                    StopajSecenegi = StopajSecenekleri.First(s => s.Ad.Contains("diğer zirai"));
                    GiderHesabi = GiderHesaplari.First(g => g.Kod == "153");
                    break;
                case GiderPusulasiTuru.KiraOdemesi:
                    StopajSecenegi = StopajSecenekleri.First(s => s.Ad.Contains("Kira"));
                    GiderHesabi = GiderHesaplari[0];
                    if (Kalemler.Count == 1 && string.IsNullOrWhiteSpace(Kalemler[0].Cinsi)) { Kalemler[0].Cinsi = $"{DateTime.Today:MM.yyyy} işyeri kirası (brüt)"; Kalemler[0].Birim = "Ay"; }
                    break;
                case GiderPusulasiTuru.SerbestMeslekMakbuzu:
                    StopajSecenegi = StopajSecenekleri.First(s => s.Ad.Contains("Serbest meslek"));
                    GiderHesabi = GiderHesaplari[0];
                    break;
                default:
                    StopajSecenegi = StopajSecenekleri[0];
                    break;
            }
            KdvOrani = value.Tur == GiderPusulasiTuru.SerbestMeslekMakbuzu ? 20 : 0;
            BildirimGonder(nameof(SaticiEtiketi));
            BildirimGonder(nameof(SmmMi));
        }
    }

    public string SaticiEtiketi => Tur.Tur switch
    {
        GiderPusulasiTuru.MustahsilMakbuzu => "Çiftçinin adı soyadı *",
        GiderPusulasiTuru.KiraOdemesi => "Kiraya verenin adı soyadı *",
        GiderPusulasiTuru.SerbestMeslekMakbuzu => "Makbuzu kesenin adı soyadı / unvanı *",
        _ => "Malı satan / işi yapanın adı soyadı *"
    };
    public bool SmmMi => Tur.Tur == GiderPusulasiTuru.SerbestMeslekMakbuzu;
    public string KarsiBelgeNo { get; set; } = "";
    private decimal _kdvOrani;
    public decimal KdvOrani { get => _kdvOrani; set { if (OzellikAyarla(ref _kdvOrani, value)) ToplamlarDegisti(); } }
    public DateTime Tarih { get; set; } = DateTime.Today;
    public string SaticiAdSoyad { get; set; } = "";
    public string SaticiTcKimlikNo { get; set; } = "";
    public string SaticiAdres { get; set; } = "";
    public string Aciklama { get; set; } = "";

    public Secenek GiderHesabi { get => _giderHesabi; set { if (OzellikAyarla(ref _giderHesabi, value)) BildirimGonder(nameof(StokUyarisi)); } }
    public string StokUyarisi => GiderHesabi.Kod is "150" or "153" ? "Not: Bu ekran stok miktarını artırmaz; mal stokta izleniyorsa stok girişini ayrıca yapın." : "";

    public StopajSecenegi? StopajSecenegi
    {
        get => _stopajSecenegi;
        set { if (OzellikAyarla(ref _stopajSecenegi, value) && value != null) StopajOrani = value.Oran; }
    }

    public decimal StopajOrani
    {
        get => _stopajOrani;
        set { if (OzellikAyarla(ref _stopajOrani, value)) ToplamlarDegisti(); }
    }

    public decimal Brut => Kalemler.Sum(k => k.Tutar);
    public decimal Stopaj => GiderPusulasiServisi.StopajHesapla(Brut, StopajOrani);
    public decimal Kdv => GiderPusulasiServisi.StopajHesapla(Brut, KdvOrani);
    public decimal Net => Brut - Stopaj + Kdv;

    private void ToplamlarDegisti()
    {
        BildirimGonder(nameof(Brut));
        BildirimGonder(nameof(Stopaj));
        BildirimGonder(nameof(Kdv));
        BildirimGonder(nameof(Net));
    }

    public string Hata { get => _hata; private set => OzellikAyarla(ref _hata, value); }
    public ICommand SatirEkleKomutu { get; }
    public ICommand SatirSilKomutu { get; }
    public ICommand KaydetKomutu { get; }
    public Action? Kaydedildi { get; set; }
    public GiderPusulasi? Kaydedilen { get; private set; }

    private async Task KaydetAsync()
    {
        Hata = string.Empty;
        var giris = new GiderPusulasiGirisDto
        {
            Tur = Tur.Tur, Tarih = Tarih, SaticiAdSoyad = SaticiAdSoyad, SaticiTcKimlikNo = SaticiTcKimlikNo, SaticiAdres = SaticiAdres,
            GiderHesapKodu = GiderHesabi.Kod, StopajOrani = StopajOrani, OdemeKaynagi = OdemeHesabi.Kaynak, OdemeHesabiId = OdemeHesabi.Id,
            Aciklama = Aciklama, KdvOrani = SmmMi ? KdvOrani : 0, KarsiBelgeNo = SmmMi ? KarsiBelgeNo : null,
            Kalemler = Kalemler.Select(k => new GiderPusulasiKalemDto { Cinsi = k.Cinsi, Miktar = k.Miktar, Birim = k.Birim, BirimFiyat = k.BirimFiyat }).ToList()
        };
        if (MessageBox.Show($"{GiderPusulasiServisi.TurAdi(Tur.Tur)} kaydedilecek:\n\nBrüt: {Brut:N2} TL\nStopaj (%{StopajOrani:0.##}): {Stopaj:N2} TL\n{(Kdv > 0 ? $"KDV (%{KdvOrani:0.##}): {Kdv:N2} TL\n" : "")}Ödenecek: {Net:N2} TL ({OdemeHesabi.Kaynak}: {OdemeHesabi.Ad})\n\nKayıttan sonra belge değiştirilemez, yalnız iptal edilebilir. Devam edilsin mi?",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            _mesgul = true;
            Kaydedilen = await _servis.KaydetAsync(giris);
            Kaydedildi?.Invoke();
        }
        catch (Exception ex)
        {
            Hata = ex.Message;
        }
        finally { _mesgul = false; }
    }
}
