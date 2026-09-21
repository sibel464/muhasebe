using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Kurumsal;

public class Sube : TemelVarlik
{
    public Guid SirketId { get; set; }
    public Sirket? Sirket { get; set; }
    public string SubeKodu { get; set; } = string.Empty;
    public string SubeAdi { get; set; } = string.Empty;
    public string? Telefon { get; set; }
    public string? Adres { get; set; }
    public bool AktifMi { get; set; } = true;
    public List<Depo> Depolar { get; set; } = new();
}
