using EtkiAlani.Modeller.Muhasebe;

namespace Uygulama.Dto.Muhasebe;

public class HesapPlaniDto
{
    public int Id { get; set; }
    public Guid SirketId { get; set; }
    public string HesapKodu { get; set; } = string.Empty;
    public string HesapAdi { get; set; } = string.Empty;
    public HesapTipi Tip { get; set; } = HesapTipi.Aktif;
    public int Seviye { get; set; } = 1;
    public BorcAlacakYonu BakiyeYonu { get; set; } = BorcAlacakYonu.Borc;
    public string ParaBirimiKodu { get; set; } = "TRY";
    public bool SistemHesabi { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public int? UstHesapId { get; set; }
    public string? UstHesapKodu { get; set; }
    public string? UstHesapAdi { get; set; }
    public bool AktifMi { get; set; } = true;
    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public decimal Bakiye => Math.Abs(ToplamBorc - ToplamAlacak);
    public string BakiyeYonuStr => ToplamBorc >= ToplamAlacak ? "Borç" : "Alacak";
    public bool HareketGordu => ToplamBorc > 0 || ToplamAlacak > 0;
    public string DurumStr => AktifMi ? "Aktif" : "Pasif";
    public List<HesapPlaniDto> AltHesaplar { get; set; } = new();
}
