using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Cari;

public class Musteri : TemelVarlik
{
    public string Ad { get; set; } = string.Empty;
    public string? VergiNo { get; set; }
    public string? TcNo { get; set; }
    public string? Telefon { get; set; }
    public string? Eposta { get; set; }
    public string? Adres { get; set; }
    public string? Sehir { get; set; }
    public bool Aktif { get; set; } = true;
    public decimal Bakiye { get; set; } = 0;
}
