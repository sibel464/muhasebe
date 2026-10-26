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

    private ObservableCollection<HesapEkstresi.Satir<BankaHareketi>> _hareketler = new();
    private HesapEkstresi.Sonuc<BankaHareketi>? _ekstre;
    private DateTime? _baslangicTarihi = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? _bitisTarihi = DateTime.Today;
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
                ParaBirimiBildir();
                _ = VerileriYukleAsync();
                _ = KurOnerAsync();
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

    // Ekstre: seçilen tarih aralığı; öncesi devreden bakiye olur. Başlangıç = bitiş = bugün → gün sonu raporu.
    public DateTime? BaslangicTarihi { get => _baslangicTarihi; set { if (OzellikAyarla(ref _baslangicTarihi, value)) Filtrele(); } }
    public DateTime? BitisTarihi { get => _bitisTarihi; set { if (OzellikAyarla(ref _bitisTarihi, value)) Filtrele(); } }
    public decimal DevirBakiye => _ekstre?.Devir ?? 0;
    public decimal DonemGiris => _ekstre?.ToplamGiris ?? 0;
    public decimal DonemCikis => _ekstre?.ToplamCikis ?? 0;
    public decimal DonemSonuBakiye => _ekstre?.Kapanis ?? 0;
    public ICommand BugunKomutu { get; }
    public ICommand BuAyKomutu { get; }

    public ObservableCollection<HesapEkstresi.Satir<BankaHareketi>> Hareketler
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

    private IReadOnlyList<PosHesapOzeti> _posHesapOzetleri = [];
    public IReadOnlyList<PosHesapOzeti> PosHesapOzetleri { get => _posHesapOzetleri; private set => OzellikAyarla(ref _posHesapOzetleri, value); }

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

    // Bu formdan girilebilen fiş türleri; virman, kasa transferi ve POS geçişi kendi sekmelerinden yapılır.
    public Array FisTurleri { get; } = new[] { BankaFisTuru.GelenHavale, BankaFisTuru.GidenHavale, BankaFisTuru.BankaGelir, BankaFisTuru.BankaGiderMasraf, BankaFisTuru.KrediOdemesi };

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

    // Dövizli hesapta tutarlar hesabın para biriminde girilir ve gösterilir; TL karşılığı kurla hesaplanır.
    public decimal YeniTutar
    {
        get => _yeniTutar;
        set { if (OzellikAyarla(ref _yeniTutar, value)) BildirimGonder(nameof(YeniTlKarsiligi)); }
    }

    private decimal _yeniKur;
    public bool SeciliHesapDovizli => DovizKuruYardimcisi.DovizliMi(SeciliBankaHesabi?.ParaBirimi);
    public string ParaSimgesi => DovizKuruYardimcisi.Simge(SeciliBankaHesabi?.ParaBirimi);
    public decimal YeniKur
    {
        get => _yeniKur;
        set { if (OzellikAyarla(ref _yeniKur, value)) BildirimGonder(nameof(YeniTlKarsiligi)); }
    }
    public decimal YeniTlKarsiligi => Math.Round(YeniTutar * YeniKur, 2, MidpointRounding.AwayFromZero);

    private void ParaBirimiBildir()
    {
        BildirimGonder(nameof(SeciliHesapDovizli));
        BildirimGonder(nameof(ParaSimgesi));
    }

    private async Task KurOnerAsync()
    {
        if (SeciliBankaHesabi is { } hesap && SeciliHesapDovizli)
            YeniKur = await DovizKuruYardimcisi.GunlukKurAsync(hesap.ParaBirimi, YeniTarih);
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
    // Döviz yalnız "Vadesiz Döviz" hesabında tutulur; tür ile para birimi birbirine göre ayarlanır.
    public BankaHesapTuru YeniHesapTuru
    {
        get => _yeniHesapTuru;
        set
        {
            if (!OzellikAyarla(ref _yeniHesapTuru, value)) return;
            if (value == BankaHesapTuru.VadesizDoviz && !DovizKuruYardimcisi.DovizliMi(YeniParaBirimi)) YeniParaBirimi = "USD";
            else if (value != BankaHesapTuru.VadesizDoviz && DovizKuruYardimcisi.DovizliMi(YeniParaBirimi)) YeniParaBirimi = DovizKuruYardimcisi.Tl;
        }
    }
    public BankaHesapTuru[] HesapTurleri => Enum.GetValues<BankaHesapTuru>();
    private string _yeniParaBirimi = DovizKuruYardimcisi.Tl;
    public string YeniParaBirimi
    {
        get => _yeniParaBirimi;
        set
        {
            if (!OzellikAyarla(ref _yeniParaBirimi, value)) return;
            if (DovizKuruYardimcisi.DovizliMi(value)) YeniHesapTuru = BankaHesapTuru.VadesizDoviz;
            else if (YeniHesapTuru == BankaHesapTuru.VadesizDoviz) YeniHesapTuru = BankaHesapTuru.VadesizTL;
        }
    }
    public IReadOnlyList<string> ParaBirimleri => DovizKuruYardimcisi.ParaBirimleri;
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

        ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.Aktar(Hareketler.Select(r => new
        {
            Tarih = r.Kayit.Tarih, r.Kayit.FisNo, r.Kayit.DekontNo, Tur = FinansMetinDonusturucu.Metin(r.Kayit.FisTuru), Hesap = r.Kayit.BankaHesabi?.Ad,
            Cari = r.Kayit.CariKart?.Unvan, r.Kayit.Aciklama, r.Giris, r.Cikis, r.Bakiye
        }), "Banka_Ekstresi"));
        BugunKomutu = new KomutSarici(() => { _baslangicTarihi = DateTime.Today; _bitisTarihi = DateTime.Today; BildirimGonder(nameof(BaslangicTarihi)); BildirimGonder(nameof(BitisTarihi)); Filtrele(); });
        BuAyKomutu = new KomutSarici(() => { _baslangicTarihi = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); _bitisTarihi = DateTime.Today; BildirimGonder(nameof(BaslangicTarihi)); BildirimGonder(nameof(BitisTarihi)); Filtrele(); });

        FisKaydetKomutu = new KomutSarici(async () => await FisKaydetAsync());
        BankaVirmanKaydetKomutu = new KomutSarici(async () => await BankaVirmanKaydetAsync());
        KasadanCekKaydetKomutu = new KomutSarici(async () => await KasadanCekKaydetAsync());
        PosBlokeGecisKaydetKomutu = new KomutSarici(async () => await PosBlokeGecisKaydetAsync());
        BankaHesabiEkleKomutu = new KomutSarici(async () => await BankaHesabiEkleAsync());

        SilKomutu = new KomutSarici(async param => { if (param is HesapEkstresi.Satir<BankaHareketi> r) await SilAsync(r.Kayit); else if (param is BankaHareketi h) await SilAsync(h); });
        YenileKomutu = new KomutSarici(async () => await VerileriYukleAsync());

        DokumYazdirKomutu = new KomutSarici(async () =>
        {
            if (_yazdirmaServisi == null || _ekstre == null) return;
            var gunSonu = BaslangicTarihi?.Date == BitisTarihi?.Date && BaslangicTarihi != null;
            await _yazdirmaServisi.HesapEkstresiYazdirAsync(new HesapEkstresiBelgesi(
                gunSonu ? "Gün Sonu Banka Raporu" : "Banka Hesap Ekstresi",
                SeciliBankaHesabi?.Ad ?? "Tüm Banka Hesapları", BaslangicTarihi, BitisTarihi, _ekstre.Devir,
                Hareketler.Select(r => new HesapEkstresiSatiri(r.Kayit.Tarih, r.Kayit.DekontNo ?? r.Kayit.FisNo ?? "", FinansMetinDonusturucu.Metin(r.Kayit.FisTuru),
                    r.Kayit.CariKart?.Unvan ?? "", r.Kayit.Aciklama, r.Giris, r.Cikis, r.Bakiye)).ToList(),
                Hareketler.Sum(r => r.Giris), Hareketler.Sum(r => r.Cikis), _ekstre.Kapanis));
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
            ParaBirimiBildir();
            _ = KurOnerAsync();

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
            ToplamBakiye = SeciliHesapDovizli
                ? await _bankaServisi.HesapDovizBakiyeHesaplaAsync(SeciliBankaHesabi.Id)
                : await _bankaServisi.HesapBakiyeHesaplaAsync(SeciliBankaHesabi.Id);
        }
        else
        {
            liste = await _bankaServisi.TumunuGetirAsync();
            ToplamBakiye = await _bankaServisi.BakiyeHesaplaAsync();
        }

        _tumHareketler = liste;
        Filtrele();

        // POS hesabında bekleyen (bankanın henüz vadesiz hesaba geçirmediği) tutar o hesabın hareket bakiyesidir.
        var posOzetleri = new List<PosHesapOzeti>();
        foreach (var pos in BankaHesaplari.Where(b => b.HesapTuru == BankaHesapTuru.POSHesabi))
            posOzetleri.Add(new PosHesapOzeti(pos.Ad, pos.PosKomisyonOrani, pos.ValorGunu, await _bankaServisi.HesapBakiyeHesaplaAsync(pos.Id)));
        PosHesapOzetleri = posOzetleri;
        ToplamBlokeBakiye = posOzetleri.Sum(p => p.BlokeBakiye);

        var ayBaslangic = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        BuAyGelir = liste.Where(h => h.IslemDurumu == FinansIslemDurumu.Tamamlandi && h.Tarih >= ayBaslangic && h.Tur == HareketTuru.Gelir).Sum(HesapTutari);
        BuAyGider = liste.Where(h => h.IslemDurumu == FinansIslemDurumu.Tamamlandi && h.Tarih >= ayBaslangic && h.Tur == HareketTuru.Gider).Sum(HesapTutari);

        DurumMesaji = $"{liste.Count} banka hareketi listeleniyor.";
    }

    // Dövizli hesap seçiliyse ekstre ve özetler hesabın para birimindedir.
    private decimal HesapTutari(BankaHareketi h) => SeciliHesapDovizli ? h.DovizTutar : h.Tutar;

    // Bakiye yalnız tamamlanmış hareketlerden yürür (iptal edilen ile ters kaydı birbirini götürür, gösterilmez).
    // Arama yalnız gösterilen satırları süzer; devir ve yürüyen bakiye tüm hareketlerden hesaplanır.
    private void Filtrele()
    {
        if (BaslangicTarihi.HasValue && BitisTarihi.HasValue && BaslangicTarihi > BitisTarihi)
        {
            HataMesaji = "Başlangıç tarihi bitiş tarihinden sonra olamaz.";
            return;
        }
        _ekstre = HesapEkstresi.Olustur(_tumHareketler.Where(h => h.IslemDurumu == FinansIslemDurumu.Tamamlandi),
            h => h.Tarih, h => h.Tur == HareketTuru.Gelir ? HesapTutari(h) : -HesapTutari(h), h => h.Id, BaslangicTarihi, BitisTarihi);
        var arama = AramaMetni.Trim();
        var satirlar = string.IsNullOrWhiteSpace(arama)
            ? _ekstre.Satirlar
            : _ekstre.Satirlar.Where(r =>
                r.Kayit.Aciklama.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ||
                (r.Kayit.FisNo?.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (r.Kayit.DekontNo?.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (r.Kayit.BankaAdi?.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (r.Kayit.CariKart?.Unvan.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ?? false)).ToList();
        Hareketler = new ObservableCollection<HesapEkstresi.Satir<BankaHareketi>>(satirlar);
        BildirimGonder(nameof(DevirBakiye));
        BildirimGonder(nameof(DonemGiris));
        BildirimGonder(nameof(DonemCikis));
        BildirimGonder(nameof(DonemSonuBakiye));
    }

    private async Task FisKaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (YeniTutar <= 0) { HataMesaji = "Geçerli bir tutar giriniz."; return; }
        if (string.IsNullOrWhiteSpace(YeniAciklama)) { HataMesaji = "Açıklama giriniz."; return; }

        if (SeciliBankaHesabi == null) { HataMesaji = "İşlem yapılacak banka hesabını seçiniz."; return; }
        if (SeciliHesapDovizli && YeniKur <= 0) { HataMesaji = $"{ParaSimgesi} kurunu giriniz."; return; }
        // Dövizli hesapta servis TL tutarı döviz × kurdan hesaplar.
        var (tutar, dovizTutar, kur) = SeciliHesapDovizli ? (0m, YeniTutar, YeniKur) : (YeniTutar, 0m, 0m);
        var tutarMetni = SeciliHesapDovizli ? $"{YeniTutar:N2} {ParaSimgesi} ({YeniTlKarsiligi:N2} ₺)" : $"{YeniTutar:N2} ₺";

        try
        {
            var cariliHavale = SeciliFisTuru is BankaFisTuru.GelenHavale or BankaFisTuru.GidenHavale;
            if (cariliHavale && SeciliCariKart == null) { HataMesaji = "Gelen/giden havale için cari kart seçiniz."; return; }

            if (SeciliFisTuru == BankaFisTuru.GelenHavale)
            {
                await _bankaServisi.HesabaTahsilatEkleAsync(SeciliBankaHesabi.Id, SeciliCariKart!.Id, tutar, YeniAciklama, YeniDekontNo, YeniTarih, dovizTutar, kur);
                BasariMesaji = $"✅ Cari Gelen Havale kaydedildi: {tutarMetni} ({SeciliCariKart.Unvan} → {SeciliBankaHesabi.Ad})";
            }
            else if (SeciliFisTuru == BankaFisTuru.GidenHavale)
            {
                await _bankaServisi.HesaptanOdemeEkleAsync(SeciliBankaHesabi.Id, SeciliCariKart!.Id, tutar, YeniAciklama, YeniDekontNo, YeniTarih, dovizTutar, kur);
                BasariMesaji = $"✅ Cari Giden Havale kaydedildi: {tutarMetni} ({SeciliBankaHesabi.Ad} → {SeciliCariKart.Unvan})";
            }
            else
            {
                var hareket = new BankaHareketi
                {
                    BankaHesabiId = SeciliBankaHesabi.Id,
                    Tarih = YeniTarih,
                    FisTuru = SeciliFisTuru,
                    Tutar = tutar,
                    DovizTutar = dovizTutar,
                    Kur = kur,
                    Aciklama = YeniAciklama,
                    DekontNo = YeniDekontNo
                };

                await _bankaServisi.EkleAsync(hareket);
                BasariMesaji = $"✅ {SeciliFisTuru} fişi kaydedildi: {tutarMetni}";
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
            BasariMesaji = $"✅ Bankalar arası virman kaydedildi: {VirmanTutari:N2} {ParaSimgesi} ({SeciliBankaHesabi.Ad} -> {SeciliTargetBankaHesabi.Ad})";
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
            BasariMesaji = $"✅ Bankadan kasaya para çekme kaydedildi: {VirmanTutari:N2} {ParaSimgesi} ({SeciliTargetKasa.Ad})";
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
                ParaBirimi = string.IsNullOrWhiteSpace(YeniParaBirimi) ? DovizKuruYardimcisi.Tl : YeniParaBirimi,
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
            $"{hareket.BankaAdi} — {hareket.Aciklama} ({(DovizKuruYardimcisi.DovizliMi(hareket.BankaHesabi?.ParaBirimi) ? $"{hareket.DovizTutar:N2} {hareket.BankaHesabi!.ParaBirimi}" : $"{hareket.Tutar:N2} ₺")}) kaydını silmek istiyor musunuz?",
            "Silme Onayı",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (onay != System.Windows.MessageBoxResult.Yes) return;

        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;
        try
        {
            await _bankaServisi.SilAsync(hareket.Id);
            BasariMesaji = "✅ Hareket silindi.";
            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Hareket silinemedi: {ex.Message}";
        }
    }
}

// POS hesabı ve bankanın vadesiz hesaba geçirmesini bekleyen tutar.
public sealed record PosHesapOzeti(string Ad, decimal PosKomisyonOrani, int ValorGunu, decimal BlokeBakiye);
