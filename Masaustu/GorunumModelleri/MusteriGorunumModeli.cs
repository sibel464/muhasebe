using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using Masaustu.Servisler;

namespace Masaustu.GorunumModelleri;

public class MusteriGorunumModeli : TemelGorunumModeli
{
    private readonly ICariServisi _cariServisi;
    private readonly ICariTipiTanimiServisi _cariTipiServisi;

    private ObservableCollection<CariKart> _musteriler = new();
    private CariKart? _seciliMusteri;
    private string _aramaMetni = string.Empty;
    private string _aramaKodu = string.Empty;
    private string _aramaGrupKodu = string.Empty;
    private int? _aramaCariTipiId;
    private string _aramaIl = string.Empty;
    private int _bakiyeDurumu = 0; // 0: Tümü, 1: Borçlular, 2: Alacaklılar
    private bool _yukleniyor = false;
    private string _durumMesaji = string.Empty;
    private readonly NavigasyonServisi? _navigasyon;
    private readonly Action? _cikisAction;

    // Master-Detail Panel State
    private bool _formAcik;
    private MusteriDuzenleGorunumModeli? _formGorunumModeli;

    public bool FormAcik
    {
        get => _formAcik;
        set => OzellikAyarla(ref _formAcik, value);
    }

    public MusteriDuzenleGorunumModeli? FormGorunumModeli
    {
        get => _formGorunumModeli;
        set => OzellikAyarla(ref _formGorunumModeli, value);
    }

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
            (EkstreKomutu as KomutSarici)?.BildirimGonder();
            (OdemeKomutu as KomutSarici)?.BildirimGonder();
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

    public string AramaGrupKodu
    {
        get => _aramaGrupKodu;
        set { OzellikAyarla(ref _aramaGrupKodu, value); _ = AraAsync(); }
    }

    public int? AramaCariTipiId
    {
        get => _aramaCariTipiId;
        set { OzellikAyarla(ref _aramaCariTipiId, value); _ = AraAsync(); }
    }

    public string AramaIl
    {
        get => _aramaIl;
        set { OzellikAyarla(ref _aramaIl, value); _ = AraAsync(); }
    }

    // Tek cari listesi: müşteri, tedarikçi veya ikisi birden; "Müşteri ve Tedarikçi" türü her iki seçimde de görünür.
    public IReadOnlyList<KeyValuePair<CariTuru?, string>> TurFiltreleri { get; } =
        [new(null, "Tüm Cariler"), new(CariTuru.Musteri, "Müşteriler"), new(CariTuru.Tedarikci, "Tedarikçiler")];
    private KeyValuePair<CariTuru?, string> _seciliTurFiltresi = new(null, "Tüm Cariler");
    public KeyValuePair<CariTuru?, string> SeciliTurFiltresi
    {
        get => _seciliTurFiltresi;
        set { OzellikAyarla(ref _seciliTurFiltresi, value); _ = AraAsync(); }
    }

    public int BakiyeDurumu
    {
        get => _bakiyeDurumu;
        set { OzellikAyarla(ref _bakiyeDurumu, value); _ = AraAsync(); }
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
    public decimal ToplamBorc => _musteriler.Sum(m => m.Borc);
    public decimal ToplamAlacak => _musteriler.Sum(m => m.Alacak);
    public decimal NetBakiye => ToplamBorc - ToplamAlacak;

    public ObservableCollection<CariTipiTanimi> CariTipleri { get; } = new();

    // Komutlar (Temel CRUD)
    public ICommand YeniEkleKomutu { get; }
    public ICommand DuzenleKomutu { get; }
    public ICommand SilKomutu { get; }
    public ICommand YenileKomutu { get; }
    public ICommand EkstreKomutu { get; }
    public ICommand TahsilatKomutu { get; }
    public ICommand OdemeKomutu { get; }
    public ICommand DetayKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }
    public ICommand HizliFiltreKomutu { get; }
    public Func<CariKart, Task>? CariDetayAc { get; set; }

    // Hızlı İşlem Navigasyon Komutları (Alt Butonlar)
    public ICommand FisEkleKomutu { get; }
    public ICommand StoklarKomutu { get; }
    public ICommand RaporlarKomutu { get; }
    public ICommand AyarlarKomutu { get; }
    public ICommand CikisKomutu { get; }

    // Dialog açıcı delegesi (View'dan set edilecek)
    public Func<CariKart?, Task<bool>>? MusteriDuzenleFormAc { get; set; }
    public Func<CariKart, Task>? CariEkstreAc { get; set; }
    public Action<CariKart>? TahsilatAc { get; set; }
    public Action<CariKart>? OdemeAc { get; set; }

    public MusteriGorunumModeli(ICariServisi cariServisi, ICariTipiTanimiServisi cariTipiServisi, NavigasyonServisi? navigasyon = null, Action? cikisAction = null)
    {
        _cariServisi = cariServisi;
        _cariTipiServisi = cariTipiServisi;
        _navigasyon = navigasyon;
        _cikisAction = cikisAction;

        ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.Aktar(Musteriler, "Musteri_Listesi"));

