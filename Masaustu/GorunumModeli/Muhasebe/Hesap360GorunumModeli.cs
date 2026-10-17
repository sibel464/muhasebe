using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class Hesap360GorunumModeli : TemelGorunumModeli
{
    private readonly IHesapPlaniServisi _hesapServisi;
    private readonly int _hesapId;

    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;
    private int _seciliSekmeIndeksi;
    private DateTime? _baslangicTarihi = DateTime.Today.AddMonths(-1);
    private DateTime? _bitisTarihi = DateTime.Today;

    private Hesap360Dto? _hesap360;

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

    public DateTime? BaslangicTarihi
    {
        get => _baslangicTarihi;
        set
        {
            if (OzellikAyarla(ref _baslangicTarihi, value))
            {
                _ = YükleAsync();
            }
        }
    }

    public DateTime? BitisTarihi
    {
        get => _bitisTarihi;
        set
        {
            if (OzellikAyarla(ref _bitisTarihi, value))
            {
                _ = YükleAsync();
            }
        }
    }

    public Hesap360Dto? Hesap360
    {
        get => _hesap360;
        private set => OzellikAyarla(ref _hesap360, value);
    }

    public ICommand YenileKomutu { get; }

    public Hesap360GorunumModeli(IHesapPlaniServisi hesapServisi)
        : this(hesapServisi, 0)
    {
    }

    public Hesap360GorunumModeli(IHesapPlaniServisi hesapServisi, int hesapId)
    {
        _hesapServisi = hesapServisi;
        _hesapId = hesapId;

        YenileKomutu = new KomutSarici(async () => await YükleAsync());

        if (_hesapId > 0)
        {
            _ = YükleAsync();
        }
    }

    public async Task YükleAsync()
    {
        if (_hesapId <= 0) return;
        try
        {
            Yukleniyor = true;
            DurumMesaji = "Hesap 360° detayları yükleniyor...";
            Hesap360 = await _hesapServisi.Hesap360GetirAsync(_hesapId, BaslangicTarihi, BitisTarihi);
            DurumMesaji = $"Hesap 360° detayları yüklendi: {Hesap360?.Hesap.HesapKodu} - {Hesap360?.Hesap.HesapAdi}";
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
}
