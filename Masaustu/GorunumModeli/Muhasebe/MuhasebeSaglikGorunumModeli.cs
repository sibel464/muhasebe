using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class MuhasebeSaglikGorunumModeli : TemelGorunumModeli
{
    private readonly IMuhasebeRaporServisi _raporServisi;
    private readonly IYevmiyeServisi _yevmiyeServisi;

    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;

    private MuhasebeSaglikDto? _saglik;
    private MuhasebelesmemisBelgeDto? _seciliBelge;

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

    public MuhasebeSaglikDto? Saglik
    {
        get => _saglik;
        private set => OzellikAyarla(ref _saglik, value);
    }

    public MuhasebelesmemisBelgeDto? SeciliBelge
    {
        get => _seciliBelge;
        set => OzellikAyarla(ref _seciliBelge, value);
    }

    public ObservableCollection<MuhasebelesmemisBelgeDto> Belgeler { get; } = new();

    public ICommand YenileKomutu { get; }
    public ICommand MuhasebelestirKomutu { get; }

    public MuhasebeSaglikGorunumModeli(IMuhasebeRaporServisi raporServisi, IYevmiyeServisi yevmiyeServisi)
    {
        _raporServisi = raporServisi;
        _yevmiyeServisi = yevmiyeServisi;

        YenileKomutu = new KomutSarici(async () => await YükleAsync());
        MuhasebelestirKomutu = new KomutSarici(async () => await MuhasebelestirAsync(), () => SeciliBelge != null);

        _ = YükleAsync();
    }

    public async Task YükleAsync()
    {
        try
        {
            Yukleniyor = true;
            DurumMesaji = "Muhasebe Sağlık Paneli ve Muhasebeleştirilmemiş Belgeler yükleniyor...";
            Belgeler.Clear();

            Saglik = await _raporServisi.SaglikPaneliGetirAsync(Guid.Empty);
            var belgeler = await _raporServisi.GetirMuhasebelesmemisBelgelerAsync(Guid.Empty);
            foreach (var b in belgeler)
            {
                Belgeler.Add(b);
            }

            DurumMesaji = $"Sağlık Paneli Güncel: {Saglik.DengesizFisSayisi} dengesiz fiş, {Saglik.MuhasebelesmemisBelgeSayisi} muhasebeleştirilmemiş belge.";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Hata: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task MuhasebelestirAsync()
    {
        if (SeciliBelge == null) return;
        try
        {
            Yukleniyor = true;
            DurumMesaji = $"[{SeciliBelge.BelgeNo}] muhasebeleştiriliyor...";

            var fis = await _yevmiyeServisi.OtomatikMuhasebelestirAsync(SeciliBelge.BelgeTipi, SeciliBelge.BelgeId, Guid.Empty, 1);
            if (fis != null)
            {
                MessageBox.Show($"Belge [{SeciliBelge.BelgeNo}] başarıyla muhasebeleştirildi.\nFiş No: {fis.FisNo}", "Muhasebeleştirildi", MessageBoxButton.OK, MessageBoxImage.Information);
                await YükleAsync();
            }
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Muhasebeleştirme hatası: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }
}
