using EtkiAlani.Yardimcilar;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class CarpanliOkumaTests
{
    [Theory]
    [InlineData("12*8690000000001", 12, "8690000000001")]
    [InlineData("3x URN-5", 3, "URN-5")]
    [InlineData("2,5*KAŞAR", 2.5, "KAŞAR")]
    [InlineData("8690000000001", 1, "8690000000001")]
    [InlineData("ABCx12", 1, "ABCx12")]   // sol taraf sayı değil: olduğu gibi kod
    [InlineData("0*123", 1, "0*123")]     // sıfır çarpan kabul edilmez
    [InlineData("5*", 1, "5*")]
    [InlineData("  ", 1, "")]
    public void Carpan_ve_kod_ayrilir(string okunan, double carpan, string kod)
    {
        var s = CarpanliOkuma.Coz(okunan);
        Assert.Equal((decimal)carpan, s.Carpan);
        Assert.Equal(kod, s.Kod);
    }
}
