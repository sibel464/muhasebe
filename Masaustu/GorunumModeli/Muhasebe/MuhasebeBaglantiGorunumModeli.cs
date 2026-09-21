using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class MuhasebeBaglantiGorunumModeli : TemelGorunumModeli
{
    private readonly IMuhasebeBaglantiServisi _baglantiServisi;

    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;
    private MuhasebeBaglantiKoduDto? _seciliKod;

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

    public MuhasebeBaglantiKoduDto? SeciliKod
    {
        get => _seciliKod;
        set => OzellikAyarla(ref _seciliKod, value);
    }

    public ObservableCollection<MuhasebeBaglantiKoduDto> Kodlar { get; } = new();

    public ICommand YenileKomutu { get; }
    public ICommand KaydetKomutu { get; }

    public MuhasebeBaglantiGorunumModeli(IMuhasebeBaglantiServisi baglantiServisi)
    {
        _baglantiServisi = baglantiServisi;

        YenileKomutu = new KomutSarici(async () => await YükleAsync());
        KaydetKomutu = new KomutSarici(async () => await KaydetAsync(), () => SeciliKod != null);

        _ = YükleAsync();
    }

    public async Task YükleAsync()
    {
        try
        {
            Yukleniyor = true;
            DurumMesaji = "Muhasebe bağlantı kodları yükleniyor...";
            Kodlar.Clear();

            var liste = await _baglantiServisi.GetirBaglantiKodlariAsync(Guid.Empty);
            foreach (var k in liste)
            {
                Kodlar.Add(k);
            }

            DurumMesaji = $"{Kodlar.Count} adet bağlantı kodu listelendi.";
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

    private async Task KaydetAsync()
    {
        if (SeciliKod == null) return;
        try
        {
            Yukleniyor = true;
            DurumMesaji = "Bağlantı kodu güncelleniyor...";
            var res = await _baglantiServisi.GuncelleBaglantiKoduAsync(SeciliKod);
            if (res)
            {
                DurumMesaji = $"[{SeciliKod.KategoriKodu}] için varsayılan hesap [{SeciliKod.VarSayilanHesapKodu}] olarak güncellendi.";
            }
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Güncelleme hatası: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }
}
