namespace Uygulama.Dto.Muhasebe;

public class SabitKiymetListeDto
{
    public int Id { get; set; }
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public string HesapKodu { get; set; } = "";
    public DateTime AlisTarihi { get; set; }
    public decimal Maliyet { get; set; }
    public int FaydaliOmurYil { get; set; }
    public string Yontem { get; set; } = "";
    public decimal Birikmis { get; set; }
    public decimal NetDeger => Maliyet - Birikmis;
    public decimal BuYilAyrilacak { get; set; }
    public string Durum { get; set; } = "";
    public string? Konum { get; set; }
    public string? SeriNo { get; set; }
    public string Ozet => $"Hesap {HesapKodu} · {FaydaliOmurYil} yıl · {Yontem}";
}

public class AmortismanKalemiDto
{
    public int SabitKiymetId { get; set; }
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public string GiderHesabi { get; set; } = "";
    public string BirikmisHesabi { get; set; } = "";
    public decimal Tutar { get; set; }
}

public class AmortismanYilOnizlemeDto
{
    public int Yil { get; set; }
    public List<AmortismanKalemiDto> Kalemler { get; set; } = new();
    public decimal Toplam => Kalemler.Sum(k => k.Tutar);
    public decimal AyrilmisToplam { get; set; }
    public List<string> Engeller { get; set; } = new();
    public bool AyrilabilirMi => Engeller.Count == 0 && Kalemler.Count > 0;
}
