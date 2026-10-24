using Uygulama.Arayuzler;

namespace Masaustu.Yardimcilar;

// Dövizli kasa/banka formlarında para birimi listesi ve günün kurunun önerilmesi.
// Kur bulunamazsa (internet yok, kur girilmemiş) 0 döner; kullanıcı kuru elle yazar.
public static class DovizKuruYardimcisi
{
    public const string Tl = "TRY";

    public static IReadOnlyList<string> ParaBirimleri { get; } = [Tl, "USD", "EUR", "GBP"];

    public static bool DovizliMi(string? paraBirimi) =>
        !string.IsNullOrWhiteSpace(paraBirimi) && !string.Equals(paraBirimi, Tl, StringComparison.OrdinalIgnoreCase);

    public static string Simge(string? paraBirimi) => DovizliMi(paraBirimi) ? paraBirimi!.ToUpperInvariant() : "₺";

    public static async Task<decimal> GunlukKurAsync(string paraBirimi, DateTime tarih)
    {
        if (!DovizliMi(paraBirimi)) return 1m;
        try
        {
            if (App.HizmetSaglayici?.GetService(typeof(IKurServisi)) is not IKurServisi kurServisi) return 0m;
            var kur = await kurServisi.GetirGecerliKurAsync(paraBirimi, tarih);
            return kur == null ? 0m : kur.DovizAlis > 0 ? kur.DovizAlis : kur.DovizSatis;
        }
        catch
        {
            return 0m;
        }
    }
}
