using EtkiAlani.Yardimcilar;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

public sealed class HesapEkstresiTests
{
    private sealed record H(int Id, DateTime Tarih, decimal Tutar);

    private static readonly H[] Hareketler =
    [
        new(1, new DateTime(2026, 10, 1, 9, 0, 0), 1000m),
        new(2, new DateTime(2026, 10, 5, 23, 30, 0), -200m),
        new(3, new DateTime(2026, 10, 6, 0, 15, 0), 500m),
        new(4, new DateTime(2026, 10, 6, 18, 0, 0), -50m),
        new(5, new DateTime(2026, 10, 7, 10, 0, 0), 75m)
    ];

    [Fact]
    public void Gun_sonu_onceki_gunleri_devir_alir_ve_gunun_tamamini_kapsar()
    {
        var e = HesapEkstresi.Olustur(Hareketler, h => h.Tarih, h => h.Tutar, h => h.Id, new DateTime(2026, 10, 6), new DateTime(2026, 10, 6));
        Assert.Equal(800m, e.Devir);
        Assert.Equal([3, 4], e.Satirlar.Select(s => s.Kayit.Id));
        Assert.Equal(500m, e.ToplamGiris);
        Assert.Equal(50m, e.ToplamCikis);
        Assert.Equal(1250m, e.Kapanis);
        Assert.Equal([1300m, 1250m], e.Satirlar.Select(s => s.Bakiye));
        Assert.Equal(0m, e.Satirlar[1].Giris);
        Assert.Equal(50m, e.Satirlar[1].Cikis);
    }

    [Fact]
    public void Tarihsiz_ekstre_tum_hareketleri_sirali_yurur_bakiyeyle_verir()
    {
        var e = HesapEkstresi.Olustur(Hareketler.Reverse(), h => h.Tarih, h => h.Tutar, h => h.Id, null, null);
        Assert.Equal(0m, e.Devir);
        Assert.Equal([1, 2, 3, 4, 5], e.Satirlar.Select(s => s.Kayit.Id));
        Assert.Equal(1325m, e.Kapanis);
        Assert.Equal(e.Kapanis, e.Satirlar[^1].Bakiye);
    }

    [Fact]
    public void Ters_tarih_araligi_reddedilir() =>
        Assert.Throws<ArgumentException>(() => HesapEkstresi.Olustur(Hareketler, h => h.Tarih, h => h.Tutar, h => h.Id, new DateTime(2026, 10, 7), new DateTime(2026, 10, 6)));
}
