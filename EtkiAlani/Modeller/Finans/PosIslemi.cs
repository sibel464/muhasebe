using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Finans;

public enum PosIslemDurumu
{
    Bekliyor = 1,
    HesabaGecti = 2,
    Iptal = 3
}

public class PosIslemi : TemelVarlik
{
    public Guid? SirketId { get; set; }
    public int BankaHesabiId { get; set; }
    public BankaHesabi? BankaHesabi { get; set; }

    public string BankaAdi { get; set; } = string.Empty;
    public string PosCihazAdi { get; set; } = string.Empty;
    public string KartTuru { get; set; } = "Kredi Kartı";
    public int TaksitSayisi { get; set; } = 1;

    public decimal BrutTutar { get; set; } = 0;
    public decimal KomisyonOrani { get; set; } = 0;
    public decimal KomisyonTutar { get; set; } = 0;
    public decimal NetTutar { get; set; } = 0;

    public DateTime IslemTarihi { get; set; } = DateTime.UtcNow;
    public DateTime BeklenenHesabaGecisTarihi { get; set; } = DateTime.UtcNow.AddDays(1);
    public DateTime? HesabaGecisTarihi { get; set; }

    public PosIslemDurumu Durum { get; set; } = PosIslemDurumu.Bekliyor;
    public int? FaturaId { get; set; }
    public int? CariKartId { get; set; }
    public string Aciklama { get; set; } = string.Empty;
}
