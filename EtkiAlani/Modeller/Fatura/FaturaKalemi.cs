using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Stok;

namespace EtkiAlani.Modeller.Fatura;

public class FaturaKalemi : TemelVarlik
{
    public int FaturaId { get; set; }
    public Fatura? Fatura { get; set; }
    public int UrunId { get; set; }
    public Urun? Urun { get; set; }
    public decimal Miktar { get; set; } = 1;
    public decimal BirimFiyat { get; set; } = 0;
    public decimal KdvOrani { get; set; } = 20;
    public decimal KdvTutar { get; set; } = 0;
    public decimal ToplamTutar { get; set; } = 0;
}
