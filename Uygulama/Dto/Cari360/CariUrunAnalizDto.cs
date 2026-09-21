using System;

namespace Uygulama.Dto.Cari360;

public class CariUrunAnalizDto
{
    public decimal SonFiyat { get; set; }
    public decimal OrtalamaFiyat { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string UrunKodu { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    
    // Alınanlar (Tedarik)
    public decimal AlinanMiktar { get; set; }
    public decimal AlinanTutar { get; set; }
    
    // Satılanlar (Müşteriye)
    public decimal SatilanMiktar { get; set; }
    public decimal SatilanTutar { get; set; }
    
    // İadeler
    public decimal IadeEdilenMiktar { get; set; } // Bizim iade ettiğimiz (Alış İade)
    public decimal IadeEdilenTutar { get; set; }
    
    public decimal IadeAlinanMiktar { get; set; } // Müşterinin iade ettiği (Satış İade)
    public decimal IadeAlinanTutar { get; set; }

    public DateTime? SonIslemTarihi { get; set; }
}
