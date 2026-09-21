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
        var tanimServisi = App.HizmetSaglayici.GetService(typeof(IMerkeziTanimServisi)) as IMerkeziTanimServisi
            ?? throw new InvalidOperationException("Merkezi tanım servisi bulunamadı.");
        var vm = new UrunDuzenleGorunumModeli(urunServisi, tanimServisi, mevcutUrun);
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

    private async void TanimYonet_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string grup)
            return;

        var servis = App.HizmetSaglayici.GetService(typeof(IMerkeziTanimServisi)) as IMerkeziTanimServisi;
        if (servis is null)
            return;

        var baslik = grup == "MARKA" ? "Markalar" : grup == "BIRIM" ? "Birimler" : "Ürün Kategorileri";
        var pencere = new Masaustu.Gorunumler.Sayfa.MerkeziTanimYonetimPenceresi(servis, grup, baslik)
        {
            Owner = this
        };
        pencere.ShowDialog();

        if (DataContext is UrunDuzenleGorunumModeli vm)
            await vm.TanimlariYukleAsync(grup, pencere.GorunumModeli.KaydedilenId);
    }
}
