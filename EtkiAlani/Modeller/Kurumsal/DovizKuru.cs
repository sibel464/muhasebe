using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Kurumsal;

public class DovizKuru : TemelVarlik
{
    public string ParaBirimiKodu { get; set; } = string.Empty; // USD, EUR, GBP, CHF etc.
    public DateTime Tarih { get; set; } // İşlem / Kur Tarihi (Date only UTC)
    public DateTime KurTarihi { get; set; } // TCMB'nin yayınladığı asıl kur tarihi (Hafta sonu/tatil fallback)
    public decimal DovizAlis { get; set; }
    public decimal DovizSatis { get; set; }
    public decimal EfektifAlis { get; set; }
    public decimal EfektifSatis { get; set; }
    public string Kaynak { get; set; } = "TCMB"; // "TCMB" veya "MANUEL"

    public int KullaniciId { get; set; }
    public string Aciklama { get; set; } = string.Empty;
}
