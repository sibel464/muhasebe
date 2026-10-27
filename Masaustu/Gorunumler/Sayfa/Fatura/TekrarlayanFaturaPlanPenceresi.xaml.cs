using System.Windows;
using Masaustu.GorunumModelleri;
using Uygulama.Arayuzler;
using FaturaVarligi = EtkiAlani.Modeller.Fatura.Fatura;
using TekrarlayanFaturaVarligi = EtkiAlani.Modeller.Fatura.TekrarlayanFatura;

namespace Masaustu.Gorunumler.Sayfa;

public partial class TekrarlayanFaturaPlanPenceresi : Window
{
    public TekrarlayanFaturaPlanPenceresi(TekrarlayanFaturaPlanModeli model)
    {
        InitializeComponent();
        DataContext = model;
        model.Kaydedildi = () => DialogResult = true;
    }

    public static bool Duzenle(Window? sahip, ITekrarlayanFaturaServisi servis, TekrarlayanFaturaVarligi plan, string ornekBilgisi) =>
        new TekrarlayanFaturaPlanPenceresi(new TekrarlayanFaturaPlanModeli(servis, plan, ornekBilgisi)) { Owner = sahip ?? Application.Current.MainWindow }.ShowDialog() == true;

    // Fatura listesinden: seçilen fatura örnek alınarak yeni plan; ilk fatura bir sonraki ay aynı gün önerilir.
    public static bool YeniOlustur(Window? sahip, FaturaVarligi ornek)
    {
        if (App.HizmetSaglayici?.GetService(typeof(ITekrarlayanFaturaServisi)) is not ITekrarlayanFaturaServisi servis)
        {
            MessageBox.Show("Tekrarlayan fatura servisi bulunamadı.", "Tekrarlayan Fatura", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        var plan = new TekrarlayanFaturaVarligi
        {
            OrnekFaturaId = ornek.Id, Ad = ornek.CariKart?.Unvan ?? string.Empty, PeriyotAy = 1,
            BaslangicTarihi = ornek.Tarih.Date.AddMonths(1), Aktif = true,
            VadeGunu = ornek.VadeTarihi is DateTime v && v.Date > ornek.Tarih.Date ? (int)(v.Date - ornek.Tarih.Date).TotalDays : null
        };
        var bilgi = $"Örnek fatura: {ornek.FaturaNo} · {ornek.CariKart?.Unvan} · {ornek.GenelToplam:N2} TL";
        if (!Duzenle(sahip, servis, plan, bilgi)) return false;
        MessageBox.Show("Tekrarlayan fatura kaydedildi. Zamanı gelen faturaları Fatura sayfasındaki 'Tekrarlayan' düğmesinden kesebilirsiniz; ana sayfa da bekleyen faturaları hatırlatır.",
            "Tekrarlayan Fatura", MessageBoxButton.OK, MessageBoxImage.Information);
        return true;
    }
}
