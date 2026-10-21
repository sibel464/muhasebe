using EtkiAlani.Modeller.Muhasebe;

namespace Uygulama.Dto.Muhasebe;

public class Hesap360Dto
{
    public HesapPlaniDto Hesap { get; set; } = new();

    // Summary Top Bar
    public decimal DevirBorc { get; set; }
    public decimal DevirAlacak { get; set; }
    public decimal DonemBorc { get; set; }
    public decimal DonemAlacak { get; set; }
    public decimal ToplamBorc => DevirBorc + DonemBorc;
    public decimal ToplamAlacak => DevirAlacak + DonemAlacak;
    public decimal NetBakiye => Math.Abs(ToplamBorc - ToplamAlacak);
    public decimal DevirNetBakiye => Math.Abs(DevirBorc - DevirAlacak);
    public string BakiyeYonuStr => ToplamBorc >= ToplamAlacak ? "Borç" : "Alacak";
    public DateTime? SonHareketTarihi { get; set; }

    // Tab Data
    public List<YevmiyeSatiriDto> Hareketler { get; set; } = new();
    public List<YevmiyeFisDto> YevmiyeFisleri { get; set; } = new();
    public List<HesapAylikOzetDto> AylikOzet { get; set; } = new();
    public MuavinRaporDto Muavin { get; set; } = new();
    public List<BagliBelgeDto> BagliBelgeler { get; set; } = new();
    public List<HesapAuditDto> AuditLogs { get; set; } = new();
}

public class HesapAylikOzetDto
{
    public int Yil { get; set; }
    public int Ay { get; set; }
    public string AyAdi { get; set; } = string.Empty;
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
    public decimal NetBakiye => Math.Abs(Borc - Alacak);
    public string BakiyeYonu => Borc >= Alacak ? "Borç" : "Alacak";
}

public class BagliBelgeDto
{
    public string BelgeTipi { get; set; } = string.Empty;
    public int BelgeId { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
}

public class HesapAuditDto
{
    public DateTime Tarih { get; set; }
    public string KullaniciAdi { get; set; } = string.Empty;
    public string IslemTuru { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
}
