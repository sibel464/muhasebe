using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Tanimlar;

namespace EtkiAlani.Modeller.Stok;

public class Urun : TemelVarlik
{
    public Guid? SirketId { get; set; }
    public Sirket? Sirket { get; set; }
    public int? DepoId { get; set; }
    public Depo? Depo { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string? Barkod { get; set; }
    public string? Kategori { get; set; }
    public string Birim { get; set; } = "Adet";
    public int? KategoriTanimiId { get; set; }
    public MerkeziTanim? KategoriTanimi { get; set; }
    public int? MarkaTanimiId { get; set; }
    public MerkeziTanim? MarkaTanimi { get; set; }
    public int? BirimTanimiId { get; set; }
    public MerkeziTanim? BirimTanimi { get; set; }
    public decimal AlisFiyati { get; set; } = 0;
    
    // Satış Fiyatları (Toptan ve Perakende ayrımı POS için harika olacak)
    public decimal PerakendeSatisFiyati { get; set; } = 0;
    public decimal ToptanSatisFiyati { get; set; } = 0;
    
    public decimal KdvOrani { get; set; } = 20;
    public string? ResimYolu { get; set; } // Ürün görselinin diskteki yolu
    public string? Aciklama { get; set; }  // Uzun açıklama metni
    public decimal StokMiktari { get; set; } = 0;
    public decimal MinimumStok { get; set; } = 0;
    public bool Aktif { get; set; } = true;
    public int Versiyon { get; set; } = 1;

    public ICollection<UrunSatisFiyati> SatisFiyatlari { get; set; } = new List<UrunSatisFiyati>();
    public ICollection<UrunLokasyonStogu> LokasyonStoklari { get; set; } = new List<UrunLokasyonStogu>();
}
