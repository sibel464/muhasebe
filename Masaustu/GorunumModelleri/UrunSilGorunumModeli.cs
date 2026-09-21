using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using System.Windows;

namespace Masaustu.GorunumModelleri;

public class UrunSilGorunumModeli : TemelGorunumModeli
{
    private readonly IUrunServisi _urunServisi;
    private string _aramaMetni = string.Empty;
    private ObservableCollection<Urun> _aramaSonuclari = new();
    private Urun? _seciliUrun;
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

    public Urun? SeciliUrun
    {
        get => _seciliUrun;
        set
        {
            OzellikAyarla(ref _seciliUrun, value);
            ((KomutSarici)SilKomutu).BildirimGonder();
        }
    }

    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }
    public string BasariMesaji { get => _basariMesaji; set => OzellikAyarla(ref _basariMesaji, value); }
    public bool IslemYapiliyor { get => _islemYapiliyor; set => OzellikAyarla(ref _islemYapiliyor, value); }

    public ICommand SilKomutu { get; }

    public UrunSilGorunumModeli(IUrunServisi urunServisi)
    {
        _urunServisi = urunServisi;
        SilKomutu = new KomutSarici(async () => await SilAsync(), () => SeciliUrun != null && !IslemYapiliyor);
        
        // İlk açıldığında tüm listeyi getir
        _ = AraAsync();
    }

    private async Task AraAsync()
    {
        IslemYapiliyor = true;
        try
        {
            var filtre = new UrunFiltre
            {
                AramaMetni = AramaMetni,
                SadeceAktifler = true
            };
            var sonuclar = await _urunServisi.FiltreliAraAsync(filtre);
            AramaSonuclari = new ObservableCollection<Urun>(sonuclar);
        }
        catch (Exception ex)
        {
            HataMesaji = $"Arama hatası: {ex.Message}";
        }
        finally
        {
            IslemYapiliyor = false;
        }
    }

    private async Task SilAsync()
    {
        if (SeciliUrun == null) return;

        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        var sonuc = MessageBox.Show(
            $"{SeciliUrun.Ad} isimli ürünü silmek istediğinize emin misiniz?",
            "Ürün Silme Onayı (1/3)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (sonuc != MessageBoxResult.Yes) return;

        var sonuc2 = MessageBox.Show(
            "Bu işlem geri alınamaz. Kesinlikle silmek istiyor musunuz?",
            "Ürün Silme Onayı (2/3)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Stop);

        if (sonuc2 != MessageBoxResult.Yes) return;

        var girisPenceresi = new Masaustu.Gorunumler.Ortak.MetinGirisPenceresi(
            "Silme Nedeni (3/3)",
            $"{SeciliUrun.Ad} ürününü neden siliyorsunuz? Lütfen açıklayınız.");

        if (girisPenceresi.ShowDialog() != true)
        {
            return;
        }

        var silmeNedeni = girisPenceresi.GirilenMetin;

        IslemYapiliyor = true;
        ((KomutSarici)SilKomutu).BildirimGonder();

        try
        {
            await _urunServisi.SilAsync(SeciliUrun.Id, silmeNedeni);
            BasariMesaji = $"{SeciliUrun.Ad} başarıyla silindi!";
            SeciliUrun = null;
            await AraAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Silme hatası: {ex.Message}";
        }
        finally
        {
            IslemYapiliyor = false;
            ((KomutSarici)SilKomutu).BildirimGonder();
        }
    }
}
