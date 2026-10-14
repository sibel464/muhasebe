using System.Windows.Input;
using Masaustu.Servisler;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.GorunumModelleri;

public class AnaGorunumModeli : TemelGorunumModeli
{
    private readonly NavigasyonServisi _navigasyon;
    private readonly IUrunServisi _urunServisi;
    private readonly ICariServisi _cariServisi;
    private readonly ICariTipiTanimiServisi _cariTipiServisi;
    private readonly IMerkeziTanimServisi _merkeziTanimServisi;
    private readonly IFaturaServisi _faturaServisi;
    private readonly ISiparisServisi _siparisServisi;
    private readonly ICariHareketServisi _cariHareketServisi;
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

    public string BugunTarih => DateTime.Now.ToString("dd MMMM yyyy, dddd", new System.Globalization.CultureInfo("tr-TR"));

    // Navigasyon Komutları
    public ICommand DashboardKomutu { get; }
    public ICommand MusteriKomutu { get; } // Bu artık Cari Listesi olarak kullanılacak
    public ICommand CariEkleKomutu { get; }
    public ICommand CariSilKomutu { get; }
    public ICommand TedarikciKomutu { get; }
    public ICommand UrunEkleKomutu { get; }
    public ICommand UrunSilKomutu { get; }
    public ICommand UrunKomutu { get; }
    public ICommand SatisKomutu { get; }
    public ICommand IrsaliyeKomutu { get; }
    public ICommand FaturaEkleKomutu { get; }
    public ICommand FaturaKomutu { get; }
    public ICommand SiparisKomutu { get; }
    public ICommand DepoKomutu { get; }
    public ICommand KasaKomutu { get; }
    public ICommand BankaKomutu { get; }
    public ICommand TahsilatKomutu { get; }
    public ICommand OdemeKomutu { get; }
    public ICommand RaporKomutu { get; }
    public ICommand AyarlarKomutu { get; }
    public ICommand KullanicilarKomutu { get; }
    public ICommand FirmaIslemleriKomutu { get; }
    public ICommand TicariIslemlerKomutu { get; }
    public ICommand CekSenetKomutu { get; }
    public ICommand FinansKomutu { get; }
    public ICommand HesapPlaniKomutu { get; }
    public ICommand YevmiyeFisKomutu { get; }
    public ICommand YevmiyeDefteriKomutu { get; }
    public ICommand BuyukDefterKomutu { get; }
    public ICommand MuavinKomutu { get; }
    public ICommand DovizKurKomutu { get; }
    public ICommand MizanKomutu { get; }
    public ICommand MuhasebeBaglantiKomutu { get; }
    public ICommand MuhasebeSaglikKomutu { get; }
    public ICommand CikisKomutu { get; }

    public Action? CikisYapildi { get; set; }

    // Yetkiler
    public bool MenuCariGorunur => _kullaniciServisi.YetkiliMi(y => y.CariGoruntule);
    public bool MenuStokGorunur => _kullaniciServisi.YetkiliMi(y => y.StokGoruntule);
    public bool MenuDepoGorunur => _kullaniciServisi.YetkiliMi(y => y.DepoGoruntule);
    public bool MenuSatisGorunur => _kullaniciServisi.YetkiliMi(y => y.SatisYap);
    public bool MenuIrsaliyeGorunur => _kullaniciServisi.YetkiliMi(y => y.IrsaliyeGoruntule);
    public bool MenuFaturaGorunur => _kullaniciServisi.YetkiliMi(y => y.FaturaGoruntule);
    public bool MenuKasaGorunur => _kullaniciServisi.YetkiliMi(y => y.KasaYonet);
    public bool MenuBankaGorunur => _kullaniciServisi.YetkiliMi(y => y.BankaGoruntule);
    public bool MenuFinansGorunur => _kullaniciServisi.YetkiliMi(y => y.FinansGoruntule);
    public bool MenuCekSenetGorunur => _kullaniciServisi.YetkiliMi(y => y.CekSenetGoruntule);
    public bool MenuTahsilatOdemeGorunur => _kullaniciServisi.YetkiliMi(y => y.TahsilatYap || y.OdemeYap);
    public bool MenuRaporGorunur => _kullaniciServisi.YetkiliMi(y => y.RaporGoruntule);
    public bool MenuMuhasebeGorunur => _kullaniciServisi.YetkiliMi(y => y.MuhasebeFisGoruntule || y.HesapPlaniYonet || y.MizanGoruntule);
    public bool MenuAyarlarGorunur => _kullaniciServisi.YetkiliMi(y => y.AyarlariYonet || y.KullaniciYonet);
    public bool MenuKullanicilarGorunur => _kullaniciServisi.YetkiliMi(y => y.KullaniciYonet);
    
