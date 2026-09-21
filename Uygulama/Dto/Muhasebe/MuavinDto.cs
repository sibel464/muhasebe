namespace Uygulama.Dto.Muhasebe;

public class MuavinSatirDto
{
    public DateTime Tarih { get; set; }
    public int FisId { get; set; }
    public string FisNo { get; set; } = string.Empty;
    public string BelgeTipi { get; set; } = string.Empty;
    public int BelgeId { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
    public decimal YuruyenBakiye { get; set; }
    public string BakiyeYonu { get; set; } = "B"; // "B" or "A"
}

public class MuavinRaporDto
{
    public int HesapId { get; set; }
    public string HesapKodu { get; set; } = string.Empty;
    public string HesapAdi { get; set; } = string.Empty;
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }
    public decimal DevirBorc { get; set; }
    public decimal DevirAlacak { get; set; }
    public decimal DevirBakiye => DevirBorc - DevirAlacak;
    public string DevirBakiyeYonu => DevirBorc >= DevirAlacak ? "Borç" : "Alacak";
    public List<MuavinSatirDto> Satirlar { get; set; } = new();
    public decimal DonemBorc => Satirlar.Sum(s => s.Borc);
    public decimal DonemAlacak => Satirlar.Sum(s => s.Alacak);
    public decimal SonBakiye => DevirBakiye + (DonemBorc - DonemAlacak);
    public string SonBakiyeYonu => SonBakiye >= 0 ? "Borç" : "Alacak";
}

public class MuavinDto : MuavinRaporDto { }
