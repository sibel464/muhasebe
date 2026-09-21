namespace Uygulama.Dto.Depo;

public class DepoStokMatrisDto
{
    public int UrunId { get; set; }
    public string UrunKodu { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public string? Barkod { get; set; }
    public string Birim { get; set; } = "Adet";

    public decimal MagazaMiktar { get; set; }
    public decimal Depo1Miktar { get; set; }
    public decimal Depo2Miktar { get; set; }
    public decimal DigerDepolarMiktar { get; set; }
    public decimal ToplamMiktar => MagazaMiktar + Depo1Miktar + Depo2Miktar + DigerDepolarMiktar;

    public decimal MinStok { get; set; } = 5;
    public decimal AlisFiyati { get; set; }
    public decimal SatisFiyati { get; set; }
    public decimal ToplamDeger => ToplamMiktar * AlisFiyati;

    public bool KritikStoktaMi => ToplamMiktar <= MinStok && ToplamMiktar > 0;
    public bool StokTukendiMi => ToplamMiktar <= 0;
    public string DurumMetni => StokTukendiMi ? "Tükendi" : (KritikStoktaMi ? "Kritik Seviye" : "Yeterli");
}
