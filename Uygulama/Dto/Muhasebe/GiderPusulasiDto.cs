using EtkiAlani.Modeller.Muhasebe;

namespace Uygulama.Dto.Muhasebe;

public class GiderPusulasiListeDto
{
    public int Id { get; set; }
    public GiderPusulasiTuru Tur { get; set; }
    public string TurAdi => Tur == GiderPusulasiTuru.MustahsilMakbuzu ? "Müstahsil Makbuzu" : "Gider Pusulası";
    public string BelgeNo { get; set; } = "";
    public DateTime Tarih { get; set; }
    public string SaticiAdSoyad { get; set; } = "";
    public string? SaticiTcKimlikNo { get; set; }
    public string GiderHesapKodu { get; set; } = "";
    public decimal BrutTutar { get; set; }
    public decimal StopajOrani { get; set; }
    public decimal StopajTutari { get; set; }
    public decimal NetTutar { get; set; }
    public string OdemeHesabi { get; set; } = "";
    public bool IptalEdildi { get; set; }
    public string Durum => IptalEdildi ? "İptal" : "Geçerli";
    public string Aciklama { get; set; } = "";
}

// Yeni belge girişi. Kalem tutarları Miktar x BirimFiyat'tan hesaplanır; stopaj brüt toplamdan.
public class GiderPusulasiGirisDto
{
    public GiderPusulasiTuru Tur { get; set; } = GiderPusulasiTuru.GiderPusulasi;
    public DateTime Tarih { get; set; } = DateTime.Today;
    public string SaticiAdSoyad { get; set; } = "";
    public string? SaticiTcKimlikNo { get; set; }
    public string? SaticiAdres { get; set; }
    public string GiderHesapKodu { get; set; } = "770";
    public decimal StopajOrani { get; set; }
    // "Kasa" veya "Banka"
    public string OdemeKaynagi { get; set; } = "Kasa";
    public int OdemeHesabiId { get; set; }
    public string? Aciklama { get; set; }
    public List<GiderPusulasiKalemDto> Kalemler { get; set; } = new();
}

public class GiderPusulasiKalemDto
{
    public string Cinsi { get; set; } = "";
    public decimal Miktar { get; set; } = 1;
    public string Birim { get; set; } = "Adet";
    public decimal BirimFiyat { get; set; }
    public decimal Tutar => decimal.Round(Miktar * BirimFiyat, 2, MidpointRounding.AwayFromZero);
}

public sealed record StopajSecenegi(decimal Oran, string Ad)
{
    public override string ToString() => Ad;
}
