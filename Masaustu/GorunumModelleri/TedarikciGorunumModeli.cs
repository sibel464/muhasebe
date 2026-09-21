using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using Masaustu.Servisler;

namespace Masaustu.GorunumModelleri;

public class TedarikciGorunumModeli : TemelGorunumModeli
{
    private readonly ICariServisi _cariServisi;

    private ObservableCollection<CariKart> _Tedarikciler = new();
    private CariKart? _seciliTedarikci;
    private string _aramaMetni = string.Empty;
    private string _aramaKodu = string.Empty;
    private string _aramaGrupKodu = string.Empty;
    private bool _yukleniyor = false;
    private string _durumMesaji = string.Empty;
    private readonly NavigasyonServisi? _navigasyon;
    private readonly Action? _cikisAction;

    public ObservableCollection<CariKart> Tedarikciler
    {
        get => _Tedarikciler;
        set => OzellikAyarla(ref _Tedarikciler, value);
    }

    public CariKart? SeciliTedarikci
    {
        get => _seciliTedarikci;
        set
        {
            OzellikAyarla(ref _seciliTedarikci, value);
            (DuzenleKomutu as KomutSarici)?.BildirimGonder();
            (SilKomutu as KomutSarici)?.BildirimGonder();
            (EkstreKomutu as KomutSarici)?.BildirimGonder();
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

    // Ã–zet Bilgiler (Sahte veri yok, sadece filtreye uygun gerÃ§ek sayÄ±lar)
    public int ToplamTedarikci => _Tedarikciler.Count;
    public int AktifTedarikci => _Tedarikciler.Count(m => m.Aktif);
    public int PasifTedarikci => _Tedarikciler.Count(m => !m.Aktif);

    // Komutlar (Temel CRUD)
    public ICommand YeniEkleKomutu { get; }
    public ICommand DuzenleKomutu { get; }
    public ICommand SilKomutu { get; }
    public ICommand YenileKomutu { get; }
    public ICommand EkstreKomutu { get; }
    public ICommand TahsilatKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }

    // HÄ±zlÄ± Ä°ÅŸlem Navigasyon KomutlarÄ± (Alt Butonlar)
    public ICommand FisEkleKomutu { get; }
    public ICommand StoklarKomutu { get; }
    public ICommand RaporlarKomutu { get; }
    public ICommand AyarlarKomutu { get; }
    public ICommand CikisKomutu { get; }

    // Dialog aÃ§Ä±cÄ± delegesi (View'dan set edilecek)
    public Func<CariKart?, Task<bool>>? TedarikciDuzenleFormAc { get; set; }
    public Func<CariKart, Task>? CariEkstreAc { get; set; }

    public TedarikciGorunumModeli(ICariServisi cariServisi, NavigasyonServisi? navigasyon = null, Action? cikisAction = null)
    {
        _cariServisi = cariServisi;
        _navigasyon = navigasyon;
        _cikisAction = cikisAction;

        ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.AktarCsv(Tedarikciler, "Tedarikci_Listesi"));

        YeniEkleKomutu = new KomutSarici(async _ => await YeniEkleAsync());
        DuzenleKomutu = new KomutSarici(async _ => await DuzenleAsync(), _ => SeciliTedarikci != null);
        SilKomutu = new KomutSarici(async _ => await SilAsync(), _ => SeciliTedarikci != null);
        YenileKomutu = new KomutSarici(async _ => await AraAsync());
        EkstreKomutu = new KomutSarici(async obj => {
            var hedef = obj as CariKart ?? SeciliTedarikci;
            if (hedef != null && CariEkstreAc != null) await CariEkstreAc(hedef);
        }, _ => SeciliTedarikci != null);

        TahsilatKomutu = new KomutSarici(_ => {
            System.Windows.MessageBox.Show("Tahsilat ve Ödeme (Kasa/Banka) modülü yapım aşamasındadır (FAZ 5).", "Bilgi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }, _ => SeciliTedarikci != null);

        // HÄ±zlÄ± Buton NavigasyonlarÄ±
        FisEkleKomutu = new KomutSarici(_ => _navigasyon?.Git("Fatura"));
        StoklarKomutu = new KomutSarici(_ => _navigasyon?.Git("Urun"));
        RaporlarKomutu = new KomutSarici(_ => _navigasyon?.Git("Rapor"));
        AyarlarKomutu = new KomutSarici(_ => _navigasyon?.Git("Ayarlar"));
        CikisKomutu = new KomutSarici(_ => _cikisAction?.Invoke());

        _ = AraAsync();
    }

    private async Task AraAsync()
    {
        Yukleniyor = true;
        DurumMesaji = "Filtreleniyor...";
        try
        {
            var liste = await _cariServisi.TumunuGetirAsync(CariTuru.Tedarikci);
            
            // 3'lÃ¼ Arama Filtresi (PRD Madde 13, 14)
            if (!string.IsNullOrWhiteSpace(AramaKodu))
                liste = liste.Where(x => x.Kod.Contains(AramaKodu, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(AramaGrupKodu))
                liste = liste.Where(x => !string.IsNullOrEmpty(x.GrupKodu) && x.GrupKodu.Contains(AramaGrupKodu, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(AramaMetni))
            {
                var aranacak = AramaMetni.ToLower();
                liste = liste.Where(x => 
                    x.Unvan.ToLower().Contains(aranacak) || 
                    (x.Ad != null && x.Ad.ToLower().Contains(aranacak)) || 
                    (x.Soyad != null && x.Soyad.ToLower().Contains(aranacak)) || 
                    (x.CepTelefonu != null && x.CepTelefonu.Contains(aranacak))).ToList();
            }

            Tedarikciler = new ObservableCollection<CariKart>(liste);
            DurumMesaji = $"{Tedarikciler.Count} mÃ¼ÅŸteri listeleniyor.";
            OzettleriGuncelle();
        }
        catch (Exception ex)
        {
            DurumMesaji = "Arama sÄ±rasÄ±nda hata oluÅŸtu.";
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task YeniEkleAsync()
    {
        if (TedarikciDuzenleFormAc == null) return;
        
        var kaydedildi = await TedarikciDuzenleFormAc(null);
        if (kaydedildi) await AraAsync();
    }

    private async Task DuzenleAsync()
    {
        if (SeciliTedarikci == null || TedarikciDuzenleFormAc == null) return;
        
        var kaydedildi = await TedarikciDuzenleFormAc(SeciliTedarikci);
        if (kaydedildi) await AraAsync();
    }

    private async Task SilAsync()
    {
        if (SeciliTedarikci == null) return;

        var sonuc = System.Windows.MessageBox.Show(
            $"'{SeciliTedarikci.Unvan}' adlı tedarikçiyi silmek istediğinize emin misiniz?",
            "Tedarikçi Sil Onayı (1/3)",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (sonuc != System.Windows.MessageBoxResult.Yes) return;

        var sonuc2 = System.Windows.MessageBox.Show(
            "Bu işlem geri alınamaz. Kesinlikle silmek istiyor musunuz?",
            "Tedarikçi Sil Onayı (2/3)",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Stop);

        if (sonuc2 != System.Windows.MessageBoxResult.Yes) return;

        var girisPenceresi = new Masaustu.Gorunumler.Ortak.MetinGirisPenceresi(
            "Silme Nedeni (3/3)",
            $"{SeciliTedarikci.Unvan} tedarikçisini neden siliyorsunuz? Lütfen açıklayınız.");

        if (girisPenceresi.ShowDialog() != true)
        {
            return;
        }

        var silmeNedeni = girisPenceresi.GirilenMetin;

        try
        {
            var basarili = await _cariServisi.SilAsync(SeciliTedarikci.Id, silmeNedeni);
            if (basarili)
            {
                DurumMesaji = "MÃ¼ÅŸteri silindi (Pasife alÄ±ndÄ±).";
                SeciliTedarikci = null;
                await AraAsync();
            }
        }
        catch (InvalidOperationException ex)
        {
            // Ticari Ä°liÅŸki GÃ¼venlik UyarÄ±sÄ± (FaturasÄ± veya hareketi varsa)
            System.Windows.MessageBox.Show(
                ex.Message, // "Bu cari kart geÃ§miÅŸ iÅŸlemler nedeniyle silinemez. Pasif duruma alÄ±nabilir."
                "Silme Engellendi",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "Silme iÅŸlemi sÄ±rasÄ±nda hata oluÅŸtu:\n\n" + ex.Message,
                "Hata",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void OzettleriGuncelle()
    {
        BildirimGonder(nameof(ToplamTedarikci));
        BildirimGonder(nameof(AktifTedarikci));
        BildirimGonder(nameof(PasifTedarikci));
    }
}

