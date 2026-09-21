using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Kurumsal;

namespace EtkiAlani.Modeller.Stok;

public enum StokHareketTuru
{
    Giris,  // Stok Giriş Hareketi (Stok Miktarını Arttırır)
    Cikis   // Stok Çıkış Hareketi (Stok Miktarını Azaltır)
}

public enum StokIslemTuru
{
    AlisFaturasi = 1,
    SatisFaturasi = 2,
    AlisIrsaliyesi = 3,
    SatisIrsaliyesi = 4,
    DepoTransferi = 5,
    SayimFarki = 6,
    SarfFire = 7,
    DevirGiris = 8,
    Diger = 9
}

public class StokHareketi : TemelVarlik
{
    public int UrunId { get; set; }
    public Urun? Urun { get; set; }

    public int? DepoId { get; set; }
    public Depo? Depo { get; set; }

    public int? HedefDepoId { get; set; }
    public Depo? HedefDepo { get; set; }

    public int? CariKartId { get; set; }
    public CariKart? CariKart { get; set; }

    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public StokHareketTuru HareketTuru { get; set; }
    public StokIslemTuru IslemTuru { get; set; } = StokIslemTuru.Diger;

    public string? FisNo { get; set; }
    public string? BelgeNo { get; set; }
    public string Aciklama { get; set; } = string.Empty;

    public decimal Miktar { get; set; } = 0;       // Her zaman pozitif tutulmalı, HareketTuru yön belirler
    public decimal BirimFiyat { get; set; } = 0;   // Hareket anındaki birim fiyat
    public decimal ToplamTutar => Miktar * BirimFiyat;

    public Guid? SirketId { get; set; }
    public Sirket? Sirket { get; set; }

    public int? KullaniciId { get; set; }
    public int? TersKayitId { get; set; }
    public bool IptalEdildi { get; set; } = false;

    // Fatura / İrsaliye / Transfer Fişi ID'si gibi ilişkili belge ID
    public int? IlgiliBelgeId { get; set; }
}

