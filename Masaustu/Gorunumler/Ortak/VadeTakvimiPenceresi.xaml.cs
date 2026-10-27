using System.Windows;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Ortak;

public partial class VadeTakvimiPenceresi : Window
{
    public VadeTakvimiPenceresi(VadeTakvimiGorunumModeli model)
    {
        InitializeComponent();
        DataContext = model;
        Loaded += async (_, _) => await model.YukleAsync();
    }

    public static void Ac(Window? sahip)
    {
        if (App.HizmetSaglayici?.GetService(typeof(IRaporMerkeziServisi)) is not IRaporMerkeziServisi servis)
        {
            MessageBox.Show("Rapor servisi bulunamadı.", "Vade Takvimi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        new VadeTakvimiPenceresi(new VadeTakvimiGorunumModeli(servis)) { Owner = sahip ?? Application.Current.MainWindow }.ShowDialog();
    }
}
