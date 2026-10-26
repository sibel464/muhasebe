using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Uygulama.Dto.Rapor;

namespace Masaustu.Yardimcilar;

// Ekrandaki DataGrid'in görünen sütunlarını (başlık, bağlama yolu, ekran biçimi) çıktı tablosuna çevirir;
// böylece Excel/PDF/yazdırma kullanıcının ekranda gördüğüyle aynı olur.
public static class EkranTablosu
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static DataGrid? IlkDataGrid(object? kok)
    {
        if (kok is DataGrid g) return g;
        if (kok is not DependencyObject d) return null;
        foreach (var cocuk in LogicalTreeHelper.GetChildren(d))
        {
            var bulunan = IlkDataGrid(cocuk);
            if (bulunan != null) return bulunan;
        }
        return null;
    }

    public static RaporTablosu Olustur(DataGrid grid, IEnumerable satirlar, string baslik, string altBaslik)
    {
        var kolonlar = grid.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .OrderBy(c => c.DisplayIndex)
            .Select(c => (Kolon: c, Baglama: (c as DataGridBoundColumn)?.Binding as Binding))
            .Where(x => x.Baglama?.Path?.Path is { Length: > 0 })
            .ToList();

        var tablo = new RaporTablosu { Baslik = baslik, AltBaslik = altBaslik };
        var hucreler = satirlar.Cast<object?>()
            .Where(s => s != null && s != CollectionView.NewItemPlaceholder)
            .Select(s => kolonlar.Select(k => Hucre(s!, k.Baglama!)).ToArray())
            .ToList();
        for (int i = 0; i < kolonlar.Count; i++)
        {
            var sayisal = hucreler.Count > 0 && hucreler.All(h => h[i].Sayi.HasValue || h[i].Metin.Length == 0);
            var ondalikli = hucreler.Any(h => h[i].Sayi is { } d && d != decimal.Truncate(d))
                || (kolonlar[i].Baglama!.StringFormat?.Contains("N2") ?? false);
            tablo.Kolonlar.Add(new RaporKolonu(kolonlar[i].Kolon.Header?.ToString() ?? "", sayisal, ondalikli));
        }
        tablo.Satirlar = hucreler;
        return tablo;
    }

    private static RaporHucresi Hucre(object satir, Binding b)
    {
        var deger = YoldanDeger(satir, b.Path.Path);
        if (b.Converter != null)
            deger = b.Converter.Convert(deger, typeof(object), b.ConverterParameter, Tr);
        decimal? sayi = deger switch
        {
            decimal d => d, int i => i, long l => l, double db => (decimal)db, float f => (decimal)f, short s => s, _ => null
        };
        string metin;
        if (deger == null) metin = b.TargetNullValue?.ToString() ?? "";
        else if (!string.IsNullOrEmpty(b.StringFormat))
            metin = string.Format(Tr, b.StringFormat.Contains('{') ? b.StringFormat : "{0:" + b.StringFormat + "}", deger);
        else if (deger is bool bl) metin = bl ? "Evet" : "Hayır";
        else if (deger is DateTime dt) metin = dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("dd.MM.yyyy", Tr) : dt.ToString("dd.MM.yyyy HH:mm", Tr);
        else metin = Convert.ToString(deger, Tr) ?? "";
        return new RaporHucresi(metin, sayi);
    }

    private static object? YoldanDeger(object? nesne, string yol)
    {
        foreach (var parca in yol.Split('.'))
        {
            if (nesne == null) return null;
            nesne = nesne.GetType().GetProperty(parca)?.GetValue(nesne);
        }
        return nesne;
    }
}
