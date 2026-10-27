using System.Windows;
using System.Windows.Controls;
using EtkiAlani.Modeller.Cari;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;
using Masaustu.Gorunumler.Sayfa.Musteri;

namespace Masaustu.Gorunumler.Sayfa;

public partial class MusteriSayfasi : UserControl
{
    public MusteriSayfasi()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MusteriGorunumModeli vm)
            {
                // Data bağlamındaki komutu tetikle
                if (vm.YenileKomutu.CanExecute(null))
                {
                    vm.YenileKomutu.Execute(null);
                }

                vm.MusteriDuzenleFormAc = async (cari) =>
                {
                    var cariServisi = App.HizmetSaglayici.GetService(typeof(ICariServisi)) as ICariServisi;
                    var cariTipiServisi = App.HizmetSaglayici.GetService(typeof(ICariTipiTanimiServisi)) as ICariTipiTanimiServisi;
                    if (cariServisi == null || cariTipiServisi == null) return false;

                    var pencere = new Musteri.MusteriDuzenlePenceresi(cariServisi, cariTipiServisi, cari)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    pencere.ShowDialog();
                    return pencere.Kaydedildi;
                };

                vm.CariEkstreAc = async (cari) =>
                {
                    var hareketServisi = App.HizmetSaglayici.GetService(typeof(ICariHareketServisi)) as ICariHareketServisi;
                    var cariServisi = App.HizmetSaglayici.GetService(typeof(ICariServisi)) as ICariServisi;
                    if (hareketServisi == null || cariServisi == null) return;

                    var pencere = new Musteri.CariHareketlerPenceresi(hareketServisi, cariServisi, cari)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    pencere.ShowDialog();
                    if (vm.YenileKomutu.CanExecute(null))
                        vm.YenileKomutu.Execute(null);
                };

                vm.CariDetayAc = async cari =>
                {
                    var pencere = new CariDetayPenceresi(cari.Id) { Owner = Window.GetWindow(this) };
                    pencere.ShowDialog();
                    await vm.AraAsyncPublic();
                };

                vm.TahsilatAc = cari =>
                {
                    var cariServisi = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ICariServisi>(App.HizmetSaglayici);
                    var tModel = new TahsilatOdemeGorunumModeli(
                        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ITahsilatOdemeServisi>(App.HizmetSaglayici),
                        cariServisi,
                        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IBelgeKapamaServisi>(App.HizmetSaglayici),
                        EtkiAlani.Modeller.Finans.FinansIslemTuru.Tahsilat);
                    tModel.IsCariKilitli = true;
                    tModel.SecilenCari = cari;
                    var p = new Window
                    {
                        Title = $"Yeni Tahsilat - {cari.Unvan}",
                        Width = 1100,
                        Height = 700,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = Window.GetWindow(this),
                        Background = System.Windows.Media.Brushes.White,
                        Content = new TahsilatOdemeSayfasi { DataContext = tModel }
                    };
                    p.ShowDialog();
                    _ = vm.AraAsyncPublic();
                };

                vm.OdemeAc = cari =>
                {
                    var cariServisi = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ICariServisi>(App.HizmetSaglayici);
                    var tModel = new TahsilatOdemeGorunumModeli(
                        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ITahsilatOdemeServisi>(App.HizmetSaglayici),
                        cariServisi,
                        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IBelgeKapamaServisi>(App.HizmetSaglayici),
                        EtkiAlani.Modeller.Finans.FinansIslemTuru.Odeme);
                    tModel.IsCariKilitli = true;
                    tModel.SecilenCari = cari;
                    var p = new Window
                    {
                        Title = $"Yeni Ödeme - {cari.Unvan}",
                        Width = 1100,
                        Height = 700,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = Window.GetWindow(this),
                        Background = System.Windows.Media.Brushes.White,
                        Content = new TahsilatOdemeSayfasi { DataContext = tModel }
                    };
                    p.ShowDialog();
                    _ = vm.AraAsyncPublic();
                };
            }
        };
    }

    private void DataGrid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MusteriGorunumModeli vm && vm.SeciliMusteri != null)
        {
            if (vm.DetayKomutu.CanExecute(null))
                vm.DetayKomutu.Execute(null);
        }
    }

    private async void ExceldenAktar_Click(object sender, RoutedEventArgs e)
    {
        if (await Masaustu.Gorunumler.Ortak.TopluAktarimPenceresi.GosterAsync(AktarimTuru.Cari) > 0
            && DataContext is MusteriGorunumModeli vm && vm.YenileKomutu.CanExecute(null))
            vm.YenileKomutu.Execute(null);
    }

    private void TopluMutabakat_Click(object sender, RoutedEventArgs e) =>
        TopluMutabakatPenceresi.Ac(Window.GetWindow(this));
}
