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
    private CariKart _cariKart;

    private ObservableCollection<CariHareketSatiri> _hareketler = new();
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
    public Action? KapatmaTetikle { get; set; }

    public CariHareketGorunumModeli(ICariHareketServisi hareketServisi, ICariServisi cariServisi, CariKart cariKart)
    {
        _hareketServisi = hareketServisi;
        _cariServisi = cariServisi;
        _cariKart = cariKart;

        YenileKomutu = new KomutSarici(async _ => await HareketleriYukleAsync());

        _ = HareketleriYukleAsync();
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
                    HareketTuruMetni = HareketTuruIsminiGetir(h.HareketTuru),
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

    private string HareketTuruIsminiGetir(CariHareketTuru tur)
    {
        return tur switch
        {
            CariHareketTuru.Devir => "Devir Bakiyesi",
            CariHareketTuru.SatisFaturasi => "Satış Faturası",
            CariHareketTuru.AlisFaturasi => "Alış Faturası",
            CariHareketTuru.Tahsilat => "Tahsilat Fişi",
            CariHareketTuru.Odeme => "Ödeme Fişi",
            CariHareketTuru.SatisIadesi => "Satış İade Fişi",
            CariHareketTuru.AlisIadesi => "Alış İade Fişi",
            CariHareketTuru.Diger => "Virman/Düzeltme Fişi",
            _ => "Diğer"
        };
    }
}
