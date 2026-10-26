using EtkiAlani.Modeller.Muhasebe;

namespace EtkiAlani.Yardimcilar;

public sealed record AmortismanSatiri(int Yil, decimal Tutar, decimal Birikmis, decimal NetDeger, bool Ayrildi, bool Devir);

// VUK'a göre yıllık amortisman planı:
// - Normal: maliyet / faydalı ömür; kıst amortismanda ilk yıl ay kesri tam ay sayılarak oranlanır, kalanı ömrün bitiminden
//   sonraki yıla kalır.
// - Azalan bakiyeler: net değer × (2 / ömür, en çok %50); ömrün son yılında kalan net değerin tamamı ayrılır.
// - Elden çıkarıldığı yıl ve sonrasında amortisman ayrılmaz. Ayrılmış (kayıtlı) yıllar gerçek tutarıyla gösterilir.
public static class AmortismanHesaplayici
{
    public static decimal Oran(SabitKiymet k) =>
        k.FaydaliOmurYil <= 0 ? 0 : k.Yontem == AmortismanYontemi.Normal ? 1m / k.FaydaliOmurYil : Math.Min(2m / k.FaydaliOmurYil, 0.5m);

    public static List<AmortismanSatiri> Plan(SabitKiymet k, IEnumerable<AmortismanKaydi>? kayitlar = null)
    {
        var sonuc = new List<AmortismanSatiri>();
        if (k.Maliyet <= 0 || k.FaydaliOmurYil <= 0) return sonuc;
        var ayrilan = (kayitlar ?? k.Kayitlar).Where(x => !x.Silindi).GroupBy(x => x.Yil).ToDictionary(g => g.Key, g => g.Sum(x => x.Tutar));

        var ilkYil = k.AlisTarihi.Year;
        var kist = k.KistAmortisman && k.Yontem == AmortismanYontemi.Normal && k.AlisTarihi.Month > 1;
        var sonYil = ilkYil + k.FaydaliOmurYil - 1 + (kist ? 1 : 0);
        var yillik = Math.Round(k.Maliyet / k.FaydaliOmurYil, 2, MidpointRounding.AwayFromZero);
        var cikisYili = k.EldenCikarmaTarihi?.Year;

        decimal birikmis = 0;
        var baslangic = ilkYil;
        if (k.DevirSonYil is int devirYili)
        {
            birikmis = Math.Min(k.DevirBirikmisAmortisman, k.Maliyet);
            if (birikmis > 0) sonuc.Add(new AmortismanSatiri(devirYili, birikmis, birikmis, k.Maliyet - birikmis, true, true));
            baslangic = Math.Max(ilkYil, devirYili + 1);
        }

        for (var yil = baslangic; yil <= Math.Max(sonYil, ayrilan.Keys.DefaultIfEmpty(0).Max()); yil++)
        {
            if (cikisYili.HasValue && yil >= cikisYili.Value) break;
            var kalan = k.Maliyet - birikmis;
            if (kalan <= 0) break;
            decimal tutar;
            var ayrildi = ayrilan.TryGetValue(yil, out var kayitli);
            if (ayrildi) tutar = kayitli;
            else if (yil >= sonYil) tutar = kalan;
            else if (k.Yontem == AmortismanYontemi.Normal)
            {
                var yilTutari = kist && yil == ilkYil
                    ? Math.Round(yillik * (13 - k.AlisTarihi.Month) / 12m, 2, MidpointRounding.AwayFromZero)
                    : yillik;
                tutar = Math.Min(yilTutari, kalan);
            }
            else tutar = Math.Min(Math.Round(kalan * Oran(k), 2, MidpointRounding.AwayFromZero), kalan);

            birikmis += tutar;
            sonuc.Add(new AmortismanSatiri(yil, tutar, birikmis, k.Maliyet - birikmis, ayrildi, false));
        }
        return sonuc;
    }

    // Verilen yıl için henüz ayrılmamış amortisman tutarı.
    public static decimal AyrilacakTutar(SabitKiymet k, int yil, IEnumerable<AmortismanKaydi>? kayitlar = null) =>
        Plan(k, kayitlar).FirstOrDefault(x => x.Yil == yil && !x.Ayrildi && !x.Devir)?.Tutar ?? 0m;

    public static decimal Birikmis(SabitKiymet k, IEnumerable<AmortismanKaydi>? kayitlar = null) =>
        Math.Min(k.DevirBirikmisAmortisman, k.Maliyet) + (kayitlar ?? k.Kayitlar).Where(x => !x.Silindi).Sum(x => x.Tutar);
}
