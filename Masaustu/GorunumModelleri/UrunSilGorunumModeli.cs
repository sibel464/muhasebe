using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

// Ürün silme: kayıt fiziksel silinmez, "silinenler" listesine taşınır ve oradan geri alınabilir.
// Stoğu veya açık siparişi olan ürün silinemez (kural serviste).
public class UrunSilGorunumModeli : TemelGorunumModeli
{
    private readonly IUrunServisi _urunServisi;
    private string _aramaMetni = string.Empty;
    private ObservableCollection<Urun> _aramaSonuclari = new();
    private ObservableCollection<Urun> _silinenler = new();
    private Urun? _seciliUrun;
    private Urun? _seciliSilinen;
    private string _hataMesaji = string.Empty;
    private string _basariMesaji = string.Empty;
    private bool _islemYapiliyor = false;

    public string AramaMetni
    {
        get => _aramaMetni;
        set { OzellikAyarla(ref _aramaMetni, value); _ = AraAsync(); }
    }

    public ObservableCollection<Urun> AramaSonuclari
    {
        get => _aramaSonuclari;
        set => OzellikAyarla(ref _aramaSonuclari, value);
    }

    public ObservableCollection<Urun> Silinenler
    {
        get => _silinenler;
        set => OzellikAyarla(ref _silinenler, value);
    }

    public Urun? SeciliUrun
    {
        get => _seciliUrun;
        set { OzellikAyarla(ref _seciliUrun, value); ((KomutSarici)SilKomutu).BildirimGonder(); }
    }

    public Urun? SeciliSilinen
    {
        get => _seciliSilinen;
        set { OzellikAyarla(ref _seciliSilinen, value); ((KomutSarici)GeriAlKomutu).BildirimGonder(); }
    }

    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }
    public string BasariMesaji { get => _basariMesaji; set => OzellikAyarla(ref _basariMesaji, value); }
    public bool IslemYapiliyor { get => _islemYapiliyor; set => OzellikAyarla(ref _islemYapiliyor, value); }

    public ICommand SilKomutu { get; }
    public ICommand GeriAlKomutu { get; }

    public UrunSilGorunumModeli(IUrunServisi urunServisi)
    {
        _urunServisi = urunServisi;
        SilKomutu = new KomutSarici(async () => await SilAsync(), () => SeciliUrun != null && !IslemYapiliyor);
        GeriAlKomutu = new KomutSarici(async () => await GeriAlAsync(), () => SeciliSilinen != null && !IslemYapiliyor);
        _ = YukleAsync();
    }

    private async Task YukleAsync()
    {
        await AraAsync();
        await SilinenleriYukleAsync();
    }

    private async Task AraAsync()
    {
        try
        {
            // Pasif ürünler de silinebilir; bu yüzden yalnızca aktifler değil hepsi listelenir.
            var sonuclar = await _urunServisi.FiltreliAraAsync(new UrunFiltre { AramaMetni = AramaMetni, SadeceAktifler = false });
            AramaSonuclari = new ObservableCollection<Urun>(sonuclar);
        }
        catch (Exception ex)
        {
            HataMesaji = $"Ürünler listelenemedi: {ex.Message}";
        }
    }

    private async Task SilinenleriYukleAsync()
    {
        try
        {
            Silinenler = new ObservableCollection<Urun>(await _urunServisi.SilinenleriGetirAsync());
        }
        catch (Exception ex)
        {
            HataMesaji = $"Silinen ürünler listelenemedi: {ex.Message}";
        }
    }

    private async Task SilAsync()
    {
        if (SeciliUrun == null) return;
        var urun = SeciliUrun;
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        // Tek pencere: hem onay hem silme nedeni. Vazgeç denirse hiçbir şey yapılmaz.
        var pencere = new Masaustu.Gorunumler.Ortak.MetinGirisPenceresi(
            "Ürünü Sil",
            $"{urun.Kod} - {urun.Ad}\n\n" +
            "Ürün silinenler listesine taşınır; geçmiş faturaları, hareketleri ve raporları korunur. " +
            "Gerekirse aşağıdaki \"Silinen Ürünler\" bölümünden geri alınabilir.\n\n" +
            "Silme nedenini yazın:");
        if (pencere.ShowDialog() != true) return;

        IslemYapiliyor = true;
        ((KomutSarici)SilKomutu).BildirimGonder();
        try
        {
            await _urunServisi.SilAsync(urun.Id, pencere.GirilenMetin);
            BasariMesaji = $"'{urun.Ad}' silindi. Silinen Ürünler listesinden geri alabilirsiniz.";
            SeciliUrun = null;
            await YukleAsync();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            HataMesaji = ex.Message;
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "UrunSil");
            HataMesaji = "Ürün silinemedi. Ayrıntılar log dosyasına yazıldı.";
        }
        finally
        {
            IslemYapiliyor = false;
            ((KomutSarici)SilKomutu).BildirimGonder();
        }
    }

    private async Task GeriAlAsync()
    {
        if (SeciliSilinen == null) return;
        var urun = SeciliSilinen;
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        IslemYapiliyor = true;
        ((KomutSarici)GeriAlKomutu).BildirimGonder();
        try
        {
            await _urunServisi.GeriAlAsync(urun.Id);
            BasariMesaji = $"'{urun.Ad}' geri alındı ve tekrar aktif.";
            SeciliSilinen = null;
            await YukleAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            HataMesaji = ex.Message;
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "UrunGeriAl");
            HataMesaji = "Ürün geri alınamadı. Ayrıntılar log dosyasına yazıldı.";
        }
        finally
        {
            IslemYapiliyor = false;
            ((KomutSarici)GeriAlKomutu).BildirimGonder();
        }
    }
}
