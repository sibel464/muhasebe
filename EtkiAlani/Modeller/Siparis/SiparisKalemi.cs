using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Stok;

namespace EtkiAlani.Modeller.Siparis;

public class SiparisKalemi : TemelVarlik
{
    public int SiparisId { get; set; }
    public Siparis? Siparis { get; set; }
    public int UrunId { get; set; }
    public Urun? Urun { get; set; }
    public string UrunKodu { get; set; } = string.Empty;
    public string Barkod { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal IskontoOrani { get; set; }
    public decimal IskontoTutari { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal KdvTutari { get; set; }
    public decimal SatirToplami { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public decimal SevkEdilenMiktar { get; set; }
    public decimal FaturalananMiktar { get; set; }
}
