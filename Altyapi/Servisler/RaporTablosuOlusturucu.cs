using System.Collections;
using System.Globalization;
using System.Reflection;
using Uygulama.Dto.Rapor;

namespace Altyapi.Servisler;

// Özellik adı → başlık eşlemesiyle verilen listeyi ortak rapor tablosuna çevirir.
public static class RaporTablosuOlusturucu
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    // Adında bunlar geçen sayısal alanlar para değildir (miktar, oran, adet...); "TL" eklenmez.
    private static readonly string[] ParaOlmayanAdlar = ["Miktar", "Oran", "Adet", "Sayi", "Yuzde", "Gun"];

    public static RaporTablosu Olustur<T>(string baslik, SatisRaporFiltreDto? filtre, IEnumerable<T> veriler, Dictionary<string, string> kolonBasliklari)
    {
        var liste = veriler.Cast<object?>().ToList();
        var tur = typeof(T) == typeof(object) ? liste.FirstOrDefault(x => x != null)?.GetType() ?? typeof(T) : typeof(T);
        var props = tur.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var eslesen = kolonBasliklari
            .Select(k => (Baslik: k.Value, Prop: props.FirstOrDefault(p => p.Name == k.Key)))
            .Where(x => x.Prop != null)
            .Select(x => (x.Baslik, Prop: x.Prop!))
            .ToList();

        var tablo = new RaporTablosu { Baslik = baslik, AltBaslik = DonemMetni(filtre) };
        foreach (var (b, p) in eslesen)
        {
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            var sayisal = t == typeof(decimal) || t == typeof(double) || t == typeof(float) || t == typeof(int) || t == typeof(long);
            tablo.Kolonlar.Add(new RaporKolonu(b, sayisal, t == typeof(decimal) || t == typeof(double) || t == typeof(float)));
        }

        foreach (var item in liste)
        {
            tablo.Satirlar.Add(eslesen.Select(e => Hucre(item == null ? null : e.Prop.GetValue(item), e.Prop.Name)).ToArray());
        }
        return tablo;
    }

    public static RaporHucresi Hucre(object? deger, string? alanAdi = null) => deger switch
    {
        null => new RaporHucresi(""),
        decimal d => new RaporHucresi(d.ToString("N2", Tr) + (ParaMi(alanAdi) ? " TL" : ""), d),
        double d => new RaporHucresi(d.ToString("N2", Tr), (decimal)d),
        float d => new RaporHucresi(d.ToString("N2", Tr), (decimal)d),
        int i => new RaporHucresi(i.ToString(Tr), i),
        long l => new RaporHucresi(l.ToString(Tr), l),
        bool b => new RaporHucresi(b ? "Evet" : "Hayır"),
        DateTime dt => new RaporHucresi(dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("dd.MM.yyyy", Tr) : dt.ToString("dd.MM.yyyy HH:mm", Tr)),
        IEnumerable e and not string => new RaporHucresi(string.Join(", ", e.Cast<object?>())),
        _ => new RaporHucresi(Convert.ToString(deger, Tr) ?? "")
    };

    private static bool ParaMi(string? alanAdi) =>
        alanAdi == null || !ParaOlmayanAdlar.Any(a => alanAdi.Contains(a, StringComparison.OrdinalIgnoreCase));

    public static string DonemMetni(SatisRaporFiltreDto? filtre)
    {
        if (filtre?.BaslangicTarihi == null && filtre?.BitisTarihi == null) return "";
        var bas = filtre.BaslangicTarihi?.ToString("dd.MM.yyyy", Tr) ?? "...";
        var bit = filtre.BitisTarihi?.ToString("dd.MM.yyyy", Tr) ?? "...";
        return $"Dönem: {bas} - {bit}";
    }
}