    public string RolAd => _kullaniciServisi.AktifKullanici?.Rol ?? "Kullanıcı";

    private readonly IKullaniciServisi _kullaniciServisi;
    private readonly IIrsaliyeServisi _irsaliyeServisi;
    private readonly IKasaServisi _kasaServisi;
    private readonly IBankaServisi _bankaServisi;
    private readonly IDepoServisi? _depoServisi;
    private readonly IBelgeYazdirmaServisi? _yazdirmaServisi;
    private readonly ISistemAyarlariServisi? _ayarlarServisi;
    private readonly IAuditLogServisi? _auditLogServisi;

    public AnaGorunumModeli(
        NavigasyonServisi navigasyon, 
        IUrunServisi urunServisi, 
        ICariServisi cariServisi, 
        ICariTipiTanimiServisi cariTipiServisi,
        IMerkeziTanimServisi merkeziTanimServisi,
        IFaturaServisi faturaServisi,
        ISiparisServisi siparisServisi,
        IIrsaliyeServisi irsaliyeServisi,
        ICariHareketServisi cariHareketServisi,
        IKullaniciServisi kullaniciServisi,
        IKasaServisi kasaServisi,
        IBankaServisi bankaServisi,
        ITahsilatOdemeServisi tahsilatOdemeServisi,
        ISirketServisi sirketServisi,
        IGlobalAramaServisi globalAramaServisi,
        IBelgeKapamaServisi belgeKapamaServisi,
        IDepoServisi? depoServisi = null,
        IBelgeYazdirmaServisi? yazdirmaServisi = null,
        ISistemAyarlariServisi? ayarlarServisi = null,
        IAuditLogServisi? auditLogServisi = null,
        ICekSenetServisi? cekSenetServisi = null)
    {
        _navigasyon = navigasyon;
        _urunServisi = urunServisi;
        _cariServisi = cariServisi;
        _cariTipiServisi = cariTipiServisi;
        _merkeziTanimServisi = merkeziTanimServisi;
        _faturaServisi = faturaServisi;
        _siparisServisi = siparisServisi;
        _irsaliyeServisi = irsaliyeServisi;
        _cariHareketServisi = cariHareketServisi;
        _kullaniciServisi = kullaniciServisi;
        _kasaServisi = kasaServisi;
        _bankaServisi = bankaServisi;
        _depoServisi = depoServisi;
        _yazdirmaServisi = yazdirmaServisi;
        _ayarlarServisi = ayarlarServisi;
        _auditLogServisi = auditLogServisi;

        KullaniciAdi = _kullaniciServisi.AktifKullanici?.Ad + " " + _kullaniciServisi.AktifKullanici?.Soyad;
        if (string.IsNullOrWhiteSpace(KullaniciAdi)) KullaniciAdi = _kullaniciServisi.AktifKullanici?.KullaniciAdi ?? "Admin";

        _navigasyon.GorunumModelDegisti += gm =>
        {
            MevcutSayfa = gm;
            if (gm is UrunGorunumModeli) AktifMenu = "Urun";
            else if (gm is UrunEkleGorunumModeli) AktifMenu = "UrunEkle";
            else if (gm is MusteriGorunumModeli) AktifMenu = "Musteri";
            else if (gm is CariEkleGorunumModeli) AktifMenu = "CariEkle";
            else if (gm is TicariIslemlerGorunumModeli) AktifMenu = "TicariIslemler";
            else if (gm is DepoGorunumModeli) AktifMenu = "Depo";
        };

        // Sayfa fabrikalarını kaydet
        _navigasyon.Kaydet("Dashboard", () => new DashboardGorunumModeli(_cariServisi, _urunServisi, _navigasyon, () => CikisYapildi?.Invoke(), _kasaServisi, _faturaServisi));
        _navigasyon.Kaydet("Musteri", () => new MusteriGorunumModeli(_cariServisi, _cariTipiServisi, _navigasyon, () => CikisYapildi?.Invoke()));
        _navigasyon.Kaydet("CariEkle", () => new CariEkleGorunumModeli(_cariServisi, _cariTipiServisi, _navigasyon));
        _navigasyon.Kaydet("CariSil", () => new CariSilGorunumModeli(_cariServisi));
        _navigasyon.Kaydet("Tedarikci", () => new TedarikciGorunumModeli(_cariServisi, _navigasyon, () => CikisYapildi?.Invoke()));
        _navigasyon.Kaydet("UrunEkle", () => new UrunEkleGorunumModeli(_urunServisi, _merkeziTanimServisi, _navigasyon));
        _navigasyon.Kaydet("UrunSil", () => new UrunSilGorunumModeli(_urunServisi));
        _navigasyon.Kaydet("Urun", () => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<StokOperasyonMerkeziGorunumModeli>(App.HizmetSaglayici));
        _navigasyon.Kaydet("Depo", () => new DepoGorunumModeli(_depoServisi ?? throw new InvalidOperationException("Depo servisi yapılandırılmamış."), _urunServisi, _yazdirmaServisi ?? new Altyapi.Servisler.BelgeYazdirmaServisi()));
        _navigasyon.Kaydet("Satis", () => new SatisGorunumModeli(_urunServisi, _cariServisi, _faturaServisi, _cariHareketServisi));
        _navigasyon.Kaydet("Irsaliye", () => new IrsaliyeGorunumModeli(_irsaliyeServisi, _cariServisi, _urunServisi, _faturaServisi, _navigasyon, _yazdirmaServisi));
        _navigasyon.Kaydet("FaturaEkle", () => new FaturaEkleGorunumModeli(_faturaServisi, _cariServisi, _urunServisi, _navigasyon));
        _navigasyon.Kaydet("Fatura", () => new FaturaGorunumModeli(_faturaServisi, _cariServisi, _urunServisi, _navigasyon, _yazdirmaServisi));
        _navigasyon.Kaydet("Siparis", () => new SiparisGorunumModeli(_siparisServisi, _cariServisi, _urunServisi));
        _navigasyon.Kaydet("Kasa", () => new KasaGorunumModeli(_kasaServisi, _yazdirmaServisi, _cariServisi, _bankaServisi));
        _navigasyon.Kaydet("Banka", () => new BankaGorunumModeli(_bankaServisi, _yazdirmaServisi, _cariServisi, _kasaServisi));
        _navigasyon.Kaydet("Tahsilat", () => new TahsilatOdemeGorunumModeli(tahsilatOdemeServisi, _cariServisi, belgeKapamaServisi, EtkiAlani.Modeller.Finans.FinansIslemTuru.Tahsilat));
        _navigasyon.Kaydet("Odeme", () => new TahsilatOdemeGorunumModeli(tahsilatOdemeServisi, _cariServisi, belgeKapamaServisi, EtkiAlani.Modeller.Finans.FinansIslemTuru.Odeme));
        _navigasyon.Kaydet("Rapor", () => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<RaporMerkeziGorunumModeli>(App.HizmetSaglayici));
        _navigasyon.Kaydet("Ayarlar", () => new AyarlarGorunumModeli(_ayarlarServisi, _auditLogServisi, Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IVeritabaniYedekServisi>(App.HizmetSaglayici)));
        _navigasyon.Kaydet("Kullanicilar", () => new KullanicilarGorunumModeli(_kullaniciServisi, _auditLogServisi));
        _navigasyon.Kaydet("FirmaIslemleri", () => new FirmaIslemleriGorunumModeli(sirketServisi));
        _navigasyon.Kaydet("TicariIslemler", () => new TicariIslemlerGorunumModeli(globalAramaServisi, _navigasyon));
        _navigasyon.Kaydet("Finans", () =>
        {
            var vm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<FinansMerkeziGorunumModeli>(App.HizmetSaglayici);
            _ = vm.YukleAsync();
            return vm;
        });
        if (cekSenetServisi != null) _navigasyon.Kaydet("CekSenet", () => new CekSenetGorunumModeli(cekSenetServisi, _cariServisi, _kasaServisi, _bankaServisi));
        _navigasyon.Kaydet("DovizKur", () => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Masaustu.GorunumModeli.Doviz.DovizKurMerkeziGorunumModeli>(App.HizmetSaglayici));
        _navigasyon.Kaydet("HesapPlani", () => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Masaustu.GorunumModeli.Muhasebe.HesapPlaniGorunumModeli>(App.HizmetSaglayici));
        _navigasyon.Kaydet("YevmiyeFis", () => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Masaustu.GorunumModeli.Muhasebe.YevmiyeFisGorunumModeli>(App.HizmetSaglayici));
        _navigasyon.Kaydet("YevmiyeDefteri", () => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Masaustu.GorunumModeli.Muhasebe.YevmiyeDefteriGorunumModeli>(App.HizmetSaglayici));
        _navigasyon.Kaydet("BuyukDefter", () => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Masaustu.GorunumModeli.Muhasebe.BuyukDefterGorunumModeli>(App.HizmetSaglayici));
        _navigasyon.Kaydet("Muavin", () => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Masaustu.GorunumModeli.Muhasebe.MuavinGorunumModeli>(App.HizmetSaglayici));
        _navigasyon.Kaydet("Mizan", () => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Masaustu.GorunumModeli.Muhasebe.MizanGorunumModeli>(App.HizmetSaglayici));
        _navigasyon.Kaydet("MuhasebeBaglanti", () => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Masaustu.GorunumModeli.Muhasebe.MuhasebeBaglantiGorunumModeli>(App.HizmetSaglayici));
        _navigasyon.Kaydet("MuhasebeSaglik", () => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Masaustu.GorunumModeli.Muhasebe.MuhasebeSaglikGorunumModeli>(App.HizmetSaglayici));

        // Komutlar
        DashboardKomutu = new KomutSarici(() => MenuGit("Dashboard"));
        MusteriKomutu = new KomutSarici(() => MenuGit("Musteri"));
        CariEkleKomutu = new KomutSarici(() => MenuGit("CariEkle"));
        CariSilKomutu = new KomutSarici(() => MenuGit("CariSil"));
        TedarikciKomutu = new KomutSarici(() => MenuGit("Tedarikci"));
        UrunEkleKomutu = new KomutSarici(() => MenuGit("UrunEkle"));
        UrunSilKomutu = new KomutSarici(() => MenuGit("UrunSil"));
        UrunKomutu = new KomutSarici(() => MenuGit("Urun"));
        DepoKomutu = new KomutSarici(() => MenuGit("Depo"));
        SatisKomutu = new KomutSarici(() => MenuGit("Satis"));
        IrsaliyeKomutu = new KomutSarici(() => MenuGit("Irsaliye"));
        FaturaEkleKomutu = new KomutSarici(() => MenuGit("FaturaEkle"));
        FaturaKomutu = new KomutSarici(() => MenuGit("Fatura"));
        SiparisKomutu = new KomutSarici(() => MenuGit("Siparis"));
        KasaKomutu = new KomutSarici(() => MenuGit("Kasa"));
        BankaKomutu = new KomutSarici(() => MenuGit("Banka"));
        TahsilatKomutu = new KomutSarici(() => MenuGit("Tahsilat"));
        OdemeKomutu = new KomutSarici(() => MenuGit("Odeme"));
        CekSenetKomutu = new KomutSarici(() => MenuGit("CekSenet"));
        FinansKomutu = new KomutSarici(() => MenuGit("Finans"));
        HesapPlaniKomutu = new KomutSarici(() => MenuGit("HesapPlani"));
        YevmiyeFisKomutu = new KomutSarici(() => MenuGit("YevmiyeFis"));
        YevmiyeDefteriKomutu = new KomutSarici(() => MenuGit("YevmiyeDefteri"));
        BuyukDefterKomutu = new KomutSarici(() => MenuGit("BuyukDefter"));
        MuavinKomutu = new KomutSarici(() => MenuGit("Muavin"));
        DovizKurKomutu = new KomutSarici(() => MenuGit("DovizKur"));
        MizanKomutu = new KomutSarici(() => MenuGit("Mizan"));
        MuhasebeBaglantiKomutu = new KomutSarici(() => MenuGit("MuhasebeBaglanti"));
        MuhasebeSaglikKomutu = new KomutSarici(() => MenuGit("MuhasebeSaglik"));
        RaporKomutu = new KomutSarici(() => MenuGit("Rapor"));
        AyarlarKomutu = new KomutSarici(() => MenuGit("Ayarlar"));
        KullanicilarKomutu = new KomutSarici(() => MenuGit("Kullanicilar"));
        FirmaIslemleriKomutu = new KomutSarici(() => MenuGit("FirmaIslemleri"));
        TicariIslemlerKomutu = new KomutSarici(() => MenuGit("TicariIslemler"));
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
