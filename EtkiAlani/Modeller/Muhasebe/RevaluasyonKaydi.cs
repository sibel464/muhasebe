using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Muhasebe;

public enum RevaluasyonDurumu
{
    Onaylandi = 1,
    Iptal = 2
}

public class RevaluasyonKaydi : TemelVarlik
{
    public Guid SirketId { get; set; }
    public int MaliDonemId { get; set; }
    public DateTime DegerlemeTarihi { get; set; }
    public string ParaBirimiKodu { get; set; } = string.Empty;
    public decimal Kur { get; set; }
    public decimal ToplamEskiYerel { get; set; }
    public decimal ToplamYeniYerel { get; set; }
    public decimal ToplamKurFarki { get; set; }
    public int? YevmiyeFisId { get; set; }
    public YevmiyeFis? YevmiyeFis { get; set; }
    public RevaluasyonDurumu Durum { get; set; } = RevaluasyonDurumu.Onaylandi;
    public int? IptalEdilenId { get; set; }
    public int KullaniciId { get; set; }
    public string OlusturanKullaniciAdi { get; set; } = string.Empty;

    public string Aciklama { get; set; } = string.Empty;

    public List<RevaluasyonKalemi> Kalemler { get; set; } = new();
}

public class RevaluasyonKalemi : TemelVarlik
{
    public int RevaluasyonKaydiId { get; set; }
    public RevaluasyonKaydi? RevaluasyonKaydi { get; set; }

    public string KaynakTuru { get; set; } = string.Empty; // "Cari", "Banka", "Kasa", "CekSenet"
    public int KaynakId { get; set; }
    public string KaynakKodu { get; set; } = string.Empty;
    public string KaynakAciklama { get; set; } = string.Empty;

    public int? HesapId { get; set; }
    public string HesapKodu { get; set; } = string.Empty;

    public decimal DovizBakiye { get; set; }
    public decimal EskiDefterDegeri { get; set; }
    public decimal OncekiDegerlemeKuru { get; set; }
    public decimal YeniDegerlemeKuru { get; set; }
    public decimal YeniYerelDeger { get; set; }
    public decimal KurFarki { get; set; }
}
