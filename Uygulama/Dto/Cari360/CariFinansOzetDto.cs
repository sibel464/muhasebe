using System;

namespace Uygulama.Dto.Cari360;

public class CariFinansOzetDto
{
    public decimal MutlakBakiye => Math.Abs(GuncelBakiye);
    public decimal GuncelBakiye { get; set; }
    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public decimal? VadesiGecmis { get; set; }
    public int? AcikFaturaSayisi { get; set; }
    public DateTime? SonIslemTarihi { get; set; }
    
    // Risk & Vade Özeti için eklenebilir alanlar
    public decimal? RiskLimiti { get; set; }
    public decimal KullanilanRisk { get; set; }
    public decimal? KalanRisk => RiskLimiti - KullanilanRisk;
    
    // Yön belirteci (Borçlu / Alacaklı)
    public string BakiyeYonu => GuncelBakiye > 0 ? "BORÇLU" : (GuncelBakiye < 0 ? "ALACAKLI" : "SIFIR");
}
