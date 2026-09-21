namespace Uygulama.Dto.Doviz;

public enum KurTipiPolicy
{
    DovizAlis = 1,
    DovizSatis = 2,
    EfektifAlis = 3,
    EfektifSatis = 4
}

public class DovizKuruDto
{
    public int Id { get; set; }
    public string ParaBirimiKodu { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
    public DateTime KurTarihi { get; set; }
    public decimal DovizAlis { get; set; }
    public decimal DovizSatis { get; set; }
    public decimal EfektifAlis { get; set; }
    public decimal EfektifSatis { get; set; }
    public string Kaynak { get; set; } = "TCMB";
    public DateTime OlusturmaTarihi { get; set; }
    public int KullaniciId { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public bool HaftaSonuFallBackMi => Tarih.Date != KurTarihi.Date;
}

public class ManuelKurGirisDto
{
    public string ParaBirimiKodu { get; set; } = string.Empty;
    public DateTime Tarih { get; set; } = DateTime.UtcNow.Date;
    public decimal DovizAlis { get; set; }
    public decimal DovizSatis { get; set; }
    public decimal EfektifAlis { get; set; }
    public decimal EfektifSatis { get; set; }
    public string Aciklama { get; set; } = string.Empty;
}

public class KurFiltreDto
{
    public string ParaBirimiKodu { get; set; } = string.Empty;
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public string Kaynak { get; set; } = string.Empty;
    public int Sayfa { get; set; } = 1;
    public int SayfaBoyutu { get; set; } = 20;
}

public class RevaluasyonKalemiDto
{
    public int Id { get; set; }
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
    public string Yön => KurFarki >= 0 ? "Gelir" : "Gider";
}

public class RevaluasyonOnizleDto
{
    public Guid SirketId { get; set; }
    public int MaliDonemId { get; set; }
    public DateTime DegerlemeTarihi { get; set; } = DateTime.UtcNow.Date;
    public string ParaBirimiKodu { get; set; } = "USD";
    public decimal YeniDegerlemeKuru { get; set; }
    public decimal ToplamEskiYerel { get; set; }
    public decimal ToplamYeniYerel { get; set; }
    public decimal ToplamKurFarki { get; set; }
    public List<RevaluasyonKalemiDto> Kalemler { get; set; } = new();
}

public class RevaluasyonKaydiDto
{
    public int Id { get; set; }
    public Guid SirketId { get; set; }
    public int MaliDonemId { get; set; }
    public DateTime DegerlemeTarihi { get; set; }
    public string ParaBirimiKodu { get; set; } = string.Empty;
    public decimal Kur { get; set; }
    public decimal ToplamEskiYerel { get; set; }
    public decimal ToplamYeniYerel { get; set; }
    public decimal ToplamKurFarki { get; set; }
    public int? YevmiyeFisId { get; set; }
    public string FisNo { get; set; } = string.Empty;
    public string DurumStr { get; set; } = "Onaylı";
    public int KullaniciId { get; set; }
    public string OlusturanKullaniciAdi { get; set; } = string.Empty;
    public DateTime OlusturmaTarihi { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public List<RevaluasyonKalemiDto> Kalemler { get; set; } = new();
}

public class DovizliBakiyeOzetDto
{
    public string ParaBirimiKodu { get; set; } = string.Empty;
    public decimal ToplamDovizBakiye { get; set; }
    public decimal ToplamDefterDegeriTRY { get; set; }
    public decimal GuncelKur { get; set; }
    public decimal GuncelDegerTRY { get; set; }
    public decimal PotansiyelKurFarki => GuncelDegerTRY - ToplamDefterDegeriTRY;
}
