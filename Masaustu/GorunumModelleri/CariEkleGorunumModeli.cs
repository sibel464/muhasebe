using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Collections.ObjectModel;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using Masaustu.Servisler;

namespace Masaustu.GorunumModelleri;

public class CariEkleGorunumModeli : TemelGorunumModeli
{
    private readonly ICariServisi _cariServisi;
    private readonly NavigasyonServisi _navigasyon;
    private readonly ICariTipiTanimiServisi _cariTipiServisi;

    private string _kod = string.Empty;
    private string _grupKodu = string.Empty;
    private CariTipiTanimi? _seciliCariTipi;
    private string _unvan = string.Empty;
    private string _ad = string.Empty;
    private string _soyad = string.Empty;
    private string _vergiDairesi = string.Empty;
    private string _vergiNo = string.Empty;
    private string _tcNo = string.Empty;
    private string _telefon = string.Empty;
    private string _cepTelefonu = string.Empty;
    private string _evTelefonu = string.Empty;
    private string _isTelefonu = string.Empty;
    private string _eposta = string.Empty;
    private string _webSitesi = string.Empty;
    private string _adres = string.Empty;
    private string _il = string.Empty;
    private string _ilce = string.Empty;
    private string _postaKodu = string.Empty;
    private string _aciklama = string.Empty;
    private bool _aktif = true;
    private CariTuru _cariTuru = CariTuru.Musteri;
    private string _kodlama = "Otomatik";
    private string _grupKodlama = "Otomatik";
    private string _durumMesaji = string.Empty;
    private bool _durumBasarili;

    private string _hataMesaji = string.Empty;
    private string _basariMesaji = string.Empty;
    private bool _kaydediliyor = false;

    public string Kod { get => _kod; set { if (OzellikAyarla(ref _kod, value) && KodManuel) _ = ManuelKodDogrulaAsync(); } }
    public string GrupKodu { get => _grupKodu; set => OzellikAyarla(ref _grupKodu, value); }
    public ObservableCollection<string> GrupKodlari { get; } = new();
    public ObservableCollection<string> CariKodlari { get; } = new();
    public IReadOnlyList<string> KodlamaSecenekleri { get; } = ["Otomatik", "Manuel"];
    public string Kodlama { get => _kodlama; set { if (OzellikAyarla(ref _kodlama, value)) { BildirimGonder(nameof(KodManuel)); BildirimGonder(nameof(KodSaltOkunur)); BildirimGonder(nameof(KodYardimMetni)); if (KodManuel) Kod = string.Empty; else _ = KodOnizlemesiniYukleAsync(); } } }
    public bool KodManuel => Kodlama == "Manuel";
    public bool KodSaltOkunur => !KodManuel;
    public string KodYardimMetni => KodManuel ? "Kendi cari kodunuzu girebilirsiniz." : "Otomatik oluşturulacak; eşzamanlı kayıtta değişebilir.";
    public string GrupKodlama { get => _grupKodlama; set { if (OzellikAyarla(ref _grupKodlama, value)) { BildirimGonder(nameof(GrupKoduManuel)); BildirimGonder(nameof(GrupKoduSaltOkunur)); BildirimGonder(nameof(GrupKoduYardimMetni)); if (GrupKoduManuel) GrupKodu = string.Empty; else _ = GrupKoduOnizlemesiniYukleAsync(); } } }
    public bool GrupKoduManuel => GrupKodlama == "Manuel";
    public bool GrupKoduSaltOkunur => !GrupKoduManuel;
    public string GrupKoduYardimMetni => GrupKoduManuel ? "Kendi kodunuzu girebilir veya mevcut kodlardan seçebilirsiniz." : "Otomatik oluşturulacak; eşzamanlı kayıtta değişebilir.";
    public ObservableCollection<CariTipiTanimi> CariTipleri { get; } = new();
    public IReadOnlyList<KeyValuePair<CariTuru, string>> CariTurleri { get; } =
    [new(CariTuru.Musteri, "Müşteri"), new(CariTuru.Tedarikci, "Tedarikçi"), new(CariTuru.MusteriTedarikci, "Müşteri ve Tedarikçi")];
    public CariTuru SeciliCariTuru { get => _cariTuru; set => OzellikAyarla(ref _cariTuru, value); }
    public CariTipiTanimi? SeciliCariTipi
    {
        get => _seciliCariTipi;
        set
        {
            if (OzellikAyarla(ref _seciliCariTipi, value))
            {
                BildirimGonder(nameof(IsKurumsal));
                BildirimGonder(nameof(IsBireysel));
            }
        }
    }
    public bool IsKurumsal => SeciliCariTipi?.Kod == "KURUMSAL";
    public bool IsBireysel => SeciliCariTipi?.Kod == "BIREYSEL";

