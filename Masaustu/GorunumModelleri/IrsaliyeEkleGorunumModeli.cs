using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Irsaliye;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public sealed class IrsaliyeKalemiGorunumModeli : TemelGorunumModeli
{
    private Urun? _urun;
    private decimal _miktar = 1;
    private decimal _birimFiyat;
    private decimal _kdvOrani = 20.00m;

    public Urun? Urun
    {
        get => _urun;
        set
        {
            if (OzellikAyarla(ref _urun, value))
            {
                UrunBilgileriniUygula();
            }
        }
    }

    public decimal Miktar
    {
        get => _miktar;
        set
        {
            if (OzellikAyarla(ref _miktar, value))
                Hesapla();
        }
    }

    public decimal BirimFiyat
    {
        get => _birimFiyat;
        set
        {
            if (OzellikAyarla(ref _birimFiyat, value))
                Hesapla();
        }
    }

    public decimal KdvOrani
    {
        get => _kdvOrani;
        set
        {
            if (OzellikAyarla(ref _kdvOrani, value))
                Hesapla();
        }
    }

    public string Kod => Urun?.Kod ?? string.Empty;
    public string Ad => Urun?.Ad ?? string.Empty;
    public string Birim => Urun?.Birim ?? "Adet";

    public decimal KdvTutari { get; private set; }
    public decimal SatirToplami { get; private set; }

    public Action? Degisti { get; set; }

    public void UrunBilgileriniUygula()
    {
        if (Urun == null) return;
        if (_miktar <= 0) _miktar = 1;
        if (_birimFiyat == 0) _birimFiyat = Urun.PerakendeSatisFiyati;
        if (_kdvOrani == 0) _kdvOrani = Urun.KdvOrani;

        BildirimGonder(nameof(BirimFiyat));
        BildirimGonder(nameof(KdvOrani));
        BildirimGonder(nameof(Kod));
        BildirimGonder(nameof(Ad));
        BildirimGonder(nameof(Birim));
        Hesapla();
    }

    public void Hesapla()
    {
        var brut = Miktar * BirimFiyat;
        KdvTutari = decimal.Round(brut * KdvOrani / 100, 4, MidpointRounding.AwayFromZero);
        SatirToplami = decimal.Round(brut + KdvTutari, 4, MidpointRounding.AwayFromZero);

        BildirimGonder(nameof(KdvTutari));
        BildirimGonder(nameof(SatirToplami));
        Degisti?.Invoke();
    }

    public IrsaliyeKalemi ToEntity() => new()
    {
        UrunId = Urun?.Id ?? 0,
        UrunKodu = Kod,
        UrunAd = Ad,
        Birim = Birim,
        Miktar = Miktar,
        BirimFiyat = BirimFiyat,
        KdvOrani = KdvOrani,
        KdvTutari = KdvTutari,
        SatirToplami = SatirToplami
    };
}

public class IrsaliyeEkleGorunumModeli : TemelGorunumModeli
{
    private readonly IIrsaliyeServisi _irsaliyeServisi;
    private readonly ICariServisi _cariServisi;
    private readonly IUrunServisi _urunServisi;

    private ObservableCollection<CariKart> _cariler = new();
    private ObservableCollection<Urun> _urunler = new();
    private List<CariKart> _tumCariler = new();
    private List<Urun> _tumUrunler = new();

    private ObservableCollection<IrsaliyeKalemiGorunumModeli> _kalemler = new();
    private CariKart? _seciliCari;
    private Urun? _seciliUrun;
    private IrsaliyeKalemiGorunumModeli? _seciliKalem;

    private bool _isSatisIrsaliyesi = true;
    private bool _isAlisIrsaliyesi;
    private string _irsaliyeNo = string.Empty;
    private DateTime _tarih = DateTime.Today;
    private string _aciklama = string.Empty;
    private string _cariAramaMetni = string.Empty;
    private string _urunAramaMetni = string.Empty;
    private string _durumMesaji = string.Empty;
    private bool _kaydediliyor;

    public ObservableCollection<CariKart> Cariler { get => _cariler; private set => OzellikAyarla(ref _cariler, value); }
    public ObservableCollection<Urun> Urunler { get => _urunler; private set => OzellikAyarla(ref _urunler, value); }
    public ObservableCollection<IrsaliyeKalemiGorunumModeli> Kalemler { get => _kalemler; private set => OzellikAyarla(ref _kalemler, value); }

    public CariKart? SeciliCari { get => _seciliCari; set => OzellikAyarla(ref _seciliCari, value); }
    public Urun? SeciliUrun
    {
        get => _seciliUrun;
        set
        {
            if (OzellikAyarla(ref _seciliUrun, value))
                (KalemEkleKomutu as KomutSarici)?.BildirimGonder();
        }
    }

    public IrsaliyeKalemiGorunumModeli? SeciliKalem
    {
        get => _seciliKalem;
        set
        {
            if (OzellikAyarla(ref _seciliKalem, value))
                (KalemSilKomutu as KomutSarici)?.BildirimGonder();
        }
    }

