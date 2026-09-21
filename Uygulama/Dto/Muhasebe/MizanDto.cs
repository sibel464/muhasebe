namespace Uygulama.Dto.Muhasebe;

public enum MizanTuru
{
    Genel = 1,
    Aylik = 2,
    Devirli = 3
}

public class MizanSatirDto
{
    public int HesapId { get; set; }
    public string HesapKodu { get; set; } = string.Empty;
    public string HesapAdi { get; set; } = string.Empty;
    public int Seviye { get; set; }
    public decimal DevirBorc { get; set; }
    public decimal DevirAlacak { get; set; }
    public decimal DonemBorc { get; set; }
    public decimal DonemAlacak { get; set; }
    public decimal ToplamBorc => DevirBorc + DonemBorc;
    public decimal ToplamAlacak => DevirAlacak + DonemAlacak;
    public decimal BorcBakiye => ToplamBorc > ToplamAlacak ? ToplamBorc - ToplamAlacak : 0m;
    public decimal AlacakBakiye => ToplamAlacak > ToplamBorc ? ToplamAlacak - ToplamBorc : 0m;
}

public class MizanRaporDto
{
    public MizanTuru Tur { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public List<MizanSatirDto> Satirlar { get; set; } = new();
    public decimal ToplamBorc => Satirlar.Sum(s => s.ToplamBorc);
    public decimal ToplamAlacak => Satirlar.Sum(s => s.ToplamAlacak);
    public decimal ToplamBorcBakiye => Satirlar.Sum(s => s.BorcBakiye);
    public decimal ToplamAlacakBakiye => Satirlar.Sum(s => s.AlacakBakiye);
    public bool DengedeMi => Math.Abs(ToplamBorc - ToplamAlacak) < 0.001m && Math.Abs(ToplamBorcBakiye - ToplamAlacakBakiye) < 0.001m;
}

public class MizanDto : MizanRaporDto { }
