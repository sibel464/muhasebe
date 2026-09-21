using System.Windows.Input;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using EtkiAlani.Modeller.Tanimlar;

namespace Masaustu.GorunumModelleri;

public class UrunDuzenleGorunumModeli : TemelGorunumModeli
{
    private readonly IUrunServisi _urunServisi;
    private readonly IMerkeziTanimServisi _tanimServisi;
    private readonly Urun? _mevcutUrun;

    private string _kod = string.Empty;
    private string _barkod = string.Empty;
    private string _ad = string.Empty;
    private string _kategori = string.Empty;
    private string _birim = "Adet";
    private MerkeziTanim? _seciliKategori, _seciliMarka, _seciliBirim;
    private decimal _alisFiyati;
    private decimal _perakendeSatisFiyati;
    private decimal _toptanSatisFiyati;
    private decimal _kdvOrani = 20;
    private decimal _stokMiktari;
    private decimal _minimumStok;
    private bool _aktif = true;
    private string _resimYolu = string.Empty;
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

    public string Barkod { get => _barkod; set => OzellikAyarla(ref _barkod, value); }

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
    public ObservableCollection<MerkeziTanim> Kategoriler { get; } = new();
    public ObservableCollection<MerkeziTanim> Markalar { get; } = new();
    public ObservableCollection<MerkeziTanim> Birimler { get; } = new();
    public MerkeziTanim? SeciliKategori { get=>_seciliKategori; set { if(OzellikAyarla(ref _seciliKategori,value)) Kategori=value?.Ad??""; } }
    public MerkeziTanim? SeciliMarka { get=>_seciliMarka; set=>OzellikAyarla(ref _seciliMarka,value); }
    public MerkeziTanim? SeciliBirim { get=>_seciliBirim; set { if(OzellikAyarla(ref _seciliBirim,value)) Birim=value?.KisaAd??value?.Ad??""; } }
    
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

    public decimal PerakendeSatisFiyati 
    { 
        get => _perakendeSatisFiyati; 
        set 
        { 
            if (OzellikAyarla(ref _perakendeSatisFiyati, value))
                DogrulaSatisFiyati(value);
        } 
    }

    public decimal ToptanSatisFiyati 
    { 
        get => _toptanSatisFiyati; 
        set => OzellikAyarla(ref _toptanSatisFiyati, value);
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
    
    public string ResimYolu 
    { 
        get => _resimYolu; 
        set 
        { 
            OzellikAyarla(ref _resimYolu, value); 
            (ResimKaldirKomutu as KomutSarici)?.BildirimGonder();
        } 
    }

    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }
    public bool Kaydediliyor { get => _kaydediliyor; set => OzellikAyarla(ref _kaydediliyor, value); }

    public List<string> BirimListesi { get; } = new() { "Adet", "Kg", "Litre", "Metre", "Kutu", "Paket", "Takım", "Set" };
    public List<decimal> KdvOranlari { get; } = new() { 0, 1, 10, 20 };

    public ICommand KaydetKomutu { get; }
    public ICommand IptalKomutu { get; }
    public ICommand ResimSecKomutu { get; }
    public ICommand ResimKaldirKomutu { get; }

    public Action<bool>? PencereKapat { get; set; }

    public UrunDuzenleGorunumModeli(IUrunServisi urunServisi, IMerkeziTanimServisi tanimServisi, Urun? mevcutUrun = null)
    {
        _urunServisi = urunServisi;
        _tanimServisi = tanimServisi;
        _mevcutUrun = mevcutUrun;

        if (mevcutUrun != null)
        {
            // Initial load
            _kod = mevcutUrun.Kod;
            _barkod = mevcutUrun.Barkod ?? string.Empty;
            _ad = mevcutUrun.Ad;
            _kategori = mevcutUrun.Kategori ?? string.Empty;
            _birim = mevcutUrun.Birim;
            _alisFiyati = mevcutUrun.AlisFiyati;
            _perakendeSatisFiyati = mevcutUrun.PerakendeSatisFiyati;
            _toptanSatisFiyati = mevcutUrun.ToptanSatisFiyati;
            _kdvOrani = mevcutUrun.KdvOrani;
            _stokMiktari = mevcutUrun.StokMiktari;
            _minimumStok = mevcutUrun.MinimumStok;
            _aktif = mevcutUrun.Aktif;
            _resimYolu = mevcutUrun.ResimYolu ?? string.Empty;
        }

        KaydetKomutu = new KomutSarici(async _ => await KaydetAsync(), _ => !Kaydediliyor && !HasErrors);
        IptalKomutu = new KomutSarici(_ => PencereKapat?.Invoke(false));
        
        ResimSecKomutu = new KomutSarici(_ => ResimSec());
        ResimKaldirKomutu = new KomutSarici(_ => ResimYolu = string.Empty, _ => !string.IsNullOrEmpty(ResimYolu));
        _ = TanimlariYukleAsync();

        if (YeniKayit)
        {
            DogrulaKod(Kod);
            DogrulaAd(Ad);
            DogrulaAlisFiyati(AlisFiyati);
            DogrulaSatisFiyati(PerakendeSatisFiyati);
        }
    }

