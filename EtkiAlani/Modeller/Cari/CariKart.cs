using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Cari;

public enum CariTuru
{
    Musteri,
    Tedarikci,
    MusteriTedarikci
}

public enum CariTipi
{
    Bireysel,
    Kurumsal
}

public class CariKart : TemelVarlik
{
    public string Kod { get; set; } = string.Empty;
    public CariTuru Tur { get; set; } = CariTuru.Musteri;
    public CariTipi Tip { get; set; } = CariTipi.Kurumsal;
    public string Unvan { get; set; } = string.Empty;
    
    // Bireysel müşteriler için Ad-Soyad (Kurumsal için opsiyonel yetkili bilgisi olarak kullanılabilir)
    public string? Ad { get; set; }
    public string? Soyad { get; set; }

    public string? VergiDairesi { get; set; }
    public string? VergiNo { get; set; }
    public string? TcNo { get; set; }

    public string? Telefon { get; set; }
    public string? CepTelefonu { get; set; }
    public string? Eposta { get; set; }
    public string? WebSitesi { get; set; }

    public string? Adres { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public string? PostaKodu { get; set; }

    public string? Aciklama { get; set; }
    public bool Aktif { get; set; } = true;
    
    // Bakiye, Borç ve Alacak tutarları ticari sistemde her zaman hareketlerden hesaplanmalıdır.
    // Ancak performans için DB'de cached olarak durabilir. 
    // Şimdilik 0 olarak ilklendiriyoruz.
    public decimal Borc { get; set; } = 0;
    public decimal Alacak { get; set; } = 0;
    public decimal Bakiye => Borc - Alacak;
}
