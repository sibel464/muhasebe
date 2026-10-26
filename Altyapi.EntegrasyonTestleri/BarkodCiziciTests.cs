using EtkiAlani.Yardimcilar;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class BarkodCiziciTests
{
    [Fact]
    public void Ean13_bilinen_barkodun_bit_dizisi_standarda_uyar()
    {
        // 4006381333931 için yayımlanmış EAN-13 kodlaması (ilk hane 4: LGLLGG paritesi).
        Assert.True(BarkodCizici.Ean13GecerliMi("4006381333931"));
        Assert.False(BarkodCizici.Ean13GecerliMi("4006381333932"));
        var bitler = BarkodCizici.Ean13Bitleri("4006381333931");
        Assert.Equal(95, bitler.Length);
        Assert.StartsWith("101" + "0001101" + "0100111", bitler); // 0 (L), 0 (G)
        Assert.Equal("01010", bitler.Substring(45, 5));
        Assert.EndsWith("1000010" + "1100110" + "101", bitler);    // son iki hane 3 ve 1 (R kodları)
    }

    [Fact]
    public void Code128_desen_tablosu_tutarli()
    {
        Assert.Equal(107, BarkodCizici.Code128.Length);
        Assert.All(BarkodCizici.Code128[..106], d =>
        {
            Assert.Equal(11, d.Sum(c => c - '0'));
            Assert.Equal(0, (d[0] - '0' + d[2] - '0' + d[4] - '0') % 2); // çubuk modül toplamı çifttir
        });
        Assert.Equal(13, BarkodCizici.Code128[106].Sum(c => c - '0'));
        Assert.Equal(107, BarkodCizici.Code128.Distinct().Count());
    }

    [Fact]
    public void Code128_saglama_hanesi_dogru_hesaplanir()
    {
        // "PJJ123C": 104 + 1×48 + 2×42 + 3×42 + 4×17 + 5×18 + 6×19 + 7×35 = 879; 879 mod 103 = 55.
        var bitler = BarkodCizici.Code128Bitleri("PJJ123C");
        var desenler = BarkodCizici.Code128;
        // Başlat B + 7 karakter + sağlama + durdur = 10 sembol; 9 × 11 + 13 = 112 modül.
        Assert.Equal(112, bitler.Length);
        var saglama = bitler.Substring(8 * 11, 11);
        Assert.Equal(Bit(desenler[55]), saglama);
        Assert.Throws<ArgumentException>(() => BarkodCizici.Code128Bitleri("ŞEKER"));
    }

    private static string Bit(string desen) => string.Concat(desen.Select((c, i) => new string(i % 2 == 0 ? '1' : '0', c - '0')));
}
