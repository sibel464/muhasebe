namespace EtkiAlani.Yardimcilar;

// Kasa ve banka ekstresi / gün sonu raporu: seçilen tarih aralığından önceki hareketler "devreden bakiye" olur,
// aralıktaki hareketler eskiden yeniye sıralanır ve her satırın yanında o ana kadarki bakiye gösterilir.
// Tarihler gün bazında ve dahil olarak karşılaştırılır (belge tarihleri yerel saatle saklanır).
public static class HesapEkstresi
{
    public sealed record Satir<T>(T Kayit, decimal Giris, decimal Cikis, decimal Bakiye);

    public sealed record Sonuc<T>(decimal Devir, IReadOnlyList<Satir<T>> Satirlar, decimal ToplamGiris, decimal ToplamCikis)
    {
        public decimal Kapanis => Devir + ToplamGiris - ToplamCikis;
    }

    // isaretliTutar: giriş için pozitif, çıkış için negatif tutar. sira: aynı andaki kayıtları sabit sıralamak için (Id).
    public static Sonuc<T> Olustur<T>(IEnumerable<T> kayitlar, Func<T, DateTime> tarih, Func<T, decimal> isaretliTutar, Func<T, int> sira,
        DateTime? baslangic, DateTime? bitis)
    {
        var bas = baslangic?.Date;
        var son = bitis?.Date;
        if (bas.HasValue && son.HasValue && bas > son)
            throw new ArgumentException("Başlangıç tarihi bitiş tarihinden sonra olamaz.");

        var sirali = kayitlar.OrderBy(tarih).ThenBy(sira).ToList();
        var devir = sirali.Where(k => bas.HasValue && tarih(k).Date < bas.Value).Sum(isaretliTutar);

        var bakiye = devir;
        var satirlar = new List<Satir<T>>();
        decimal giris = 0, cikis = 0;
        foreach (var k in sirali.Where(k => (!bas.HasValue || tarih(k).Date >= bas.Value) && (!son.HasValue || tarih(k).Date <= son.Value)))
        {
            var tutar = isaretliTutar(k);
            bakiye += tutar;
            if (tutar >= 0) giris += tutar; else cikis -= tutar;
            satirlar.Add(new Satir<T>(k, Math.Max(tutar, 0), Math.Max(-tutar, 0), bakiye));
        }
        return new Sonuc<T>(devir, satirlar, giris, cikis);
    }
}
