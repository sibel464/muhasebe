using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Finans;

public class BankaMutabakat : TemelVarlik
{
    public Guid? SirketId { get; set; }
    public int BankaHesabiId { get; set; }
    public BankaHesabi? BankaHesabi { get; set; }
    public DateTime Tarih { get; set; } = DateTime.UtcNow;

    public decimal EkstreBakiyesi { get; set; } = 0;
    public decimal SistemBakiyesi { get; set; } = 0;
    public decimal Fark => EkstreBakiyesi - SistemBakiyesi;

    public string Aciklama { get; set; } = string.Empty;
    public int? OlusturanKullaniciId { get; set; }
}
