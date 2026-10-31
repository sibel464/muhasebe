using System.Collections.ObjectModel;
using System.Windows.Input;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;
using Masaustu.Servisler;

namespace Masaustu.GorunumModelleri;

public class DashboardGorunumModeli : TemelGorunumModeli
{
    private readonly ICariServisi? _cariServisi;
    private readonly IUrunServisi? _urunServisi;
    private readonly NavigasyonServisi? _navigasyon;
    private readonly IKasaServisi? _kasaServisi;
    private readonly IFaturaServisi? _faturaServisi;
    private readonly IBaslangicKontrolServisi? _baslangicServisi;
    private readonly IVeritabaniYedekServisi? _yedekServisi;
    private readonly ITekrarlayanFaturaServisi? _tekrarlayanServisi;
    private readonly IRaporMerkeziServisi? _raporServisi;
    private readonly IPersonelServisi? _personelServisi;

    private int _musteriSayisi;
    private int _urunSayisi;
    private int _aylikFatura;
    private decimal _kasaBakiyesi;
    private int _kritikStokSayisi;

    public int MusteriSayisi { get => _musteriSayisi; set => OzellikAyarla(ref _musteriSayisi, value); }
    public int UrunSayisi { get => _urunSayisi; set => OzellikAyarla(ref _urunSayisi, value); }
    public int AylikFatura { get => _aylikFatura; set => OzellikAyarla(ref _aylikFatura, value); }
    public decimal KasaBakiyesi { get => _kasaBakiyesi; set => OzellikAyarla(ref _kasaBakiyesi, value); }
    public int KritikStokSayisi { get => _kritikStokSayisi; set => OzellikAyarla(ref _kritikStokSayisi, value); }

    // "Başlarken": yeni kurulumda yapılması gereken tanımlar; hepsi tamamlanınca kart gizlenir.
    public ObservableCollection<BaslangicAdimi> BaslangicAdimlari { get; } = new();
    private string _baslangicOzeti = string.Empty;
    public string BaslangicOzeti { get => _baslangicOzeti; set => OzellikAyarla(ref _baslangicOzeti, value); }
    private bool _baslangicGorunur;
    public bool BaslangicGorunur { get => _baslangicGorunur; set => OzellikAyarla(ref _baslangicGorunur, value); }
    private string _sonYedekMetni = string.Empty;
    public string SonYedekMetni { get => _sonYedekMetni; set => OzellikAyarla(ref _sonYedekMetni, value); }
    private bool _yedekUyarisi;
    public bool YedekUyarisi { get => _yedekUyarisi; set => OzellikAyarla(ref _yedekUyarisi, value); }
    public ICommand AdimaGitKomutu { get; }
    private string _tekrarlayanUyarisi = string.Empty;
    public string TekrarlayanUyarisi { get => _tekrarlayanUyarisi; set { OzellikAyarla(ref _tekrarlayanUyarisi, value); BildirimGonder(nameof(TekrarlayanUyarisiGorunur)); } }
    public bool TekrarlayanUyarisiGorunur => TekrarlayanUyarisi.Length > 0;
    public ICommand TekrarlayanFaturalarKomutu { get; }
    // Önümüzdeki 7 günün (ve vadesi geçmişlerin) tahsilat / ödeme özeti; rapor yetkisi yoksa gizli.
    private string _vadeTahsilat = string.Empty, _vadeOdeme = string.Empty;
    private bool _vadeGorunur;
    public string VadeTahsilat { get => _vadeTahsilat; set => OzellikAyarla(ref _vadeTahsilat, value); }
    public string VadeOdeme { get => _vadeOdeme; set => OzellikAyarla(ref _vadeOdeme, value); }
    public bool VadeGorunur { get => _vadeGorunur; set => OzellikAyarla(ref _vadeGorunur, value); }
    public ICommand VadeTakvimiKomutu { get; }
    // Geçen ayın maaş tahakkuku, ödenmemiş maaşlar ve ödenecek vergi / SGK; personel yetkisi yoksa gizli.
    private string _personelUyarisi = string.Empty;
    public string PersonelUyarisi { get => _personelUyarisi; set { OzellikAyarla(ref _personelUyarisi, value); BildirimGonder(nameof(PersonelUyarisiGorunur)); } }
    public bool PersonelUyarisiGorunur => PersonelUyarisi.Length > 0;
    public ICommand PersonelKomutu { get; }

