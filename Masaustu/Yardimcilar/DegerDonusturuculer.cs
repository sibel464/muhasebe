using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Masaustu.Yardimcilar;

public class BosBoslukDonusturucu : IValueConverter
{
    public static readonly BosBoslukDonusturucu Ornek = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class TersBosBoslukDonusturucu : IValueConverter
{
    public static readonly TersBosBoslukDonusturucu Ornek = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class KaydediliyorMetinDonusturucu : IValueConverter
{
    public static readonly KaydediliyorMetinDonusturucu Ornek = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "Kaydediliyor..." : "Kaydet";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
