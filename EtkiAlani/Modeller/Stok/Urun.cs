using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Stok;

public class Urun : TemelVarlik
{
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string? Barkod { get; set; }
    public string? Kategori { get; set; }
    public string Birim { get; set; } = "Adet";
    public decimal AlisFiyati { get; set; } = 0;
    public decimal SatisFiyati { get; set; } = 0;
    public decimal KdvOrani { get; set; } = 20;
    public decimal StokMiktari { get; set; } = 0;
    public decimal MinimumStok { get; set; } = 0;
    public bool Aktif { get; set; } = true;
}