    // Dialog açma fonksiyonları
    public Func<Task<bool>>? MusteriDuzenleFormAc { get; set; }

    public ICommand YeniMusteriKomutu { get; }
    public ICommand YeniUrunKomutu { get; }
    public ICommand HizliSatisKomutu { get; }
    public ICommand CikisKomutu { get; }

    public DashboardGorunumModeli(
        ICariServisi cariServisi, 
        IUrunServisi urunServisi, 
        NavigasyonServisi? navigasyon = null, 
        Action? cikisAction = null,
        IKasaServisi? kasaServisi = null,
        IFaturaServisi? faturaServisi = null,
        IBaslangicKontrolServisi? baslangicServisi = null,
        IVeritabaniYedekServisi? yedekServisi = null,
        ITekrarlayanFaturaServisi? tekrarlayanServisi = null,
        IRaporMerkeziServisi? raporServisi = null,
        IPersonelServisi? personelServisi = null,
        IEBelgeServisi? eBelgeServisi = null)
    {
        _eBelgeServisi = eBelgeServisi;
        FaturalarKomutu = new KomutSarici(_ => _navigasyon?.Git("Fatura"));
        _personelServisi = personelServisi;
        _raporServisi = raporServisi;
        _tekrarlayanServisi = tekrarlayanServisi;
        _cariServisi = cariServisi;
        _urunServisi = urunServisi;
        _navigasyon = navigasyon;
        _kasaServisi = kasaServisi;
        _faturaServisi = faturaServisi;
        _baslangicServisi = baslangicServisi;
        _yedekServisi = yedekServisi;

        YeniMusteriKomutu = new KomutSarici(_ => _navigasyon?.Git("CariEkle"));
        YeniUrunKomutu = new KomutSarici(_ => _navigasyon?.Git("UrunEkle"));
        HizliSatisKomutu = new KomutSarici(_ => _navigasyon?.Git("Satis"));
        CikisKomutu = new KomutSarici(_ => cikisAction?.Invoke());
        AdimaGitKomutu = new KomutSarici(p => { if (p is string sayfa) _navigasyon?.Git(sayfa); });
        VadeTakvimiKomutu = new KomutSarici(_ => Masaustu.Gorunumler.Ortak.VadeTakvimiPenceresi.Ac(null));
        PersonelKomutu = new KomutSarici(_ => _navigasyon?.Git("Personel"));
        TekrarlayanFaturalarKomutu = new KomutSarici(async _ =>
        {
            Masaustu.Gorunumler.Sayfa.TekrarlayanFaturalarPenceresi.Ac(null);
            await TekrarlayanYukleAsync();
        });
        
        _ = VerileriYukleAsync();
    }
    
    public DashboardGorunumModeli() 
    {
        YeniMusteriKomutu = new KomutSarici(_ => { });
        YeniUrunKomutu = new KomutSarici(_ => { });
        HizliSatisKomutu = new KomutSarici(_ => { });
        CikisKomutu = new KomutSarici(_ => { });
        AdimaGitKomutu = new KomutSarici(_ => { });
        TekrarlayanFaturalarKomutu = new KomutSarici(_ => { });
        VadeTakvimiKomutu = new KomutSarici(_ => { });
        PersonelKomutu = new KomutSarici(_ => { });
        FaturalarKomutu = new KomutSarici(_ => { });
    }

    private readonly IEBelgeServisi? _eBelgeServisi;
    private string _eBelgeUyarisi = string.Empty;
    public string EBelgeUyarisi { get => _eBelgeUyarisi; set { OzellikAyarla(ref _eBelgeUyarisi, value); BildirimGonder(nameof(EBelgeUyarisiGorunur)); } }
    public bool EBelgeUyarisiGorunur => EBelgeUyarisi.Length > 0;
    public ICommand FaturalarKomutu { get; }

