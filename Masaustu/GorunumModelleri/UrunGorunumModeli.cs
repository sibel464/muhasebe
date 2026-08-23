using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class UrunGorunumModeli : TemelGorunumModeli
{
    private readonly IUrunServisi _urunServisi;

    private ObservableCollection<Urun> _urunler = new();
    private Urun? _seciliUrun;
    private string _aramaMetni = string.Empty;
    private bool _yukleniyor = false;
    private string _durumMesaji = string.Empty;

    public ObservableCollection<Urun> Urunler
    {
        get => _urunler;
        set => OzellikAyarla(ref _urunler, value);
    }

    public Urun? SeciliUrun
    {
        get => _seciliUrun;
        set
        {
            OzellikAyarla(ref _seciliUrun, value);
            // Komutların durumunu güncelle
            (DuzenleKomutu as KomutSarici)?.BildirimGonder();
            (SilKomutu as KomutSarici)?.BildirimGonder();
        }
    }

    public string AramaMetni
    {
        get => _aramaMetni;
        set
        {
            OzellikAyarla(ref _aramaMetni, value);
            _ = AraAsync();
        }
    }

    public bool Yukleniyor
    {
        get => _yukleniyor;
        set => OzellikAyarla(ref _yukleniyor, value);
    }

    public string DurumMesaji
    {
        get => _durumMesaji;
        set => OzellikAyarla(ref _durumMesaji, value);
    }

    // Özet bilgiler
    public int ToplamUrun => _urunler.Count;
    public int AktifUrun => _urunler.Count(u => u.Aktif);
    public int DusukStokUrun => _urunler.Count(u => u.StokMiktari < u.MinimumStok && u.Aktif);

    // Komutlar
    public ICommand YeniEkleKomutu { get; }
    public ICommand DuzenleKomutu { get; }
    public ICommand SilKomutu { get; }
    public ICommand YenileKomutu { get; }

    // Pencere açma aksiyonları (View'dan set edilir)
    public Func<Urun?, Task<bool>>? DuzenleFormAc { get; set; }

    public UrunGorunumModeli(IUrunServisi urunServisi)
    {
        _urunServisi = urunServisi;

        YeniEkleKomutu = new KomutSarici(async _ => await YeniEkleAsync());
        DuzenleKomutu = new KomutSarici(async _ => await DuzenleAsync(), _ => SeciliUrun != null);
        SilKomutu = new KomutSarici(async _ => await SilAsync(), _ => SeciliUrun != null);
        YenileKomutu = new KomutSarici(async _ => await VerileriYukleAsync());

        _ = VerileriYukleAsync();
    }

    public async Task VerileriYukleAsync()
    {
        Yukleniyor = true;
        DurumMesaji = "Yükleniyor...";
        try
        {
            var liste = await _urunServisi.TumunuGetirAsync();
            Urunler = new ObservableCollection<Urun>(liste.Where(u => !u.Silindi));
            OzettleriGuncelle();
            DurumMesaji = $"{Urunler.Count} ürün listeleniyor.";
        }
        catch (Exception ex)
        {
            DurumMesaji = "Veritabanı bağlantı hatası!";
            System.Windows.MessageBox.Show(
                "Veritabanına bağlanılamadı. PostgreSQL bağlantı ayarlarınızı veya servis durumunu kontrol ediniz.\n\nAyrıntı: " + ex.Message,
                "Bağlantı Hatası",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task AraAsync()
    {
        if (string.IsNullOrWhiteSpace(AramaMetni))
        {
            await VerileriYukleAsync();
            return;
        }
        var sonuclar = await _urunServisi.AraAsync(AramaMetni);
        Urunler = new ObservableCollection<Urun>(sonuclar.Where(u => !u.Silindi));
        DurumMesaji = $"{Urunler.Count} sonuç bulundu.";
        OzettleriGuncelle();
    }

    private async Task YeniEkleAsync()
    {
        if (DuzenleFormAc == null) return;
        var kaydedildi = await DuzenleFormAc(null);
        if (kaydedildi) await VerileriYukleAsync();
    }

    private async Task DuzenleAsync()
    {
        if (SeciliUrun == null || DuzenleFormAc == null) return;
        var kaydedildi = await DuzenleFormAc(SeciliUrun);
        if (kaydedildi) await VerileriYukleAsync();
    }

    private async Task SilAsync()
    {
        if (SeciliUrun == null) return;
        var basarili = await _urunServisi.SilAsync(SeciliUrun.Id);
        if (basarili)
        {
            DurumMesaji = $"'{SeciliUrun.Ad}' silindi.";
            SeciliUrun = null;
            await VerileriYukleAsync();
        }
    }

    private void OzettleriGuncelle()
    {
        BildirimGonder(nameof(ToplamUrun));
        BildirimGonder(nameof(AktifUrun));
        BildirimGonder(nameof(DusukStokUrun));
    }
}
