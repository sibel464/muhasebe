using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Finans;

public enum HareketTuru { Gelir, Gider }

public class KasaHareketi : TemelVarlik
{
    public DateTime Tarih { get; set; } = DateTime.Today;
    public HareketTuru Tur { get; set; }
    public decimal Tutar { get; set; } = 0;
    public string Aciklama { get; set; } = string.Empty;
    public string? Belge { get; set; }
}
