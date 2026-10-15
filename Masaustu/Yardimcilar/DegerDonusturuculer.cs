using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using EtkiAlani.Modeller.Siparis;
using EtkiAlani.Modeller.Finans;

namespace Masaustu.Yardimcilar;

// Veritabanında UTC tutulan zamanı bilgisayarın yerel saatiyle gösterir (parametre: biçim, ör. "dd.MM.yyyy HH:mm").
public class UtcYerelTarihDonusturucu : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is DateTime t
        ? DateTime.SpecifyKind(t, DateTimeKind.Utc).ToLocalTime().ToString(parameter as string ?? "dd.MM.yyyy HH:mm", culture)
        : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public class SiparisDurumRenkDonusturucu : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        SiparisDurumu.Taslak => new SolidColorBrush(Color.FromRgb(226, 232, 240)),
        SiparisDurumu.Beklemede => new SolidColorBrush(Color.FromRgb(254, 243, 199)),
        SiparisDurumu.Onaylandi => new SolidColorBrush(Color.FromRgb(219, 234, 254)),
        SiparisDurumu.KismenKarsilandi => new SolidColorBrush(Color.FromRgb(224, 231, 255)),
        SiparisDurumu.Tamamlandi => new SolidColorBrush(Color.FromRgb(220, 252, 231)),
        SiparisDurumu.Iptal => new SolidColorBrush(Color.FromRgb(254, 226, 226)),
        _ => Brushes.Transparent
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public class SiparisMetinDonusturucu : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        SiparisTuru.Satis => "Satış Siparişi",
        SiparisTuru.Alis => "Alış Siparişi",
        SiparisDurumu.Onaylandi => "Onaylandı",
        SiparisDurumu.KismenKarsilandi => "Kısmen Karşılandı",
        SiparisDurumu.Tamamlandi => "Tamamlandı",
        SiparisDurumu.Beklemede => "Beklemede",
        SiparisDurumu.Taslak => "Taslak",
        SiparisDurumu.Iptal => "İptal",
        SiparisOdemeSekli.KrediKarti => "Kredi Kartı",
        SiparisOdemeSekli.AcikHesap => "Açık Hesap",
        SiparisOdemeSekli.Belirtilmedi => "Belirtilmedi",
        SiparisOdemeSekli.Nakit => "Nakit",
        SiparisOdemeSekli.Banka => "Banka",
        _ => value?.ToString() ?? string.Empty
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public class FinansMetinDonusturucu : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        FinansIslemTuru.Tahsilat => "Tahsilat",
        FinansIslemTuru.Odeme => "Ödeme",
        OdemeYontemi.Nakit => "Nakit",
        OdemeYontemi.Banka => "Banka",
        OdemeYontemi.KrediKarti => "Kredi Kartı",
        FinansIslemDurumu.Tamamlandi => "Tamamlandı",
        FinansIslemDurumu.IptalEdildi => "İptal Edildi",
        FinansIslemDurumu.TersKayit => "Ters Kayıt",
        _ => value?.ToString() ?? string.Empty
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class SifirGorunurlukDonusturucu : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int sayi && sayi == 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public class BosBoslukDonusturucu : IValueConverter
{
    public static readonly BosBoslukDonusturucu Ornek = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class TersBosBoslukDonusturucu : IValueConverter
{
    public static readonly TersBosBoslukDonusturucu Ornek = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class KaydediliyorMetinDonusturucu : IValueConverter
{
    public static readonly KaydediliyorMetinDonusturucu Ornek = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "Kaydediliyor..." : "Kaydet";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BuyukMuDonusturucu : IValueConverter
{
    public static readonly BuyukMuDonusturucu SifirdanBuyuk = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return d > 0;
        if (value is double db)
            return db > 0;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class KucukMuDonusturucu : IValueConverter
{
    public static readonly KucukMuDonusturucu SifirdanKucuk = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return d < 0;
        if (value is double db)
            return db < 0;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BakiyeRenkDonusturucu : IValueConverter
{
    public static readonly BakiyeRenkDonusturucu Ornek = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d)
        {
            if (d > 0) return "Alacak";
            if (d < 0) return "Borc";
        }
        else if (value is double db)
        {
            if (db > 0) return "Alacak";
            if (db < 0) return "Borc";
        }
        return "Sifir";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class HareketTurDonusturucu : IValueConverter
{
    public static readonly HareketTurDonusturucu Ornek = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is EtkiAlani.Modeller.Finans.HareketTuru tur)
        {
            var p = parameter?.ToString();
            if (p == "Renk")
                return tur == EtkiAlani.Modeller.Finans.HareketTuru.Gelir ? "#10B981" : "#EF4444";
            if (p == "ArkaPlan")
                return tur == EtkiAlani.Modeller.Finans.HareketTuru.Gelir ? "#D1FAE5" : "#FEE2E2";
            if (p == "Simge")
                return tur == EtkiAlani.Modeller.Finans.HareketTuru.Gelir ? "📈" : "📉";

            return tur == EtkiAlani.Modeller.Finans.HareketTuru.Gelir ? "GELİR" : "GİDER";
        }
        return "TANIMSIZ";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }
}
