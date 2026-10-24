using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Finans360;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class Kasa360GorunumModeli : TemelGorunumModeli
{
    private readonly IFinansMerkeziServisi _finansServisi;
    private readonly IAktifFirmaBaglami _firmaBaglami;

    private int _kasaId;
    private KasaOzetDto? _kasaOzet;
    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;
    private int _seciliSekmeIndeksi;

    public ObservableCollection<YuruyenFinansSatirDto> TumHareketler { get; } = new();
    public ObservableCollection<YuruyenFinansSatirDto> Tahsilatlar { get; } = new();
    public ObservableCollection<YuruyenFinansSatirDto> Odemeler { get; } = new();
    public ObservableCollection<YuruyenFinansSatirDto> Virmanlar { get; } = new();

    public KasaOzetDto? KasaOzet
    {
        get => _kasaOzet;
        private set => OzellikAyarla(ref _kasaOzet, value);
    }

    public bool Yukleniyor
    {
        get => _yukleniyor;
        private set => OzellikAyarla(ref _yukleniyor, value);
    }

    public string DurumMesaji
    {
        get => _durumMesaji;
        private set => OzellikAyarla(ref _durumMesaji, value);
    }

    public int SeciliSekmeIndeksi
    {
        get => _seciliSekmeIndeksi;
        set => OzellikAyarla(ref _seciliSekmeIndeksi, value);
    }

    public ICommand YenileKomutu { get; }

    public Kasa360GorunumModeli(
        IFinansMerkeziServisi finansServisi,
        IAktifFirmaBaglami firmaBaglami)
    {
        _finansServisi = finansServisi;
        _firmaBaglami = firmaBaglami;

        YenileKomutu = new KomutSarici(async _ => await YukleAsync(_kasaId));
    }

    // Hesaplar arası para hareketi (virman, kasa ↔ banka, POS geçişi) tahsilat/ödeme sayılmaz.
    private static bool Transfer(YuruyenFinansSatirDto h) =>
        h.IslemTuru.Contains("Virman") || h.IslemTuru.Contains("Transfer") || h.IslemTuru.Contains("POS");

    public async Task YukleAsync(int kasaId)
    {
        _kasaId = kasaId;
        Yukleniyor = true;
        DurumMesaji = "Kasa 360° detayları yükleniyor...";

        try
        {
            var kasalar = await _finansServisi.GetKasalarAsync(_firmaBaglami.SirketId);
            KasaOzet = kasalar.FirstOrDefault(x => x.Id == kasaId);

            var hareketler = await _finansServisi.GetKasaHareketleri360Async(kasaId, _firmaBaglami.SirketId);

            TumHareketler.Clear();
            Tahsilatlar.Clear();
            Odemeler.Clear();
            Virmanlar.Clear();

            foreach (var h in hareketler)
            {
                TumHareketler.Add(h);
                if (h.Giris > 0 && !Transfer(h)) Tahsilatlar.Add(h);
                if (h.Cikis > 0 && !Transfer(h)) Odemeler.Add(h);
                if (Transfer(h)) Virmanlar.Add(h);
            }

            DurumMesaji = $"{TumHareketler.Count} kasa hareketi yüklendi.";
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "Kasa360GorunumModeli");
            DurumMesaji = "Kasa bilgileri yüklenirken bir hata oluştu.";
        }
        finally
        {
            Yukleniyor = false;
        }
    }
}
