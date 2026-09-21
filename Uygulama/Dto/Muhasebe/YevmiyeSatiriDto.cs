namespace Uygulama.Dto.Muhasebe;

public class YevmiyeSatiriDto
{
    public int Id { get; set; }
    public int FisId { get; set; }
    public string FisNo { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
    public int HesapId { get; set; }
    public string HesapKodu { get; set; } = string.Empty;
    public string HesapAdi { get; set; } = string.Empty;
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public string BelgeNo { get; set; } = string.Empty;
    public string ParaBirimi { get; set; } = "TRY";
    public decimal Kur { get; set; } = 1.0m;
    public decimal DovizBorc { get; set; }
    public decimal DovizAlacak { get; set; }
    public int? CariKartId { get; set; }
    public string? CariUnvan { get; set; }
    public string FisTuru { get; set; } = string.Empty;
    public string KullaniciAdi { get; set; } = string.Empty;
}
