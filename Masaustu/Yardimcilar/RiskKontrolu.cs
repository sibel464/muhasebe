using System.Windows;
using Uygulama.Arayuzler;

namespace Masaustu.Yardimcilar;

// Açık hesap satışta carinin risk limiti aşılacaksa kullanıcıya sorulur (limit boşsa sınırsız).
// Bakiye kayıttan hemen önce veritabanından okunur; ekranda bekleyen eski bakiye kullanılmaz.
public static class RiskKontrolu
{
    public static async Task<bool> OnayAlAsync(ICariServisi cariServisi, int cariId, decimal eklenecekBorc)
    {
        var cari = await cariServisi.IdIleGetirAsync(cariId);
        if (cari?.RiskLimiti is not decimal limit || limit <= 0)
            return true;

        var yeniBakiye = cari.Bakiye + eklenecekBorc;
        if (yeniBakiye <= limit)
            return true;

        var cevap = MessageBox.Show(
            $"{cari.Unvan} için risk limiti aşılıyor.\n\n" +
            $"Risk limiti:      {limit:N2} ₺\n" +
            $"Mevcut bakiye:  {cari.Bakiye:N2} ₺\n" +
            $"Bu satış:          {eklenecekBorc:N2} ₺\n" +
            $"Satış sonrası:   {yeniBakiye:N2} ₺  (limit {yeniBakiye - limit:N2} ₺ aşılıyor)\n\n" +
            "Yine de açık hesaba satış yapılsın mı?",
            "Risk Limiti Aşımı", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        return cevap == MessageBoxResult.Yes;
    }
}
