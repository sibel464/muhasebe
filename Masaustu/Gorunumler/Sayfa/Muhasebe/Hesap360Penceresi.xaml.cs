using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Uygulama.Arayuzler;
using Masaustu.GorunumModeli.Muhasebe;

namespace Masaustu.Gorunumler.Sayfa.Muhasebe;

public partial class Hesap360Penceresi : Window
{
    public Hesap360Penceresi(int hesapId)
    {
        InitializeComponent();
        var hesapServisi = App.HizmetSaglayici.GetRequiredService<IHesapPlaniServisi>();
        DataContext = new Hesap360GorunumModeli(hesapServisi, hesapId);
    }
}
