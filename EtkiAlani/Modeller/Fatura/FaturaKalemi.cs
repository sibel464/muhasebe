using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Siparis;
using EtkiAlani.Modeller.Irsaliye;

namespace EtkiAlani.Modeller.Fatura;

public class FaturaKalemi : TemelVarlik
{
    public int? SiparisKalemiId { get; set; }
    public SiparisKalemi? SiparisKalemi { get; set; }
    public int? IrsaliyeKalemiId { get; set; }
    public IrsaliyeKalemi? IrsaliyeKalemi { get; set; }
    public int FaturaId { get; set; }
    public Fatura? Fatura { get; set; }

    public int UrunId { get; set; }
    public Urun? Urun { get; set; }

    // Ürün kartı silinir veya güncellenirse geçmiş faturaların bozulmasını önlemek için snapshot alanlar
    public string UrunKodu { get; set; } = string.Empty;
    public string UrunAd { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;

    public decimal Miktar { get; set; } = 0;
    public decimal BirimFiyat { get; set; } = 0;
    public decimal IskontoOrani { get; set; } = 0;       // Satır iskontosu (%)
    public decimal IskontoTutari { get; set; } = 0;      // Hesaplanan satır iskonto tutarı
    public decimal KdvOrani { get; set; } = 0;           // Satır KDV oranı (%)
    public decimal KdvTutari { get; set; } = 0;          // Hesaplanan KDV tutarı
    public decimal SatirToplami { get; set; } = 0;       // Net Satır Toplamı (Miktar * Fiyat - İskonto + KDV)

    public ICollection<FaturaKalemiStokDagilimi> StokDagilimlari { get; set; } = new List<FaturaKalemiStokDagilimi>();
}
