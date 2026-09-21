using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Masaustu.Yardimcilar;

/// <summary>TR ve noktalı ondalık para girişlerini decimal'e güvenli biçimde dönüştürür.</summary>
public sealed class ParaTutariDonusturucu : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is decimal tutar ? tutar.ToString("N2", CultureInfo.GetCultureInfo("tr-TR")) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var metin = value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(metin))
            return 0m;

        var normal = NormalizeEt(metin);
        return decimal.TryParse(normal, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var tutar)
            ? tutar
            : DependencyProperty.UnsetValue;
    }

    private static string NormalizeEt(string metin)
    {
        var virgul = metin.LastIndexOf(',');
        var nokta = metin.LastIndexOf('.');
        if (virgul >= 0 && nokta >= 0)
        {
            var ondalikAyirac = virgul > nokta ? ',' : '.';
            var grupAyirac = ondalikAyirac == ',' ? "." : ",";
            return metin.Replace(grupAyirac, string.Empty).Replace(ondalikAyirac, '.');
        }

        if (virgul >= 0)
            return metin.Replace(".", string.Empty).Replace(',', '.');

        // Tek nokta üç haneli grup ayracıysa (1.250) Türkçe sayı kabul edilir; aksi halde ondalıktır.
        if (nokta >= 0 && metin.Length - nokta - 1 == 3)
            return metin.Replace(".", string.Empty);

        return metin;
    }
}
