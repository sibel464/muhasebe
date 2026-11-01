using System.Net.Http;
using System.Text.Json;

namespace Masaustu.Servisler;

// Kayıtlı olmayan bir barkod okutulduğunda ürün adını açık ürün veritabanlarından (Open Food Facts ve kardeş
// projeleri: gıda, kozmetik, temizlik ve genel ürünler) önerir. İnternet yoksa ya da ürün bulunamazsa sessizce null döner;
// program hiçbir işlem için internete muhtaç değildir. Gönderilen tek bilgi barkod numarasıdır.
public static class CevrimiciUrunBilgisi
{
    public sealed record Bilgi(string Ad, string? Marka, string? Miktar, string Kaynak);

    private static readonly HttpClient Istemci = YeniIstemci();
    private static readonly string[] Kaynaklar =
    [
        "https://world.openfoodfacts.org", "https://world.openbeautyfacts.org", "https://world.openproductsfacts.org"
    ];

    private static HttpClient YeniIstemci()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("MuhasebeYonetim/1.0 (masaustu muhasebe programi)");
        return c;
    }

    public static async Task<Bilgi?> BulAsync(string barkod, CancellationToken iptal = default)
    {
        barkod = (barkod ?? "").Trim();
        if (barkod.Length is < 8 or > 14 || !barkod.All(char.IsDigit)) return null;
        foreach (var kaynak in Kaynaklar)
        {
            try
            {
                var json = await Istemci.GetStringAsync($"{kaynak}/api/v2/product/{barkod}?fields=product_name,product_name_tr,brands,quantity", iptal);
                var b = Coz(json, kaynak);
                if (b != null) return b;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                // İnternet yok / zaman aşımı: sonraki kaynağı denemek de büyük olasılıkla boşuna.
                if (ex is not JsonException) return null;
            }
        }
        return null;
    }

    public static Bilgi? Coz(string json, string kaynak)
    {
        using var d = JsonDocument.Parse(json);
        if (!d.RootElement.TryGetProperty("status", out var s) || s.ValueKind != JsonValueKind.Number || s.GetInt32() != 1) return null;
        if (!d.RootElement.TryGetProperty("product", out var p)) return null;
        string? Al(string ad) => p.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()!.Trim() : null;
        var urunAdi = Al("product_name_tr") ?? Al("product_name");
        if (urunAdi == null) return null;
        var marka = Al("brands")?.Split(',')[0].Trim();
        // "400 g e": sondaki "e" Avrupa tahmini miktar işaretidir.
        var miktar = Al("quantity") is { } m ? System.Text.RegularExpressions.Regex.Replace(m, @"\s+e$", "").Trim() : null;
        // Marka adın içinde geçiyorsa (ilk kelimesiyle) ya da miktar zaten adda yazılıysa tekrar eklenmez.
        var markaIlkKelime = marka?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var markaEkle = marka != null && (markaIlkKelime == null || !urunAdi.Contains(markaIlkKelime, StringComparison.OrdinalIgnoreCase));
        var miktarEkle = miktar != null && !urunAdi.Replace(" ", "").Contains(miktar.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
        var tamAd = string.Join(" ", new[] { markaEkle ? marka : null, urunAdi, miktarEkle ? miktar : null }.Where(x => x != null));
        if (tamAd.Length > 200) tamAd = tamAd[..200];
        return new Bilgi(tamAd, marka, miktar, new Uri(kaynak).Host);
    }
}
