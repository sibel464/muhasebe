namespace Uygulama.Dto.Rapor;

public class SatisRaporFiltreDto
{
    public int SirketId { get; set; } = 1;
    public int? SubeId { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public int? CariId { get; set; }
    public int? UrunId { get; set; }
    public int? KategoriId { get; set; }
    public int? MarkaId { get; set; }
    public int? KullaniciId { get; set; }
    public string? Durum { get; set; }
    public string? Doviz { get; set; }
    public bool IptalleriDahilEt { get; set; } = false;
    public bool MaliyetGor { get; set; } = true;
    public string? AramaMetni { get; set; }
    public string PresetKey { get; set; } = "BuAy";
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

public class YoneticiOzetDto
{
    public decimal BugunkuSatis { get; set; }
    public decimal BuAySatis { get; set; }
    public decimal BuAyAlis { get; set; }
    public decimal BuAyTahsilat { get; set; }
    public decimal BuAyOdeme { get; set; }
    public decimal? BrutKar { get; set; }
    public decimal ToplamAlacak { get; set; }
    public decimal ToplamBorc { get; set; }
    public decimal KasaToplami { get; set; }
    public decimal BankaToplami { get; set; }
    public decimal VadesiGecmisAlacak { get; set; }
    public int KritikStokSayisi { get; set; }
}

public class DonemKarsilastirmaKalemiDto
{
    public string MetrikAd { get; set; } = "";
    public decimal MevcutDeger { get; set; }
    public decimal ÖncekiDeger { get; set; }
    public decimal DegisimMiktari => MevcutDeger - ÖncekiDeger;
    public decimal YuzdeDegisim
    {
        get
        {
            if (ÖncekiDeger == 0)
                return MevcutDeger > 0 ? 100 : 0;
            return Math.Round((MevcutDeger - ÖncekiDeger) / Math.Abs(ÖncekiDeger) * 100, 2);
        }
    }
}

public class DonemKarsilastirmaResultDto
{
    public string PeriodType { get; set; } = "BuAyVsGecenAy"; // BugunVsDun, BuAyVsGecenAy, BuYilVsGecenYil
    public List<DonemKarsilastirmaKalemiDto> Kalemler { get; set; } = new();
}

public class SatisRaporuKalemiDto
{
    public int FaturaId { get; set; }
    public DateTime Tarih { get; set; }
    public string BelgeNo { get; set; } = "";
    public string CariKod { get; set; } = "";
    public string CariUnvan { get; set; } = "";
    public string UrunKod { get; set; } = "";
    public string UrunAd { get; set; } = "";
    public string Kategori { get; set; } = "";
    public string Marka { get; set; } = "";
    public string SubeAd { get; set; } = "";
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal Iskonto { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal NetTutar { get; set; }
    public decimal KdvTutar { get; set; }
    public decimal ToplamTutar { get; set; }
    public decimal? Maliyet { get; set; }
    public decimal? BrutKar { get; set; }
    public decimal? KarYuzdesi => NetTutar > 0 && BrutKar.HasValue ? Math.Round(BrutKar.Value / NetTutar * 100, 2) : null;
    public string KullaniciAd { get; set; } = "";
    public string Durum { get; set; } = "Aktif";
}

public class TopUrunRaporKalemiDto
{
    public int UrunId { get; set; }
    public string UrunKod { get; set; } = "";
    public string UrunAd { get; set; } = "";
    public string Kategori { get; set; } = "";
    public string Marka { get; set; } = "";
    public decimal SatilanMiktar { get; set; }
    public decimal Ciro { get; set; }
    public decimal? BrutKar { get; set; }
    public decimal? KarYuzdesi => Ciro > 0 && BrutKar.HasValue ? Math.Round(BrutKar.Value / Ciro * 100, 2) : null;
    public DateTime? SonSatisTarihi { get; set; }
    public decimal MevcutStok { get; set; }
}

public class AlisRaporuKalemiDto
{
    public int FaturaId { get; set; }
    public DateTime Tarih { get; set; }
    public string BelgeNo { get; set; } = "";
    public string TedarikciKod { get; set; } = "";
    public string TedarikciUnvan { get; set; } = "";
    public string UrunKod { get; set; } = "";
    public string UrunAd { get; set; } = "";
    public string Kategori { get; set; } = "";
    public decimal Miktar { get; set; }
    public decimal ToplamTutar { get; set; }
    public decimal SonAlisFiyati { get; set; }
    public decimal OrtalamaAlisMaliyeti { get; set; }
}

public class CariRaporuKalemiDto
{
    public int CariId { get; set; }
    public string CariKod { get; set; } = "";
    public string CariUnvan { get; set; } = "";
    public string CariTur { get; set; } = "";
    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public decimal NetBakiye => ToplamAlacak - ToplamBorc; // Pozitif: Alacaklı, Negatif: Borçlu
    public string BakiyeTipi => NetBakiye > 0 ? "Alacaklı" : NetBakiye < 0 ? "Borçlu" : "Sıfır";
    public decimal RiskLimiti { get; set; }
    public bool RiskLimitiAsildimi => RiskLimiti > 0 && Math.Abs(Math.Min(0, NetBakiye)) > RiskLimiti;
    public decimal VadesiGecmisTutar { get; set; }
    public int AcikFaturaAdedi { get; set; }
}

public class CariYaslandirmaRaporuKalemiDto
{
    public int CariId { get; set; }
    public string CariKod { get; set; } = "";
    public string CariUnvan { get; set; } = "";
    public string CariTur { get; set; } = "";
    public decimal VadesiGelmemis { get; set; }
    public decimal Gun1_30 { get; set; }
    public decimal Gun31_60 { get; set; }
    public decimal Gun61_90 { get; set; }
    public decimal Gun90Plus { get; set; }
    public decimal ToplamBakiye => VadesiGelmemis + Gun1_30 + Gun31_60 + Gun61_90 + Gun90Plus;
    public string RiskSeviyesi => Gun90Plus > 0 ? "Yüksek Risk" : Gun61_90 > 0 ? "Orta Risk" : Gun31_60 > 0 ? "Düşük Risk" : "Normal";
}

public class VadeTakvimiKalemiDto
{
    public DateTime Vade { get; set; }
    // true: bize ödenecek (tahsilat), false: bizim ödeyeceğimiz
    public bool Tahsilat { get; set; }
    public string Yon => Tahsilat ? "Tahsilat" : "Ödeme";
    public string Kaynak { get; set; } = "";
    public string BelgeNo { get; set; } = "";
    public string CariKod { get; set; } = "";
    public string CariUnvan { get; set; } = "";
    public decimal Tutar { get; set; }
    public int GecikmeGun { get; set; }
    public string Aciklama { get; set; } = "";
}

public class VadesiGecmisAlacakDto
{
    public int CariId { get; set; }
    public string CariKod { get; set; } = "";
    public string CariUnvan { get; set; } = "";
    public string BelgeNo { get; set; } = "";
    public string BelgeTuru { get; set; } = "";
    public DateTime BelgeTarihi { get; set; }
    public DateTime VadeTarihi { get; set; }
    public decimal Tutar { get; set; }
    public decimal AcikTutar { get; set; }
    public int GecikmeGun { get; set; }
}

public class TahsilatPerformansiKalemiDto
{
    public int CariId { get; set; }
    public string CariKod { get; set; } = "";
    public string CariUnvan { get; set; } = "";
    public string FaturaNo { get; set; } = "";
    public DateTime FaturaTarihi { get; set; }
    public DateTime VadeTarihi { get; set; }
    public decimal Tutar { get; set; }
    public decimal KalanTutar { get; set; }
    public DateTime? TahsilTarihi { get; set; }
    public string Durum => TahsilTarihi.HasValue ? "Tahsil edildi" : KalanTutar < Tutar ? "Kısmen tahsil" : "Açık";
    public int GecikmeGun => TahsilTarihi.HasValue
        ? Math.Max(0, (TahsilTarihi.Value.Date - VadeTarihi.Date).Days)
        : Math.Max(0, (DateTime.Today - VadeTarihi.Date).Days);
    public double OrtalamaTahsilSuresiGun { get; set; }
}

public class StokRaporuKalemiDto
{
    public int UrunId { get; set; }
    public string UrunKod { get; set; } = "";
    public string UrunAd { get; set; } = "";
    public string Kategori { get; set; } = "";
    public string Marka { get; set; } = "";
    public string LokasyonAd { get; set; } = "";
    public decimal FizikselStok { get; set; }
    public decimal RezerveStok { get; set; }
    public decimal KullanilabilirStok => FizikselStok - RezerveStok;
    public decimal? BirimMaliyet { get; set; }
    public decimal? StokDegeri => BirimMaliyet.HasValue ? FizikselStok * BirimMaliyet.Value : null;
    public decimal KritikStokSeviyesi { get; set; }
    public bool KritikStoktaMi => FizikselStok <= KritikStokSeviyesi;
    public DateTime? SonHareketTarihi { get; set; }
    public int StokYasiGun => SonHareketTarihi.HasValue ? (DateTime.Today - SonHareketTarihi.Value.Date).Days : 0;
    public decimal StokDevirHizi { get; set; }
}

public class HareketsizUrunKalemiDto
{
    public int UrunId { get; set; }
    public string UrunKod { get; set; } = "";
    public string UrunAd { get; set; } = "";
    public string Kategori { get; set; } = "";
    public decimal StokMiktari { get; set; }
    public DateTime? SonHareketTarihi { get; set; }
    public int HareketsizGunSayisi => SonHareketTarihi.HasValue ? (DateTime.Today - SonHareketTarihi.Value.Date).Days : 999;
    public decimal StokDegeri { get; set; }
}

public class StokDegerRaporuKalemiDto
{
    public string LokasyonAd { get; set; } = "";
    public int UrunCesidiSayisi { get; set; }
    public decimal ToplamMiktar { get; set; }
    public decimal ToplamStokDegeri { get; set; }
}

public class FinansRaporuKalemiDto
{
    public long IslemId { get; set; }
    public DateTime Tarih { get; set; }
    public string IslemTuru { get; set; } = "";
    public string HesabAd { get; set; } = "";
    public string CariUnvan { get; set; } = "";
    public decimal Giren { get; set; }
    public decimal Cikan { get; set; }
    public decimal Bakiye { get; set; }
    public string Aciklama { get; set; } = "";
}

public class NakitAkisRaporuDto
{
    public decimal DonemBasiNakit { get; set; }
    public decimal Tahsilatlar { get; set; }
    public decimal DigerGirisler { get; set; }
    public decimal Odemeler { get; set; }
    public decimal DigerCikislar { get; set; }
    public decimal NetNakitAkisi => (Tahsilatlar + DigerGirisler) - (Odemeler + DigerCikislar);
    public decimal DonemSonuNakit => DonemBasiNakit + NetNakitAkisi;
    public List<KasaBankaRaporKalemiDto> KasaBankaDetaylari { get; set; } = new();
}

public class KasaBankaRaporKalemiDto
{
    public int HesapId { get; set; }
    public string HesapAd { get; set; } = "";
    public string Hesapturu { get; set; } = "Kasa"; // Kasa veya Banka
    public decimal Devir { get; set; }
    public decimal Giris { get; set; }
    public decimal Cikis { get; set; }
    public decimal Bakiye => Devir + Giris - Cikis;
    public string ParaBirimi { get; set; } = "TRY";
}

public class PosRaporuKalemiDto
{
    public int PosId { get; set; }
    public string CihazAd { get; set; } = "";
    public string BankaAd { get; set; } = "";
    public int TaksitSayisi { get; set; }
    public decimal BrutTutar { get; set; }
    public decimal KomisyonOrani { get; set; }
    public decimal KomisyonTutar { get; set; }
    public decimal NetTutar => BrutTutar - KomisyonTutar;
    public decimal BekleyenTutar { get; set; }
    public decimal HesabaGecenTutar { get; set; }
    public decimal IadeTutar { get; set; }
}

public class CekSenetRaporuKalemiDto
{
    public int Id { get; set; }
    public string PortfoyNo { get; set; } = "";
    public string Tur { get; set; } = "Çek"; // Çek veya Senet
    public string Direction { get; set; } = "Alınan"; // Alınan veya Verilen
    public DateTime VadeTarihi { get; set; }
    public decimal Tutar { get; set; }
    public string Durum { get; set; } = "Portföyde"; // Portföyde, Bankada, Ciro, Tahsil, Karşılıksız, İade
    public string BorcluCari { get; set; } = "";
    public string BankaAd { get; set; } = "";
    public bool VadesiGectiMi => VadeTarihi.Date < DateTime.Today && Durum != "Tahsil Edildi";
    public bool VadesiYaklastiMi => VadeTarihi.Date >= DateTime.Today && (VadeTarihi.Date - DateTime.Today).TotalDays <= 7 && Durum == "Portföyde";
}

public class KdvRaporuKalemiDto
{
    public decimal KdvOrani { get; set; }
    public decimal SatisMatrah { get; set; }
    public decimal SatisKdv { get; set; }
    public decimal SatisToplam => SatisMatrah + SatisKdv;
    public decimal AlisMatrah { get; set; }
    public decimal AlisKdv { get; set; }
    public decimal AlisToplam => AlisMatrah + AlisKdv;
    // Kısmi tevkifat: satışta alıcının tevkif ettiği KDV satıcı beyannamesine (KDV-1) girmez; alışta tevkif edilen
    // KDV'nin tamamı indirilir, ayrıca KDV-2 ile beyan edilip ödenir.
    public decimal SatisTevkifat { get; set; }
    public decimal AlisTevkifat { get; set; }
    public decimal NetKdv => SatisKdv - SatisTevkifat - AlisKdv;
}

public class RaporFavoriDto
{
    public int Id { get; set; }
    public string RaporAd { get; set; } = "";
    public string FiltreDataJson { get; set; } = "";
    public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
    public string KullaniciAd { get; set; } = "";
}

// Ba-Bs bildirimi: aynı kişi/kurumla bir ayda KDV hariç sınırı (5.000 TL) aşan alışlar Ba, satışlar Bs formuna yazılır.
public class BaBsKalemiDto
{
    public string Form { get; set; } = "";
    public int Yil { get; set; }
    public int Ay { get; set; }
    public string Donem => $"{Ay:00}/{Yil}";
    public string CariKod { get; set; } = "";
    public string Unvan { get; set; } = "";
    public string VergiKimlikNo { get; set; } = "";
    public string VergiDairesi { get; set; } = "";
    public int BelgeSayisi { get; set; }
    public decimal Tutar { get; set; }
    public string Uyari { get; set; } = "";
}
