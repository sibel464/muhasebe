using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class CariHareketSatiri
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public CariHareketTuru HareketTuru { get; set; }
    public string HareketTuruMetni { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public string? BelgeNo { get; set; }
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
    public decimal Bakiye { get; set; } // Satır bazında kümülatif bakiye
}

public class CariHareketGorunumModeli : TemelGorunumModeli
{
    private readonly ICariHareketServisi _hareketServisi;
    private readonly ICariServisi _cariServisi;
    private readonly IBelgeYazdirmaServisi? _yazdirmaServisi;
    private readonly IRaporMerkeziServisi? _raporServisi;
    private DateTime _mutabakatTarihi = DateTime.Today;
    private bool _baBsEkle = true;
    private CariKart _cariKart;

    private ObservableCollection<CariHareketSatiri> _hareketler = new();
    private List<CariHareket> _hamHareketler = new();
    private bool _yukleniyor = false;
    private string _durumMesaji = string.Empty;

    public CariKart CariKart
    {
        get => _cariKart;
        set => OzellikAyarla(ref _cariKart, value);
    }

    public ObservableCollection<CariHareketSatiri> Hareketler
    {
        get => _hareketler;
        set => OzellikAyarla(ref _hareketler, value);
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

    // Toplam Özetler (Gerçek CariKart verilerinden beslenir)
    public decimal ToplamBorc => CariKart.Borc;
    public decimal ToplamAlacak => CariKart.Alacak;
    public decimal Bakiye => CariKart.Bakiye;
    public string BakiyeTuru => Bakiye >= 0 ? "B" : "A"; // B: Borçlu (Alacaklı Değil), A: Alacaklı
    public decimal MutlakBakiye => Math.Abs(Bakiye);

    public ICommand YenileKomutu { get; }
    public ICommand EkstreYazdirKomutu { get; }
    public ICommand MutabakatYazdirKomutu { get; }

    public DateTime MutabakatTarihi
    {
        get => _mutabakatTarihi;
        set => OzellikAyarla(ref _mutabakatTarihi, value);
    }

    // Mektuba mutabakat tarihinin ayına ait Ba-Bs tutarları da eklensin mi
    public bool BaBsEkle
    {
        get => _baBsEkle;
        set => OzellikAyarla(ref _baBsEkle, value);
    }
    public Action? KapatmaTetikle { get; set; }

    public CariHareketGorunumModeli(
        ICariHareketServisi hareketServisi, 
        ICariServisi cariServisi, 
        CariKart cariKart,
        IBelgeYazdirmaServisi? yazdirmaServisi = null,
        IRaporMerkeziServisi? raporServisi = null)
    {
        _hareketServisi = hareketServisi;
        _cariServisi = cariServisi;
        _cariKart = cariKart;
        _yazdirmaServisi = yazdirmaServisi;
        _raporServisi = raporServisi;

        YenileKomutu = new KomutSarici(async _ => await HareketleriYukleAsync());
        EkstreYazdirKomutu = new KomutSarici(async _ =>
        {
            if (_yazdirmaServisi == null) return;
            try { await _yazdirmaServisi.CariEkstreYazdirAsync(CariKart, _hamHareketler); }
            catch (Exception ex) { DurumMesaji = "Ekstre yazdırılamadı: " + ex.Message; }
        });
        MutabakatYazdirKomutu = new KomutSarici(async _ => await MutabakatYazdirAsync());

        _ = HareketleriYukleAsync();
    }

    // Mutabakat tarihindeki bakiye o güne kadarki hareketlerden hesaplanır (iptal ve ters kaydı birbirini götürür).
    public decimal TarihtekiBakiye(DateTime tarih) =>
        _hamHareketler.Where(h => h.Tarih.Date <= tarih.Date).Sum(h => h.Borc - h.Alacak);

    private async Task MutabakatYazdirAsync()
    {
        if (_yazdirmaServisi == null) return;
        try
        {
            var tarih = MutabakatTarihi.Date;
            List<Uygulama.Dto.Rapor.BaBsKalemiDto>? baBs = null;
            if (BaBsEkle && _raporServisi != null)
            {
                try
                {
                    var ayBasi = new DateTime(tarih.Year, tarih.Month, 1);
                    baBs = await _raporServisi.GetBaBsRaporuAsync(new Uygulama.Dto.Rapor.SatisRaporFiltreDto { BaslangicTarihi = ayBasi, BitisTarihi = ayBasi.AddMonths(1).AddTicks(-1), CariId = CariKart.Id }, 0m);
                }
                catch (YetkisizErisimException)
                {
                    DurumMesaji = "Rapor yetkiniz olmadığı için Ba-Bs bölümü mektuba eklenmedi.";
                }
            }
            await _yazdirmaServisi.MutabakatMektubuYazdirAsync(CariKart, tarih, TarihtekiBakiye(tarih), baBs);
        }
        catch (Exception ex)
        {
            DurumMesaji = "Mutabakat mektubu hazırlanamadı: " + ex.Message;
        }
    }

    public async Task HareketleriYukleAsync()
    {
        Yukleniyor = true;
        DurumMesaji = "Hareketler yükleniyor...";
        try
        {
            // Veritabanından CariKart bilgilerini en güncel bakiye için çekelim
            var guncelCari = await _cariServisi.IdIleGetirAsync(CariKart.Id);
            if (guncelCari != null)
            {
                CariKart = guncelCari;
                BildirimGonder(nameof(ToplamBorc));
                BildirimGonder(nameof(ToplamAlacak));
                BildirimGonder(nameof(Bakiye));
                BildirimGonder(nameof(BakiyeTuru));
                BildirimGonder(nameof(MutlakBakiye));
            }

            var dbHareketler = await _hareketServisi.CariKartinHareketleriniGetirAsync(CariKart.Id);
            _hamHareketler = dbHareketler;
            var list = new List<CariHareketSatiri>();
            decimal kumulatifBakiye = 0;

            foreach (var h in dbHareketler)
            {
                // Muhasebe kümülatif bakiye mantığı:
                // Borç hanesi bakiyeyi artırır, alacak hanesi düşürür.
                kumulatifBakiye += (h.Borc - h.Alacak);

                list.Add(new CariHareketSatiri
                {
                    Id = h.Id,
                    Tarih = h.Tarih,
                    HareketTuru = h.HareketTuru,
                    HareketTuruMetni = h.HareketTuru.Ad(),
                    Aciklama = h.Aciklama,
                    BelgeNo = h.BelgeNo,
                    Borc = h.Borc,
                    Alacak = h.Alacak,
                    Bakiye = kumulatifBakiye
                });
            }

            // Arayüze en yeni işlemler üstte olacak şekilde tarih sırasına göre tersten de gösterilebilir.
            // Ancak kümülatif bakiye takibinin kronolojik (tarih sırası) izlenmesi ticari bir kuraldır.
            // Bu nedenle sıralamayı bozmadan listeliyoruz.
            Hareketler = new ObservableCollection<CariHareketSatiri>(list);
            DurumMesaji = $"{Hareketler.Count} hareket listelendi.";
        }
        catch (Exception ex)
        {
            DurumMesaji = "Yükleme başarısız.";
            System.Windows.MessageBox.Show(
                "Cari hareketler yüklenirken hata oluştu:\n\n" + ex.Message,
                "Hata",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            Yukleniyor = false;
        }
    }
}
