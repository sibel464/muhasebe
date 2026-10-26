using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Muhasebe;

public enum AmortismanYontemi
{
    Normal = 1,          // Eşit tutarlı (VUK 315)
    AzalanBakiyeler = 2  // Normal oranın iki katı, en çok %50 (VUK 315 mükerrer)
}

// Demirbaş / sabit kıymet kartı. Alış maliyeti faturayla 25x hesabına işlenir; kart amortisman takibini yapar.
public class SabitKiymet : TemelVarlik
{
    public Guid SirketId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string HesapKodu { get; set; } = "255";
    public string BirikmisAmortismanHesapKodu { get; set; } = "257";
    public string GiderHesapKodu { get; set; } = "770";
    public DateTime AlisTarihi { get; set; }
    public decimal Maliyet { get; set; }
    public int FaydaliOmurYil { get; set; } = 5;
    public AmortismanYontemi Yontem { get; set; } = AmortismanYontemi.Normal;
    // Binek otomobillerde ilk yıl ay kesri tam ay sayılarak kıst amortisman; kalanı ömrün sonundaki yıla kalır.
    public bool KistAmortisman { get; set; }

    // Program kullanılmadan önce ayrılmış amortisman: o yıla kadar (dahil) ayrılan toplam.
    public decimal DevirBirikmisAmortisman { get; set; }
    public int? DevirSonYil { get; set; }

    public string? SeriNo { get; set; }
    public string? Konum { get; set; }
    public string Aciklama { get; set; } = string.Empty;

    public DateTime? EldenCikarmaTarihi { get; set; }
    public int? EldenCikarmaFisId { get; set; }

    public List<AmortismanKaydi> Kayitlar { get; set; } = new();
}

public class AmortismanKaydi : TemelVarlik
{
    public int SabitKiymetId { get; set; }
    public SabitKiymet? SabitKiymet { get; set; }
    public int Yil { get; set; }
    public decimal Tutar { get; set; }
    public int YevmiyeFisId { get; set; }
}
