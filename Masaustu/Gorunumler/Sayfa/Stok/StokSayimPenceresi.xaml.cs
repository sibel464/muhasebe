using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa.Stok;

public partial class StokSayimPenceresi : Window
{
    private readonly StokSayimGorunumModeli _model;

    // devamEdilecekSayimId: Sayım Geçmişi'nden "Devam Et" ile açılınca o yarım sayım yüklenir.
    public StokSayimPenceresi(int? devamEdilecekSayimId = null)
    {
        InitializeComponent();
        _model = App.HizmetSaglayici.GetRequiredService<StokSayimGorunumModeli>();
        DataContext = _model;
        var onaylandi = false;
        _model.KapatAction = () => { onaylandi = true; Close(); };
        // Okutulan ürün listede görünür olsun; imleç barkod kutusunda kalsın ki okuyucu kesintisiz çalışsın.
        _model.KalemeGitAction = kalem =>
        {
            KalemTablosu.ScrollIntoView(kalem);
            BarkodKutusu.Focus();
        };
        Loaded += async (_, _) =>
        {
            await _model.InitAsync(devamEdilecekSayimId);
            BarkodKutusu.Focus();
        };
        Closing += (_, e) =>
        {
            if (onaylandi && !_model.KaydedilmemisDegisiklik) return;
            if (_model.KaydedilmemisDegisiklik && !_model.OnayIste(
                    "Kaydedilmemiş sayımlar var ve kapatırsanız kaybolacak.\n\nKaydetmeden çıkılsın mı? (Saklamak için Hayır deyip \"Kaydet, Sonra Devam Et\" düğmesine basın.)"))
                e.Cancel = true;
        };
    }
}
