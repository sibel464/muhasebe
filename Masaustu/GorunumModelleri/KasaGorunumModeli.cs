using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using EtkiAlani.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class KasaGorunumModeli : TemelGorunumModeli
{
    private readonly IKasaServisi _kasaServisi;
    private readonly ICariServisi? _cariServisi;
    private readonly IBankaServisi? _bankaServisi;
    private readonly IBelgeYazdirmaServisi? _yazdirmaServisi;

    private ObservableCollection<Kasa> _kasalar = new();
    private Kasa? _seciliKasa;

    private ObservableCollection<CariKart> _cariKartlar = new();
    private CariKart? _seciliCariKart;

    private ObservableCollection<BankaHesabi> _bankaHesaplari = new();
    private BankaHesabi? _seciliTargetBankaHesabi;

    private ObservableCollection<HesapEkstresi.Satir<KasaHareketi>> _hareketler = new();
    private HesapEkstresi.Sonuc<KasaHareketi>? _ekstre;
    private DateTime? _baslangicTarihi = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? _bitisTarihi = DateTime.Today;
    private List<KasaHareketi> _tumHareketler = new();
    private string _aramaMetni = string.Empty;
    private decimal _toplamBakiye;
    private decimal _buAyGelir;
    private decimal _buAyGider;
    private string _durumMesaji = "Yükleniyor...";
    private bool _yukleniyor;

    // Fiş Giriş Formu (Logo ERP Style)
    private KasaFisTuru _seciliFisTuru = KasaFisTuru.Tahsilat;
    private decimal _yeniTutar;
    private string _yeniAciklama = string.Empty;
    private string _yeniMakbuzNo = string.Empty;
    private string _yeniMasrafKodu = string.Empty;
    private DateTime _yeniTarih = DateTime.Today;

    // Virman Formu
    private Kasa? _seciliTargetKasa;
    private decimal _virmanTutari;
    private string _virmanAciklama = string.Empty;

    // Mutabakat / Sayım Formu
    private int _adet200;
    private int _adet100;
    private int _adet50;
    private int _adet20;
    private int _adet10;
    private int _adet5;
    private decimal _bozukParaToplam;
    private string _mutabakatAciklama = string.Empty;

    // Kasa Ekle Formu
    private string _yeniKasaKodu = string.Empty;
    private string _yeniKasaAd = string.Empty;
    private string _yeniKasaMuhasebeKodu = "100.01.001";
    private string _yeniKasaParaBirimi = DovizKuruYardimcisi.Tl;
    private decimal _yeniKur;

    private string _hataMesaji = string.Empty;
    private string _basariMesaji = string.Empty;

    public ObservableCollection<Kasa> Kasalar
    {
        get => _kasalar;
        set => OzellikAyarla(ref _kasalar, value);
    }

    public Kasa? SeciliKasa
    {
        get => _seciliKasa;
        set
        {
            if (OzellikAyarla(ref _seciliKasa, value))
            {
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

    public ObservableCollection<BankaHesabi> BankaHesaplari
    {
        get => _bankaHesaplari;
        set => OzellikAyarla(ref _bankaHesaplari, value);
    }

    public BankaHesabi? SeciliTargetBankaHesabi
    {
        get => _seciliTargetBankaHesabi;
        set => OzellikAyarla(ref _seciliTargetBankaHesabi, value);
    }

    public Kasa? SeciliTargetKasa
    {
        get => _seciliTargetKasa;
        set => OzellikAyarla(ref _seciliTargetKasa, value);
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

    public ObservableCollection<HesapEkstresi.Satir<KasaHareketi>> Hareketler
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
    public KasaFisTuru SeciliFisTuru
    {
        get => _seciliFisTuru;
        set => OzellikAyarla(ref _seciliFisTuru, value);
    }

    // Bu formdan girilebilen fiş türleri; virman, banka transferi ve sayım farkı kendi sekmelerinden yapılır.
    public Array FisTurleri { get; } = new[] { KasaFisTuru.Tahsilat, KasaFisTuru.Odeme, KasaFisTuru.Gelir, KasaFisTuru.Gider, KasaFisTuru.AcilisDevir };

    // Dövizli kasada tutarlar kasanın para biriminde girilir ve gösterilir; TL karşılığı kurla hesaplanır.
    public decimal YeniTutar
    {
        get => _yeniTutar;
        set { if (OzellikAyarla(ref _yeniTutar, value)) BildirimGonder(nameof(YeniTlKarsiligi)); }
    }

    public bool SeciliKasaDovizli => DovizKuruYardimcisi.DovizliMi(SeciliKasa?.ParaBirimi);
    public string ParaSimgesi => DovizKuruYardimcisi.Simge(SeciliKasa?.ParaBirimi);
    public decimal YeniKur
    {
        get => _yeniKur;
        set { if (OzellikAyarla(ref _yeniKur, value)) BildirimGonder(nameof(YeniTlKarsiligi)); }
    }
    public decimal YeniTlKarsiligi => Math.Round(YeniTutar * YeniKur, 2, MidpointRounding.AwayFromZero);
    public IReadOnlyList<string> ParaBirimleri => DovizKuruYardimcisi.ParaBirimleri;
    public string YeniKasaParaBirimi { get => _yeniKasaParaBirimi; set => OzellikAyarla(ref _yeniKasaParaBirimi, value); }

    private void ParaBirimiBildir()
    {
        BildirimGonder(nameof(SeciliKasaDovizli));
        BildirimGonder(nameof(ParaSimgesi));
    }

    private async Task KurOnerAsync()
    {
        if (SeciliKasa is { } kasa && SeciliKasaDovizli)
            YeniKur = await DovizKuruYardimcisi.GunlukKurAsync(kasa.ParaBirimi, YeniTarih);
    }

    public string YeniAciklama
    {
        get => _yeniAciklama;
        set => OzellikAyarla(ref _yeniAciklama, value);
    }

    public string YeniMakbuzNo
    {
        get => _yeniMakbuzNo;
        set => OzellikAyarla(ref _yeniMakbuzNo, value);
    }

    public string YeniMasrafKodu
    {
        get => _yeniMasrafKodu;
        set => OzellikAyarla(ref _yeniMasrafKodu, value);
    }

    public DateTime YeniTarih
    {
        get => _yeniTarih;
        set => OzellikAyarla(ref _yeniTarih, value);
    }

    // Virman Özellikleri
    public decimal VirmanTutari
    {
        get => _virmanTutari;
        set => OzellikAyarla(ref _virmanTutari, value);
    }

    public string VirmanAciklama
    {
        get => _virmanAciklama;
        set => OzellikAyarla(ref _virmanAciklama, value);
    }

    // Mutabakat Sayım Kırılımları
    public int Adet200 { get => _adet200; set { if (OzellikAyarla(ref _adet200, value)) GuncelleSayimToplam(); } }
    public int Adet100 { get => _adet100; set { if (OzellikAyarla(ref _adet100, value)) GuncelleSayimToplam(); } }
    public int Adet50 { get => _adet50; set { if (OzellikAyarla(ref _adet50, value)) GuncelleSayimToplam(); } }
    public int Adet20 { get => _adet20; set { if (OzellikAyarla(ref _adet20, value)) GuncelleSayimToplam(); } }
    public int Adet10 { get => _adet10; set { if (OzellikAyarla(ref _adet10, value)) GuncelleSayimToplam(); } }
    public int Adet5 { get => _adet5; set { if (OzellikAyarla(ref _adet5, value)) GuncelleSayimToplam(); } }
    public decimal BozukParaToplam { get => _bozukParaToplam; set { if (OzellikAyarla(ref _bozukParaToplam, value)) GuncelleSayimToplam(); } }
    public string MutabakatAciklama { get => _mutabakatAciklama; set => OzellikAyarla(ref _mutabakatAciklama, value); }

    public decimal SayimFizikiToplam => (Adet200 * 200) + (Adet100 * 100) + (Adet50 * 50) + (Adet20 * 20) + (Adet10 * 10) + (Adet5 * 5) + BozukParaToplam;
    public decimal SayimFark => SayimFizikiToplam - ToplamBakiye;

    // Kasa Ekle Form Özellikleri
    public string YeniKasaKodu { get => _yeniKasaKodu; set => OzellikAyarla(ref _yeniKasaKodu, value); }
    public string YeniKasaAd { get => _yeniKasaAd; set => OzellikAyarla(ref _yeniKasaAd, value); }
    public string YeniKasaMuhasebeKodu { get => _yeniKasaMuhasebeKodu; set => OzellikAyarla(ref _yeniKasaMuhasebeKodu, value); }

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

    public bool BakiyePositif => ToplamBakiye >= 0;

    // Komutlar
    public ICommand FisKaydetKomutu { get; }
    public ICommand KasaVirmanKaydetKomutu { get; }
    public ICommand BankayaYatirKaydetKomutu { get; }
    public ICommand MutabakatKaydetKomutu { get; }
    public ICommand KasaEkleKomutu { get; }
    public ICommand SilKomutu { get; }
    public ICommand YenileKomutu { get; }
    public ICommand DokumYazdirKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }

    public KasaGorunumModeli(
        IKasaServisi kasaServisi,
        IBelgeYazdirmaServisi? yazdirmaServisi = null,
        ICariServisi? cariServisi = null,
        IBankaServisi? bankaServisi = null)
    {
        _kasaServisi = kasaServisi;
        _yazdirmaServisi = yazdirmaServisi;
        _cariServisi = cariServisi;
        _bankaServisi = bankaServisi;

        ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.Aktar(Hareketler.Select(r => new
        {
            Tarih = r.Kayit.Tarih, r.Kayit.FisNo, Tur = FinansMetinDonusturucu.Metin(r.Kayit.FisTuru), Kasa = r.Kayit.Kasa?.Ad,
            Cari = r.Kayit.CariKart?.Unvan, r.Kayit.Aciklama, r.Giris, r.Cikis, r.Bakiye
        }), "Kasa_Ekstresi"));
        BugunKomutu = new KomutSarici(() => { _baslangicTarihi = DateTime.Today; _bitisTarihi = DateTime.Today; BildirimGonder(nameof(BaslangicTarihi)); BildirimGonder(nameof(BitisTarihi)); Filtrele(); });
        BuAyKomutu = new KomutSarici(() => { _baslangicTarihi = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); _bitisTarihi = DateTime.Today; BildirimGonder(nameof(BaslangicTarihi)); BildirimGonder(nameof(BitisTarihi)); Filtrele(); });

        FisKaydetKomutu = new KomutSarici(async () => await FisKaydetAsync());
        KasaVirmanKaydetKomutu = new KomutSarici(async () => await KasaVirmanKaydetAsync());
        BankayaYatirKaydetKomutu = new KomutSarici(async () => await BankayaYatirKaydetAsync());
        MutabakatKaydetKomutu = new KomutSarici(async () => await MutabakatKaydetAsync());
        KasaEkleKomutu = new KomutSarici(async () => await KasaEkleAsync());
        SilKomutu = new KomutSarici(async param => { if (param is HesapEkstresi.Satir<KasaHareketi> r) await SilAsync(r.Kayit); else if (param is KasaHareketi h) await SilAsync(h); });
        YenileKomutu = new KomutSarici(async () => await VerileriYukleAsync());
        DokumYazdirKomutu = new KomutSarici(async () =>
        {
            if (_yazdirmaServisi == null || _ekstre == null) return;
            var gunSonu = BaslangicTarihi?.Date == BitisTarihi?.Date && BaslangicTarihi != null;
            await _yazdirmaServisi.HesapEkstresiYazdirAsync(new HesapEkstresiBelgesi(
                gunSonu ? "Gün Sonu Kasa Raporu" : "Kasa Ekstresi",
                SeciliKasa?.Ad ?? "Tüm Kasalar", BaslangicTarihi, BitisTarihi, _ekstre.Devir,
                Hareketler.Select(r => new HesapEkstresiSatiri(r.Kayit.Tarih, r.Kayit.FisNo ?? r.Kayit.MakbuzNo ?? "", FinansMetinDonusturucu.Metin(r.Kayit.FisTuru),
                    r.Kayit.CariKart?.Unvan ?? "", r.Kayit.Aciklama, r.Giris, r.Cikis, r.Bakiye)).ToList(),
                Hareketler.Sum(r => r.Giris), Hareketler.Sum(r => r.Cikis), _ekstre.Kapanis));
        });

        _ = IlkYuklemeAsync();
    }

    private void GuncelleSayimToplam()
    {
        BildirimGonder(nameof(SayimFizikiToplam));
        BildirimGonder(nameof(SayimFark));
    }

    private readonly SemaphoreSlim _yuklemeKilidi = new(1, 1);

    private async Task IlkYuklemeAsync()
    {
        await _yuklemeKilidi.WaitAsync();
        Yukleniyor = true;
        try
        {
            var kList = await _kasaServisi.KasalariGetirAsync();
            Kasalar = new ObservableCollection<Kasa>(kList);
            _seciliKasa = Kasalar.FirstOrDefault();
            BildirimGonder(nameof(SeciliKasa));
            ParaBirimiBildir();
            _ = KurOnerAsync();

            if (_cariServisi != null)
            {
                var cList = await _cariServisi.TumunuGetirAsync();
                CariKartlar = new ObservableCollection<CariKart>(cList);
            }

            if (_bankaServisi != null)
            {
                var bList = await _bankaServisi.BankaHesaplariGetirAsync();
                BankaHesaplari = new ObservableCollection<BankaHesabi>(bList);
                SeciliTargetBankaHesabi = BankaHesaplari.FirstOrDefault();
            }

            await VerileriYukleKilitsizAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Kasa verileri yüklenirken hata: {ex.Message}";
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
            HataMesaji = $"Kasa verileri yüklenirken hata: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
            _yuklemeKilidi.Release();
        }
    }

    private async Task VerileriYukleKilitsizAsync()
    {
        List<KasaHareketi> liste;
        if (SeciliKasa != null)
        {
            liste = await _kasaServisi.KasaHareketleriGetirAsync(SeciliKasa.Id);
            ToplamBakiye = SeciliKasaDovizli
                ? await _kasaServisi.KasaDovizBakiyeHesaplaAsync(SeciliKasa.Id)
                : await _kasaServisi.KasaBakiyeHesaplaAsync(SeciliKasa.Id);
        }
        else
        {
            liste = await _kasaServisi.TumunuGetirAsync();
            ToplamBakiye = await _kasaServisi.BakiyeHesaplaAsync();
        }

        _tumHareketler = liste;
        Filtrele();

        BildirimGonder(nameof(BakiyePositif));
        GuncelleSayimToplam();

        var ayBaslangic = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        BuAyGelir = liste.Where(h => h.IslemDurumu == FinansIslemDurumu.Tamamlandi && h.Tarih >= ayBaslangic && h.Tur == HareketTuru.Gelir).Sum(HesapTutari);
        BuAyGider = liste.Where(h => h.IslemDurumu == FinansIslemDurumu.Tamamlandi && h.Tarih >= ayBaslangic && h.Tur == HareketTuru.Gider).Sum(HesapTutari);

        DurumMesaji = $"{liste.Count} kasa hareketi listeleniyor.";
    }

    // Dövizli kasa seçiliyse ekstre ve özetler kasanın para birimindedir.
    private decimal HesapTutari(KasaHareketi h) => SeciliKasaDovizli ? h.DovizTutar : h.Tutar;

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
                (r.Kayit.MakbuzNo?.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (r.Kayit.CariKart?.Unvan.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ?? false)).ToList();
        Hareketler = new ObservableCollection<HesapEkstresi.Satir<KasaHareketi>>(satirlar);
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
        if (string.IsNullOrWhiteSpace(YeniAciklama)) { HataMesaji = "Açıklama boş bırakılamaz."; return; }

        if (SeciliKasa == null) { HataMesaji = "İşlem yapılacak kasayı seçiniz."; return; }
        if (SeciliKasaDovizli && YeniKur <= 0) { HataMesaji = $"{ParaSimgesi} kurunu giriniz."; return; }
        if (SeciliFisTuru is KasaFisTuru.Tahsilat or KasaFisTuru.Odeme && SeciliCariKart == null)
        {
            HataMesaji = "Cari tahsilat / ödeme için cari kart seçiniz.";
            return;
        }

        var kasaId = SeciliKasa.Id;
        // Dövizli kasada servis TL tutarı döviz × kurdan hesaplar.
        var (tutar, dovizTutar, kur) = SeciliKasaDovizli ? (0m, YeniTutar, YeniKur) : (YeniTutar, 0m, 0m);
        var tutarMetni = SeciliKasaDovizli ? $"{YeniTutar:N2} {ParaSimgesi} ({YeniTlKarsiligi:N2} ₺)" : $"{YeniTutar:N2} ₺";

        try
        {
            if (SeciliFisTuru == KasaFisTuru.Tahsilat && SeciliCariKart != null)
            {
                await _kasaServisi.TahsilatEkleAsync(kasaId, SeciliCariKart.Id, tutar, YeniAciklama, YeniMakbuzNo, YeniTarih, dovizTutar, kur);
                BasariMesaji = $"✅ Cari Tahsilat kaydedildi: {tutarMetni} ({SeciliCariKart.Unvan})";
            }
            else if (SeciliFisTuru == KasaFisTuru.Odeme && SeciliCariKart != null)
            {
                await _kasaServisi.OdemeEkleAsync(kasaId, SeciliCariKart.Id, tutar, YeniAciklama, YeniMakbuzNo, YeniTarih, dovizTutar, kur);
                BasariMesaji = $"✅ Cari Ödeme kaydedildi: {tutarMetni} ({SeciliCariKart.Unvan})";
            }
            else
            {
                var tur = (SeciliFisTuru == KasaFisTuru.Tahsilat || SeciliFisTuru == KasaFisTuru.Gelir || SeciliFisTuru == KasaFisTuru.AcilisDevir)
                    ? HareketTuru.Gelir
                    : HareketTuru.Gider;

                var hareket = new KasaHareketi
                {
                    KasaId = kasaId,
                    Tarih = YeniTarih,
                    Tur = tur,
                    FisTuru = SeciliFisTuru,
                    Tutar = tutar,
                    DovizTutar = dovizTutar,
                    Kur = kur,
                    Aciklama = YeniAciklama,
                    MakbuzNo = YeniMakbuzNo,
                    MasrafKodu = YeniMasrafKodu
                };

                await _kasaServisi.EkleAsync(hareket);
                BasariMesaji = $"✅ {SeciliFisTuru} fişi kaydedildi: {tutarMetni}";
            }

            YeniTutar = 0;
            YeniAciklama = string.Empty;
            YeniMakbuzNo = string.Empty;

            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"İşlem kaydedilemedi: {ex.Message}";
        }
    }

    private async Task KasaVirmanKaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (SeciliKasa == null || SeciliTargetKasa == null) { HataMesaji = "Kaynak ve Hedef kasa seçiniz."; return; }
        if (VirmanTutari <= 0) { HataMesaji = "Geçerli virman tutarı giriniz."; return; }

        try
        {
            await _kasaServisi.KasaVirmanYapAsync(SeciliKasa.Id, SeciliTargetKasa.Id, VirmanTutari, VirmanAciklama, DateTime.Today);
            BasariMesaji = $"✅ Virman tamamlandı: {VirmanTutari:N2} {ParaSimgesi} ({SeciliKasa.Ad} -> {SeciliTargetKasa.Ad})";
            VirmanTutari = 0;
            VirmanAciklama = string.Empty;

            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Virman başarısız: {ex.Message}";
        }
    }

    private async Task BankayaYatirKaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (SeciliKasa == null || SeciliTargetBankaHesabi == null) { HataMesaji = "Kasa ve Banka Hesabı seçiniz."; return; }
        if (VirmanTutari <= 0) { HataMesaji = "Geçerli yatırma tutarı giriniz."; return; }

        try
        {
            await _kasaServisi.BankayaParaYatirAsync(SeciliKasa.Id, SeciliTargetBankaHesabi.Id, VirmanTutari, VirmanAciklama, DateTime.Today);
            BasariMesaji = $"✅ Bankaya yatırma kaydedildi: {VirmanTutari:N2} {ParaSimgesi} ({SeciliTargetBankaHesabi.Ad})";
            VirmanTutari = 0;
            VirmanAciklama = string.Empty;

            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Bankaya yatırma başarısız: {ex.Message}";
        }
    }

    private async Task MutabakatKaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (SeciliKasa == null) { HataMesaji = "Kasa seçiniz."; return; }

        try
        {
            var m = new KasaSayimMutabakat
            {
                KasaId = SeciliKasa.Id,
                Tarih = DateTime.Today,
                Adet200 = Adet200,
                Adet100 = Adet100,
                Adet50 = Adet50,
                Adet20 = Adet20,
                Adet10 = Adet10,
                Adet5 = Adet5,
                BozukParaToplam = BozukParaToplam,
                Aciklama = MutabakatAciklama
            };

            await _kasaServisi.KasaSayimMutabakatKaydetAsync(m);
            BasariMesaji = $"✅ Kasa Mutabakatı kaydedildi. (Fark: {m.Fark:N2} ₺)";

            Adet200 = 0; Adet100 = 0; Adet50 = 0; Adet20 = 0; Adet10 = 0; Adet5 = 0; BozukParaToplam = 0;
            MutabakatAciklama = string.Empty;

            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Mutabakat kaydedilemedi: {ex.Message}";
        }
    }

    private async Task KasaEkleAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (string.IsNullOrWhiteSpace(YeniKasaAd)) { HataMesaji = "Kasa adı boş girilemez."; return; }

        try
        {
            var k = new Kasa
            {
                Kod = string.IsNullOrWhiteSpace(YeniKasaKodu) ? $"KSA-0{Kasalar.Count + 1}" : YeniKasaKodu.Trim(),
                Ad = YeniKasaAd.Trim(),
                MuhasebeKodu = string.IsNullOrWhiteSpace(YeniKasaMuhasebeKodu) ? "100.01.001" : YeniKasaMuhasebeKodu.Trim(),
                ParaBirimi = string.IsNullOrWhiteSpace(YeniKasaParaBirimi) ? DovizKuruYardimcisi.Tl : YeniKasaParaBirimi,
                AktifMi = true
            };

            await _kasaServisi.KasaEkleAsync(k);
            BasariMesaji = $"✅ Yeni kasa eklendi: {k.Ad}";

            YeniKasaKodu = string.Empty;
            YeniKasaAd = string.Empty;
            YeniKasaParaBirimi = DovizKuruYardimcisi.Tl;

            var list = await _kasaServisi.KasalariGetirAsync();
            Kasalar = new ObservableCollection<Kasa>(list);
            SeciliKasa = k;
        }
        catch (Exception ex)
        {
            HataMesaji = $"Kasa eklenemedi: {ex.Message}";
        }
    }

    private static string HareketTutarMetni(KasaHareketi h) =>
        DovizKuruYardimcisi.DovizliMi(h.Kasa?.ParaBirimi) ? $"{h.DovizTutar:N2} {h.Kasa!.ParaBirimi}" : $"{h.Tutar:N2} ₺";

    private async Task SilAsync(KasaHareketi hareket)
    {
        var onay = System.Windows.MessageBox.Show(
            $"{hareket.Aciklama} — {HareketTutarMetni(hareket)} kaydını silmek istiyor musunuz?",
            "Silme Onayı",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (onay != System.Windows.MessageBoxResult.Yes) return;

        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;
        try
        {
            await _kasaServisi.SilAsync(hareket.Id);
            BasariMesaji = "✅ Hareket silindi.";
            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Hareket silinemedi: {ex.Message}";
        }
    }
}

