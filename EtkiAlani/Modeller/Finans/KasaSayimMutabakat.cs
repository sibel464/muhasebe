using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Finans;

public class KasaSayimMutabakat : TemelVarlik
{
    public Guid? SirketId { get; set; }
    public int KasaId { get; set; }
    public Kasa? Kasa { get; set; }
    public DateTime Tarih { get; set; } = DateTime.UtcNow;

    // Banknot Kırılımları
    public int Adet200 { get; set; } = 0;
    public int Adet100 { get; set; } = 0;
    public int Adet50 { get; set; } = 0;
    public int Adet20 { get; set; } = 0;
    public int Adet10 { get; set; } = 0;
    public int Adet5 { get; set; } = 0;
    public decimal BozukParaToplam { get; set; } = 0;

    // Toplamlar ve Fark
    public decimal FizikiToplam => (Adet200 * 200) + (Adet100 * 100) + (Adet50 * 50) + (Adet20 * 20) + (Adet10 * 10) + (Adet5 * 5) + BozukParaToplam;
    public decimal SistemBakiyesi { get; set; } = 0;
    public decimal Fark => FizikiToplam - SistemBakiyesi; // + ise Kasa Fazlası, - ise Kasa Noksanı

    public string Aciklama { get; set; } = string.Empty;
    public int? OlusturanKullaniciId { get; set; }
}
