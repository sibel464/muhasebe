using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa.Stok;

public partial class StokSayimPenceresi : Window
{
    private readonly StokSayimGorunumModeli _model;

    public StokSayimPenceresi()
    {
        InitializeComponent();
        _model = App.HizmetSaglayici.GetRequiredService<StokSayimGorunumModeli>();
        DataContext = _model;
        _model.KapatAction = Close;
        Loaded += async (_, _) => await _model.InitAsync();
    }
}
