using Masaustu.GorunumModelleri;
using Microsoft.Extensions.DependencyInjection;

namespace Masaustu.Servisler;

// Her sayfa kendi servis kapsamında (dolayısıyla kendi veritabanı bağlamıyla) açılır. Tüm sayfalar tek bağlamı
// paylaşınca bir sayfa yüklenirken başka menüye geçmek "aynı bağlamda ikinci işlem" hatasına ve sıfır/eski veriye
// yol açıyordu. Önceki sayfanın kapsamı, yarım kalan yüklemeleri bozmamak için bir süre sonra kapatılır.
public class NavigasyonServisi
{
    private static readonly TimeSpan KapsamKapatmaGecikmesi = TimeSpan.FromSeconds(60);

    private readonly Dictionary<string, Func<IServiceProvider, TemelGorunumModeli>> _gorunumModelFabrika = new();
    private TemelGorunumModeli? _mevcutGorunumModeli;
    private IServiceScope? _kapsam;

    public event Action<TemelGorunumModeli?>? GorunumModelDegisti;

    public TemelGorunumModeli? MevcutGorunumModeli
    {
        get => _mevcutGorunumModeli;
        private set
        {
            _mevcutGorunumModeli = value;
            GorunumModelDegisti?.Invoke(_mevcutGorunumModeli);
        }
    }

    public void Kaydet(string anahtar, Func<IServiceProvider, TemelGorunumModeli> fabrika)
    {
        _gorunumModelFabrika[anahtar] = fabrika;
    }

    public void Kaydet(string anahtar, Func<TemelGorunumModeli> fabrika)
    {
        _gorunumModelFabrika[anahtar] = _ => fabrika();
    }

    public void Git(string anahtar) => Git(anahtar, _ => { });

    // Sayfa açılmadan önce görünüm modelini hazırlamak için (ör. ürün kartını düzenleme için yüklemek).
    public void Git(string anahtar, Action<TemelGorunumModeli> hazirla)
    {
        if (!_gorunumModelFabrika.TryGetValue(anahtar, out var fabrika))
            return;

        var oncekiKapsam = _kapsam;
        var yeniKapsam = App.HizmetSaglayici?.CreateScope();
        try
        {
            var gorunumModeli = fabrika(yeniKapsam?.ServiceProvider ?? App.HizmetSaglayici!);
            hazirla(gorunumModeli);
            _kapsam = yeniKapsam;
            MevcutGorunumModeli = gorunumModeli;
        }
        catch
        {
            yeniKapsam?.Dispose();
            throw;
        }

        if (oncekiKapsam != null)
            _ = Task.Delay(KapsamKapatmaGecikmesi).ContinueWith(_ => oncekiKapsam.Dispose(), TaskScheduler.Default);
    }
}