    private async Task EBelgeYukleAsync()
    {
        if (_eBelgeServisi == null) return;
        try
        {
            EBelgeUyarisi = (await _eBelgeServisi.UyariAsync()).Metin();
        }
        catch (Uygulama.Arayuzler.YetkisizErisimException)
        {
            EBelgeUyarisi = string.Empty;
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "DashboardGorunumModeli.EBelge");
        }
    }

    private async Task VerileriYukleAsync()
    {
        try
        {
            if (_cariServisi != null)
            {
                var musteriler = await _cariServisi.TumunuGetirAsync(); // kart "Müşteri & Tedarikçi" sayısını gösterir
                MusteriSayisi = musteriler.Count;
            }
            
            if (_urunServisi != null)
            {
                var urunler = await _urunServisi.TumunuGetirAsync();
                UrunSayisi = urunler.Count;
                KritikStokSayisi = urunler.Count(u => u.StokTakibi && u.StokMiktari <= u.MinimumStok && u.Aktif);
            }

            if (_kasaServisi != null)
            {
                // Kasa görüntüleme yetkisi olmayan kullanıcıda panelin geri kalanı yine yüklenmeli.
                try { KasaBakiyesi = await _kasaServisi.BakiyeHesaplaAsync(); }
                catch (Uygulama.Arayuzler.YetkisizErisimException) { KasaBakiyesi = 0; }
            }

            if (_faturaServisi != null)
            {
                var ayBaslangic = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                AylikFatura = await _faturaServisi.SayAsync(DateTime.SpecifyKind(ayBaslangic, DateTimeKind.Utc));
            }
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "DashboardGorunumModeli");
        }

        await BaslangicVeYedekYukleAsync();
        await TekrarlayanYukleAsync();
        await VadeYukleAsync();
        await PersonelYukleAsync();
        await EBelgeYukleAsync();
    }

    private async Task PersonelYukleAsync()
    {
        if (_personelServisi == null) return;
        try
        {
            var u = await _personelServisi.UyariAsync(DateTime.Today);
            PersonelUyarisi = u.Metin(DateTime.Today);
        }
        catch (Uygulama.Arayuzler.YetkisizErisimException)
        {
            PersonelUyarisi = string.Empty;
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "DashboardGorunumModeli.Personel");
        }
    }

    private async Task VadeYukleAsync()
    {
        if (_raporServisi == null) return;
        try
        {
            var kalemler = await _raporServisi.GetVadeTakvimiAsync(new Uygulama.Dto.Rapor.SatisRaporFiltreDto { BitisTarihi = DateTime.Today.AddDays(7) });
            string Metin(bool tahsilat)
            {
                var k = kalemler.Where(x => x.Tahsilat == tahsilat).ToList();
                var gecmis = k.Where(x => x.GecikmeGun > 0).Sum(x => x.Tutar);
                return k.Count == 0 ? "Yok" : $"{k.Sum(x => x.Tutar):N2} ₺ ({k.Count} kalem" + (gecmis > 0 ? $", {gecmis:N2} ₺ vadesi geçmiş)" : ")");
            }
            VadeTahsilat = Metin(true);
            VadeOdeme = Metin(false);
            VadeGorunur = true;
        }
        catch (Uygulama.Arayuzler.YetkisizErisimException)
        {
            VadeGorunur = false;
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "DashboardGorunumModeli.Vade");
        }
    }

    private async Task TekrarlayanYukleAsync()
    {
        if (_tekrarlayanServisi == null) return;
        try
        {
            var bekleyen = await _tekrarlayanServisi.BekleyenSayisiAsync();
            TekrarlayanUyarisi = bekleyen > 0 ? $"🔁 Zamanı gelmiş {bekleyen} tekrarlayan fatura (kira, aidat vb.) kesilmeyi bekliyor." : string.Empty;
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "DashboardGorunumModeli.Tekrarlayan");
        }
    }

    private async Task BaslangicVeYedekYukleAsync()
    {
        try
        {
            if (_baslangicServisi != null)
            {
                var adimlar = await _baslangicServisi.AdimlariGetirAsync();
                BaslangicAdimlari.Clear();
                foreach (var a in adimlar) BaslangicAdimlari.Add(a);
                BaslangicOzeti = $"{adimlar.Count(a => a.Tamam)} / {adimlar.Count} tamamlandı";
                BaslangicGorunur = adimlar.Any(a => !a.Tamam);
            }

            if (_yedekServisi != null)
            {
                var son = _yedekServisi.YedekleriListele().MaxBy(y => y.OlusturmaTarihi);
                SonYedekMetni = son == null ? "Henüz yedek alınmadı" : $"Son yedek: {son.OlusturmaTarihi:dd.MM.yyyy HH:mm}";
                YedekUyarisi = son == null || son.OlusturmaTarihi < DateTime.Now.AddDays(-2);
            }
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "DashboardGorunumModeli.Baslangic");
        }
    }
}
