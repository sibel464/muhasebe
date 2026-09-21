namespace EtkiAlani.Modeller.Cari;

public class CariFiltre
{
    public string AramaMetni { get; set; } = string.Empty;
    public string Kod { get; set; } = string.Empty;
    public string GrupKodu { get; set; } = string.Empty;
    public CariTuru? Tur { get; set; }
    public int? CariTipiId { get; set; }
    public string Il { get; set; } = string.Empty;
    public bool SadeceBorclular { get; set; }
    public bool SadeceAlacaklilar { get; set; }
}
