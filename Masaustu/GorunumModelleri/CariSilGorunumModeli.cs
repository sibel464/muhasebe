using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using System.Windows;

namespace Masaustu.GorunumModelleri;

public class CariSilGorunumModeli : TemelGorunumModeli
{
    private readonly ICariServisi _cariServisi;
    private string _aramaMetni = string.Empty;
    private ObservableCollection<CariKart> _aramaSonuclari = new();
    private CariKart? _seciliCari;
    private string _hataMesaji = string.Empty;
    private string _basariMesaji = string.Empty;
    private bool _islemYapiliyor = false;

    public string AramaMetni
    {
        get => _aramaMetni;
        set { OzellikAyarla(ref _aramaMetni, value); _ = AraAsync(); }
    }

    public ObservableCollection<CariKart> AramaSonuclari
    {
        get => _aramaSonuclari;
        set => OzellikAyarla(ref _aramaSonuclari, value);
    }

    public CariKart? SeciliCari
    {
        get => _seciliCari;
        set
        {
            OzellikAyarla(ref _seciliCari, value);
            ((KomutSarici)SilKomutu).BildirimGonder();
        }
    }

    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }
    public string BasariMesaji { get => _basariMesaji; set => OzellikAyarla(ref _basariMesaji, value); }
    public bool IslemYapiliyor { get => _islemYapiliyor; set => OzellikAyarla(ref _islemYapiliyor, value); }

    public ICommand SilKomutu { get; }

    public CariSilGorunumModeli(ICariServisi cariServisi)
    {
        _cariServisi = cariServisi;
        SilKomutu = new KomutSarici(async () => await SilAsync(), () => SeciliCari != null && !IslemYapiliyor);
        
        // İlk açıldığında tüm listeyi getir
        _ = AraAsync();
    }

    private async Task AraAsync()
    {
        IslemYapiliyor = true;
        try
        {
            var sonuclar = await _cariServisi.AraAsync(AramaMetni, CariTuru.Musteri); // Tedarikçiler için ayrı sayfa veya genel olabilir, şimdilik Musteri.
            AramaSonuclari = new ObservableCollection<CariKart>(sonuclar);
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
        if (SeciliCari == null) return;

        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        var sonuc = MessageBox.Show(
            $"{SeciliCari.Unvan} isimli cari kaydını silmek istediğinize emin misiniz?",
            "Kayıt Silme Onayı (1/3)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (sonuc != MessageBoxResult.Yes) return;

        var sonuc2 = MessageBox.Show(
            "Bu işlem geri alınamaz. Kesinlikle silmek istiyor musunuz?",
            "Kayıt Silme Onayı (2/3)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Stop);

        if (sonuc2 != MessageBoxResult.Yes) return;

        var girisPenceresi = new Masaustu.Gorunumler.Ortak.MetinGirisPenceresi(
            "Silme Nedeni (3/3)",
            $"{SeciliCari.Unvan} carisini neden siliyorsunuz? Lütfen açıklayınız.");

        if (girisPenceresi.ShowDialog() != true)
        {
            return;
        }
        
        var silmeNedeni = girisPenceresi.GirilenMetin;

        IslemYapiliyor = true;
        ((KomutSarici)SilKomutu).BildirimGonder();

        try
        {
            await _cariServisi.SilAsync(SeciliCari.Id, silmeNedeni);
            BasariMesaji = $"{SeciliCari.Unvan} başarıyla silindi!";
            SeciliCari = null;
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
