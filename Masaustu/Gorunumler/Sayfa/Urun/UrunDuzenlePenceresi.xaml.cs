using System.Windows;
using EtkiAlani.Modeller.Stok;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;
using UrunModel = EtkiAlani.Modeller.Stok.Urun;

namespace Masaustu.Gorunumler.Sayfa.Urun;

public partial class UrunDuzenlePenceresi : Window
{
    public bool Kaydedildi { get; private set; }

    public UrunDuzenlePenceresi(IUrunServisi urunServisi, UrunModel? mevcutUrun = null)

    {
        InitializeComponent();
        var vm = new UrunDuzenleGorunumModeli(urunServisi, mevcutUrun);
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
