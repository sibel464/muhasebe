using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Kurumsal;

namespace EtkiAlani.Modeller.Finans;

public enum HareketTuru { Gelir, Gider }

public enum KasaFisTuru
{
    Tahsilat,        // Cari Tahsilat (Nakit Giriş)
    Odeme,           // Cari Ödeme (Nakit Çıkış)
    Gelir,           // Cari Harici Gelir
    Gider,           // Cari Harici Gider / Masraf
    KasaVirman,      // Kasalar Arası Transfer
    BankayaYatirma,  // Kasadan Bankaya Para Yatırma
    BankadanCekme,   // Bankadan Kasaya Para Çekme
    AcilisDevir,     // Kasa Açılış / Devir
    SayimFarki       // Kasa Mutabakat / Sayım Farkı
}

public class KasaHareketi : TemelVarlik
{
    public Guid? SirketId { get; set; }
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    
    public int? KasaId { get; set; }
    public Kasa? Kasa { get; set; }

    public HareketTuru Tur { get; set; }
    public KasaFisTuru FisTuru { get; set; } = KasaFisTuru.Tahsilat;
    public decimal Tutar { get; set; } = 0; // Yerel para birimi cinsinden tutar (Örn: TL)
    
    // Logo ERP Kurumsal Kodlar
    public string? FisNo { get; set; }
    public string? MakbuzNo { get; set; }
    public string? OzelKod { get; set; }
    public string? MasrafKodu { get; set; }

    // Çoklu Para Birimi (Döviz)
    public int? ParaBirimiId { get; set; }
    public ParaBirimi? ParaBirimi { get; set; }
    public decimal Kur { get; set; } = 1;
    public decimal DovizTutar { get; set; } = 0;

    public string Aciklama { get; set; } = string.Empty;
    public string? Belge { get; set; }

    // Entegrasyon alanları
    public int? CariKartId { get; set; }
    public CariKart? CariKart { get; set; }
    public int? FaturaId { get; set; }
    public int? IlgiliBelgeId { get; set; }
    public FinansIslemDurumu IslemDurumu { get; set; } = FinansIslemDurumu.Tamamlandi;
    public int? TersKayitId { get; set; }
}

