namespace Uygulama.Dto.Muhasebe;

public class KdvMahsupSatiriDto
{
    public string HesapKodu { get; set; } = "";
    public string HesapAdi { get; set; } = "";
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
}

// Ay sonu KDV mahsubu: 391 (hesaplanan) ve 191 (indirilecek) kapatılır; fark 360'a (ödenecek) ya da 190'a (devreden) aktarılır.
public class KdvMahsupOnizlemeDto
{
    public int Yil { get; set; }
    public int Ay { get; set; }
    public DateTime FisTarihi { get; set; }
    public bool Yapildi { get; set; }
    public string? FisNo { get; set; }
    public decimal HesaplananKdv { get; set; }
    public decimal IndirilecekKdv { get; set; }
    public decimal OncekiDevreden { get; set; }
    public decimal KullanilanDevreden { get; set; }
    public decimal OdenecekKdv { get; set; }
    public decimal SonrakiDonemeDevreden { get; set; }
    public List<KdvMahsupSatiriDto> Satirlar { get; set; } = new();
    public List<string> Engeller { get; set; } = new();
    public List<string> Uyarilar { get; set; } = new();
    public bool MahsupEdilebilirMi => !Yapildi && Engeller.Count == 0;
}

public class KdvMahsupAyDto
{
    public int Ay { get; set; }
    public string AyAdi { get; set; } = "";
    public bool Yapildi { get; set; }
    public string? FisNo { get; set; }
    public decimal OdenecekKdv { get; set; }
    public decimal SonrakiDonemeDevreden { get; set; }
    public string Durum => Yapildi ? "Yapıldı" : "Yapılmadı";
}
