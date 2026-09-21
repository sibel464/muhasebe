using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Muhasebe;

public class YevmiyeSatiri : TemelVarlik
{
    public int FisId { get; set; }
    public YevmiyeFis? Fis { get; set; }
    public int HesapId { get; set; }
    public HesapPlani? Hesap { get; set; }
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public string BelgeNo { get; set; } = string.Empty;
    public string ParaBirimi { get; set; } = "TRY";
    public decimal Kur { get; set; } = 1.0m;
    public decimal DovizBorc { get; set; }
    public decimal DovizAlacak { get; set; }
    public int? CariKartId { get; set; }
}
