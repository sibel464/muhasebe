using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Cari;

namespace EtkiAlani.Modeller.Fatura;

public enum FaturaTuru { Satis, Alis }

public class Fatura : TemelVarlik
{
    public string FaturaNo { get; set; } = string.Empty;
    public FaturaTuru Tur { get; set; }
    public DateTime Tarih { get; set; } = DateTime.Today;
    public int? MusteriId { get; set; }
    public Musteri? Musteri { get; set; }
    public int? TedarikciId { get; set; }
    public Tedarikci? Tedarikci { get; set; }
    public decimal AraToplam { get; set; } = 0;
    public decimal KdvToplam { get; set; } = 0;
    public decimal GenelToplam { get; set; } = 0;
    public string? Notlar { get; set; }
    public ICollection<FaturaKalemi> Kalemler { get; set; } = new List<FaturaKalemi>();
}
