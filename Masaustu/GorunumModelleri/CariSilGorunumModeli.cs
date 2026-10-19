using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

// Cari silme: yalnızca hiç işlem görmemiş cari silinir (kural serviste); silinen cari geri alınabilir.
// KVKK: seçili (aranan ya da silinmiş) carinin kişisel veri dökümü alınır, yasal saklama süresi dolmuşsa anonimleştirilir.
public class CariSilGorunumModeli : TemelGorunumModeli
{
    private readonly ICariServisi _cariServisi;
    private readonly IKvkkServisi _kvkkServisi;
    private string _aramaMetni = string.Empty;
    private ObservableCollection<CariKart> _aramaSonuclari = new();
    private ObservableCollection<CariKart> _silinenler = new();
    private CariKart? _seciliCari;
    private CariKart? _seciliSilinen;
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

    public ObservableCollection<CariKart> Silinenler
    {
        get => _silinenler;
        set => OzellikAyarla(ref _silinenler, value);
    }

    public CariKart? SeciliCari
    {
        get => _seciliCari;
        set { OzellikAyarla(ref _seciliCari, value); ((KomutSarici)SilKomutu).BildirimGonder(); KvkkKomutlariniYenile(); }
    }

    public CariKart? SeciliSilinen
    {
        get => _seciliSilinen;
        set { OzellikAyarla(ref _seciliSilinen, value); ((KomutSarici)GeriAlKomutu).BildirimGonder(); KvkkKomutlariniYenile(); }
    }

    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }
    public string BasariMesaji { get => _basariMesaji; set => OzellikAyarla(ref _basariMesaji, value); }
    public bool IslemYapiliyor { get => _islemYapiliyor; set => OzellikAyarla(ref _islemYapiliyor, value); }

    public ICommand SilKomutu { get; }
    public ICommand GeriAlKomutu { get; }
    public ICommand KvkkDokumKomutu { get; }
    public ICommand KvkkAnonimlestirKomutu { get; }

    // KVKK işlemleri son seçilen cariye (üst ya da alt listeden) uygulanır.
    private CariKart? KvkkHedefi => _kvkkHedefi;
    private CariKart? _kvkkHedefi;
    private void KvkkKomutlariniYenile()
    {
        _kvkkHedefi = _seciliSilinen ?? _seciliCari;
        ((KomutSarici?)KvkkDokumKomutu)?.BildirimGonder();
        ((KomutSarici?)KvkkAnonimlestirKomutu)?.BildirimGonder();
    }

    private readonly SemaphoreSlim _sorguKilidi = new(1, 1);

    public CariSilGorunumModeli(ICariServisi cariServisi, IKvkkServisi kvkkServisi)
    {
        _cariServisi = cariServisi;
        _kvkkServisi = kvkkServisi;
        KvkkDokumKomutu = new KomutSarici(async () => await KvkkDokumAsync(), () => KvkkHedefi != null && !IslemYapiliyor);
        KvkkAnonimlestirKomutu = new KomutSarici(async () => await KvkkAnonimlestirAsync(), () => KvkkHedefi != null && !IslemYapiliyor);
        SilKomutu = new KomutSarici(async () => await SilAsync(), () => SeciliCari != null && !IslemYapiliyor);
        GeriAlKomutu = new KomutSarici(async () => await GeriAlAsync(), () => SeciliSilinen != null && !IslemYapiliyor);
        _ = YukleAsync();
    }

    private async Task YukleAsync()
    {
        await AraAsync();
        await SilinenleriYukleAsync();
    }

    // Servisin veritabanı bağlamı aynı anda tek sorgu çalıştırır; yazarken arama sorguları sıraya alınır.
    private async Task AraAsync()
    {
        await _sorguKilidi.WaitAsync();
        try
        {
            AramaSonuclari = new ObservableCollection<CariKart>(await _cariServisi.AraAsync(AramaMetni));
        }
        catch (Exception ex)
        {
            HataMesaji = $"Cariler listelenemedi: {ex.Message}";
        }
        finally
        {
            _sorguKilidi.Release();
        }
    }

    private async Task SilinenleriYukleAsync()
    {
        await _sorguKilidi.WaitAsync();
        try
        {
            Silinenler = new ObservableCollection<CariKart>(await _cariServisi.SilinenleriGetirAsync());
        }
        catch (Exception ex)
        {
            HataMesaji = $"Silinen cariler listelenemedi: {ex.Message}";
        }
        finally
        {
            _sorguKilidi.Release();
        }
    }

    private async Task SilAsync()
    {
        if (SeciliCari == null) return;
        var cari = SeciliCari;
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (!CariSilmeOnayi.Al(cari, out var neden)) return;

        IslemYapiliyor = true;
        ((KomutSarici)SilKomutu).BildirimGonder();
        try
        {
            await _cariServisi.SilAsync(cari.Id, neden);
            BasariMesaji = $"'{cari.Unvan}' silindi. Silinen Cariler listesinden geri alabilirsiniz.";
            SeciliCari = null;
            await YukleAsync();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            HataMesaji = ex.Message;
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "CariSil");
            HataMesaji = "Cari silinemedi. Ayrıntılar log dosyasına yazıldı.";
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
        var cari = SeciliSilinen;
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        IslemYapiliyor = true;
        ((KomutSarici)GeriAlKomutu).BildirimGonder();
        try
        {
            await _cariServisi.GeriAlAsync(cari.Id);
            BasariMesaji = $"'{cari.Unvan}' geri alındı ve tekrar aktif.";
            SeciliSilinen = null;
            await YukleAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            HataMesaji = ex.Message;
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "CariGeriAl");
            HataMesaji = "Cari geri alınamadı. Ayrıntılar log dosyasına yazıldı.";
        }
        finally
        {
            IslemYapiliyor = false;
            ((KomutSarici)GeriAlKomutu).BildirimGonder();
        }
    }

    private async Task KvkkDokumAsync()
    {
        if (KvkkHedefi is not { } cari) return;
        HataMesaji = BasariMesaji = string.Empty;
        try
        {
            var metin = await _kvkkServisi.KisiselVeriDokumuAsync(cari.Id);
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"KVKK-Dokum-{cari.Kod}-{DateTime.Today:yyyyMMdd}.txt", Filter = "Metin dosyası (*.txt)|*.txt", Title = "Kişisel veri dökümünü kaydet"
            };
            if (sfd.ShowDialog() != true) return;
            await System.IO.File.WriteAllTextAsync(sfd.FileName, metin, System.Text.Encoding.UTF8);
            BasariMesaji = $"Kişisel veri dökümü kaydedildi: {sfd.FileName}. Talep sahibine kimliği doğrulandıktan sonra verin.";
        }
        catch (Exception ex) when (ex is KeyNotFoundException or UnauthorizedAccessException or System.IO.IOException or YetkisizErisimException)
        {
            HataMesaji = ex.Message;
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "KvkkDokum");
            HataMesaji = "Döküm hazırlanamadı. Ayrıntılar log dosyasına yazıldı.";
        }
    }

    private async Task KvkkAnonimlestirAsync()
    {
        if (KvkkHedefi is not { } cari) return;
        HataMesaji = BasariMesaji = string.Empty;
        try
        {
            var durum = await _kvkkServisi.SaklamaDurumuAsync(cari.Id);
            if (!durum.AnonimlestirilebilirMi)
            {
                HataMesaji = durum.Aciklama;
                return;
            }
            var pencere = new Masaustu.Gorunumler.Ortak.MetinGirisPenceresi(
                "KVKK Anonimleştirme",
                $"{cari.Kod} - {cari.Unvan}\n\n{durum.Aciklama}\n\nAd, kimlik/vergi no, telefon, e-posta, adres ve IBAN kalıcı olarak silinir; " +
                "tutarlar ve belgeler kalır. Bu işlem GERİ ALINAMAZ.\n\nTalebi yazın (ör. 19.10.2026 tarihli e-posta başvurusu):");
            if (pencere.ShowDialog() != true) return;
            IslemYapiliyor = true;
            KvkkKomutlariniYenile();
            await _kvkkServisi.AnonimlestirAsync(cari.Id, pencere.GirilenMetin);
            BasariMesaji = $"{cari.Kod} carisinin kişisel verileri anonimleştirildi.";
            SeciliCari = null;
            SeciliSilinen = null;
            await YukleAsync();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or YetkisizErisimException)
        {
            HataMesaji = ex.Message;
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "KvkkAnonimlestir");
            HataMesaji = "Anonimleştirme yapılamadı. Ayrıntılar log dosyasına yazıldı.";
        }
        finally
        {
            IslemYapiliyor = false;
            KvkkKomutlariniYenile();
        }
    }
}

// Cari listesi ve Cari Silme ekranı aynı onay penceresini kullanır: tek pencerede onay + silme nedeni.
public static class CariSilmeOnayi
{
    public static bool Al(CariKart cari, out string neden)
    {
        var pencere = new Masaustu.Gorunumler.Ortak.MetinGirisPenceresi(
            "Cariyi Sil",
            $"{cari.Kod} - {cari.Unvan}\n\n" +
            "Yalnızca hiç işlem görmemiş cari silinebilir. Silinen cari Cari Silme ekranındaki " +
            "\"Silinen Cariler\" bölümünden geri alınabilir.\n\nSilme nedenini yazın:");
        neden = string.Empty;
        if (pencere.ShowDialog() != true) return false;
        neden = pencere.GirilenMetin;
        return true;
    }
}
