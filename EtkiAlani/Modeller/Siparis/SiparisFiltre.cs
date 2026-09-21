namespace EtkiAlani.Modeller.Siparis;

public class SiparisFiltre
{
    public string? Arama { get; set; }
    public string? SiparisNo { get; set; }
    public SiparisTuru? Tur { get; set; }
    public SiparisDurumu? Durum { get; set; }
    public int? CariKartId { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public DateTime? TeslimTarihi { get; set; }
    public int? OlusturanKullaniciId { get; set; }
}
