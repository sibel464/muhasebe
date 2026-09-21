namespace Uygulama.Dto.Muhasebe;

public class BuyukDefterSatirDto
{
    public int HesapId { get; set; }
    public string HesapKodu { get; set; } = string.Empty;
    public string HesapAdi { get; set; } = string.Empty;
    public decimal DevirBorc { get; set; }
    public decimal DevirAlacak { get; set; }
    public decimal DonemBorc { get; set; }
    public decimal DonemAlacak { get; set; }
    public decimal ToplamBorc => DevirBorc + DonemBorc;
    public decimal ToplamAlacak => DevirAlacak + DonemAlacak;
    public decimal Bakiye => Math.Abs(ToplamBorc - ToplamAlacak);
    public string BakiyeYonu => ToplamBorc >= ToplamAlacak ? "Borç" : "Alacak";
    public int HareketSayisi { get; set; }
}

public class BuyukDefterDto : BuyukDefterSatirDto { }