    public async Task TanimlariYukleAsync(string? grup=null, int? secilecekId=null)
    {
        try
        {
            async Task Yukle(string g, ObservableCollection<MerkeziTanim> hedef, int? mevcutId)
            { var l=await _tanimServisi.ListeleAsync(g,true,mevcutId); hedef.Clear(); foreach(var x in l)hedef.Add(x); }
            if(grup==null||grup==TanimGruplari.UrunKategorisi){await Yukle(TanimGruplari.UrunKategorisi,Kategoriler,_mevcutUrun?.KategoriTanimiId);SeciliKategori=Kategoriler.FirstOrDefault(x=>x.Id==(secilecekId??_mevcutUrun?.KategoriTanimiId));}
            if(grup==null||grup==TanimGruplari.Marka){await Yukle(TanimGruplari.Marka,Markalar,_mevcutUrun?.MarkaTanimiId);SeciliMarka=Markalar.FirstOrDefault(x=>x.Id==(secilecekId??_mevcutUrun?.MarkaTanimiId));}
            if(grup==null||grup==TanimGruplari.Birim){await Yukle(TanimGruplari.Birim,Birimler,_mevcutUrun?.BirimTanimiId);SeciliBirim=Birimler.FirstOrDefault(x=>x.Id==(secilecekId??_mevcutUrun?.BirimTanimiId))??Birimler.FirstOrDefault(x=>x.Ad==_birim||x.KisaAd==_birim);}
        }
        catch
        {
            Kategoriler.Clear();
            Markalar.Clear();
            Birimler.Clear();
        }
    }

    private void ResimSec()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Ürün Resmi Seç",
            Filter = "Resim Dosyaları (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|Tüm Dosyalar (*.*)|*.*",
            Multiselect = false
        };

        if (ofd.ShowDialog() == true)
        {
            ResimYolu = ofd.FileName;
        }
    }

    // Doğrulama Kuralları
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
        catch { }
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
        TemizleHata(nameof(PerakendeSatisFiyati));
        if (value < 0)
        {
            EkleHata("Satış fiyatı sıfırdan küçük olamaz.", nameof(PerakendeSatisFiyati));
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
        DogrulaKod(Kod);
        DogrulaAd(Ad);
        DogrulaAlisFiyati(AlisFiyati);
        DogrulaSatisFiyati(PerakendeSatisFiyati);
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
                    Kod = Kod, Barkod = Barkod, Ad = Ad, Kategori = SeciliKategori?.Ad, Birim = SeciliBirim?.KisaAd ?? SeciliBirim?.Ad ?? Birim,
                    KategoriTanimiId=SeciliKategori?.Id, MarkaTanimiId=SeciliMarka?.Id, BirimTanimiId=SeciliBirim?.Id,
                    AlisFiyati = AlisFiyati, PerakendeSatisFiyati = PerakendeSatisFiyati, ToptanSatisFiyati = ToptanSatisFiyati,
                    KdvOrani = KdvOrani, StokMiktari = StokMiktari,
                    MinimumStok = MinimumStok, Aktif = Aktif, ResimYolu = ResimYolu
                }
                : new Urun
                {
                    Kod = Kod, Barkod = Barkod, Ad = Ad, Kategori = SeciliKategori?.Ad, Birim = SeciliBirim?.KisaAd ?? SeciliBirim?.Ad ?? Birim,
                    KategoriTanimiId=SeciliKategori?.Id, MarkaTanimiId=SeciliMarka?.Id, BirimTanimiId=SeciliBirim?.Id,
                    AlisFiyati = AlisFiyati, PerakendeSatisFiyati = PerakendeSatisFiyati, ToptanSatisFiyati = ToptanSatisFiyati,
                    KdvOrani = KdvOrani, StokMiktari = StokMiktari,
                    MinimumStok = MinimumStok, Aktif = Aktif, ResimYolu = ResimYolu
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
