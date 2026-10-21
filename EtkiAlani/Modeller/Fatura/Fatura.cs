using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Kurumsal;
using SiparisVarligi = EtkiAlani.Modeller.Siparis.Siparis;
using IrsaliyeVarligi = EtkiAlani.Modeller.Irsaliye.Irsaliye;

namespace EtkiAlani.Modeller.Fatura;

public enum FaturaTuru
{
    Satis,       // Satış Faturası (Cari Borçlandırılır, Stoktan Düşülür)
    Alis,        // Alış Faturası (Cari Alacaklandırılır, Stoğa Girilir)
    SatisIade,   // Satış İade Faturası (Cari Alacaklandırılır, Stoğa Girilir)
    AlisIade     // Alış İade Faturası (Cari Borçlandırılır, Stoktan Düşülür)
}

public enum BelgeDurumu
{
    Taslak,      // Cari ve Stok hareketi oluşturmaz, serbestçe düzenlenebilir
    Beklemede,   // Askıya alınmış satış (Satışı Beklet), işlem yapılmaz
    Onaylandi,   // Cari ve Stok hareketi oluşturur, kilitlidir
    Iptal        // Entegrasyon hareketleri soft-delete edilir, kilitlidir
}
public enum FaturaKaynakTuru { Manuel, Pos, Siparisten, Irsaliyeden }
public enum FaturaOdemeSekli { AcikHesap, Nakit, KrediKarti, Banka }

public class Fatura : TemelVarlik
{
    public FaturaKaynakTuru KaynakTuru { get; set; } = FaturaKaynakTuru.Manuel;
    public FaturaOdemeSekli OdemeSekli { get; set; } = FaturaOdemeSekli.AcikHesap;
    public int? SiparisId { get; set; }
    public SiparisVarligi? Siparis { get; set; }
    public int? IrsaliyeId { get; set; }
    public IrsaliyeVarligi? Irsaliye { get; set; }
    public DateTime? VadeTarihi { get; set; }
    public Guid? SirketId { get; set; }
    public Sirket? Sirket { get; set; }
    public int? SubeId { get; set; }
    public Sube? Sube { get; set; }
    public int? MaliDonemId { get; set; }
    public MaliDonem? MaliDonem { get; set; }
    public int? ParaBirimiId { get; set; }
    public ParaBirimi? ParaBirimi { get; set; }
    public string FaturaNo { get; set; } = string.Empty; // Sıralı, benzersiz ticari fatura numarası
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    
    public int CariKartId { get; set; }
    public CariKart? CariKart { get; set; }
    
    public FaturaTuru Tur { get; set; }
    public BelgeDurumu Durum { get; set; } = BelgeDurumu.Taslak;

    // Finansal Toplamlar
    public decimal AraToplam { get; set; } = 0;              // Kalemlerin Brüt Toplamı (Miktar * Fiyat)
    public decimal SatirIskontosuToplam { get; set; } = 0;    // Kalemlerdeki satır iskontoları toplamı
    public decimal GenelIskontoOrani { get; set; } = 0;      // Fatura altı iskonto yüzdesi (%)
    public decimal GenelIskontoTutari { get; set; } = 0;     // Hesaplanan fatura altı iskonto tutarı
    public decimal KdvToplam { get; set; } = 0;               // Toplam KDV tutarı
    public decimal YuvarlamaTutari { get; set; } = 0;         // Kuruş yuvarlamaları
    public decimal GenelToplam { get; set; } = 0;             // Cari karta yansıyacak nihai toplam tutar

    public string Aciklama { get; set; } = string.Empty;
    public int Versiyon { get; set; } = 0;                   // Concurrency Token

    // e-Fatura / e-Arşiv. ETTN belge ilk hazırlandığında verilir ve bir daha değişmez.
    public Guid? Ettn { get; set; }
    public EBelgeTuru EBelgeTuru { get; set; } = EBelgeTuru.Yok;
    public EFaturaSenaryosu EFaturaSenaryosu { get; set; } = EFaturaSenaryosu.Temel;
    public EBelgeDurumu EBelgeDurumu { get; set; } = EBelgeDurumu.Yok;
    public DateTime? EBelgeTarihi { get; set; }
    public string? EBelgeMesaji { get; set; }

    // İade faturasında iade edilen (karşı tarafın) faturanın numarası ve tarihi; e-belgede zorunludur.
    public string? IadeEdilenFaturaNo { get; set; }
    public DateTime? IadeEdilenFaturaTarihi { get; set; }

    public List<FaturaKalemi> Kalemler { get; set; } = new();
}
