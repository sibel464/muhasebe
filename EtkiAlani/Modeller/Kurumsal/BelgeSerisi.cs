using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Kurumsal;

public class BelgeSerisi : TemelVarlik
{
    public Guid SirketId { get; set; }
    public Sirket? Sirket { get; set; }
    public string BelgeTipi { get; set; } = string.Empty;
    public string Seri { get; set; } = string.Empty;
    public long SonNumara { get; set; }
    public string? Prefix { get; set; }
    public string? Format { get; set; }
}
