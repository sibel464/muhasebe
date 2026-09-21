using EtkiAlani.Modeller.Cari;
using KullaniciVarligi = EtkiAlani.Modeller.Kullanici.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Siparis;

public enum SiparisTuru { Satis, Alis }
public enum SiparisDurumu { Taslak, Beklemede, Onaylandi, KismenKarsilandi, Tamamlandi, Iptal }
public enum SiparisOdemeSekli { Belirtilmedi, Nakit, KrediKarti, Banka, AcikHesap }

public class Siparis : TemelVarlik
{
    public string SiparisNo { get; set; } = string.Empty;
    public SiparisTuru SiparisTuru { get; set; }
    public int CariKartId { get; set; }
    public CariKart? CariKart { get; set; }
    public DateTime SiparisTarihi { get; set; } = DateTime.UtcNow;
    public DateTime? TeslimTarihi { get; set; }
    public SiparisDurumu Durum { get; set; } = SiparisDurumu.Taslak;

    public string TeslimatAdSoyad { get; set; } = string.Empty;
    public string TeslimatTelefon { get; set; } = string.Empty;
    public string TeslimatAdres { get; set; } = string.Empty;
    public string TeslimatIl { get; set; } = string.Empty;
    public string TeslimatIlce { get; set; } = string.Empty;

    public int ParaBirimiId { get; set; }
    public ParaBirimi? ParaBirimi { get; set; }
    public decimal Kur { get; set; } = 1m;
    public DateTime? VadeTarihi { get; set; }
    public SiparisOdemeSekli OdemeSekli { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public string ReferansNo { get; set; } = string.Empty;

    public decimal BrutToplam { get; set; }
    public decimal IskontoToplami { get; set; }
    public decimal AraToplam { get; set; }
    public decimal KdvToplami { get; set; }
    public decimal GenelToplam { get; set; }

    public int OlusturanKullaniciId { get; set; }
    public KullaniciVarligi? OlusturanKullanici { get; set; }
    public int? GuncelleyenKullaniciId { get; set; }
    public KullaniciVarligi? GuncelleyenKullanici { get; set; }
    public int Versiyon { get; set; }
    public List<SiparisKalemi> Kalemler { get; set; } = new();
}
