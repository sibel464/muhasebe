using System.Windows.Input;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class UrunDuzenleGorunumModeli : TemelGorunumModeli
{
    private readonly IUrunServisi _urunServisi;
    private readonly Urun? _mevcutUrun;

    private string _kod = string.Empty;
    private string _ad = string.Empty;
    private string _kategori = string.Empty;
    private string _birim = "Adet";
    private decimal _alisFiyati;
    private decimal _satisFiyati;
    private decimal _kdvOrani = 20;
    private decimal _stokMiktari;
    private decimal _minimumStok;
    private bool _aktif = true;
    private string _hataMesaji = string.Empty;
    private bool _kaydediliyor = false;

    public bool YeniKayit => _mevcutUrun == null;
    public string PencereBasligi => YeniKayit ? "Yeni Ürün Ekle" : "Ürün Düzenle";

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

    public string Ad 
    { 
        get => _ad; 
        set 
        { 
            if (OzellikAyarla(ref _ad, value))
            {
                DogrulaAd(value);
            }
        } 
    }

    public string Kategori { get => _kategori; set => OzellikAyarla(ref _kategori, value); }
    public string Birim { get => _birim; set => OzellikAyarla(ref _birim, value); }
    
    public decimal AlisFiyati 
    { 
        get => _alisFiyati; 
        set 
        { 
            if (OzellikAyarla(ref _alisFiyati, value))
            {
                DogrulaAlisFiyati(value);
            }
        } 
    }

    public decimal SatisFiyati 
    { 
        get => _satisFiyati; 
        set 
        { 
            if (OzellikAyarla(ref _satisFiyati, value))
            {
                DogrulaSatisFiyati(value);
            }
        } 
    }

    public decimal KdvOrani 
    { 
        get => _kdvOrani; 
        set 
        { 
            if (OzellikAyarla(ref _kdvOrani, value))
            {
                DogrulaKdvOrani(value);
            }
        } 
    }

    public decimal StokMiktari { get => _stokMiktari; set => OzellikAyarla(ref _stokMiktari, value); }
    public decimal MinimumStok { get => _minimumStok; set => OzellikAyarla(ref _minimumStok, value); }
    public bool Aktif { get => _aktif; set => OzellikAyarla(ref _aktif, value); }
    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }
    public bool Kaydediliyor { get => _kaydediliyor; set => OzellikAyarla(ref _kaydediliyor, value); }

    public List<string> BirimListesi { get; } = new() { "Adet", "Kg", "Litre", "Metre", "Kutu", "Paket", "Takım", "Set" };
    public List<decimal> KdvOranlari { get; } = new() { 0, 1, 10, 20 };

    public ICommand KaydetKomutu { get; }
    public ICommand IptalKomutu { get; }

    public Action<bool>? PencereKapat { get; set; }

    public UrunDuzenleGorunumModeli(IUrunServisi urunServisi, Urun? mevcutUrun = null)
    {
        _urunServisi = urunServisi;
        _mevcutUrun = mevcutUrun;

        if (mevcutUrun != null)
        {
            // Initial load without raising validation errors immediately
            _kod = mevcutUrun.Kod;
            _ad = mevcutUrun.Ad;
            _kategori = mevcutUrun.Kategori ?? string.Empty;
            _birim = mevcutUrun.Birim;
            _alisFiyati = mevcutUrun.AlisFiyati;
            _satisFiyati = mevcutUrun.SatisFiyati;
            _kdvOrani = mevcutUrun.KdvOrani;
            _stokMiktari = mevcutUrun.StokMiktari;
            _minimumStok = mevcutUrun.MinimumStok;
            _aktif = mevcutUrun.Aktif;
        }

        KaydetKomutu = new KomutSarici(async _ => await KaydetAsync(), _ => !Kaydediliyor && !HasErrors);
        IptalKomutu = new KomutSarici(_ => PencereKapat?.Invoke(false));

        // İlk açılışta boş değerlerin validasyonu (yeni kayıtlarda)
        if (YeniKayit)
        {
            DogrulaKod(Kod);
            DogrulaAd(Ad);
            DogrulaAlisFiyati(AlisFiyati);
            DogrulaSatisFiyati(SatisFiyati);
        }
    }

    // Doğrulama Kuralları (Sync Validation)
    private void DogrulaKod(string value)
    {
        TemizleHata(nameof(Kod));
        if (string.IsNullOrWhiteSpace(value))
        {
            EkleHata("Ürün kodu boş bırakılamaz.", nameof(Kod));
        }
    }

    private async Task DogrulaKodBenzersizligiAsync(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        try
        {
            var mevcut = await _urunServisi.KodMevcutMuAsync(value, _mevcutUrun?.Id);
            if (mevcut)
            {
                EkleHata("Bu ürün kodu zaten kullanılmaktadır.", nameof(Kod));
            }
        }
        catch
        {
            // Veritabanı hatası durumunda validasyon bloke olmasın, kayıtta yakalanacak
        }
        finally
        {
            (KaydetKomutu as KomutSarici)?.BildirimGonder();
        }
    }

    private void DogrulaAd(string value)
    {
        TemizleHata(nameof(Ad));
        if (string.IsNullOrWhiteSpace(value))
        {
            EkleHata("Ürün adı boş bırakılamaz.", nameof(Ad));
        }
    }

    private void DogrulaAlisFiyati(decimal value)
    {
        TemizleHata(nameof(AlisFiyati));
        if (value < 0)
        {
            EkleHata("Alış fiyatı negatif olamaz.", nameof(AlisFiyati));
        }
    }

    private void DogrulaSatisFiyati(decimal value)
    {
        TemizleHata(nameof(SatisFiyati));
        if (value <= 0)
        {
            EkleHata("Satış fiyatı sıfırdan büyük olmalıdır.", nameof(SatisFiyati));
        }
    }

    private void DogrulaKdvOrani(decimal value)
    {
        TemizleHata(nameof(KdvOrani));
        if (value < 0 || value > 100)
        {
            EkleHata("KDV oranı 0 ile 100 arasında olmalıdır.", nameof(KdvOrani));
        }
    }

    private async Task KaydetAsync()
    {
        // Son bir kez doğrulamaları zorla çalıştır
        DogrulaKod(Kod);
        DogrulaAd(Ad);
        DogrulaAlisFiyati(AlisFiyati);
        DogrulaSatisFiyati(SatisFiyati);
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
            var urun = _mevcutUrun != null
                ? new Urun
                {
                    Id = _mevcutUrun.Id,
                    OlusturmaTarihi = _mevcutUrun.OlusturmaTarihi,
                    Kod = Kod, Ad = Ad, Kategori = Kategori, Birim = Birim,
                    AlisFiyati = AlisFiyati, SatisFiyati = SatisFiyati,
                    KdvOrani = KdvOrani, StokMiktari = StokMiktari,
                    MinimumStok = MinimumStok, Aktif = Aktif
                }
                : new Urun
                {
                    Kod = Kod, Ad = Ad, Kategori = Kategori, Birim = Birim,
                    AlisFiyati = AlisFiyati, SatisFiyati = SatisFiyati,
                    KdvOrani = KdvOrani, StokMiktari = StokMiktari,
                    MinimumStok = MinimumStok, Aktif = Aktif
                };

            if (_mevcutUrun != null)
                await _urunServisi.GuncelleAsync(urun);
            else
                await _urunServisi.EkleAsync(urun);

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
