using System.Windows.Input;
using Masaustu.Servisler;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.GorunumModelleri;

public class AnaGorunumModeli : TemelGorunumModeli
{
    private readonly NavigasyonServisi _navigasyon;
    private readonly IUrunServisi _urunServisi;
    private TemelGorunumModeli? _mevcutSayfa;
    private string _aktifMenu = "Dashboard";
    private string _kullaniciAdi = "Admin";

    public TemelGorunumModeli? MevcutSayfa
    {
        get => _mevcutSayfa;
        set => OzellikAyarla(ref _mevcutSayfa, value);
    }

    public string AktifMenu
    {
        get => _aktifMenu;
        set => OzellikAyarla(ref _aktifMenu, value);
    }

    public string KullaniciAdi
    {
        get => _kullaniciAdi;
        set => OzellikAyarla(ref _kullaniciAdi, value);
    }

    // Navigasyon Komutları
    public ICommand DashboardKomutu { get; }
    public ICommand MusteriKomutu { get; }
    public ICommand TedarikciKomutu { get; }
    public ICommand UrunKomutu { get; }
    public ICommand FaturaKomutu { get; }
    public ICommand KasaKomutu { get; }
    public ICommand BankaKomutu { get; }
    public ICommand RaporKomutu { get; }
    public ICommand AyarlarKomutu { get; }
    public ICommand CikisKomutu { get; }

    public Action? CikisYapildi { get; set; }

    public AnaGorunumModeli(NavigasyonServisi navigasyon, IUrunServisi urunServisi)
    {
        _navigasyon = navigasyon;
        _urunServisi = urunServisi;

        _navigasyon.GorunumModelDegisti += gm => MevcutSayfa = gm;

        // Sayfa fabrikalarını kaydet
        _navigasyon.Kaydet("Dashboard", () => new DashboardGorunumModeli());
        _navigasyon.Kaydet("Musteri", () => new MusteriGorunumModeli());
        _navigasyon.Kaydet("Tedarikci", () => new TedarikciGorunumModeli());
        _navigasyon.Kaydet("Urun", () => new UrunGorunumModeli(_urunServisi));
        _navigasyon.Kaydet("Fatura", () => new FaturaGorunumModeli());
        _navigasyon.Kaydet("Kasa", () => new KasaGorunumModeli());
        _navigasyon.Kaydet("Banka", () => new BankaGorunumModeli());
        _navigasyon.Kaydet("Rapor", () => new RaporGorunumModeli());
        _navigasyon.Kaydet("Ayarlar", () => new AyarlarGorunumModeli());

        // Komutlar
        DashboardKomutu = new KomutSarici(() => MenuGit("Dashboard"));
        MusteriKomutu = new KomutSarici(() => MenuGit("Musteri"));
        TedarikciKomutu = new KomutSarici(() => MenuGit("Tedarikci"));
        UrunKomutu = new KomutSarici(() => MenuGit("Urun"));
        FaturaKomutu = new KomutSarici(() => MenuGit("Fatura"));
        KasaKomutu = new KomutSarici(() => MenuGit("Kasa"));
        BankaKomutu = new KomutSarici(() => MenuGit("Banka"));
        RaporKomutu = new KomutSarici(() => MenuGit("Rapor"));
        AyarlarKomutu = new KomutSarici(() => MenuGit("Ayarlar"));
        CikisKomutu = new KomutSarici(() => CikisYapildi?.Invoke());

        // Dashboard ile başla
        _navigasyon.Git("Dashboard");
    }

    private void MenuGit(string sayfa)
    {
        AktifMenu = sayfa;
        _navigasyon.Git(sayfa);
    }
}
