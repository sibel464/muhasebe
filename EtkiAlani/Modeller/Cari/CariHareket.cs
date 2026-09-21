using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Cari;

public class CariHareket : TemelVarlik
{
    public int CariKartId { get; set; }
    public CariKart? CariKart { get; set; }

    // İşlem Tarihi (kullanıcı tarafından girilen)
    public DateTime Tarih { get; set; } = DateTime.UtcNow;

    public CariHareketTuru HareketTuru { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public string? BelgeNo { get; set; }

    public decimal Borc { get; set; } = 0;
    public decimal Alacak { get; set; } = 0;

    // Çoklu Para Birimi (Döviz)
    public int? ParaBirimiId { get; set; }
    public EtkiAlani.Modeller.Kurumsal.ParaBirimi? ParaBirimi { get; set; }
    public decimal Kur { get; set; } = 1;
    public decimal DovizBorc { get; set; } = 0;
    public decimal DovizAlacak { get; set; } = 0;

    // Fatura Kapama (Eşleştirme)
    public decimal KapaliTutar { get; set; } = 0;
    public decimal KapaliDovizTutar { get; set; } = 0;

    // Fatura veya Kasa/Banka Hareket ID'si
    public int? IlgiliBelgeId { get; set; }
}
