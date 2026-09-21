using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Yardimcilar;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class BankaGorunumModeli : TemelGorunumModeli
{
    private readonly IBankaServisi _bankaServisi;
    private readonly IBelgeYazdirmaServisi? _yazdirmaServisi;
    private readonly ICariServisi? _cariServisi;
    private readonly IKasaServisi? _kasaServisi;

    private ObservableCollection<BankaHesabi> _bankaHesaplari = new();
    private BankaHesabi? _seciliBankaHesabi;

    private ObservableCollection<CariKart> _cariKartlar = new();
    private CariKart? _seciliCariKart;

    private ObservableCollection<Kasa> _kasalar = new();
    private Kasa? _seciliTargetKasa;

    private BankaHesabi? _seciliTargetBankaHesabi;

    private ObservableCollection<BankaHareketi> _hareketler = new();
    private List<BankaHareketi> _tumHareketler = new();
    private string _aramaMetni = string.Empty;
    private decimal _toplamBakiye;
    private decimal _toplamBlokeBakiye;
    private decimal _buAyGelir;
    private decimal _buAyGider;
    private string _durumMesaji = "Yükleniyor...";
    private bool _yukleniyor;

    // Fiş Giriş Formu (Logo ERP Style)
    private BankaFisTuru _seciliFisTuru = BankaFisTuru.GelenHavale;
    private string _yeniBankaAdi = "Ziraat Bankası";
    private string _yeniHesapNo = string.Empty;
    private decimal _yeniTutar;
    private string _yeniDekontNo = string.Empty;
    private string _yeniAciklama = string.Empty;
    private DateTime _yeniTarih = DateTime.Today;

    // Virman & POS Formları
    private decimal _virmanTutari;
    private decimal _posKomisyonTutari;
    private string _virmanAciklama = string.Empty;

    // Banka Hesabı Ekle Formu
    private string _yeniKod = string.Empty;
    private string _yeniAd = string.Empty;
    private BankaHesapTuru _yeniHesapTuru = BankaHesapTuru.VadesizTL;
    private string _yeniSube = string.Empty;
    private string _yeniIban = string.Empty;
    private decimal _yeniPosKomisyonOrani = 1.85m;
    private int _yeniValorGunu = 1;
    private string _yeniMuhasebeKodu = "102.01.001";

    private string _hataMesaji = string.Empty;
    private string _basariMesaji = string.Empty;

    public ObservableCollection<BankaHesabi> BankaHesaplari
    {
        get => _bankaHesaplari;
        set => OzellikAyarla(ref _bankaHesaplari, value);
    }

    public BankaHesabi? SeciliBankaHesabi
    {
        get => _seciliBankaHesabi;
        set
        {
            if (OzellikAyarla(ref _seciliBankaHesabi, value))
            {
                if (value != null)
                {
                    YeniBankaAdi = value.Ad;
                    YeniHesapNo = value.Iban;
                }
                _ = VerileriYukleAsync();
            }
        }
    }

    public ObservableCollection<CariKart> CariKartlar
    {
        get => _cariKartlar;
        set => OzellikAyarla(ref _cariKartlar, value);
    }

    public CariKart? SeciliCariKart
    {
        get => _seciliCariKart;
        set => OzellikAyarla(ref _seciliCariKart, value);
    }

    public ObservableCollection<Kasa> Kasalar
    {
        get => _kasalar;
        set => OzellikAyarla(ref _kasalar, value);
    }

    public Kasa? SeciliTargetKasa
    {
        get => _seciliTargetKasa;
        set => OzellikAyarla(ref _seciliTargetKasa, value);
    }

    public BankaHesabi? SeciliTargetBankaHesabi
    {
        get => _seciliTargetBankaHesabi;
        set => OzellikAyarla(ref _seciliTargetBankaHesabi, value);
    }

    public ObservableCollection<BankaHareketi> Hareketler
    {
        get => _hareketler;
        set => OzellikAyarla(ref _hareketler, value);
    }

    public string AramaMetni
    {
        get => _aramaMetni;
        set
        {
            if (OzellikAyarla(ref _aramaMetni, value ?? string.Empty))
                Filtrele();
        }
    }

    public decimal ToplamBakiye
    {
        get => _toplamBakiye;
        set => OzellikAyarla(ref _toplamBakiye, value);
    }

    public decimal ToplamBlokeBakiye
    {
        get => _toplamBlokeBakiye;
        set => OzellikAyarla(ref _toplamBlokeBakiye, value);
    }

    public decimal BuAyGelir
    {
        get => _buAyGelir;
        set => OzellikAyarla(ref _buAyGelir, value);
    }

    public decimal BuAyGider
    {
        get => _buAyGider;
        set => OzellikAyarla(ref _buAyGider, value);
    }

    public string DurumMesaji
    {
        get => _durumMesaji;
        set => OzellikAyarla(ref _durumMesaji, value);
    }

    public bool Yukleniyor
    {
        get => _yukleniyor;
        set => OzellikAyarla(ref _yukleniyor, value);
    }

    // Fiş Form Özellikleri
    public BankaFisTuru SeciliFisTuru
    {
        get => _seciliFisTuru;
        set => OzellikAyarla(ref _seciliFisTuru, value);
    }

    public Array FisTurleri => Enum.GetValues(typeof(BankaFisTuru));

    public string YeniBankaAdi
    {
        get => _yeniBankaAdi;
        set => OzellikAyarla(ref _yeniBankaAdi, value);
    }

    public string YeniHesapNo
    {
        get => _yeniHesapNo;
        set => OzellikAyarla(ref _yeniHesapNo, value?.Trim() ?? string.Empty);
    }

    public decimal YeniTutar
    {
        get => _yeniTutar;
        set => OzellikAyarla(ref _yeniTutar, value);
    }

    public string YeniDekontNo
    {
        get => _yeniDekontNo;
        set => OzellikAyarla(ref _yeniDekontNo, value);
    }

    public string YeniAciklama
    {
        get => _yeniAciklama;
        set => OzellikAyarla(ref _yeniAciklama, value);
    }

    public DateTime YeniTarih
    {
        get => _yeniTarih;
        set => OzellikAyarla(ref _yeniTarih, value);
    }

    // Virman & POS Özellikleri
    public decimal VirmanTutari
    {
        get => _virmanTutari;
        set => OzellikAyarla(ref _virmanTutari, value);
    }

    public decimal PosKomisyonTutari
    {
        get => _posKomisyonTutari;
        set => OzellikAyarla(ref _posKomisyonTutari, value);
    }

    public string VirmanAciklama
    {
        get => _virmanAciklama;
        set => OzellikAyarla(ref _virmanAciklama, value);
    }

    // Banka Hesabı Ekle Özellikleri
    public string YeniKod { get => _yeniKod; set => OzellikAyarla(ref _yeniKod, value); }
    public string YeniAd { get => _yeniAd; set => OzellikAyarla(ref _yeniAd, value); }
    public BankaHesapTuru YeniHesapTuru { get => _yeniHesapTuru; set => OzellikAyarla(ref _yeniHesapTuru, value); }
    public Array HesapTurleri => Enum.GetValues(typeof(BankaHesapTuru));
    public string YeniSube { get => _yeniSube; set => OzellikAyarla(ref _yeniSube, value); }
    public string YeniIban { get => _yeniIban; set => OzellikAyarla(ref _yeniIban, value); }
    public decimal YeniPosKomisyonOrani { get => _yeniPosKomisyonOrani; set => OzellikAyarla(ref _yeniPosKomisyonOrani, value); }
    public int YeniValorGunu { get => _yeniValorGunu; set => OzellikAyarla(ref _yeniValorGunu, value); }
    public string YeniMuhasebeKodu { get => _yeniMuhasebeKodu; set => OzellikAyarla(ref _yeniMuhasebeKodu, value); }

    public string HataMesaji
    {
        get => _hataMesaji;
        set => OzellikAyarla(ref _hataMesaji, value);
    }

    public string BasariMesaji
    {
        get => _basariMesaji;
        set => OzellikAyarla(ref _basariMesaji, value);
    }

    public List<string> BankaListesi { get; } = new()
    {
        "Garanti BBVA",
        "İş Bankası",
        "Akbank",
        "Yapı Kredi",
        "Ziraat Bankası",
        "VakıfBank",
        "Halkbank",
        "QNB Finansbank"
    };

    // Komutlar
    public ICommand FisKaydetKomutu { get; }
    public ICommand BankaVirmanKaydetKomutu { get; }
    public ICommand KasadanCekKaydetKomutu { get; }
    public ICommand PosBlokeGecisKaydetKomutu { get; }
    public ICommand BankaHesabiEkleKomutu { get; }
    public ICommand SilKomutu { get; }
    public ICommand YenileKomutu { get; }
    public ICommand DokumYazdirKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }

    public BankaGorunumModeli(
        IBankaServisi bankaServisi,
        IBelgeYazdirmaServisi? yazdirmaServisi = null,
        ICariServisi? cariServisi = null,
        IKasaServisi? kasaServisi = null)
    {
        _bankaServisi = bankaServisi;
        _yazdirmaServisi = yazdirmaServisi;
        _cariServisi = cariServisi;
        _kasaServisi = kasaServisi;

        ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.AktarCsv(Hareketler, "Banka_Hareketleri_Listesi"));

        FisKaydetKomutu = new KomutSarici(async () => await FisKaydetAsync());
        BankaVirmanKaydetKomutu = new KomutSarici(async () => await BankaVirmanKaydetAsync());
        KasadanCekKaydetKomutu = new KomutSarici(async () => await KasadanCekKaydetAsync());
        PosBlokeGecisKaydetKomutu = new KomutSarici(async () => await PosBlokeGecisKaydetAsync());
        BankaHesabiEkleKomutu = new KomutSarici(async () => await BankaHesabiEkleAsync());

        SilKomutu = new KomutSarici(async param => { if (param is BankaHareketi h) await SilAsync(h); });
        YenileKomutu = new KomutSarici(async () => await VerileriYukleAsync());

        DokumYazdirKomutu = new KomutSarici(async () =>
        {
            if (_yazdirmaServisi != null)
                await _yazdirmaServisi.BankaDokumuYazdirAsync(Hareketler.ToList(), ToplamBakiye);
        });

        _ = IlkYuklemeAsync();
    }

    private readonly SemaphoreSlim _yuklemeKilidi = new(1, 1);

    private async Task IlkYuklemeAsync()
    {
        await _yuklemeKilidi.WaitAsync();
        Yukleniyor = true;
        try
        {
            var bList = await _bankaServisi.BankaHesaplariGetirAsync();
            BankaHesaplari = new ObservableCollection<BankaHesabi>(bList);
            _seciliBankaHesabi = BankaHesaplari.FirstOrDefault();
            BildirimGonder(nameof(SeciliBankaHesabi));

            if (_seciliBankaHesabi != null)
            {
                YeniBankaAdi = _seciliBankaHesabi.Ad;
                YeniHesapNo = _seciliBankaHesabi.Iban;
            }

            if (_cariServisi != null)
            {
                var cList = await _cariServisi.TumunuGetirAsync();
                CariKartlar = new ObservableCollection<CariKart>(cList);
            }

            if (_kasaServisi != null)
            {
                var kList = await _kasaServisi.KasalariGetirAsync();
                Kasalar = new ObservableCollection<Kasa>(kList);
                SeciliTargetKasa = Kasalar.FirstOrDefault();
            }

            await VerileriYukleKilitsizAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Banka verileri yüklenirken hata: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
            _yuklemeKilidi.Release();
        }
    }

    private async Task VerileriYukleAsync()
    {
        await _yuklemeKilidi.WaitAsync();
        Yukleniyor = true;
        try
        {
            await VerileriYukleKilitsizAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Banka verileri yüklenirken hata: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
            _yuklemeKilidi.Release();
        }
    }

    private async Task VerileriYukleKilitsizAsync()
    {
        List<BankaHareketi> liste;
        if (SeciliBankaHesabi != null)
        {
            liste = await _bankaServisi.HesapHareketleriGetirAsync(SeciliBankaHesabi.Id);
            ToplamBakiye = await _bankaServisi.HesapBakiyeHesaplaAsync(SeciliBankaHesabi.Id);
        }
        else
        {
            liste = await _bankaServisi.TumunuGetirAsync();
            ToplamBakiye = await _bankaServisi.BakiyeHesaplaAsync();
        }

        _tumHareketler = liste;
        Filtrele();

        // POS Bloke hesap bakiyelerini topla
        ToplamBlokeBakiye = BankaHesaplari.Where(b => b.HesapTuru == BankaHesapTuru.POSHesabi).Sum(b => b.BlokeBakiye);

        var ayBaslangic = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        BuAyGelir = liste.Where(h => h.Tarih >= ayBaslangic && h.Tur == HareketTuru.Gelir).Sum(h => h.Tutar);
        BuAyGider = liste.Where(h => h.Tarih >= ayBaslangic && h.Tur == HareketTuru.Gider).Sum(h => h.Tutar);

        DurumMesaji = $"{liste.Count} banka hareketi listeleniyor.";
    }

    private void Filtrele()
    {
        var arama = AramaMetni.Trim();
        var filtrelenmis = string.IsNullOrWhiteSpace(arama)
            ? _tumHareketler
            : _tumHareketler.Where(h =>
                h.Aciklama.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ||
                (h.FisNo?.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (h.DekontNo?.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (h.BankaAdi?.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (h.CariKart?.Unvan.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ?? false));
        Hareketler = new ObservableCollection<BankaHareketi>(filtrelenmis);
    }

    private async Task FisKaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (YeniTutar <= 0) { HataMesaji = "Geçerli bir tutar giriniz."; return; }
        if (string.IsNullOrWhiteSpace(YeniAciklama)) { HataMesaji = "Açıklama giriniz."; return; }

        var bankaAdi = string.IsNullOrWhiteSpace(YeniBankaAdi) ? (SeciliBankaHesabi?.Ad ?? "Ziraat Bankası") : YeniBankaAdi;
        var hesapNo = string.IsNullOrWhiteSpace(YeniHesapNo) ? (SeciliBankaHesabi?.Iban ?? string.Empty) : YeniHesapNo.Trim();

        if (hesapNo.StartsWith("TR", StringComparison.OrdinalIgnoreCase))
        {
            if (!IbanDogrulayici.Gecerlidir(hesapNo, out hesapNo))
            {
                HataMesaji = "Geçerli bir IBAN giriniz.";
                return;
            }
            YeniHesapNo = hesapNo;
        }

        try
        {
            if (SeciliFisTuru == BankaFisTuru.GelenHavale && SeciliCariKart != null)
            {
                await _bankaServisi.TahsilatEkleAsync(SeciliCariKart.Id, bankaAdi, hesapNo, YeniTutar, YeniAciklama, YeniDekontNo, YeniTarih);
                BasariMesaji = $"✅ Cari Gelen Havale kaydedildi: {YeniTutar:N2} ₺ ({SeciliCariKart.Unvan})";
            }
            else if (SeciliFisTuru == BankaFisTuru.GidenHavale && SeciliCariKart != null)
            {
                await _bankaServisi.OdemeEkleAsync(SeciliCariKart.Id, bankaAdi, hesapNo, YeniTutar, YeniAciklama, YeniDekontNo, YeniTarih);
                BasariMesaji = $"✅ Cari Giden Havale kaydedildi: {YeniTutar:N2} ₺ ({SeciliCariKart.Unvan})";
            }
            else
            {
                var tur = (SeciliFisTuru == BankaFisTuru.GelenHavale || SeciliFisTuru == BankaFisTuru.BankaGelir || SeciliFisTuru == BankaFisTuru.PosHesabaGecis)
                    ? HareketTuru.Gelir
                    : HareketTuru.Gider;

                var hareket = new BankaHareketi
                {
                    BankaHesabiId = SeciliBankaHesabi?.Id,
                    BankaAdi = bankaAdi,
                    HesapNo = hesapNo,
                    Tarih = YeniTarih,
                    Tur = tur,
                    FisTuru = SeciliFisTuru,
                    Tutar = YeniTutar,
                    Aciklama = YeniAciklama,
                    DekontNo = YeniDekontNo,
                    CariKartId = SeciliCariKart?.Id
                };

                await _bankaServisi.EkleAsync(hareket);
                BasariMesaji = $"✅ {SeciliFisTuru} fişi kaydedildi: {YeniTutar:N2} ₺";
            }

            YeniTutar = 0;
            YeniAciklama = string.Empty;
            YeniDekontNo = string.Empty;

            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"İşlem kaydedilemedi: {ex.Message}";
        }
    }

    private async Task BankaVirmanKaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (SeciliBankaHesabi == null || SeciliTargetBankaHesabi == null) { HataMesaji = "Kaynak ve Hedef banka hesabı seçiniz."; return; }
        if (VirmanTutari <= 0) { HataMesaji = "Geçerli virman tutarı giriniz."; return; }

        try
        {
            await _bankaServisi.BankaVirmanYapAsync(SeciliBankaHesabi.Id, SeciliTargetBankaHesabi.Id, VirmanTutari, VirmanAciklama, DateTime.Today);
            BasariMesaji = $"✅ Bankalar arası virman kaydedildi: {VirmanTutari:N2} ₺ ({SeciliBankaHesabi.Ad} -> {SeciliTargetBankaHesabi.Ad})";
            VirmanTutari = 0;
            VirmanAciklama = string.Empty;

            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Virman başarısız: {ex.Message}";
        }
    }

    private async Task KasadanCekKaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (SeciliBankaHesabi == null || SeciliTargetKasa == null) { HataMesaji = "Banka Hesabı ve Kasa seçiniz."; return; }
        if (VirmanTutari <= 0) { HataMesaji = "Geçerli tutar giriniz."; return; }

        try
        {
            await _bankaServisi.BankadanKasaCekAsync(SeciliBankaHesabi.Id, SeciliTargetKasa.Id, VirmanTutari, VirmanAciklama, DateTime.Today);
            BasariMesaji = $"✅ Bankadan kasaya para çekme kaydedildi: {VirmanTutari:N2} ₺ ({SeciliTargetKasa.Ad})";
            VirmanTutari = 0;
            VirmanAciklama = string.Empty;

            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Kasaya çekim başarısız: {ex.Message}";
        }
    }

    private async Task PosBlokeGecisKaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (SeciliBankaHesabi == null || SeciliTargetBankaHesabi == null) { HataMesaji = "POS Hesabı ve Vadesiz Hesap seçiniz."; return; }
        if (VirmanTutari <= 0) { HataMesaji = "Geçerli bloke çözümü tutarı giriniz."; return; }

        try
        {
            await _bankaServisi.PosBlokeHesabaGecirAsync(SeciliBankaHesabi.Id, SeciliTargetBankaHesabi.Id, VirmanTutari, PosKomisyonTutari, VirmanAciklama, DateTime.Today);
            BasariMesaji = $"✅ POS Bloke hesaba aktarıldı: {VirmanTutari:N2} ₺ (Net: {VirmanTutari - PosKomisyonTutari:N2} ₺)";
            VirmanTutari = 0;
            PosKomisyonTutari = 0;
            VirmanAciklama = string.Empty;

            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"POS Hesaba geçiş başarısız: {ex.Message}";
        }
    }

    private async Task BankaHesabiEkleAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (string.IsNullOrWhiteSpace(YeniAd)) { HataMesaji = "Banka / Hesap adı boş girilemez."; return; }

        var iban = YeniIban.Trim();
        if (iban.StartsWith("TR", StringComparison.OrdinalIgnoreCase))
        {
            if (!IbanDogrulayici.Gecerlidir(iban, out iban))
            {
                HataMesaji = "Geçerli bir IBAN giriniz.";
                return;
            }
        }

        try
        {
            var h = new BankaHesabi
            {
                Kod = string.IsNullOrWhiteSpace(YeniKod) ? $"BNK-0{BankaHesaplari.Count + 1}" : YeniKod.Trim(),
                Ad = YeniAd.Trim(),
                HesapTuru = YeniHesapTuru,
                Sube = YeniSube.Trim(),
                HesapNo = string.IsNullOrWhiteSpace(iban) ? "000000" : iban,
                Iban = iban,
                PosKomisyonOrani = YeniPosKomisyonOrani,
                ValorGunu = YeniValorGunu,
                MuhasebeKodu = string.IsNullOrWhiteSpace(YeniMuhasebeKodu) ? "102.01.001" : YeniMuhasebeKodu.Trim(),
                ParaBirimi = "TRY",
                AktifMi = true
            };

            await _bankaServisi.BankaHesabiEkleAsync(h);
            BasariMesaji = $"✅ Yeni banka hesabı eklendi: {h.Ad}";

            YeniKod = string.Empty;
            YeniAd = string.Empty;
            YeniIban = string.Empty;

            var list = await _bankaServisi.BankaHesaplariGetirAsync();
            BankaHesaplari = new ObservableCollection<BankaHesabi>(list);
            SeciliBankaHesabi = h;
        }
        catch (Exception ex)
        {
            HataMesaji = $"Banka hesabı eklenemedi: {ex.Message}";
        }
    }

    private async Task SilAsync(BankaHareketi hareket)
    {
        var onay = System.Windows.MessageBox.Show(
            $"{hareket.BankaAdi} — {hareket.Aciklama} ({hareket.Tutar:N2} ₺) kaydını silmek istiyor musunuz?",
            "Silme Onayı",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (onay != System.Windows.MessageBoxResult.Yes) return;

        await _bankaServisi.SilAsync(hareket.Id);
        await VerileriYukleAsync();
    }
}

