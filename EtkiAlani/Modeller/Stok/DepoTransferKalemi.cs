using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Stok;

public class DepoTransferKalemi : TemelVarlik
{
    public int DepoTransferFisiId { get; set; }
    public DepoTransferFisi? DepoTransferFisi { get; set; }

    public int UrunId { get; set; }
    public Urun? Urun { get; set; }

    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal ToplamTutar => Miktar * BirimFiyat;

    public string? Not { get; set; }
    public string? RafKodu { get; set; }
}
