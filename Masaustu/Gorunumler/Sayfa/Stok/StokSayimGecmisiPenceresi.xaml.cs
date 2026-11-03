using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa.Stok;

public partial class StokSayimGecmisiPenceresi : Window
{
    public StokSayimGecmisiPenceresi()
    {
        InitializeComponent();
        var model = App.HizmetSaglayici.GetRequiredService<StokSayimGecmisiGorunumModeli>();
        DataContext = model;
        model.DevamEtAction = async id =>
        {
            new StokSayimPenceresi(id) { Owner = this }.ShowDialog();
            await model.YukleAsync();
        };
        Loaded += async (_, _) => await model.YukleAsync();
    }
}
