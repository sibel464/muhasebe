using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Muhasebe;

public class HesapPlani : TemelVarlik
{
    public Guid SirketId { get; set; }
    public Sirket? Sirket { get; set; }
    public string HesapKodu { get; set; } = string.Empty;
    public string HesapAdi { get; set; } = string.Empty;
    public HesapTipi Tip { get; set; } = HesapTipi.Aktif;
    public int Seviye { get; set; } = 1;
    public BorcAlacakYonu BakiyeYonu { get; set; } = BorcAlacakYonu.Borc;
    public string ParaBirimiKodu { get; set; } = "TRY";
    public bool SistemHesabi { get; set; } = false;
    public string Aciklama { get; set; } = string.Empty;
    public int? UstHesapId { get; set; }
    public HesapPlani? UstHesap { get; set; }
    public bool AktifMi { get; set; } = true;
    public List<HesapPlani> AltHesaplar { get; set; } = new();
}
