using System.Windows;
using EtkiAlani.Modeller.Cari;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa.Musteri;

public partial class MusteriDuzenlePenceresi : Window
{
    public bool Kaydedildi { get; private set; }

    public MusteriDuzenlePenceresi(ICariServisi cariServisi, CariKart? mevcutCari = null)
    {
        InitializeComponent();
        var vm = new MusteriDuzenleGorunumModeli(cariServisi, mevcutCari);
        vm.PencereKapat = kaydedildi =>
        {
            Kaydedildi = kaydedildi;
            this.Close();
        };
        DataContext = vm;

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                try { DragMove(); } catch { }
        };
    }
}
