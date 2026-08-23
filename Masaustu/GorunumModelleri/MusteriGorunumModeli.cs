using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class MusteriGorunumModeli : TemelGorunumModeli
{
    private readonly ICariServisi _cariServisi;

    private ObservableCollection<CariKart> _musteriler = new();
    private CariKart? _seciliMusteri;
    private string _aramaMetni = string.Empty;
    private bool _yukleniyor = false;
    private string _durumMesaji = string.Empty;

    public ObservableCollection<CariKart> Musteriler
    {
        get => _musteriler;
        set => OzellikAyarla(ref _musteriler, value);
    }

    public CariKart? SeciliMusteri
    {
        get => _seciliMusteri;
        set
        {
            OzellikAyarla(ref _seciliMusteri, value);
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

    // Özet Bilgiler (Sahte veri yok, sadece filtreye uygun gerçek sayılar)
    public int ToplamMusteri => _musteriler.Count;
    public int AktifMusteri => _musteriler.Count(m => m.Aktif);
    public int PasifMusteri => _musteriler.Count(m => !m.Aktif);

    // Komutlar
    public ICommand YeniEkleKomutu { get; }
    public ICommand DuzenleKomutu { get; }
    public ICommand SilKomutu { get; }
    public ICommand YenileKomutu { get; }
    public ICommand EkstreKomutu { get; }

    // Dialog açıcı delegesi (View'dan set edilecek)
    public Func<CariKart?, Task<bool>>? MusteriDuzenleFormAc { get; set; }
    public Func<CariKart, Task>? CariEkstreAc { get; set; }

    public MusteriGorunumModeli(ICariServisi cariServisi)
    {
        _cariServisi = cariServisi;

        YeniEkleKomutu = new KomutSarici(async _ => await YeniEkleAsync());
        DuzenleKomutu = new KomutSarici(async _ => await DuzenleAsync(), _ => SeciliMusteri != null);
        SilKomutu = new KomutSarici(async _ => await SilAsync(), _ => SeciliMusteri != null);
        YenileKomutu = new KomutSarici(async _ => await VerileriYukleAsync());
        EkstreKomutu = new KomutSarici(async obj => { if (obj is CariKart c) { if (CariEkstreAc != null) await CariEkstreAc(c); } });

        _ = VerileriYukleAsync();
    }

    public async Task VerileriYukleAsync()
    {
        Yukleniyor = true;
        DurumMesaji = "Yükleniyor...";
        try
        {
            var liste = await _cariServisi.TumunuGetirAsync(CariTuru.Musteri);
            Musteriler = new ObservableCollection<CariKart>(liste);
            OzettleriGuncelle();
            DurumMesaji = $"{Musteriler.Count} müşteri listeleniyor.";
        }
        catch (Exception ex)
        {
            DurumMesaji = "Veritabanı hatası!";
            System.Windows.MessageBox.Show(
                "Müşteri verileri yüklenirken hata oluştu:\n\n" + ex.Message,
                "Bağlantı Hatası",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
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

        try
        {
            var sonuclar = await _cariServisi.AraAsync(AramaMetni, CariTuru.Musteri);
            Musteriler = new ObservableCollection<CariKart>(sonuclar);
            DurumMesaji = $"{Musteriler.Count} sonuç bulundu.";
            OzettleriGuncelle();
        }
        catch (Exception ex)
        {
            DurumMesaji = "Arama sırasında hata oluştu.";
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
    }

    private async Task YeniEkleAsync()
    {
        if (MusteriDuzenleFormAc == null) return;
        
        var kaydedildi = await MusteriDuzenleFormAc(null);
        if (kaydedildi) await VerileriYukleAsync();
    }

    private async Task DuzenleAsync()
    {
        if (SeciliMusteri == null || MusteriDuzenleFormAc == null) return;
        
        var kaydedildi = await MusteriDuzenleFormAc(SeciliMusteri);
        if (kaydedildi) await VerileriYukleAsync();
    }

    private async Task SilAsync()
    {
        if (SeciliMusteri == null) return;

        var sonuc = System.Windows.MessageBox.Show(
            $"'{SeciliMusteri.Unvan}' adlı müşteriyi silmek istediğinize emin misiniz?",
            "Müşteri Sil",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (sonuc != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            var basarili = await _cariServisi.SilAsync(SeciliMusteri.Id);
            if (basarili)
            {
                DurumMesaji = "Müşteri silindi (Pasife alındı).";
                SeciliMusteri = null;
                await VerileriYukleAsync();
            }
        }
        catch (InvalidOperationException ex)
        {
            // Ticari İlişki Güvenlik Uyarısı (Faturası veya hareketi varsa)
            System.Windows.MessageBox.Show(
                ex.Message, // "Bu cari kart geçmiş işlemler nedeniyle silinemez. Pasif duruma alınabilir."
                "Silme Engellendi",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "Silme işlemi sırasında hata oluştu:\n\n" + ex.Message,
                "Hata",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void OzettleriGuncelle()
    {
        BildirimGonder(nameof(ToplamMusteri));
        BildirimGonder(nameof(AktifMusteri));
        BildirimGonder(nameof(PasifMusteri));
    }
}
