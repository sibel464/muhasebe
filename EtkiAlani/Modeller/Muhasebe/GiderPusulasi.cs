using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Muhasebe;

public enum GiderPusulasiTuru
{
    // VUK 234: vergi mükellefi olmayan kişilerden (esnaf muaflığındakiler, bireyler) alınan mal ve hizmetler.
    GiderPusulasi = 1,
    // VUK 235: çiftçiden alınan zirai ürünler.
    MustahsilMakbuzu = 2,
    // GVK 94/5-a: gerçek kişiden işyeri kirası; belgeyi kiracı düzenler (kira ödeme belgesi), stopaj kesilir.
    KiraOdemesi = 3,
    // GVK 94/2: serbest meslek makbuzu (avukat, müşavir, doktor...). Makbuzu satıcı keser; KDV'si indirilir, stopaj kesilir.
    SerbestMeslekMakbuzu = 4
}

// Gider pusulası / müstahsil makbuzu. Kayıt anında tek fiş: gider hesabı borç (brüt) / 360 stopaj, 336 net alacak;
// ardından kasa/banka ödemesi 336'yı kapatır. İptal ters fişle yapılır, kayıt silinmez.
public class GiderPusulasi : TemelVarlik
{
    public Guid SirketId { get; set; }
    public GiderPusulasiTuru Tur { get; set; } = GiderPusulasiTuru.GiderPusulasi;
    public string BelgeNo { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }

    public string SaticiAdSoyad { get; set; } = string.Empty;
    public string? SaticiTcKimlikNo { get; set; }
    public string? SaticiAdres { get; set; }

    public string GiderHesapKodu { get; set; } = "770";
    public decimal BrutTutar { get; set; }
    // Yüzde (ör. 2, 5, 20). Sıfır olabilir (stopaja tabi olmayan alım).
    public decimal StopajOrani { get; set; }
    public decimal StopajTutari { get; set; }
    // Yalnız serbest meslek makbuzunda (brüt üzerinden).
    public decimal KdvOrani { get; set; }
    public decimal KdvTutari { get; set; }
    // Ödenen = brüt - stopaj + KDV
    public decimal NetTutar { get; set; }
    // Satıcının kestiği belgenin numarası (serbest meslek makbuzu / e-SMM).
    public string? KarsiBelgeNo { get; set; }

    public int? KasaHareketiId { get; set; }
    public int? BankaHareketiId { get; set; }
    public int? YevmiyeFisId { get; set; }

    public bool IptalEdildi { get; set; }
    public DateTime? IptalTarihi { get; set; }
    public string? IptalNedeni { get; set; }

    public string Aciklama { get; set; } = string.Empty;
    public List<GiderPusulasiKalemi> Kalemler { get; set; } = new();
}

public class GiderPusulasiKalemi : TemelVarlik
{
    public int GiderPusulasiId { get; set; }
    public string Cinsi { get; set; } = string.Empty;
    public decimal Miktar { get; set; } = 1;
    public string Birim { get; set; } = "Adet";
    public decimal BirimFiyat { get; set; }
    public decimal Tutar { get; set; }
}
