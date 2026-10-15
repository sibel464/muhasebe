using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Collections.ObjectModel;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class MusteriDuzenleGorunumModeli : TemelGorunumModeli
{
    private readonly ICariServisi _cariServisi;
    private readonly ICariTipiTanimiServisi _cariTipiServisi;
    private readonly CariKart? _mevcutCari;

    private string _kod = string.Empty;
    private string? _grupKodu;
    private CariTipiTanimi? _seciliCariTipi;
    private string _unvan = string.Empty;
    private string? _ad;
    private string? _soyad;
    private string? _vergiDairesi;
    private string? _vergiNo;
    private string? _tcNo;
    private string? _telefon;
    private string? _cepTelefonu;
    private string? _evTelefonu;
    private string? _isTelefonu;
    private string? _eposta;
    private string? _webSitesi;
    private string? _adres;
    private string? _il;
    private string? _ilce;
    private string? _postaKodu;
    private string? _aciklama;
    private bool _aktif = true;
    private CariTuru _cariTuru = CariTuru.Musteri;
    private decimal? _riskLimiti;
    private int? _vadeGunu;
    private string? _iban;

    private string _hataMesaji = string.Empty;
    private bool _kaydediliyor = false;

    public bool YeniKayit => _mevcutCari == null;
    public string PencereBasligi => YeniKayit ? "Yeni Cari Kart" : "Cari Kartı Düzenle";

    public string Kod 
    { 
        get => _kod; 
        set 
        { 
            if (OzellikAyarla(ref _kod, value))
            {
                DogrulaKod(value);
                _ = DogrulaKodBenzersizligiAsync(value);
            }
        } 
    }

    public ObservableCollection<CariTipiTanimi> CariTipleri { get; } = new();
    public IReadOnlyList<KeyValuePair<CariTuru, string>> CariTurleri { get; } = [new(CariTuru.Musteri, "Müşteri"), new(CariTuru.Tedarikci, "Tedarikçi"), new(CariTuru.MusteriTedarikci, "Müşteri ve Tedarikçi")];
    public CariTuru SeciliCariTuru { get => _cariTuru; set => OzellikAyarla(ref _cariTuru, value); }
    public ObservableCollection<string> GrupKodlari { get; } = new();
    public CariTipiTanimi? SeciliCariTipi
    { 
        get => _seciliCariTipi;
        set 
        { 
            if (OzellikAyarla(ref _seciliCariTipi, value))
            {
                // Tip değiştiğinde ilgili vergi no / tc no kontrollerini tetikle
                DogrulaVergiNo(VergiNo);
                DogrulaTcNo(TcNo);
                BildirimGonder(nameof(IsKurumsal));
                BildirimGonder(nameof(IsBireysel));
            }
        } 
    }

    public bool IsKurumsal => SeciliCariTipi?.Kod == "KURUMSAL";
    public bool IsBireysel => SeciliCariTipi?.Kod == "BIREYSEL";

    public string Unvan 
    { 
        get => _unvan; 
        set 
        { 
            if (OzellikAyarla(ref _unvan, value))
            {
                DogrulaUnvan(value);
            }
        } 
    }

    public string? Ad 
    { 
        get => _ad; 
        set 
        { 
            if (OzellikAyarla(ref _ad, value))
            {
                // Bireysel ise Ad Soyad'ı otomatik Unvan olarak birleştir
                if (IsBireysel)
                {
                    Unvan = $"{Ad} {Soyad}".Trim();
                }
            }
        } 
    }

    public string? Soyad 
    { 
        get => _soyad; 
        set 
        { 
            if (OzellikAyarla(ref _soyad, value))
            {
                // Bireysel ise Ad Soyad'ı otomatik Unvan olarak birleştir
                if (IsBireysel)
                {
                    Unvan = $"{Ad} {Soyad}".Trim();
                }
            }
        } 
    }

    public string? VergiDairesi { get => _vergiDairesi; set => OzellikAyarla(ref _vergiDairesi, value); }
    
    public string? VergiNo 
    { 
        get => _vergiNo; 
        set 
        { 
            if (OzellikAyarla(ref _vergiNo, value))
            {
                DogrulaVergiNo(value);
            }
        } 
    }

    public string? TcNo 
    { 
        get => _tcNo; 
        set 
        { 
            if (OzellikAyarla(ref _tcNo, value))
            {
                DogrulaTcNo(value);
            }
        } 
    }

    public string? Telefon 
    { 
        get => _telefon; 
        set 
        { 
            if (OzellikAyarla(ref _telefon, value))
            {
                DogrulaTelefon(value);
            }
        } 
    }

    public string? CepTelefonu 
    { 
        get => _cepTelefonu; 
        set 
        { 
            if (OzellikAyarla(ref _cepTelefonu, value))
            {
                DogrulaTelefon(value, nameof(CepTelefonu));
            }
        } 
    }

    public string? EvTelefonu 
    { 
        get => _evTelefonu; 
        set 
        { 
            if (OzellikAyarla(ref _evTelefonu, value))
            {
                DogrulaTelefon(value, nameof(EvTelefonu));
            }
        } 
    }

    public string? IsTelefonu 
    { 
        get => _isTelefonu; 
        set 
        { 
            if (OzellikAyarla(ref _isTelefonu, value))
            {
                DogrulaTelefon(value, nameof(IsTelefonu));
            }
        } 
    }

    public string? GrupKodu { get => _grupKodu; set => OzellikAyarla(ref _grupKodu, value); }
    public string? Eposta 
    { 
        get => _eposta; 
        set 
        { 
            if (OzellikAyarla(ref _eposta, value))
            {
                DogrulaEposta(value);
            }
        } 
    }

    public string? WebSitesi { get => _webSitesi; set => OzellikAyarla(ref _webSitesi, value); }
    public string? Adres { get => _adres; set => OzellikAyarla(ref _adres, value); }
    public string? Il { get => _il; set => OzellikAyarla(ref _il, value); }
    public string? Ilce { get => _ilce; set => OzellikAyarla(ref _ilce, value); }
    public string? PostaKodu { get => _postaKodu; set => OzellikAyarla(ref _postaKodu, value); }
    public string? Aciklama { get => _aciklama; set => OzellikAyarla(ref _aciklama, value); }
    public bool Aktif { get => _aktif; set => OzellikAyarla(ref _aktif, value); }

    // Risk limiti: carinin açık bakiyesi bu tutarı aşınca satışta uyarı verilir (boş = sınırsız).
    public decimal? RiskLimiti
    {
        get => _riskLimiti;
        set
        {
            if (!OzellikAyarla(ref _riskLimiti, value)) return;
            TemizleHata(nameof(RiskLimiti));
            if (value < 0) EkleHata("Risk limiti negatif olamaz.", nameof(RiskLimiti));
        }
    }

    public int? VadeGunu
    {
        get => _vadeGunu;
        set
        {
            if (!OzellikAyarla(ref _vadeGunu, value)) return;
            TemizleHata(nameof(VadeGunu));
            if (value is < 0 or > 365) EkleHata("Vade günü 0 ile 365 arasında olmalıdır.", nameof(VadeGunu));
        }
    }

    public string? Iban
    {
        get => _iban;
        set
        {
            if (!OzellikAyarla(ref _iban, value)) return;
            TemizleHata(nameof(Iban));
            if (!string.IsNullOrWhiteSpace(value) && !EtkiAlani.Yardimcilar.IbanDogrulayici.Gecerlidir(value, out _))
                EkleHata("Geçersiz IBAN. TR ile başlayan 26 karakterlik IBAN girin.", nameof(Iban));
        }
    }

    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }
    public bool Kaydediliyor { get => _kaydediliyor; set => OzellikAyarla(ref _kaydediliyor, value); }

    public ICommand KaydetKomutu { get; }
    public ICommand IptalKomutu { get; }

    public Action<bool>? PencereKapat { get; set; }

    public MusteriDuzenleGorunumModeli(ICariServisi cariServisi, ICariTipiTanimiServisi cariTipiServisi, CariKart? mevcutCari = null)
    {
        _cariServisi = cariServisi;
        _cariTipiServisi = cariTipiServisi;
        _mevcutCari = mevcutCari;

        if (mevcutCari != null)
        {
            _kod = mevcutCari.Kod;
            _grupKodu = mevcutCari.GrupKodu;
            _unvan = mevcutCari.Unvan;
            _ad = mevcutCari.Ad;
            _soyad = mevcutCari.Soyad;
            _vergiDairesi = mevcutCari.VergiDairesi;
            _vergiNo = mevcutCari.VergiNo;
            _tcNo = mevcutCari.TcNo;
            _telefon = mevcutCari.Telefon;
            _cepTelefonu = mevcutCari.CepTelefonu;
            _evTelefonu = mevcutCari.EvTelefonu;
            _isTelefonu = mevcutCari.IsTelefonu;
            _eposta = mevcutCari.Eposta;
            _webSitesi = mevcutCari.WebSitesi;
            _adres = mevcutCari.Adres;
            _il = mevcutCari.Il;
            _ilce = mevcutCari.Ilce;
            _postaKodu = mevcutCari.PostaKodu;
            _aciklama = mevcutCari.Aciklama;
            _aktif = mevcutCari.Aktif;
            _cariTuru = mevcutCari.Tur;
            _riskLimiti = mevcutCari.RiskLimiti;
            _vadeGunu = mevcutCari.VadeGunu;
            _iban = mevcutCari.Iban;
        }

        KaydetKomutu = new KomutSarici(async _ => await KaydetAsync(), _ => !Kaydediliyor && !HasErrors);
        IptalKomutu = new KomutSarici(_ => PencereKapat?.Invoke(false));
        CariTipleriniYenileKomutu = new KomutSarici(async _ => await CariTipleriniYukleAsync());
        _ = CariTipleriniYukleAsync();

        // İlk açılışta boş değerlerin validasyonu (yeni kayıtlarda)
        if (YeniKayit)
        {
            DogrulaKod(Kod);
            DogrulaUnvan(Unvan);
        }
    }

    public ICommand CariTipleriniYenileKomutu { get; }
    private async Task CariTipleriniYukleAsync()
    {
        var secilecekId = SeciliCariTipi?.Id ?? _mevcutCari?.CariTipiId ?? CariTipiTanimi.KurumsalId;
        var liste = await _cariTipiServisi.ListeleAsync(true, _mevcutCari?.CariTipiId);
        CariTipleri.Clear();
        foreach (var tip in liste) CariTipleri.Add(tip);
        SeciliCariTipi = CariTipleri.FirstOrDefault(x => x.Id == secilecekId) ?? CariTipleri.FirstOrDefault();
        GrupKodlari.Clear(); foreach (var grup in await _cariServisi.GrupKodlariniGetirAsync()) GrupKodlari.Add(grup);
    }
    public async Task CariTipiniSecAsync(int id) { await CariTipleriniYukleAsync(); SeciliCariTipi = CariTipleri.FirstOrDefault(x => x.Id == id) ?? SeciliCariTipi; }

    // Doğrulama Kuralları (Validation Rules)
    private void DogrulaKod(string value)
    {
        TemizleHata(nameof(Kod));
        if (string.IsNullOrWhiteSpace(value))
        {
            EkleHata("Cari kodu boş bırakılamaz.", nameof(Kod));
        }
    }

    private async Task DogrulaKodBenzersizligiAsync(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        try
        {
            var mevcut = await _cariServisi.KodMevcutMuAsync(value, _mevcutCari?.Id);
            if (mevcut)
            {
                EkleHata("Bu cari kodu zaten kullanılmaktadır.", nameof(Kod));
            }
        }
        catch
        {
            // DB kapalıysa kayıtta yakalanır
        }
        finally
        {
            (KaydetKomutu as KomutSarici)?.BildirimGonder();
        }
    }

    private void DogrulaUnvan(string value)
    {
        TemizleHata(nameof(Unvan));
        if (string.IsNullOrWhiteSpace(value))
        {
            EkleHata("Ünvan / Ad Soyad alanı boş bırakılamaz.", nameof(Unvan));
        }
    }

    private void DogrulaVergiNo(string? value)
    {
        TemizleHata(nameof(VergiNo));
        if (IsKurumsal && !string.IsNullOrWhiteSpace(value))
        {
            if (!EtkiAlani.Yardimcilar.KimlikNoDogrulayici.VergiNoGecerliMi(value))
            {
                EkleHata("Geçersiz vergi numarası (10 hane ve sağlama hanesi tutmalı).", nameof(VergiNo));
            }
        }
    }

    private void DogrulaTcNo(string? value)
    {
        TemizleHata(nameof(TcNo));
        if (IsBireysel && !string.IsNullOrWhiteSpace(value))
        {
            if (!EtkiAlani.Yardimcilar.KimlikNoDogrulayici.TcKimlikNoGecerliMi(value))
            {
                EkleHata("Geçersiz T.C. Kimlik numarası (11 hane ve sağlama haneleri tutmalı).", nameof(TcNo));
            }
        }
    }

    private void DogrulaTelefon(string? value, [System.Runtime.CompilerServices.CallerMemberName] string? propName = null)
    {
        if (string.IsNullOrEmpty(propName)) return;
        TemizleHata(propName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            // Basit telefon format kontrolü (sadece rakamlar ve +, -, (, ), boşluk)
            if (!Regex.IsMatch(value, @"^[+0-9\s()-\d]+$"))
            {
                EkleHata("Geçersiz telefon numarası formatı.", propName);
            }
        }
    }

    private void DogrulaEposta(string? value)
    {
        TemizleHata(nameof(Eposta));
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.Trim();
            if (!Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                EkleHata("Geçersiz e-posta adresi formatı.", nameof(Eposta));
            }
        }
    }

    private async Task KaydetAsync()
    {
        // Son kontroller
        DogrulaKod(Kod);
        DogrulaUnvan(Unvan);
        DogrulaVergiNo(VergiNo);
        DogrulaTcNo(TcNo);
        DogrulaEposta(Eposta);
        await DogrulaKodBenzersizligiAsync(Kod);

        if (HasErrors)
        {
            HataMesaji = "Lütfen formdaki hataları düzeltiniz.";
            return;
        }

        Kaydediliyor = true;
        HataMesaji = string.Empty;

        try
        {
            var cari = _mevcutCari != null
                ? new CariKart
                {
                    Id = _mevcutCari.Id,
                    OlusturmaTarihi = _mevcutCari.OlusturmaTarihi,
                    Kod = Kod, GrupKodu = GrupKodu, Tur = SeciliCariTuru, CariTipiId = SeciliCariTipi?.Id ?? 0, Unvan = Unvan,
                    Ad = Ad, Soyad = Soyad, VergiDairesi = VergiDairesi,
                    VergiNo = VergiNo, TcNo = TcNo, Telefon = Telefon,
                    CepTelefonu = CepTelefonu, EvTelefonu = EvTelefonu, IsTelefonu = IsTelefonu, Eposta = Eposta, WebSitesi = WebSitesi,
                    Adres = Adres, Il = Il, Ilce = Ilce, PostaKodu = PostaKodu,
                    Aciklama = Aciklama, Aktif = Aktif, RiskLimiti = RiskLimiti, VadeGunu = VadeGunu, Iban = Iban,
                    Borc = _mevcutCari.Borc, Alacak = _mevcutCari.Alacak
                }
                : new CariKart
                {
                    Kod = Kod, GrupKodu = GrupKodu, Tur = SeciliCariTuru, CariTipiId = SeciliCariTipi?.Id ?? 0, Unvan = Unvan,
                    Ad = Ad, Soyad = Soyad, VergiDairesi = VergiDairesi,
                    VergiNo = VergiNo, TcNo = TcNo, Telefon = Telefon,
                    CepTelefonu = CepTelefonu, EvTelefonu = EvTelefonu, IsTelefonu = IsTelefonu, Eposta = Eposta, WebSitesi = WebSitesi,
                    Adres = Adres, Il = Il, Ilce = Ilce, PostaKodu = PostaKodu,
                    Aciklama = Aciklama, Aktif = Aktif, RiskLimiti = RiskLimiti, VadeGunu = VadeGunu, Iban = Iban
                };

            if (_mevcutCari != null)
                await _cariServisi.GuncelleAsync(cari);
            else
                await _cariServisi.EkleAsync(cari);

            PencereKapat?.Invoke(true);
        }
        catch (Exception ex)
        {
            HataMesaji = $"Kayıt sırasında hata oluştu: {ex.Message}";
        }
        finally
        {
            Kaydediliyor = false;
        }
    }
}
