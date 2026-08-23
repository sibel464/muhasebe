using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Kullanici;

public class Kullanici : TemelVarlik
{
    public string KullaniciAdi { get; set; } = string.Empty;
    public string SifreHash { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Soyad { get; set; } = string.Empty;
    public string? Eposta { get; set; }
    public bool Aktif { get; set; } = true;
    public string Rol { get; set; } = "Kullanici";
    public DateTime? SonGirisTarihi { get; set; }
}
