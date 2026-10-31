using System.Windows;
using EtkiAlani.Modeller.Cari;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;

namespace Masaustu.Gorunumler.Sayfa.Musteri;

public partial class MusteriDuzenlePenceresi : Window
{
    public bool Kaydedildi { get; private set; }

    public MusteriDuzenlePenceresi(ICariServisi cariServisi, ICariTipiTanimiServisi? cariTipiServisi = null, CariKart? mevcutCari = null)
    {
        InitializeComponent();
        cariTipiServisi ??= App.HizmetSaglayici.GetService(typeof(ICariTipiTanimiServisi)) as ICariTipiTanimiServisi;
        var vm = new MusteriDuzenleGorunumModeli(cariServisi, cariTipiServisi!, mevcutCari,
            App.HizmetSaglayici?.GetService(typeof(IEBelgeServisi)) as IEBelgeServisi);
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
