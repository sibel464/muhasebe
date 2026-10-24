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
        IFaturaServisi? faturaServisi = null)
    {
        _cariServisi = cariServisi;
        _urunServisi = urunServisi;
        _navigasyon = navigasyon;
        _kasaServisi = kasaServisi;
        _faturaServisi = faturaServisi;

        YeniMusteriKomutu = new KomutSarici(_ => _navigasyon?.Git("CariEkle"));
        YeniUrunKomutu = new KomutSarici(_ => _navigasyon?.Git("UrunEkle"));
        HizliSatisKomutu = new KomutSarici(_ => _navigasyon?.Git("Satis"));
        CikisKomutu = new KomutSarici(_ => cikisAction?.Invoke());
        
        _ = VerileriYukleAsync();
    }
    
    public DashboardGorunumModeli() 
    {
        YeniMusteriKomutu = new KomutSarici(_ => { });
        YeniUrunKomutu = new KomutSarici(_ => { });
        HizliSatisKomutu = new KomutSarici(_ => { });
        CikisKomutu = new KomutSarici(_ => { });
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
                KritikStokSayisi = urunler.Count(u => u.StokMiktari <= u.MinimumStok && u.Aktif);
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
    }
}