    public string Unvan { get => _unvan; set => OzellikAyarla(ref _unvan, value); }
    public string Ad { get => _ad; set => OzellikAyarla(ref _ad, value); }
    public string Soyad { get => _soyad; set => OzellikAyarla(ref _soyad, value); }
    public string VergiDairesi { get => _vergiDairesi; set => OzellikAyarla(ref _vergiDairesi, value); }
    public string VergiNo { get => _vergiNo; set => OzellikAyarla(ref _vergiNo, value); }
    public string TcNo { get => _tcNo; set => OzellikAyarla(ref _tcNo, value); }
    public string Telefon { get => _telefon; set => OzellikAyarla(ref _telefon, value); }
    public string CepTelefonu { get => _cepTelefonu; set => OzellikAyarla(ref _cepTelefonu, value); }
    public string EvTelefonu { get => _evTelefonu; set => OzellikAyarla(ref _evTelefonu, value); }
    public string IsTelefonu { get => _isTelefonu; set => OzellikAyarla(ref _isTelefonu, value); }
    public string Eposta { get => _eposta; set => OzellikAyarla(ref _eposta, value); }
    public string WebSitesi { get => _webSitesi; set => OzellikAyarla(ref _webSitesi, value); }
    public string Adres { get => _adres; set => OzellikAyarla(ref _adres, value); }
    public string Il { get => _il; set => OzellikAyarla(ref _il, value); }
    public string Ilce { get => _ilce; set => OzellikAyarla(ref _ilce, value); }
    public string PostaKodu { get => _postaKodu; set => OzellikAyarla(ref _postaKodu, value); }
    public string Aciklama { get => _aciklama; set => OzellikAyarla(ref _aciklama, value); }
    public bool Aktif { get => _aktif; set => OzellikAyarla(ref _aktif, value); }

    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }
    public string BasariMesaji { get => _basariMesaji; set => OzellikAyarla(ref _basariMesaji, value); }
    public bool Kaydediliyor { get => _kaydediliyor; set { if (OzellikAyarla(ref _kaydediliyor, value)) BildirimGonder(nameof(KaydetDugmeMetni)); } }
    public string KaydetDugmeMetni => Kaydediliyor ? "Kaydediliyor..." : "Cariyi Kaydet";
    public string DurumMesaji { get => _durumMesaji; set { if (OzellikAyarla(ref _durumMesaji, value)) BildirimGonder(nameof(DurumGorunur)); } }
    public bool DurumGorunur => !string.IsNullOrWhiteSpace(DurumMesaji);
    public bool DurumBasarili { get => _durumBasarili; set => OzellikAyarla(ref _durumBasarili, value); }

    public ICommand KaydetKomutu { get; }
    public ICommand FormuTemizleKomutu { get; }
    public ICommand CariKoduEkleKomutu { get; }
    public ICommand GrupKoduEkleKomutu { get; }

    public CariEkleGorunumModeli(ICariServisi cariServisi, ICariTipiTanimiServisi cariTipiServisi, NavigasyonServisi navigasyon)
    {
        _cariServisi = cariServisi;
        _navigasyon = navigasyon;
        _cariTipiServisi = cariTipiServisi;
        
        KaydetKomutu = new KomutSarici(async () => await KaydetAsync(), () => !Kaydediliyor);
        FormuTemizleKomutu = new KomutSarici(FormuTemizle);
        CariKoduEkleKomutu = new KomutSarici(() => Kodlama = "Manuel");
        GrupKoduEkleKomutu = new KomutSarici(() => GrupKodlama = "Manuel");
        _ = IlkVerileriYukleAsync();
    }

    private async Task IlkVerileriYukleAsync()
    {
        await CariTipleriniYukleAsync();
        GrupKodlari.Clear();
        foreach (var grup in await _cariServisi.GrupKodlariniGetirAsync()) GrupKodlari.Add(grup);
        CariKodlari.Clear(); foreach (var kod in await _cariServisi.CariKodlariniGetirAsync()) CariKodlari.Add(kod);
        await KodOnizlemesiniYukleAsync();
        await GrupKoduOnizlemesiniYukleAsync();
    }

    private async Task KodOnizlemesiniYukleAsync()
    {
        if (!KodManuel) try { Kod = await _cariServisi.OtomatikKodOnizleAsync(); } catch { Kod = "Otomatik atanacak"; }
    }

    private async Task GrupKoduOnizlemesiniYukleAsync()
    { if (!GrupKoduManuel) try { GrupKodu = await _cariServisi.OtomatikGrupKoduOnizleAsync(); } catch { GrupKodu = "Otomatik atanacak"; } }

    private async Task ManuelKodDogrulaAsync()
    {
        TemizleHata(nameof(Kod));
        if (string.IsNullOrWhiteSpace(Kod)) { EkleHata("Cari kodu boş bırakılamaz.", nameof(Kod)); return; }
        if (await _cariServisi.KodMevcutMuAsync(Kod)) EkleHata("Bu cari kodu zaten kullanılıyor.", nameof(Kod));
    }

    public async Task CariTipleriniYukleAsync(int? secilecekId = null)
    {
        var liste = await _cariTipiServisi.ListeleAsync(); CariTipleri.Clear(); foreach (var x in liste) CariTipleri.Add(x);
        SeciliCariTipi = CariTipleri.FirstOrDefault(x => x.Id == (secilecekId ?? CariTipiTanimi.KurumsalId)) ?? CariTipleri.FirstOrDefault();
    }

    private async Task KaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;
        DurumMesaji = string.Empty;
        TemizleHata(nameof(Kod)); TemizleHata(nameof(Ad)); TemizleHata(nameof(Soyad));
        if (KodManuel) await ManuelKodDogrulaAsync();
        // Ad/soyad yalnızca şahıs (bireysel) carilerde zorunlu; firmada ünvan yeterli.
        var unvanYok = string.IsNullOrWhiteSpace(Unvan);
        if (IsBireysel || (!IsKurumsal && unvanYok))
        {
            if (string.IsNullOrWhiteSpace(Ad)) EkleHata("Ad boş bırakılamaz.", nameof(Ad));
            if (string.IsNullOrWhiteSpace(Soyad)) EkleHata("Soyad boş bırakılamaz.", nameof(Soyad));
        }
        if (HasErrors) { DurumBasarili = false; DurumMesaji = "✕ Cari kaydedilemedi. Formdaki alanları kontrol edin."; return; }
        
        if (string.IsNullOrWhiteSpace(Kod)) { HataMesaji = "Lütfen cari kodu giriniz."; return; }
        if (IsKurumsal && string.IsNullOrWhiteSpace(Unvan)) { HataMesaji = "Lütfen ünvan giriniz."; return; }
        if (IsBireysel && (string.IsNullOrWhiteSpace(Ad) || string.IsNullOrWhiteSpace(Soyad))) { HataMesaji = "Lütfen ad ve soyad giriniz."; return; }

        // TCKN Validasyonu
        if (IsBireysel && !string.IsNullOrWhiteSpace(TcNo) && !TcNoDogrula(TcNo))
        {
            HataMesaji = "Geçersiz T.C. Kimlik Numarası girdiniz."; return;
        }

        // VKN Validasyonu
        if (IsKurumsal && !string.IsNullOrWhiteSpace(VergiNo) && !VknDogrula(VergiNo))
        {
            HataMesaji = "Geçersiz Vergi Numarası girdiniz."; return;
        }

        // E-Posta Validasyonu
        if (!string.IsNullOrWhiteSpace(Eposta) && !Regex.IsMatch(Eposta, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            HataMesaji = "Geçersiz E-Posta adresi formatı."; return;
        }

        Kaydediliyor = true;
        DurumBasarili = false;
        DurumMesaji = "⟳ Cari kaydediliyor...";
        ((KomutSarici)KaydetKomutu).BildirimGonder();

        try
        {
            var cari = new CariKart
            {
                Kod = Kod, GrupKodu = GrupKodu, Tur = SeciliCariTuru, CariTipiId = SeciliCariTipi?.Id ?? 0,
                Unvan = string.IsNullOrWhiteSpace(Unvan) ? $"{Ad} {Soyad}".Trim() : Unvan,
                Ad = Ad, Soyad = Soyad, VergiDairesi = VergiDairesi,
                VergiNo = VergiNo, TcNo = TcNo, Telefon = Telefon,
                CepTelefonu = CepTelefonu, EvTelefonu = EvTelefonu, IsTelefonu = IsTelefonu, 
                Eposta = Eposta, WebSitesi = WebSitesi,
                Adres = Adres, Il = Il, Ilce = Ilce, PostaKodu = PostaKodu,
                Aciklama = Aciklama, Aktif = Aktif
                // Not: FotografYolu UI'dan binding yapilacak sonraki asamada eklenebilir.
            };

            cari = KodManuel && GrupKoduManuel ? await _cariServisi.EkleAsync(cari) : await _cariServisi.OtomatikKodlaEkleAsync(cari, !GrupKoduManuel, !KodManuel);
            BasariMesaji = $"{cari.Unvan} başarıyla sisteme eklendi!";
            FormuTemizle();
            DurumBasarili = true;
            DurumMesaji = $"✓ Cari başarıyla kaydedildi.\nCari Kodu: {cari.Kod}\nGrup Kodu: {cari.GrupKodu}";
            System.Windows.MessageBox.Show(DurumMesaji, "Kayıt başarılı", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            GrupKodlari.Clear(); foreach (var grup in await _cariServisi.GrupKodlariniGetirAsync()) GrupKodlari.Add(grup);
            await KodOnizlemesiniYukleAsync();
            await GrupKoduOnizlemesiniYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Kayıt hatası: {ex.Message}";
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(HataMesaji)) { DurumBasarili = false; DurumMesaji = "✕ Cari kaydedilemedi. Sebep: " + (HataMesaji.Contains("zaten", StringComparison.OrdinalIgnoreCase) ? "Bu cari kodu zaten kullanılıyor." : "Girilen bilgileri ve bağlantıyı kontrol edin."); }
            Kaydediliyor = false;
            ((KomutSarici)KaydetKomutu).BildirimGonder();
        }
    }

    private bool TcNoDogrula(string tcNo)
    {
        if (tcNo.Length != 11 || !tcNo.All(char.IsDigit) || tcNo[0] == '0') return false;
        int[] digits = tcNo.Select(c => int.Parse(c.ToString())).ToArray();
        int sumOdd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        int sumEven = digits[1] + digits[3] + digits[5] + digits[7];
        int c10 = (sumOdd * 7 - sumEven) % 10;
        int c11 = (sumOdd + sumEven + digits[9]) % 10;
        return digits[9] == c10 && digits[10] == c11;
    }

    private bool VknDogrula(string vkn)
    {
        if (vkn.Length != 10 || !vkn.All(char.IsDigit)) return false;
        int[] digits = vkn.Select(c => int.Parse(c.ToString())).ToArray();
        int sum = 0;
        for (int i = 0; i < 9; i++)
        {
            int tmp = (digits[i] + (10 - (i + 1))) % 10;
            if (tmp == 9) sum += tmp;
            else sum += (tmp * (int)Math.Pow(2, 10 - (i + 1))) % 9;
        }
        int lastDigit = (10 - (sum % 10)) % 10;
        return digits[9] == lastDigit;
    }

    private void FormuTemizle()
    {
        if (KodManuel) Kod = string.Empty;
        GrupKodu = string.Empty;
        Unvan = string.Empty;
        Ad = string.Empty;
        Soyad = string.Empty;
        VergiNo = string.Empty;
        TcNo = string.Empty;
        CepTelefonu = string.Empty;
        IsTelefonu = string.Empty;
        EvTelefonu = string.Empty;
        Eposta = string.Empty;
        Il = string.Empty;
        Ilce = string.Empty;
        Adres = string.Empty;
        PostaKodu = string.Empty;
    }
}
