using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Stok;

public class UrunSatisFiyati : TemelVarlik
{
    public int UrunId { get; set; }
    public Urun? Urun { get; set; }

    public int TaksitSayisi { get; set; } = 1; // 1 = Tek Çekim, 2..9 = Taksitli
    public decimal Fiyat { get; set; } = 0;
    public int? ParaBirimiId { get; set; }
    public bool AktifMi { get; set; } = true;
}
