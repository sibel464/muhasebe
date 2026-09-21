using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa.Stok;

public partial class StokIslemPenceresi : Window
{
    private readonly StokIslemGorunumModeli _model;

    public StokIslemPenceresi(int? urunId = null, bool isGiris = true)
    {
        InitializeComponent();
        _model = App.HizmetSaglayici.GetRequiredService<StokIslemGorunumModeli>();
        DataContext = _model;
        _model.KapatAction = Close;
        Loaded += async (_, _) => await _model.InitAsync(urunId, isGiris);
    }
}
