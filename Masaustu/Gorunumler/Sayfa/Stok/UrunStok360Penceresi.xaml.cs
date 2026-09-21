using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa.Stok;

public partial class UrunStok360Penceresi : Window
{
    private readonly UrunStok360GorunumModeli _model;

    public UrunStok360Penceresi(int urunId)
    {
        InitializeComponent();
        _model = App.HizmetSaglayici.GetRequiredService<UrunStok360GorunumModeli>();
        DataContext = _model;
        _model.KapatAction = Close;
        Loaded += async (_, _) => await _model.InitAsync(urunId);
    }
}
