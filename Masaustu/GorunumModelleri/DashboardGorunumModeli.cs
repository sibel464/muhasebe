using Masaustu.GorunumModelleri;

namespace Masaustu.GorunumModelleri;

public class DashboardGorunumModeli : TemelGorunumModeli
{
    private int _musteriSayisi;
    private int _urunSayisi;
    private int _aylikFatura;
    private decimal _kasaBakiyesi;

    public int MusteriSayisi { get => _musteriSayisi; set => OzellikAyarla(ref _musteriSayisi, value); }
    public int UrunSayisi { get => _urunSayisi; set => OzellikAyarla(ref _urunSayisi, value); }
    public int AylikFatura { get => _aylikFatura; set => OzellikAyarla(ref _aylikFatura, value); }
    public decimal KasaBakiyesi { get => _kasaBakiyesi; set => OzellikAyarla(ref _kasaBakiyesi, value); }

    public DashboardGorunumModeli()
    {
        // Örnek veriler - ileride veritabanından gelecek
        MusteriSayisi = 0;
        UrunSayisi = 0;
        AylikFatura = 0;
        KasaBakiyesi = 0;
    }
}
