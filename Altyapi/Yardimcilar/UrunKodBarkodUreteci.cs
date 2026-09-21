using Altyapi.Veri;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Yardimcilar;

public class UrunKodBarkodUreteci : IUrunKodBarkodUreteci
{
    private readonly MuhasebeBaglami _baglam;

    public UrunKodBarkodUreteci(MuhasebeBaglami baglam)
    {
        _baglam = baglam;
    }

    public async Task<string> SonrakiKodUretAsync(CancellationToken cancellationToken = default)
    {
        var kodlar = await _baglam.Urunler
            .IgnoreQueryFilters()
            .Where(u => u.Kod.StartsWith("URN-"))
            .Select(u => u.Kod)
            .ToListAsync(cancellationToken);

        int maxSayi = 0;
        foreach (var k in kodlar)
        {
            var parca = k.Replace("URN-", "").Trim();
            if (int.TryParse(parca, out var sayi) && sayi > maxSayi)
            {
                maxSayi = sayi;
            }
        }

        return $"URN-{(maxSayi + 1):D6}";
    }

    public async Task<string> SonrakiBarkodUretAsync(CancellationToken cancellationToken = default)
    {
        var barkodlar = await _baglam.Urunler
            .IgnoreQueryFilters()
            .Where(u => u.Barkod != null && u.Barkod.StartsWith("210"))
            .Select(u => u.Barkod!)
            .ToListAsync(cancellationToken);

        int maxSayi = 0;
        foreach (var b in barkodlar)
        {
            if (b.Length == 13 && b.StartsWith("210"))
            {
                var govde = b.Substring(3, 9);
                if (int.TryParse(govde, out var sayi) && sayi > maxSayi)
                {
                    maxSayi = sayi;
                }
            }
        }

        var yeniGovde = $"210{(maxSayi + 1):D9}";
        int kontrolBasamagi = Ean13KontrolBasamagiHesapla(yeniGovde);
        return $"{yeniGovde}{kontrolBasamagi}";
    }

    public bool Ean13BarkodGecerliMi(string barkod)
    {
        if (string.IsNullOrWhiteSpace(barkod) || barkod.Length != 13 || !barkod.All(char.IsDigit))
            return false;

        var govde = barkod.Substring(0, 12);
        int beklenenKontrol = Ean13KontrolBasamagiHesapla(govde);
        int mevcutKontrol = barkod[12] - '0';
        return beklenenKontrol == mevcutKontrol;
    }

    public static int Ean13KontrolBasamagiHesapla(string ilk12Hane)
    {
        if (ilk12Hane.Length < 12)
            ilk12Hane = ilk12Hane.PadLeft(12, '0');

        int toplam = 0;
        for (int i = 0; i < 12; i++)
        {
            int hane = ilk12Hane[i] - '0';
            toplam += (i % 2 == 0) ? hane : hane * 3;
        }

        int kalan = toplam % 10;
        return (10 - kalan) % 10;
    }
}
