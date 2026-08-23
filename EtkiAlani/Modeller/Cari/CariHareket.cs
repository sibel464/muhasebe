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

    // Fatura veya Kasa/Banka Hareket ID'si
    public int? IlgiliBelgeId { get; set; }

    // Sistem zaman damgası (değiştirilemez)
    public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
}
