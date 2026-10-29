using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Muhasebe;

// Personel kartı. Bordro tutarları (kesintiler, işveren payları) mali müşavirin bordrosundan girilir; kart son
// tahakkukun tutarlarını saklar ki sonraki ay aynı değerlerle önerilsin.
public class Personel : TemelVarlik
{
    public Guid SirketId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string AdSoyad { get; set; } = string.Empty;
    public string? TcKimlikNo { get; set; }
    public string? Gorev { get; set; }
    public string? Departman { get; set; }
    public DateTime IseGirisTarihi { get; set; }
    public DateTime? IstenCikisTarihi { get; set; }
    public string? Telefon { get; set; }
    public string? Eposta { get; set; }
    public string? Iban { get; set; }
    // Ücret gideri: 770 genel yönetim, 760 pazarlama, 740 hizmet üretim, 720 direkt işçilik, 730 genel üretim.
    public string GiderHesapKodu { get; set; } = "770";

    public decimal NetUcret { get; set; }
    public decimal SgkIsciPayi { get; set; }
    public decimal IssizlikIsciPayi { get; set; }
    public decimal GelirVergisi { get; set; }
    public decimal DamgaVergisi { get; set; }
    public decimal SgkIsverenPayi { get; set; }
    public decimal IssizlikIsverenPayi { get; set; }

    public string Aciklama { get; set; } = string.Empty;
}

public enum PersonelHareketTuru
{
    MaasTahakkuku = 1,
    MaasOdemesi = 2,
    Avans = 3
}

// Personelin alt hesabı: tahakkuk 335'i alacaklandırır, ödeme borçlandırır; avans 196'da izlenir ve tahakkukta mahsup edilir.
public class PersonelHareketi : TemelVarlik
{
    public Guid SirketId { get; set; }
    public int PersonelId { get; set; }
    public Personel? Personel { get; set; }
    public PersonelHareketTuru Tur { get; set; }
    public DateTime Tarih { get; set; }
    // Tahakkukta bordro dönemi (yyyyMM); ödeme ve avansta 0.
    public int Donem { get; set; }

    public decimal NetUcret { get; set; }
    public decimal SgkIsciPayi { get; set; }
    public decimal IssizlikIsciPayi { get; set; }
    public decimal GelirVergisi { get; set; }
    public decimal DamgaVergisi { get; set; }
    public decimal SgkIsverenPayi { get; set; }
    public decimal IssizlikIsverenPayi { get; set; }
    public decimal AvansKesintisi { get; set; }

    // Ödeme ve avansta ödenen tutar.
    public decimal Tutar { get; set; }

    public int? KasaHareketiId { get; set; }
    public int? BankaHareketiId { get; set; }
    public int? YevmiyeFisId { get; set; }
    public string Aciklama { get; set; } = string.Empty;

    public decimal BrutUcret => NetUcret + SgkIsciPayi + IssizlikIsciPayi + GelirVergisi + DamgaVergisi;
    public decimal IsverenMaliyeti => BrutUcret + SgkIsverenPayi + IssizlikIsverenPayi;
    // Personele ödenecek: net ücretten avans düşülür.
    public decimal OdenecekNet => NetUcret - AvansKesintisi;
}
