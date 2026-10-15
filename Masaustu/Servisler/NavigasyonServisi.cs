using Masaustu.GorunumModelleri;

namespace Masaustu.Servisler;

public class NavigasyonServisi
{
    private readonly Dictionary<string, Func<TemelGorunumModeli>> _gorunumModelFabrika = new();
    private TemelGorunumModeli? _mevcutGorunumModeli;

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

    public void Kaydet(string anahtar, Func<TemelGorunumModeli> fabrika)
    {
        _gorunumModelFabrika[anahtar] = fabrika;
    }

    public void Git(string anahtar)
    {
        if (_gorunumModelFabrika.TryGetValue(anahtar, out var fabrika))
        {
            MevcutGorunumModeli = fabrika();
        }
    }

    // Sayfa açılmadan önce görünüm modelini hazırlamak için (ör. ürün kartını düzenleme için yüklemek).
    public void Git(string anahtar, Action<TemelGorunumModeli> hazirla)
    {
        if (_gorunumModelFabrika.TryGetValue(anahtar, out var fabrika))
        {
            var gorunumModeli = fabrika();
            hazirla(gorunumModeli);
            MevcutGorunumModeli = gorunumModeli;
        }
    }
}
