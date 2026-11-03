using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using EtkiAlani.Modeller.Stok;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;
using Uygulama.Dto.StokOperasyon;

namespace Masaustu.GorunumModelleri;

// Sayım Geçmişi: onaylı, iptal ve yarım kalan sayımlar; tutanak yazdırma, yarım sayıma devam ve iptal.
public class StokSayimGecmisiGorunumModeli : TemelGorunumModeli
{
    private readonly IStokOperasyonServisi _operasyonServisi;
    private readonly IBelgeYazdirmaServisi _yazdirma;
    private readonly IAktifFirmaBaglami _firmaBaglami;
    private readonly IKullaniciServisi _kullaniciServisi;

    private StokSayimOzetiDto? _secili;
    private string _hataMesaji = string.Empty;

    public ObservableCollection<StokSayimOzetiDto> Sayimlar { get; } = new();

    public StokSayimOzetiDto? Secili
    {
        get => _secili;
        set { if (OzellikAyarla(ref _secili, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }

    public ICommand YenileKomutu { get; }
    public ICommand YazdirKomutu { get; }
    public ICommand DevamEtKomutu { get; }
    public ICommand IptalEtKomutu { get; }

    // Yarım sayıma devam: pencere bu sayımla sayım ekranını açar.
    public Action<int>? DevamEtAction { get; set; }

    public StokSayimGecmisiGorunumModeli(IStokOperasyonServisi operasyonServisi, IBelgeYazdirmaServisi yazdirma,
        IAktifFirmaBaglami firmaBaglami, IKullaniciServisi kullaniciServisi)
    {
        _operasyonServisi = operasyonServisi;
        _yazdirma = yazdirma;
        _firmaBaglami = firmaBaglami;
        _kullaniciServisi = kullaniciServisi;

        YenileKomutu = new KomutSarici(async _ => await YukleAsync());
        YazdirKomutu = new KomutSarici(async _ => await YazdirAsync(), _ => Secili != null);
        DevamEtKomutu = new KomutSarici(_ => { if (Secili != null) DevamEtAction?.Invoke(Secili.Id); },
            _ => Secili is { Durum: StokSayimDurumu.Sayimda or StokSayimDurumu.Taslak });
        IptalEtKomutu = new KomutSarici(async _ => await IptalEtAsync(),
            _ => Secili is { Durum: StokSayimDurumu.Sayimda or StokSayimDurumu.Taslak });
    }

    public async Task YukleAsync()
    {
        HataMesaji = string.Empty;
        try
        {
            var seciliId = Secili?.Id;
            Sayimlar.Clear();
            foreach (var s in await _operasyonServisi.SayimlariListeleAsync(_firmaBaglami.SirketId))
                Sayimlar.Add(s);
            Secili = Sayimlar.FirstOrDefault(s => s.Id == seciliId) ?? Sayimlar.FirstOrDefault();
        }
        catch (Exception ex) { HataMesaji = ex.Message; }
    }

    private async Task YazdirAsync()
    {
        if (Secili == null) return;
        HataMesaji = string.Empty;
        try
        {
            var sayim = await _operasyonServisi.SayimGetirAsync(Secili.Id, _firmaBaglami.SirketId)
                ?? throw new InvalidOperationException("Sayım bulunamadı.");
            await _yazdirma.SayimTutanagiYazdirAsync(sayim);
        }
        catch (Exception ex) { HataMesaji = "Tutanak hazırlanamadı: " + ex.Message; }
    }

    private async Task IptalEtAsync()
    {
        if (Secili == null) return;
        if (MessageBox.Show($"{Secili.SayimNo} numaralı yarım sayım iptal edilsin mi? Stok değişmez.", "Sayım İptali",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        HataMesaji = string.Empty;
        try
        {
            int kullaniciId = _kullaniciServisi.AktifKullanici?.Id ?? 1;
            await _operasyonServisi.SayimIptalEtAsync(Secili.Id, _firmaBaglami.SirketId, kullaniciId);
            await YukleAsync();
        }
        catch (Exception ex) { HataMesaji = ex.Message; }
    }
}
