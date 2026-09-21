using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa.Stok;

public partial class StokTransferPenceresi : Window
{
    private readonly StokTransferGorunumModeli _model;

    public StokTransferPenceresi(int? urunId = null)
    {
        InitializeComponent();
        _model = App.HizmetSaglayici.GetRequiredService<StokTransferGorunumModeli>();
        DataContext = _model;
        _model.KapatAction = Close;
        Loaded += async (_, _) => await _model.InitAsync(urunId);
    }
}
