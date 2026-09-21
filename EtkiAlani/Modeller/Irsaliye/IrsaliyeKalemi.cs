using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Siparis;

namespace EtkiAlani.Modeller.Irsaliye;

public class IrsaliyeKalemi : TemelVarlik
{
    public int IrsaliyeId { get; set; }
    public Irsaliye? Irsaliye { get; set; }
    public int? SiparisKalemiId { get; set; }
    public SiparisKalemi? SiparisKalemi { get; set; }

    public int UrunId { get; set; }
    public Urun? Urun { get; set; }

    public string UrunKodu { get; set; } = string.Empty;
    public string UrunAd { get; set; } = string.Empty;
    public string Barkod { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public string Birim { get; set; } = "Adet";

    public decimal Miktar { get; set; } = 0;
    public decimal BirimFiyat { get; set; } = 0;
    public decimal KdvOrani { get; set; } = 20.00m;
    public decimal KdvTutari { get; set; } = 0;
    public decimal SatirToplami { get; set; } = 0;
    public List<IrsaliyeKalemiStokDagilimi> StokDagilimlari { get; set; } = new();
}