        YeniEkleKomutu = new KomutSarici(async _ => await YeniEkleAsync());
        DuzenleKomutu = new KomutSarici(async _ => await DuzenleAsync(), _ => SeciliMusteri != null);
        SilKomutu = new KomutSarici(async _ => await SilAsync(), _ => SeciliMusteri != null);
        YenileKomutu = new KomutSarici(async _ => await AraAsync());
        EkstreKomutu = new KomutSarici(async obj => {
            var hedef = obj as CariKart ?? SeciliMusteri;
            if (hedef != null && CariEkstreAc != null) await CariEkstreAc(hedef);
        }, _ => SeciliMusteri != null);
        DetayKomutu = new KomutSarici(async obj =>
        {
            var cari = obj as CariKart ?? SeciliMusteri;
            if (cari != null && CariDetayAc != null) await CariDetayAc(cari);
        }, obj => obj is CariKart || SeciliMusteri != null);

        TahsilatKomutu = new KomutSarici(obj => {
            var cari = obj as CariKart ?? SeciliMusteri;
            if (cari != null && TahsilatAc != null)
                TahsilatAc(cari);
        }, _ => SeciliMusteri != null);

        // Tahsilat müşteriden, ödeme tedarikçiye yapılır (kural TahsilatOdemeServisi'nde).
        OdemeKomutu = new KomutSarici(obj => {
            var cari = obj as CariKart ?? SeciliMusteri;
            if (cari != null && OdemeAc != null)
                OdemeAc(cari);
        }, _ => SeciliMusteri != null);

        HizliFiltreKomutu = new KomutSarici(p => {
            var mod = p?.ToString();
            if (mod == "borclu") BakiyeDurumu = 1;
            else if (mod == "alacakli") BakiyeDurumu = 2;
            else BakiyeDurumu = 0;
        });

        // Hızlı Buton Navigasyonları
        FisEkleKomutu = new KomutSarici(_ => _navigasyon?.Git("Fatura"));
        StoklarKomutu = new KomutSarici(_ => _navigasyon?.Git("Urun"));
        RaporlarKomutu = new KomutSarici(_ => _navigasyon?.Git("Rapor"));
        AyarlarKomutu = new KomutSarici(_ => _navigasyon?.Git("Ayarlar"));
        CikisKomutu = new KomutSarici(_ => _cikisAction?.Invoke());

        _ = BaslatAsync();
    }

    private readonly SemaphoreSlim _yuklemeKilidi = new(1, 1);

    private async Task BaslatAsync()
    {
        await _yuklemeKilidi.WaitAsync();
        try
        {
            await CariTipleriniYukleKilitsizAsync();
            await AraKilitsizAsync();
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Veriler yüklenirken hata: {ex.Message}";
        }
        finally
        {
            _yuklemeKilidi.Release();
        }
    }

    private async Task CariTipleriniYukleAsync()
    {
        await _yuklemeKilidi.WaitAsync();
        try
        {
            await CariTipleriniYukleKilitsizAsync();
        }
        finally
        {
            _yuklemeKilidi.Release();
        }
    }

    private async Task CariTipleriniYukleKilitsizAsync()
    {
        try
        {
            var tipler = await _cariTipiServisi.ListeleAsync(sadeceAktif: true);
            CariTipleri.Clear();
            foreach (var tip in tipler)
                CariTipleri.Add(tip);
        }
        catch { }
    }

    private async Task AraAsync()
    {
        await _yuklemeKilidi.WaitAsync();
        try
        {
            await AraKilitsizAsync();
        }
        finally
        {
            _yuklemeKilidi.Release();
        }
    }

    private async Task AraKilitsizAsync()
    {
        Yukleniyor = true;
        DurumMesaji = "Filtreleniyor...";
        try
        {
            var filtre = new CariFiltre
            {
                AramaMetni = AramaMetni,
                Kod = AramaKodu,
                GrupKodu = AramaGrupKodu,
                Tur = SeciliTurFiltresi.Key,
                CariTipiId = AramaCariTipiId,
                Il = AramaIl,
                SadeceBorclular = BakiyeDurumu == 1,
                SadeceAlacaklilar = BakiyeDurumu == 2
            };

            var sonuclar = await _cariServisi.FiltreliAraAsync(filtre);
            Musteriler = new ObservableCollection<CariKart>(sonuclar);
            DurumMesaji = $"{Musteriler.Count} cari listeleniyor.";
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

    public Task AraAsyncPublic() => AraAsync();

    private async Task YeniEkleAsync()
    {
        if (MusteriDuzenleFormAc == null) return;
        var kaydedildi = await MusteriDuzenleFormAc(null);
        if (kaydedildi) await AraAsync();
    }

    private async Task DuzenleAsync()
    {
        if (SeciliMusteri == null || MusteriDuzenleFormAc == null) return;
        var kaydedildi = await MusteriDuzenleFormAc(SeciliMusteri);
        if (kaydedildi) await AraAsync();
    }

    private async Task SilAsync()
    {
        if (SeciliMusteri == null) return;
        if (!CariSilmeOnayi.Al(SeciliMusteri, out var silmeNedeni)) return;

        try
        {
            var basarili = await _cariServisi.SilAsync(SeciliMusteri.Id, silmeNedeni);
            if (basarili)
            {
                DurumMesaji = $"'{SeciliMusteri.Unvan}' silindi. Cari Silme ekranından geri alınabilir.";
                SeciliMusteri = null;
                await AraAsync();
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Hareketi olan cari silinemez; servis ne yapılacağını mesajda söyler.
            System.Windows.MessageBox.Show(
                ex.Message,
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
        BildirimGonder(nameof(ToplamBorc));
        BildirimGonder(nameof(ToplamAlacak));
        BildirimGonder(nameof(NetBakiye));
    }
}
