using EtkiAlani.Yardimcilar;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class TutarYaziciTests
{
    [Theory]
    [InlineData("0", "Yalnız Sıfır Türk Lirası")]
    [InlineData("1", "Yalnız Bir Türk Lirası")]
    [InlineData("0.50", "Yalnız Elli Kuruş")]
    [InlineData("100", "Yalnız Yüz Türk Lirası")]
    [InlineData("1000", "Yalnız Bin Türk Lirası")]
    [InlineData("1250.50", "Yalnız Bin İki Yüz Elli Türk Lirası Elli Kuruş")]
    [InlineData("11111", "Yalnız On Bir Bin Yüz On Bir Türk Lirası")]
    [InlineData("101000", "Yalnız Yüz Bir Bin Türk Lirası")]
    [InlineData("1000001.05", "Yalnız Bir Milyon Bir Türk Lirası Beş Kuruş")]
    [InlineData("2000000000", "Yalnız İki Milyar Türk Lirası")]
    [InlineData("-36.999", "Yalnız Otuz Yedi Türk Lirası")]
    public void Tutar_yaziyla_okunur(string tutar, string beklenen) =>
        Assert.Equal(beklenen, TutarYazici.Yaziyla(decimal.Parse(tutar, System.Globalization.CultureInfo.InvariantCulture)));
}
