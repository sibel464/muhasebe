using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using SiparisVarligi = EtkiAlani.Modeller.Siparis.Siparis;
using KullaniciVarligi = EtkiAlani.Modeller.Kullanici.Kullanici;

namespace EtkiAlani.Modeller.Irsaliye;

public enum IrsaliyeTuru
{
    Satis, // Satış İrsaliyesi (Sevk İrsaliyesi - Stoğu Azaltır)
    Alis   // Alış İrsaliyesi (Stoğu Arttırır)
}

public enum IrsaliyeDurumu
{
    Taslak,      // Stok hareketi oluşturmaz, serbestçe düzenlenebilir
    Onaylandi,   // Stok hareketi oluşturur (depodan çıkar/girer), cari hareket henüz oluşmaz
    Faturalandi, // İrsaliye resmi faturaya dönüştürülmüştür, cari hareket faturada oluşur
    Iptal        // İptal edilmiştir, stok hareketleri geri alınır
}

public class Irsaliye : TemelVarlik
{
    public string IrsaliyeNo { get; set; } = string.Empty;
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public DateTime SevkTarihi { get; set; } = DateTime.UtcNow;
    public string SevkTarihiMetni => SevkTarihi == default ? "—" : SevkTarihi.ToString("dd.MM.yyyy HH:mm");
    public bool OnaylanabilirMi => Durum == IrsaliyeDurumu.Taslak;
    public bool FaturalanabilirMi => Durum == IrsaliyeDurumu.Onaylandi;
    public bool IptalEdilebilirMi => Durum is IrsaliyeDurumu.Taslak or IrsaliyeDurumu.Onaylandi;
    public string BelgeAkisOzeti => Durum switch
    {
        IrsaliyeDurumu.Taslak => "1/3 • Hazırlanıyor",
        IrsaliyeDurumu.Onaylandi => "2/3 • Sevk ve stok işlendi",
        IrsaliyeDurumu.Faturalandi => "3/3 • Fatura ve cari kayıt tamamlandı",
        _ => "İşlem iptal edildi"
    };

    public int? SiparisId { get; set; }
    public SiparisVarligi? Siparis { get; set; }

    public int CariKartId { get; set; }
    // Firma ayrımı için: kayıt hangi firmaya ait (yeni kayıtta carinin firmasından doldurulur).
    public Guid? SirketId { get; set; }
    public CariKart? CariKart { get; set; }

    public IrsaliyeTuru Tur { get; set; } = IrsaliyeTuru.Satis;
    public IrsaliyeDurumu Durum { get; set; } = IrsaliyeDurumu.Taslak;

    public string Aciklama { get; set; } = string.Empty;
    public string TeslimatAdSoyad { get; set; } = string.Empty;
    public string TeslimatTelefon { get; set; } = string.Empty;
    public string TeslimatAdres { get; set; } = string.Empty;
    public string TeslimatIl { get; set; } = string.Empty;
    public string TeslimatIlce { get; set; } = string.Empty;
    public string ReferansNo { get; set; } = string.Empty;
    public string Tasiyici { get; set; } = string.Empty;
    public string AracPlaka { get; set; } = string.Empty;
    public string SoforAdi { get; set; } = string.Empty;
    public string SoforTelefon { get; set; } = string.Empty;
    // e-İrsaliyede şoförün T.C. kimlik numarası zorunludur (taşıyıcı firma yoksa).
    public string SoforTckn { get; set; } = string.Empty;

    // e-İrsaliye. Belge numarası GİB biçimindedir (3 harf + yıl + 9 hane) ve ilk hazırlamada verilir; ETTN de değişmez.
    public Guid? Ettn { get; set; }
    public string? EBelgeNo { get; set; }
    public EtkiAlani.Modeller.Fatura.EBelgeDurumu EBelgeDurumu { get; set; } = EtkiAlani.Modeller.Fatura.EBelgeDurumu.Yok;
    public string? EBelgeMesaji { get; set; }

    // Finansal Toplamlar
    public decimal AraToplam { get; set; } = 0;
    public decimal KdvToplam { get; set; } = 0;
    public decimal GenelToplam { get; set; } = 0;

    // Faturaya dönüştürüldüğünde üretilen Fatura ID'si
    public int? DonusturulenFaturaId { get; set; }
    public Fatura.Fatura? DonusturulenFatura { get; set; }

    public int Versiyon { get; set; } = 1;
    public int? OlusturanKullaniciId { get; set; }
    public KullaniciVarligi? OlusturanKullanici { get; set; }
    public int? GuncelleyenKullaniciId { get; set; }
    public KullaniciVarligi? GuncelleyenKullanici { get; set; }

    public List<IrsaliyeKalemi> Kalemler { get; set; } = new();
}
