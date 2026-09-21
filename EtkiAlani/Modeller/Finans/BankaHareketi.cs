using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Kurumsal;

namespace EtkiAlani.Modeller.Finans;

public enum BankaFisTuru
{
    GelenHavale,       // Cari Tahsilat (Gelen Havale/EFT)
    GidenHavale,       // Cari Ödeme (Giden Havale/EFT)
    BankaGelir,        // Faiz / Mevduat Getirisi / Kur Farkı
    BankaGiderMasraf,  // Banka Hesap İşletim / EFT Masrafı
    BankaVirman,       // Bankalar Arası Transfer
    KasadanCekme,      // Bankadan Kasaya Para Çekme
    BankayaYatirma,    // Kasadan Bankaya Para Yatırma
    PosHesabaGecis,    // POS Bloke Çözümü & Vadesiz Hesaba Aktarım
    KrediOdemesi       // Banka Kredi Taksit Ödemesi
}

public class BankaHareketi : TemelVarlik
{
    public Guid? SirketId { get; set; }
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    
    public int? BankaHesabiId { get; set; }
    public BankaHesabi? BankaHesabi { get; set; }

    public HareketTuru Tur { get; set; }
    public BankaFisTuru FisTuru { get; set; } = BankaFisTuru.GelenHavale;
    public decimal Tutar { get; set; } = 0; // Yerel para birimi cinsinden tutar (Örn: TL)

    // Logo ERP Kurumsal & POS/Valör Kodları
    public string? FisNo { get; set; }
    public string? DekontNo { get; set; }
    public string? OzelKod { get; set; }
    public decimal KomisyonTutari { get; set; } = 0;
    public decimal MasrafTutari { get; set; } = 0;
    public DateTime? ValorTarihi { get; set; }

    // Çoklu Para Birimi (Döviz)
    public int? ParaBirimiId { get; set; }
    public ParaBirimi? ParaBirimi { get; set; }
    public decimal Kur { get; set; } = 1;
    public decimal DovizTutar { get; set; } = 0;

    public string Aciklama { get; set; } = string.Empty;
    public string? HesapNo { get; set; }
    public string? BankaAdi { get; set; }

    // Entegrasyon alanları
    public int? CariKartId { get; set; }
    public CariKart? CariKart { get; set; }
    public int? FaturaId { get; set; }
    public int? IlgiliBelgeId { get; set; }
    public OdemeYontemi OdemeYontemi { get; set; } = OdemeYontemi.Banka;
    public FinansIslemDurumu IslemDurumu { get; set; } = FinansIslemDurumu.Tamamlandi;
    public int? TersKayitId { get; set; }
}

