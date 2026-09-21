using EtkiAlani.Modeller.Ortak;
namespace EtkiAlani.Modeller.Cari;
public sealed class CariNotu : TemelVarlik
{
    public Guid SirketId { get; set; }
    public int CariKartId { get; set; }
    public CariKart CariKart { get; set; } = null!;
    public int KullaniciId { get; set; }
    public string KullaniciAdi { get; set; } = "";
    public string Baslik { get; set; } = "";
    public string Metin { get; set; } = "";
}
