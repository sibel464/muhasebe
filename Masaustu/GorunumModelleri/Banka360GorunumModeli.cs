using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Finans360;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class Banka360GorunumModeli : TemelGorunumModeli
{
    private readonly IFinansMerkeziServisi _finansServisi;
    private readonly IAktifFirmaBaglami _firmaBaglami;

    private int _bankaHesapId;
    private BankaHesapOzetDto? _bankaHesapOzet;
    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;
    private int _seciliSekmeIndeksi;

    public ObservableCollection<YuruyenFinansSatirDto> TumHareketler { get; } = new();
    public ObservableCollection<YuruyenFinansSatirDto> Tahsilatlar { get; } = new();
    public ObservableCollection<YuruyenFinansSatirDto> Odemeler { get; } = new();
    public ObservableCollection<YuruyenFinansSatirDto> Transferler { get; } = new();
    public ObservableCollection<PosIslemOzetDto> PosIslemleri { get; } = new();

    public BankaHesapOzetDto? BankaHesapOzet
    {
        get => _bankaHesapOzet;
        private set => OzellikAyarla(ref _bankaHesapOzet, value);
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

    public Banka360GorunumModeli(
        IFinansMerkeziServisi finansServisi,
        IAktifFirmaBaglami firmaBaglami)
    {
        _finansServisi = finansServisi;
        _firmaBaglami = firmaBaglami;

        YenileKomutu = new KomutSarici(async _ => await YukleAsync(_bankaHesapId));
    }

    public async Task YukleAsync(int bankaHesapId)
    {
        _bankaHesapId = bankaHesapId;
        Yukleniyor = true;
        DurumMesaji = "Banka 360° detayları yükleniyor...";

        try
        {
            var hesaplar = await _finansServisi.GetBankaHesaplariAsync(_firmaBaglami.SirketId);
            BankaHesapOzet = hesaplar.FirstOrDefault(x => x.Id == bankaHesapId);

            var hareketler = await _finansServisi.GetBankaHareketleri360Async(bankaHesapId, _firmaBaglami.SirketId);

            TumHareketler.Clear();
            Tahsilatlar.Clear();
            Odemeler.Clear();
            Transferler.Clear();

            foreach (var h in hareketler)
            {
                TumHareketler.Add(h);
                if (h.Giris > 0 && !h.IslemTuru.Contains("Virman") && !h.IslemTuru.Contains("Transfer")) Tahsilatlar.Add(h);
                if (h.Cikis > 0 && !h.IslemTuru.Contains("Virman") && !h.IslemTuru.Contains("Transfer")) Odemeler.Add(h);
                if (h.IslemTuru.Contains("Virman") || h.IslemTuru.Contains("Transfer")) Transferler.Add(h);
            }

            PosIslemleri.Clear();
            var poslar = await _finansServisi.GetPosIslemleriAsync(_firmaBaglami.SirketId);
            foreach (var p in poslar) PosIslemleri.Add(p);

            DurumMesaji = $"{TumHareketler.Count} banka hareketi yüklendi.";
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "Banka360GorunumModeli");
            DurumMesaji = "Banka detayları yüklenirken bir hata oluştu.";
        }
        finally
        {
            Yukleniyor = false;
        }
    }
}
