using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Guvenlik;

public enum AuditIslemTuru
{
    Ekleme = 1,      // INSERT
    Guncelleme = 2,  // UPDATE
    Silme = 3,       // DELETE
    SoftDelete = 4   // Mantıksal Silme
}

public class AuditLog : TemelVarlik
{
    public int? KullaniciId { get; set; }
    public string? KullaniciAdi { get; set; } = "Sistem";
    public AuditIslemTuru IslemTuru { get; set; }
    public string TabloAdi { get; set; } = string.Empty;
    public string KayitId { get; set; } = string.Empty;
    public string? EskiDegerJson { get; set; }
    public string? YeniDegerJson { get; set; }
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public string? IstemciBilgisi { get; set; }
    public string? IslemDetayi { get; set; }
}
