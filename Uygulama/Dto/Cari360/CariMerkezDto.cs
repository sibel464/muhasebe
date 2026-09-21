using EtkiAlani.Modeller.Cari;
namespace Uygulama.Dto.Cari360;

public enum CariSekme { Genel, Ekstre, Satislar, Alislar, Siparisler, Irsaliyeler, Faturalar, TahsilatOdeme, Iadeler, CariFisleri, CekSenet, RiskVade, Notlar, Sistem }
public sealed record CariParaSecimi(int? Id, string Ad);
public sealed record CariSecenek(string? Deger, string Ad);
public sealed class CariSorgu
{
    public int? BelgeId { get; set; }
    public string? Kaynak { get; set; }
    public DateTime? Baslangic { get; set; }
    public DateTime? Bitis { get; set; }
    public string? Tur { get; set; }
    public string? BelgeNo { get; set; }
    public string? Yon { get; set; }
    public string? Durum { get; set; }
    public int? ParaBirimiId { get; set; }
    public bool IptalleriGoster { get; set; }
    public int Sayfa { get; set; } = 1;
    public int SayfaBoyutu { get; set; } = 50;
    public void Dogrula()
    {
        if (Sayfa < 1 || SayfaBoyutu is not (50 or 100)) throw new ArgumentException("Sayfa boyutu 50 veya 100 olmalıdır.");
        if (Baslangic > Bitis) throw new ArgumentException("Başlangıç tarihi bitiş tarihinden sonra olamaz.");
    }
}
public sealed class CariSayfaDto
{
    public List<CariMerkezSatirDto> Satirlar { get; set; } = new();
    public int ToplamKayit { get; set; }
    public decimal DevirBakiyesi { get; set; }
    public decimal? Toplam { get; set; }
    public decimal? BuAy { get; set; }
    public decimal? Iade { get; set; }
    public DateTime? SonTarih { get; set; }
    public string Aciklama { get; set; } = "";
}
public sealed class CariMerkezSatirDto
{
    public int Id { get; set; }
    public int? BelgeId { get; set; }
    public string Kaynak { get; set; } = "";
    public string BelgeNo { get; set; } = "";
    public DateTime Tarih { get; set; }
    public DateTime? Vade { get; set; }
    public string Tur { get; set; } = "";
    public string Durum { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public string Referans { get; set; } = "";
    public string ParaBirimi { get; set; } = "TRY";
    public decimal Kur { get; set; } = 1;
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
    public decimal Bakiye { get; set; }
    public decimal MutlakBakiye => Math.Abs(Bakiye);
    public string BakiyeYonu => Bakiye > 0 ? "Borç" : Bakiye < 0 ? "Alacak" : "Sıfır";
    public decimal Net { get; set; }
    public decimal Kdv { get; set; }
    public decimal Toplam { get; set; }
    public decimal? Odenen { get; set; }
    public decimal? Kalan { get; set; }
    public decimal Miktar { get; set; }
    public decimal SevkEdilen { get; set; }
    public decimal KalanSevk { get; set; }
    public decimal Faturalanan { get; set; }
    public decimal KalanFatura { get; set; }
    public string Yontem { get; set; } = "";
    public string KasaBanka { get; set; } = "";
    public string Kullanici { get; set; } = "";
    public int? TersKayitId { get; set; }
    public int KalanGun => Vade.HasValue ? (Vade.Value.Date - DateTime.Today).Days : 0;
    public int GecikmeGunu => Math.Max(0, -KalanGun);
}
public sealed class CariRiskDto
{
    public decimal VadesiGelmemis { get; set; }
    public decimal Gun1_30 { get; set; }
    public decimal Gun31_60 { get; set; }
    public decimal Gun61_90 { get; set; }
    public decimal Gun90Ustu { get; set; }
    public decimal Vadesiz { get; set; }
    public decimal VadesiGecmisBorc { get; set; }
    public DateTime? EnEskiAcikBelge { get; set; }
    public decimal? OrtalamaTahsilatGunu { get; set; }
    public decimal? OrtalamaOdemeGunu { get; set; }
    public string Aciklama { get; set; } = "Yaşlandırma açık satış alacaklarıdır. Alış borçları ayrı gösterilir; vadesiz belgeler gecikmiş sayılmaz.";
}
public sealed class CariBelgeDetayDto
{
    public CariMerkezSatirDto Belge { get; set; } = new();
    public List<CariMerkezSatirDto> Kalemler { get; set; } = new();
    public List<CariMerkezSatirDto> Baglantilar { get; set; } = new();
}
