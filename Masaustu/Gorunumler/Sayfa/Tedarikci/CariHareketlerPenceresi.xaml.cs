using System.Windows;
using EtkiAlani.Modeller.Cari;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa.Tedarikci;

public partial class CariHareketlerPenceresi : Window
{
    public CariHareketlerPenceresi(ICariHareketServisi hareketServisi, ICariServisi cariServisi, CariKart cariKart)
    {
        InitializeComponent();
        
        var yazdirmaServisi = App.HizmetSaglayici?.GetService(typeof(IBelgeYazdirmaServisi)) as IBelgeYazdirmaServisi;
        var vm = new CariHareketGorunumModeli(hareketServisi, cariServisi, cariKart, yazdirmaServisi);
        vm.KapatmaTetikle = () => this.Close();
        DataContext = vm;

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                try { DragMove(); } catch { }
        };
    }

    private void Kapat_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}

