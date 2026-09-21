using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using Masaustu.Servisler;

namespace Masaustu.GorunumModelleri;

public class UrunGorunumModeli : TemelGorunumModeli
{
    private readonly IUrunServisi _urunServisi;
    private readonly IMerkeziTanimServisi _tanimServisi;

    private ObservableCollection<Urun> _urunler = new();
    private Urun? _seciliUrun;
    private string _aramaMetni = string.Empty;
    private string _aramaKodu = string.Empty;
    private string _aramaKategori = string.Empty;
    private bool _sadeceAktifler = true;
    private bool _sadeceKritikStok = false;
    private bool _yukleniyor = false;
    private string _durumMesaji = string.Empty;
    private readonly NavigasyonServisi? _navigasyon;
    private readonly Action? _cikisAction;

    // Master-Detail Panel State
    private bool _formAcik;
    private UrunDuzenleGorunumModeli? _formGorunumModeli;

    public bool FormAcik
    {
        get => _formAcik;
        set => OzellikAyarla(ref _formAcik, value);
    }

    public UrunDuzenleGorunumModeli? FormGorunumModeli
    {
        get => _formGorunumModeli;
        set => OzellikAyarla(ref _formGorunumModeli, value);
    }

    // Dialog açıcı delege (View'dan set edilecek)
    public Func<Urun?, Task<bool>>? UrunDuzenleFormAc { get; set; }

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
        set { OzellikAyarla(ref _aramaMetni, value); _ = AraAsync(); }
    }

    public string AramaKodu
    {
        get => _aramaKodu;
        set { OzellikAyarla(ref _aramaKodu, value); _ = AraAsync(); }
    }

    public string AramaKategori
    {
        get => _aramaKategori;
        set { OzellikAyarla(ref _aramaKategori, value); _ = AraAsync(); }
    }

    public bool SadeceAktifler
    {
        get => _sadeceAktifler;
        set { OzellikAyarla(ref _sadeceAktifler, value); _ = AraAsync(); }
    }

    public bool SadeceKritikStok
    {
        get => _sadeceKritikStok;
        set { OzellikAyarla(ref _sadeceKritikStok, value); _ = AraAsync(); }
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
    public int DusukStokUrun => _urunler.Count(u => u.StokMiktari <= u.MinimumStok && u.Aktif);

    // Temel Komutlar
    public ICommand YeniEkleKomutu { get; }
    public ICommand DuzenleKomutu { get; }
    public ICommand SilKomutu { get; }
    public ICommand YenileKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }

    // Hızlı İşlem Navigasyon Komutları (Alt Butonlar)
    public ICommand MusterilerKomutu { get; }
    public ICommand FisEkleKomutu { get; }
    public ICommand StoklarKomutu { get; }
    public ICommand CikisKomutu { get; }

    public UrunGorunumModeli(IUrunServisi urunServisi, IMerkeziTanimServisi tanimServisi, NavigasyonServisi? navigasyon = null, Action? cikisAction = null)
    {
        _urunServisi = urunServisi;
        _tanimServisi = tanimServisi;
        _navigasyon = navigasyon;
        _cikisAction = cikisAction;

        ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.AktarCsv(Urunler, "Stok_Urun_Listesi"));
        YeniEkleKomutu = new KomutSarici(async _ => await YeniEkleAsync());
        DuzenleKomutu = new KomutSarici(async _ => await DuzenleAsync(), _ => SeciliUrun != null);
        SilKomutu = new KomutSarici(async _ => await SilAsync(), _ => SeciliUrun != null);
        YenileKomutu = new KomutSarici(async _ => await AraAsync());

        // Hızlı Buton Navigasyonları
        MusterilerKomutu = new KomutSarici(_ => _navigasyon?.Git("Musteri"));
        FisEkleKomutu = new KomutSarici(_ => _navigasyon?.Git("Fatura"));
        StoklarKomutu = new KomutSarici(_ => {
            System.Windows.MessageBox.Show("Stok Hareketleri ve Raporlaması yakında eklenecektir.", "Bilgi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }, _ => SeciliUrun != null);
        CikisKomutu = new KomutSarici(_ => _cikisAction?.Invoke());

        _ = AraAsync();
    }

    private async Task AraAsync()
    {
        Yukleniyor = true;
        DurumMesaji = "Filtreleniyor...";
        try
        {
            var filtre = new UrunFiltre
            {
                AramaMetni = AramaMetni,
                Kategori = AramaKategori,
                SadeceAktifler = SadeceAktifler,
                SadeceKritikStok = SadeceKritikStok
            };

            var sonuclar = await _urunServisi.FiltreliAraAsync(filtre);
            var sorgu = sonuclar.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(AramaKodu))
                sorgu = sorgu.Where(u => u.Kod.Contains(AramaKodu, StringComparison.OrdinalIgnoreCase) || 
                                         (u.Barkod != null && u.Barkod.Contains(AramaKodu, StringComparison.OrdinalIgnoreCase)));

            Urunler = new ObservableCollection<Urun>(sorgu.ToList());
            DurumMesaji = $"{Urunler.Count} ürün listeleniyor.";
            OzettleriGuncelle();
        }
        catch (Exception ex)
        {
            DurumMesaji = "Arama sırasında hata oluştu.";
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task YeniEkleAsync()
    {
        if (UrunDuzenleFormAc == null) return;
        var kaydedildi = await UrunDuzenleFormAc(null);
        if (kaydedildi) await AraAsync();
    }

    private async Task DuzenleAsync()
    {
        if (SeciliUrun == null || UrunDuzenleFormAc == null) return;
        var kaydedildi = await UrunDuzenleFormAc(SeciliUrun);
        if (kaydedildi) await AraAsync();
    }

    private async Task SilAsync()
    {
        if (SeciliUrun == null) return;

        var sonuc = System.Windows.MessageBox.Show(
            $"'{SeciliUrun.Ad}' adlı ürünü silmek istediğinize emin misiniz?",
            "Ürün Sil Onayı (1/3)",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (sonuc != System.Windows.MessageBoxResult.Yes) return;

        var sonuc2 = System.Windows.MessageBox.Show(
            "Bu işlem geri alınamaz. Kesinlikle silmek istiyor musunuz?",
            "Ürün Sil Onayı (2/3)",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Stop);

        if (sonuc2 != System.Windows.MessageBoxResult.Yes) return;

        var girisPenceresi = new Masaustu.Gorunumler.Ortak.MetinGirisPenceresi(
            "Silme Nedeni (3/3)",
            $"{SeciliUrun.Ad} ürününü neden siliyorsunuz? Lütfen açıklayınız.");

        if (girisPenceresi.ShowDialog() != true)
        {
            return;
        }

        var silmeNedeni = girisPenceresi.GirilenMetin;

        var basarili = await _urunServisi.SilAsync(SeciliUrun.Id, silmeNedeni);
        if (basarili)
        {
            DurumMesaji = $"'{SeciliUrun.Ad}' silindi.";
            SeciliUrun = null;
            await AraAsync();
        }
    }

    private void OzettleriGuncelle()
    {
        BildirimGonder(nameof(ToplamUrun));
        BildirimGonder(nameof(AktifUrun));
        BildirimGonder(nameof(DusukStokUrun));
    }
}
