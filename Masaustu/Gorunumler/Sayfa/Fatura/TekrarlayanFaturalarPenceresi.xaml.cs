using System.Windows;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa;

public partial class TekrarlayanFaturalarPenceresi : Window
{
    public TekrarlayanFaturalarPenceresi(TekrarlayanFaturalarModeli model, ITekrarlayanFaturaServisi servis)
    {
        InitializeComponent();
        DataContext = model;
        model.PlanDuzenle = (plan, bilgi) => TekrarlayanFaturaPlanPenceresi.Duzenle(this, servis, plan, bilgi);
        model.Onay = mesaj => MessageBox.Show(this, mesaj, "Tekrarlayan Fatura", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        Loaded += async (_, _) => await model.YukleAsync();
    }

    // Döner: pencerede fatura kesildiyse true (fatura listesi yenilenmeli).
    public static bool Ac(Window? sahip)
    {
        if (App.HizmetSaglayici?.GetService(typeof(ITekrarlayanFaturaServisi)) is not ITekrarlayanFaturaServisi servis)
        {
            MessageBox.Show("Tekrarlayan fatura servisi bulunamadı.", "Tekrarlayan Fatura", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        var model = new TekrarlayanFaturalarModeli(servis);
        new TekrarlayanFaturalarPenceresi(model, servis) { Owner = sahip ?? Application.Current.MainWindow }.ShowDialog();
        return model.FaturaKesildi;
    }
}
