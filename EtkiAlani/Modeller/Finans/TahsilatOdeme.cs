namespace EtkiAlani.Modeller.Finans;

public enum FinansIslemTuru { Tahsilat, Odeme }
public enum OdemeYontemi { Nakit, Banka, KrediKarti, Cek, Senet, Havale, Virman }
public enum FinansIslemDurumu { Tamamlandi, IptalEdildi, TersKayit }

public sealed class TahsilatOdemeKaydi
{
    public string Kaynak { get; init; } = string.Empty;
    public int HareketId { get; init; }
    public DateTime Tarih { get; init; }
    public FinansIslemTuru IslemTuru { get; init; }
    public OdemeYontemi OdemeYontemi { get; init; }
    public FinansIslemDurumu Durum { get; init; }
    public int CariKartId { get; init; }
    public string CariKod { get; init; } = string.Empty;
    public string CariUnvan { get; init; } = string.Empty;
    public decimal Tutar { get; init; }
    public int? ParaBirimiId { get; init; }
    public decimal Kur { get; init; } = 1;
    public decimal DovizTutar { get; init; }
    public string Aciklama { get; init; } = string.Empty;
    public string? BelgeNo { get; init; }
    public string? BankaAdi { get; init; }
    public string? HesapNo { get; init; }
    public string? MakbuzNo { get; init; }
    // Kredi kartı
    public int? TaksitSayisi { get; init; }
    public decimal KomisyonOrani { get; init; }
    public decimal NetTutar { get; init; }
    // Çek / Senet
    public DateTime? VadeTarihi { get; init; }
    public string? CekSenetNo { get; init; }
    public string? Kesideci { get; init; }
    public int? TersKayitId { get; init; }
}

public sealed record TahsilatOdemeTalebi(
    FinansIslemTuru IslemTuru,
    OdemeYontemi OdemeYontemi,
    int CariKartId,
    decimal Tutar,
    DateTime Tarih,
    string Aciklama,
    int? ParaBirimiId = null,
    decimal Kur = 1,
    decimal DovizTutar = 0,
    string? BelgeNo = null,
    string? MakbuzNo = null,
    string? BankaAdi = null,
    string? HesapNo = null,
    int? KasaId = null,
    int? BankaHesabiId = null,
    // Kredi Kartı
    int? TaksitSayisi = null,
    decimal KomisyonOrani = 0,
    // Çek / Senet
    DateTime? VadeTarihi = null,
    string? CekSenetNo = null,
    string? Kesideci = null,
    // Bu tahsilat/ödemeyle kapatılacak açık belgelerin (fatura vb.) cari hareketleri; sırayla, tutar bitene kadar kapatılır.
    IReadOnlyList<int>? KapatilacakCariHareketIdleri = null);
