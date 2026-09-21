using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Finans360;
using Masaustu.Yardimcilar;
using System.Windows;

namespace Masaustu.GorunumModelleri;

public class FinansMerkeziGorunumModeli : TemelGorunumModeli
{
    private readonly IFinansMerkeziServisi _finansServisi;
    private readonly IAktifFirmaBaglami _firmaBaglami;
    private readonly IKullaniciServisi _kullaniciServisi;

    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;
    private int _seciliSekmeIndeksi;

    private FinansDashboardOzetDto? _dashboard;
    private KasaOzetDto? _seciliKasa;
    private BankaHesapOzetDto? _seciliBankaHesabi;
    private PosIslemOzetDto? _seciliPosIslemi;
    private YuruyenFinansSatirDto? _seciliHareket;

    public ObservableCollection<KasaOzetDto> Kasalar { get; } = new();
    public ObservableCollection<BankaHesapOzetDto> BankaHesaplari { get; } = new();
    public ObservableCollection<YuruyenFinansSatirDto> Hareketler { get; } = new();
    public ObservableCollection<PosIslemOzetDto> PosIslemleri { get; } = new();
    public ObservableCollection<VadeKalemOzetDto> Vadeler { get; } = new();

    public FinansDashboardOzetDto? Dashboard
    {
        get => _dashboard;
        private set => OzellikAyarla(ref _dashboard, value);
    }

    public bool Yukleniyor
    {
        get => _yukleniyor;
        private set { OzellikAyarla(ref _yukleniyor, value); CommandManager.InvalidateRequerySuggested(); }
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

    public KasaOzetDto? SeciliKasa
    {
        get => _seciliKasa;
        set => OzellikAyarla(ref _seciliKasa, value);
    }

    public BankaHesapOzetDto? SeciliBankaHesabi
    {
        get => _seciliBankaHesabi;
        set => OzellikAyarla(ref _seciliBankaHesabi, value);
    }

    public PosIslemOzetDto? SeciliPosIslemi
    {
        get => _seciliPosIslemi;
        set => OzellikAyarla(ref _seciliPosIslemi, value);
    }

    public YuruyenFinansSatirDto? SeciliHareket
    {
        get => _seciliHareket;
        set => OzellikAyarla(ref _seciliHareket, value);
    }

    // Commands
    public ICommand YenileKomutu { get; }
    public ICommand Kasa360AcKomutu { get; }
    public ICommand Banka360AcKomutu { get; }
    public ICommand PosHesabaGecirKomutu { get; }
    public ICommand TransferAcKomutu { get; }
    public ICommand MutabakatAcKomutu { get; }
    public ICommand HareketIptalKomutu { get; }

    // Actions for view / navigation
    public Action<int>? Kasa360AcAction { get; set; }
    public Action<int>? Banka360AcAction { get; set; }
    public Action<string>? TransferAcAction { get; set; }
    public Action? MutabakatAcAction { get; set; }

    public FinansMerkeziGorunumModeli(
        IFinansMerkeziServisi finansServisi,
        IAktifFirmaBaglami firmaBaglami,
        IKullaniciServisi kullaniciServisi)
    {
        _finansServisi = finansServisi;
        _firmaBaglami = firmaBaglami;
        _kullaniciServisi = kullaniciServisi;

        YenileKomutu = new KomutSarici(async _ => await YukleAsync());

        Kasa360AcKomutu = new KomutSarici(p =>
        {
            int id = p is int i ? i : (SeciliKasa?.Id ?? 0);
            if (id > 0) Kasa360AcAction?.Invoke(id);
        });

        Banka360AcKomutu = new KomutSarici(p =>
        {
            int id = p is int i ? i : (SeciliBankaHesabi?.Id ?? 0);
            if (id > 0) Banka360AcAction?.Invoke(id);
        });

        PosHesabaGecirKomutu = new KomutSarici(async p =>
        {
            int id = p is int i ? i : (SeciliPosIslemi?.Id ?? 0);
            if (id > 0)
            {
                try
                {
                    await _finansServisi.PosIslemiHesabaGecirAsync(id, _firmaBaglami.SirketId, _kullaniciServisi.AktifKullanici?.Id ?? 1);
                    MessageBox.Show("POS işlemi başarıyla banka hesabına aktarıldı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    await YukleAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"POS aktarımı sırasında hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }, _ => _kullaniciServisi.YetkiliMi(x => x.POSYonet));

        TransferAcKomutu = new KomutSarici(p =>
        {
            string tur = p as string ?? "KasaVirman";
            TransferAcAction?.Invoke(tur);
        }, _ => _kullaniciServisi.YetkiliMi(x => x.VirmanYap));

        MutabakatAcKomutu = new KomutSarici(_ =>
        {
            MutabakatAcAction?.Invoke();
        }, _ => _kullaniciServisi.YetkiliMi(x => x.Mutabakat));

        HareketIptalKomutu = new KomutSarici(async _ =>
        {
            if (SeciliHareket == null) return;
            var res = MessageBox.Show($"Seçili {SeciliHareket.IslemNo} numaralı finansal işlemi iptal etmek istediğinize emin misiniz?", "İptal Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    await _finansServisi.FinansIslemIptalEtAsync(
                        SeciliHareket.Id,
                        SeciliHareket.Kaynak,
                        _firmaBaglami.SirketId,
                        _kullaniciServisi.AktifKullanici?.Id ?? 1,
                        "Kullanıcı tarafından Finans Merkezi üzerinden iptal edildi.");
                    MessageBox.Show("İşlem iptal edildi ve ters kayıt oluşturuldu.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    await YukleAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"İptal hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }, _ => SeciliHareket != null && SeciliHareket.Durum != "IptalEdildi" && _kullaniciServisi.YetkiliMi(x => x.FinansIptal));
    }

    public async Task YukleAsync()
    {
        Yukleniyor = true;
        DurumMesaji = "Finansal veriler yükleniyor...";
        try
        {
            Dashboard = await _finansServisi.GetDashboardOzetAsync(_firmaBaglami.SirketId);

            Kasalar.Clear();
            foreach (var k in Dashboard.KasaDagilimi) Kasalar.Add(k);

            BankaHesaplari.Clear();
            foreach (var b in Dashboard.BankaHesapBakiyeleri) BankaHesaplari.Add(b);

            Hareketler.Clear();
            foreach (var h in Dashboard.SonFinansalHareketler) Hareketler.Add(h);

            PosIslemleri.Clear();
            var poslar = await _finansServisi.GetPosIslemleriAsync(_firmaBaglami.SirketId);
            foreach (var p in poslar) PosIslemleri.Add(p);

            Vadeler.Clear();
            var vadeler = await _finansServisi.GetVadeMerkeziAsync(_firmaBaglami.SirketId);
            foreach (var v in vadeler) Vadeler.Add(v);

            DurumMesaji = "Finans verileri güncellendi.";
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "FinansMerkeziGorunumModeli");
            DurumMesaji = "Finans verileri yüklenirken bir hata oluştu.";
        }
        finally
        {
            Yukleniyor = false;
        }
    }
}
