using EtkiAlani.Modeller.Muhasebe;

namespace Uygulama.Dto.Muhasebe;

public class YevmiyeFiltreDto
{
    public Guid SirketId { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public string? FisNo { get; set; }
    public FisTuru? FisTuru { get; set; }
    public YevmiyeDurumu? Durum { get; set; }
    public string? HesapKodu { get; set; }
    public string? BelgeTipi { get; set; }
    public string? AramaMetni { get; set; }
    public int Sayfa { get; set; } = 1;
    public int SayfaBoyutu { get; set; } = 50;
}
