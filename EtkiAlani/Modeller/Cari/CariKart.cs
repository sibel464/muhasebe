using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Kurumsal;

namespace EtkiAlani.Modeller.Cari;

public enum CariTuru
{
    Musteri,
    Tedarikci,
    MusteriTedarikci
}

public class CariKart : TemelVarlik
{
    public Guid? SirketId { get; set; }
    public Sirket? Sirket { get; set; }
    public int? SubeId { get; set; }
    public Sube? Sube { get; set; }
    public string Kod { get; set; } = string.Empty;
    public CariTuru Tur { get; set; } = CariTuru.Musteri;
    public int CariTipiId { get; set; } = CariTipiTanimi.KurumsalId;
    public CariTipiTanimi CariTipi { get; set; } = null!;
    public string Unvan { get; set; } = string.Empty;
    public string? GrupKodu { get; set; } // Örn: ESNAF, TOPTANCI, BİREYSEL
    
    // Bireysel müşteriler için Ad-Soyad (Kurumsal için opsiyonel yetkili bilgisi olarak kullanılabilir)
    public string? Ad { get; set; }
    public string? Soyad { get; set; }

    public string? VergiDairesi { get; set; }
    public string? VergiNo { get; set; }
    public string? TcNo { get; set; }

    public string? Telefon { get; set; }
    public string? EvTelefonu { get; set; }
    public string? IsTelefonu { get; set; }
    public string? CepTelefonu { get; set; }
    public string? Eposta { get; set; }
    public string? WebSitesi { get; set; }

    public string? Adres { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public string? PostaKodu { get; set; }

    public string? FotografYolu { get; set; }
    
    // Kullanıcı Takibi
    public int? OlusturanKullaniciId { get; set; }
    public Kullanici.Kullanici? OlusturanKullanici { get; set; }
    public int? GuncelleyenKullaniciId { get; set; }
    public Kullanici.Kullanici? GuncelleyenKullanici { get; set; }

    // GİB e-Fatura sistemine kayıtlı alıcıya e-Fatura, diğerlerine e-Arşiv fatura kesilir.
    public bool EFaturaMukellefi { get; set; }
    public string? EFaturaEtiketi { get; set; } // Posta kutusu etiketi, ör. urn:mail:defaultpk@firma.com.tr

    public decimal? RiskLimiti { get; set; }
    public int? VadeGunu { get; set; }
    public string? Iban { get; set; }
    public string? Aciklama { get; set; }
    public bool Aktif { get; set; } = true;
    
    // Bakiye, Borç ve Alacak tutarları ticari sistemde her zaman hareketlerden hesaplanmalıdır.
    // Ancak performans için DB'de cached olarak durabilir. 
    // Şimdilik 0 olarak ilklendiriyoruz.
    public decimal Borc { get; set; } = 0;
    public decimal Alacak { get; set; } = 0;
    public decimal Bakiye => Borc - Alacak;
    
    // Concurrency token to prevent concurrent balance update conflicts
    public int Versiyon { get; set; } = 0;
}
