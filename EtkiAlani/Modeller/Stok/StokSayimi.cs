using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Tanimlar;

namespace EtkiAlani.Modeller.Stok;

public enum StokSayimDurumu
{
    Taslak = 1,
    Sayimda = 2,
    Onaylandi = 3,
    Iptal = 4
}

public class StokSayimi : TemelVarlik
{
    public Guid SirketId { get; set; }
    public Sirket? Sirket { get; set; }

    public string SayimNo { get; set; } = string.Empty;
    public DateTime Tarih { get; set; } = DateTime.UtcNow;

    public int DepoId { get; set; }
    public Depo? Depo { get; set; }

    public StokSayimDurumu Durum { get; set; } = StokSayimDurumu.Taslak;

    public string Aciklama { get; set; } = string.Empty;
    public int? OlusturanKullaniciId { get; set; }
    public int? OnaylayanKullaniciId { get; set; }
    public DateTime? OnayTarihi { get; set; }

    public List<StokSayimKalemi> Kalemler { get; set; } = new();
}
