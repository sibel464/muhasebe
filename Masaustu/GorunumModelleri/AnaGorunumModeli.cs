using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
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
    public ICommand UrunEkleKomutu { get; }
    public ICommand UrunSilKomutu { get; }
    public ICommand UrunKomutu { get; }
    public ICommand SatisKomutu { get; }
    public ICommand YardimKomutu { get; }
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
    public ICommand YilSonuKomutu { get; }
    public ICommand MaliTablolarKomutu { get; }
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
    
    public string RolAd
    {
        get
        {
            var rol = _kullaniciServisi.AktifKullanici?.Rol ?? "Kullanıcı";
            var firma = App.HizmetSaglayici?.GetService(typeof(IAktifFirmaBaglami)) is IAktifFirmaBaglami f ? f.SirketAdi : null;
            return string.IsNullOrWhiteSpace(firma) ? rol : $"{rol} • {firma}";
        }
    }

    private readonly IKullaniciServisi _kullaniciServisi;
    private readonly IIrsaliyeServisi _irsaliyeServisi;
    private readonly IKasaServisi _kasaServisi;
    private readonly IBankaServisi _bankaServisi;
    private readonly IDepoServisi? _depoServisi;
    private readonly IBelgeYazdirmaServisi? _yazdirmaServisi;
    private readonly ISistemAyarlariServisi? _ayarlarServisi;
    private readonly IAuditLogServisi? _auditLogServisi;
    private readonly IEBelgeServisi? _eBelgeServisi;

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
        ICekSenetServisi? cekSenetServisi = null,
        IEBelgeServisi? eBelgeServisi = null)
    {
        _eBelgeServisi = eBelgeServisi;
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
            if (gm is StokOperasyonMerkeziGorunumModeli) AktifMenu = "Urun";
            else if (gm is UrunEkleGorunumModeli) AktifMenu = "UrunEkle";
            else if (gm is MusteriGorunumModeli) AktifMenu = "Musteri";
            else if (gm is CariEkleGorunumModeli) AktifMenu = "CariEkle";
            else if (gm is TicariIslemlerGorunumModeli) AktifMenu = "TicariIslemler";
            else if (gm is DepoGorunumModeli) AktifMenu = "Depo";
        };

        // Sayfa fabrikalarını kaydet. Servisler her sayfanın kendi kapsamından (sp) alınır; böylece her sayfa kendi
        // veritabanı bağlamıyla çalışır ve açılışta güncel veriyi okur (bkz. NavigasyonServisi).
        static T G<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();
        _navigasyon.Kaydet("Dashboard", sp => new DashboardGorunumModeli(G<ICariServisi>(sp), G<IUrunServisi>(sp), _navigasyon, () => CikisYapildi?.Invoke(), G<IKasaServisi>(sp), G<IFaturaServisi>(sp), sp.GetService<IBaslangicKontrolServisi>(), sp.GetService<IVeritabaniYedekServisi>()));
        _navigasyon.Kaydet("Musteri", sp => new MusteriGorunumModeli(G<ICariServisi>(sp), G<ICariTipiTanimiServisi>(sp), _navigasyon, () => CikisYapildi?.Invoke()));
        _navigasyon.Kaydet("CariEkle", sp => new CariEkleGorunumModeli(G<ICariServisi>(sp), G<ICariTipiTanimiServisi>(sp), _navigasyon));
        _navigasyon.Kaydet("CariSil", sp => new CariSilGorunumModeli(G<ICariServisi>(sp), G<IKvkkServisi>(sp)));
        _navigasyon.Kaydet("UrunEkle", sp => new UrunEkleGorunumModeli(G<IUrunServisi>(sp), G<IMerkeziTanimServisi>(sp), _navigasyon));
        _navigasyon.Kaydet("UrunSil", sp => new UrunSilGorunumModeli(G<IUrunServisi>(sp)));
        _navigasyon.Kaydet("Urun", sp => G<StokOperasyonMerkeziGorunumModeli>(sp));
        _navigasyon.Kaydet("Depo", sp => new DepoGorunumModeli(G<IDepoServisi>(sp), G<IUrunServisi>(sp), G<IBelgeYazdirmaServisi>(sp)));
        _navigasyon.Kaydet("Satis", sp => new SatisGorunumModeli(G<IUrunServisi>(sp), G<ICariServisi>(sp), G<IFaturaServisi>(sp), G<ICariHareketServisi>(sp), sp.GetService<IBelgeYazdirmaServisi>()));
        _navigasyon.Kaydet("Irsaliye", sp => new IrsaliyeGorunumModeli(G<IIrsaliyeServisi>(sp), G<ICariServisi>(sp), G<IUrunServisi>(sp), G<IFaturaServisi>(sp), _navigasyon, G<IBelgeYazdirmaServisi>(sp), sp.GetService<IEBelgeServisi>()));
        _navigasyon.Kaydet("FaturaEkle", sp => new FaturaEkleGorunumModeli(G<IFaturaServisi>(sp), G<ICariServisi>(sp), G<IUrunServisi>(sp), _navigasyon));
        _navigasyon.Kaydet("Fatura", sp => new FaturaGorunumModeli(G<IFaturaServisi>(sp), G<ICariServisi>(sp), G<IUrunServisi>(sp), _navigasyon, G<IBelgeYazdirmaServisi>(sp), G<IEBelgeServisi>(sp)));
        _navigasyon.Kaydet("Siparis", sp => new SiparisGorunumModeli(G<ISiparisServisi>(sp), G<ICariServisi>(sp), G<IUrunServisi>(sp), sp.GetService<IBelgeYazdirmaServisi>()));
        _navigasyon.Kaydet("Kasa", sp => new KasaGorunumModeli(G<IKasaServisi>(sp), G<IBelgeYazdirmaServisi>(sp), G<ICariServisi>(sp), G<IBankaServisi>(sp)));
        _navigasyon.Kaydet("Banka", sp => new BankaGorunumModeli(G<IBankaServisi>(sp), G<IBelgeYazdirmaServisi>(sp), G<ICariServisi>(sp), G<IKasaServisi>(sp)));
        _navigasyon.Kaydet("Tahsilat", sp => new TahsilatOdemeGorunumModeli(G<ITahsilatOdemeServisi>(sp), G<ICariServisi>(sp), G<IBelgeKapamaServisi>(sp), EtkiAlani.Modeller.Finans.FinansIslemTuru.Tahsilat));
        _navigasyon.Kaydet("Odeme", sp => new TahsilatOdemeGorunumModeli(G<ITahsilatOdemeServisi>(sp), G<ICariServisi>(sp), G<IBelgeKapamaServisi>(sp), EtkiAlani.Modeller.Finans.FinansIslemTuru.Odeme));
        _navigasyon.Kaydet("Rapor", sp => G<RaporMerkeziGorunumModeli>(sp));
        _navigasyon.Kaydet("Ayarlar", sp => new AyarlarGorunumModeli(G<ISistemAyarlariServisi>(sp), G<IAuditLogServisi>(sp), sp.GetService<IVeritabaniYedekServisi>()));
        _navigasyon.Kaydet("Kullanicilar", sp => new KullanicilarGorunumModeli(_kullaniciServisi, G<IAuditLogServisi>(sp)));
        _navigasyon.Kaydet("FirmaIslemleri", sp => new FirmaIslemleriGorunumModeli(G<ISirketServisi>(sp)));
        _navigasyon.Kaydet("TicariIslemler", sp => new TicariIslemlerGorunumModeli(G<IGlobalAramaServisi>(sp), _navigasyon));
        _navigasyon.Kaydet("Finans", sp =>
        {
            var vm = G<FinansMerkeziGorunumModeli>(sp);
            _ = vm.YukleAsync();
            return vm;
        });
        _navigasyon.Kaydet("CekSenet", sp => new CekSenetGorunumModeli(G<ICekSenetServisi>(sp), G<ICariServisi>(sp), G<IKasaServisi>(sp), G<IBankaServisi>(sp)));
        _navigasyon.Kaydet("DovizKur", sp => G<Masaustu.GorunumModeli.Doviz.DovizKurMerkeziGorunumModeli>(sp));
        _navigasyon.Kaydet("HesapPlani", sp => G<Masaustu.GorunumModeli.Muhasebe.HesapPlaniGorunumModeli>(sp));
        _navigasyon.Kaydet("YevmiyeFis", sp => G<Masaustu.GorunumModeli.Muhasebe.YevmiyeFisGorunumModeli>(sp));
        _navigasyon.Kaydet("YevmiyeDefteri", sp => G<Masaustu.GorunumModeli.Muhasebe.YevmiyeDefteriGorunumModeli>(sp));
        _navigasyon.Kaydet("BuyukDefter", sp => G<Masaustu.GorunumModeli.Muhasebe.BuyukDefterGorunumModeli>(sp));
        _navigasyon.Kaydet("Muavin", sp => G<Masaustu.GorunumModeli.Muhasebe.MuavinGorunumModeli>(sp));
        _navigasyon.Kaydet("Mizan", sp => G<Masaustu.GorunumModeli.Muhasebe.MizanGorunumModeli>(sp));
        _navigasyon.Kaydet("MuhasebeBaglanti", sp => G<Masaustu.GorunumModeli.Muhasebe.MuhasebeBaglantiGorunumModeli>(sp));
        _navigasyon.Kaydet("MuhasebeSaglik", sp => G<Masaustu.GorunumModeli.Muhasebe.MuhasebeSaglikGorunumModeli>(sp));
        _navigasyon.Kaydet("YilSonu", sp => G<Masaustu.GorunumModeli.Muhasebe.YilSonuGorunumModeli>(sp));
        _navigasyon.Kaydet("MaliTablolar", sp => G<Masaustu.GorunumModeli.Muhasebe.MaliTablolarGorunumModeli>(sp));

        // Komutlar
        DashboardKomutu = new KomutSarici(() => MenuGit("Dashboard"));
        MusteriKomutu = new KomutSarici(() => MenuGit("Musteri"));
        CariEkleKomutu = new KomutSarici(() => MenuGit("CariEkle"));
        CariSilKomutu = new KomutSarici(() => MenuGit("CariSil"));
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
        YilSonuKomutu = new KomutSarici(() => MenuGit("YilSonu"));
        MaliTablolarKomutu = new KomutSarici(() => MenuGit("MaliTablolar"));
        RaporKomutu = new KomutSarici(() => MenuGit("Rapor"));
        AyarlarKomutu = new KomutSarici(() => MenuGit("Ayarlar"));
        YardimKomutu = new KomutSarici(() => YardimAc());
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

    // Kullanım kılavuzu programla birlikte kurulan HTML dosyasıdır; varsayılan tarayıcıda açılır (F1 / Yardım menüsü).
    public static void YardimAc()
    {
        var yol = System.IO.Path.Combine(AppContext.BaseDirectory, "Yardim", "KullanimKilavuzu.html");
        if (!System.IO.File.Exists(yol))
        {
            System.Windows.MessageBox.Show("Kullanım kılavuzu bulunamadı: " + yol, "Yardım", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(yol) { UseShellExecute = true });
    }
}
