namespace EtkiAlani.Yardimcilar;

/// <summary>
/// Fatura tutar/iskonto/KDV hesabı. Ekran önizlemesi ve kayıt aynı hesaplayıcıyı kullanır.
/// Kurallar: satır iskontosu satırdan düşülür; fatura altı (genel) iskonto satırlara net tutarları
/// oranında dağıtılır ve KDV matrahını düşürür; KDV her satırda kuruşa yuvarlanır.
/// </summary>
public static class FaturaHesaplayici
{
    public sealed record Girdi(decimal Miktar, decimal BirimFiyat, decimal IskontoOrani, decimal KdvOrani);

    public sealed record SatirSonucu(decimal Brut, decimal SatirIskontosu, decimal GenelIskontoPayi, decimal Matrah, decimal Kdv, decimal Toplam);

    public sealed record Sonuc(
        IReadOnlyList<SatirSonucu> Satirlar,
        decimal AraToplam,
        decimal SatirIskontosuToplam,
        decimal GenelIskontoTutari,
        decimal Matrah,
        decimal KdvToplam,
        decimal GenelToplam);

    private static decimal Kurus(decimal d) => decimal.Round(d, 2, MidpointRounding.AwayFromZero);

    public static Sonuc Hesapla(IReadOnlyList<Girdi> kalemler, decimal genelIskontoOrani)
    {
        var brutler = kalemler.Select(k => Kurus(k.Miktar * k.BirimFiyat)).ToList();
        var satirIskontolari = kalemler.Select((k, i) => Kurus(brutler[i] * k.IskontoOrani / 100m)).ToList();
        var netler = brutler.Select((b, i) => b - satirIskontolari[i]).ToList();
        var netToplam = netler.Sum();

        // Genel iskonto netler oranında dağıtılır; kuruş farkı son pay alan satıra yazılır, toplam her zaman tutar.
        var genelIskonto = Kurus(netToplam * genelIskontoOrani / 100m);
        var paylar = new decimal[kalemler.Count];
        if (genelIskonto != 0 && netToplam != 0)
        {
            var dagitilan = 0m;
            var sonIndeks = netler.FindLastIndex(n => n != 0);
            for (var i = 0; i < kalemler.Count; i++)
            {
                if (netler[i] == 0) continue;
                paylar[i] = i == sonIndeks ? genelIskonto - dagitilan : Kurus(genelIskonto * netler[i] / netToplam);
                dagitilan += paylar[i];
            }
        }

        var satirlar = new List<SatirSonucu>(kalemler.Count);
        for (var i = 0; i < kalemler.Count; i++)
        {
            var matrah = netler[i] - paylar[i];
            var kdv = Kurus(matrah * kalemler[i].KdvOrani / 100m);
            satirlar.Add(new SatirSonucu(brutler[i], satirIskontolari[i], paylar[i], matrah, kdv, matrah + kdv));
        }

        var toplamMatrah = satirlar.Sum(s => s.Matrah);
        var kdvToplam = satirlar.Sum(s => s.Kdv);
        return new Sonuc(satirlar, brutler.Sum(), satirIskontolari.Sum(), genelIskonto, toplamMatrah, kdvToplam, toplamMatrah + kdvToplam);
    }
}