    public bool IsSatisIrsaliyesi
    {
        get => _isSatisIrsaliyesi;
        set
        {
            if (OzellikAyarla(ref _isSatisIrsaliyesi, value))
            {
                _isAlisIrsaliyesi = !value;
                BildirimGonder(nameof(IsAlisIrsaliyesi));
                _ = IrsaliyeNoGuncelleAsync();
            }
        }
    }

    public bool IsAlisIrsaliyesi
    {
        get => _isAlisIrsaliyesi;
        set
        {
            if (OzellikAyarla(ref _isAlisIrsaliyesi, value))
            {
                _isSatisIrsaliyesi = !value;
                BildirimGonder(nameof(IsSatisIrsaliyesi));
                _ = IrsaliyeNoGuncelleAsync();
            }
        }
    }

    public string IrsaliyeNo { get => _irsaliyeNo; set => OzellikAyarla(ref _irsaliyeNo, value); }

    // Stoğun çıktığı (satış) / girdiği (alış) stok yeri; tüm kalemler için geçerlidir. İlk aktif mağaza önerilir.
    public ObservableCollection<StokYerleri.StokYeri> StokYerleriListesi { get; } = new();
    private StokYerleri.StokYeri? _seciliStokYeri;
    public StokYerleri.StokYeri? SeciliStokYeri { get => _seciliStokYeri; set => OzellikAyarla(ref _seciliStokYeri, value); }
    public DateTime Tarih { get => _tarih; set => OzellikAyarla(ref _tarih, value); }
    public string Aciklama { get => _aciklama; set => OzellikAyarla(ref _aciklama, value); }

    public string CariAramaMetni
    {
        get => _cariAramaMetni;
        set
        {
            if (OzellikAyarla(ref _cariAramaMetni, value))
                CariFiltrele();
        }
    }

    public string UrunAramaMetni
    {
        get => _urunAramaMetni;
        set
        {
            if (OzellikAyarla(ref _urunAramaMetni, value))
                UrunFiltrele();
        }
    }

    public decimal AraToplam => Kalemler.Sum(k => k.Miktar * k.BirimFiyat);
    public decimal KdvToplam => Kalemler.Sum(k => k.KdvTutari);
    public decimal GenelToplam => AraToplam + KdvToplam;

    public string DurumMesaji { get => _durumMesaji; private set => OzellikAyarla(ref _durumMesaji, value); }
    public bool Kaydediliyor { get => _kaydediliyor; private set => OzellikAyarla(ref _kaydediliyor, value); }

    public Action<bool>? KapatIstendi { get; set; }

    public ICommand KalemEkleKomutu { get; }
    public ICommand KalemSilKomutu { get; }
    public ICommand KaydetKomutu { get; }
    public ICommand KaydetVeOnaylaKomutu { get; }
    public ICommand IptalKomutu { get; }

    public IrsaliyeEkleGorunumModeli(
        IIrsaliyeServisi irsaliyeServisi,
        ICariServisi cariServisi,
        IUrunServisi urunServisi)
    {
        _irsaliyeServisi = irsaliyeServisi;
        _cariServisi = cariServisi;
        _urunServisi = urunServisi;

        KalemEkleKomutu = new KomutSarici(KalemEkle, () => SeciliUrun != null);
        KalemSilKomutu = new KomutSarici(KalemSil, () => SeciliKalem != null);
        KaydetKomutu = new KomutSarici(async () => await KaydetAsync(onayla: false));
        KaydetVeOnaylaKomutu = new KomutSarici(async () => await KaydetAsync(onayla: true));
        IptalKomutu = new KomutSarici(() => KapatIstendi?.Invoke(false));

        _ = VerileriYukleAsync();
    }

    private async Task VerileriYukleAsync()
    {
        try
        {
            _tumCariler = (await _cariServisi.TumunuGetirAsync()).Where(c => c.Aktif).ToList();
            Cariler = new ObservableCollection<CariKart>(_tumCariler);

            _tumUrunler = (await _urunServisi.TumunuGetirAsync()).Where(u => u.Aktif).ToList();
            Urunler = new ObservableCollection<Urun>(_tumUrunler);

            await StokYerleri.YukleAsync(_urunServisi);
            foreach (var yer in StokYerleri.Liste) StokYerleriListesi.Add(yer);
            SeciliStokYeri = StokYerleriListesi.FirstOrDefault();

            await IrsaliyeNoGuncelleAsync();
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Veriler yüklenemedi: {ex.Message}";
        }
    }

    private async Task IrsaliyeNoGuncelleAsync()
    {
        try
        {
            var tur = IsSatisIrsaliyesi ? IrsaliyeTuru.Satis : IrsaliyeTuru.Alis;
            IrsaliyeNo = await _irsaliyeServisi.SiraliIrsaliyeNoUretAsync(tur, Tarih);
        }
        catch
        {
        }
    }

    private void CariFiltrele()
    {
        if (string.IsNullOrWhiteSpace(CariAramaMetni))
        {
            Cariler = new ObservableCollection<CariKart>(_tumCariler);
            return;
        }

        var arama = CariAramaMetni.Trim().ToLower();
        var filtreli = _tumCariler.Where(c =>
            (c.Unvan != null && c.Unvan.ToLower().Contains(arama)) ||
            (c.Kod != null && c.Kod.ToLower().Contains(arama)) ||
            (c.VergiNo != null && c.VergiNo.Contains(arama))).ToList();

        Cariler = new ObservableCollection<CariKart>(filtreli);
    }

    private void UrunFiltrele()
    {
        if (string.IsNullOrWhiteSpace(UrunAramaMetni))
        {
            Urunler = new ObservableCollection<Urun>(_tumUrunler);
            return;
        }

        var arama = UrunAramaMetni.Trim().ToLower();
        var filtreli = _tumUrunler.Where(u =>
            (u.Ad != null && u.Ad.ToLower().Contains(arama)) ||
            (u.Kod != null && u.Kod.ToLower().Contains(arama)) ||
            (u.Barkod != null && u.Barkod.Contains(arama))).ToList();

        Urunler = new ObservableCollection<Urun>(filtreli);
    }

    private void KalemEkle()
    {
        if (SeciliUrun == null) return;

        var fiyat = IsSatisIrsaliyesi ? SeciliUrun.PerakendeSatisFiyati : SeciliUrun.AlisFiyati;

        var yeniKalem = new IrsaliyeKalemiGorunumModeli
        {
            Urun = SeciliUrun,
            Miktar = 1,
            BirimFiyat = fiyat,
            KdvOrani = SeciliUrun.KdvOrani,
            Degisti = ToplamlariGuncelle
        };

        yeniKalem.Hesapla();
        Kalemler.Add(yeniKalem);
        ToplamlariGuncelle();

        SeciliUrun = null;
        UrunAramaMetni = string.Empty;
    }

    private void KalemSil()
    {
        if (SeciliKalem == null) return;
        Kalemler.Remove(SeciliKalem);
        SeciliKalem = null;
        ToplamlariGuncelle();
    }

    private void ToplamlariGuncelle()
    {
        BildirimGonder(nameof(AraToplam));
        BildirimGonder(nameof(KdvToplam));
        BildirimGonder(nameof(GenelToplam));
    }

    private async Task KaydetAsync(bool onayla)
    {
        if (SeciliCari == null)
        {
            MessageBox.Show("Lütfen bir Cari Kart seçin.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Kalemler.Any())
        {
            MessageBox.Show("Lütfen en az bir ürün kalemi ekleyin.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Kaydediliyor = true;
        DurumMesaji = "İrsaliye kaydediliyor...";

        try
        {
            // Belge tarihleri yerel saatle (UTC etiketli) saklanır; sevk, seçilen günün şu anki saatidir.
            var sevk = DateTime.SpecifyKind(Tarih.Date + DateTime.Now.TimeOfDay, DateTimeKind.Utc);
            var kalemler = Kalemler.Select(k => k.ToEntity()).ToList();
            if (SeciliStokYeri != null)
                foreach (var kalem in kalemler)
                    kalem.StokDagilimlari = [new IrsaliyeKalemiStokDagilimi { DepoId = SeciliStokYeri.Id, Miktar = kalem.Miktar }];
            var irsaliye = new Irsaliye
            {
                IrsaliyeNo = IrsaliyeNo,
                Tarih = DateTime.SpecifyKind(Tarih.Date, DateTimeKind.Utc),
                SevkTarihi = sevk,
                CariKartId = SeciliCari.Id,
                Tur = IsSatisIrsaliyesi ? IrsaliyeTuru.Satis : IrsaliyeTuru.Alis,
                Durum = IrsaliyeDurumu.Taslak,
                Aciklama = Aciklama,
                Kalemler = kalemler
            };

            var eklenen = await _irsaliyeServisi.EkleAsync(irsaliye);

            if (onayla)
            {
                DurumMesaji = "İrsaliye onaylanıyor ve stok hareketleri işleniyor...";
                try
                {
                    await _irsaliyeServisi.OnaylaAsync(eklenen.Id);
                }
                catch (Exception onayHatasi)
                {
                    // Taslak kaydedildi: yeniden "Kaydet" denirse ikinci irsaliye oluşmasın diye pencere kapatılır.
                    MessageBox.Show($"'{eklenen.IrsaliyeNo}' numaralı irsaliye TASLAK olarak kaydedildi ama onaylanamadı:\n{onayHatasi.Message}\n\n" +
                                    "Sorunu giderip irsaliye listesinden onaylayabilirsiniz; yeniden kaydetmeyin.",
                        "Onaylanamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    KapatIstendi?.Invoke(true);
                    return;
                }
                MessageBox.Show($"'{eklenen.IrsaliyeNo}' numaralı irsaliye başarıyla kaydedildi ve ONAYLANDI (Stok hareketleri işlendi).", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"'{eklenen.IrsaliyeNo}' numaralı irsaliye TASLAK olarak kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            KapatIstendi?.Invoke(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kayıt hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Kaydediliyor = false;
        }
    }
}
