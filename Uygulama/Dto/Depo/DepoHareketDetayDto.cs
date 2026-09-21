using EtkiAlani.Modeller.Stok;

namespace Uygulama.Dto.Depo;

public class DepoHareketDetayDto
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public string? FisNo { get; set; }
    public string? BelgeNo { get; set; }
    public StokIslemTuru IslemTuru { get; set; }
    public StokHareketTuru HareketTuru { get; set; }

    public int UrunId { get; set; }
    public string UrunKodu { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public string? Barkod { get; set; }

    public int? DepoId { get; set; }
    public string DepoAdi { get; set; } = "-";

    public int? HedefDepoId { get; set; }
    public string HedefDepoAdi { get; set; } = "-";

    public int? CariKartId { get; set; }
    public string CariUnvan { get; set; } = "-";

    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal ToplamTutar => Miktar * BirimFiyat;
    public string Aciklama { get; set; } = string.Empty;

    public string NeredenNereye
    {
        get
        {
            if (IslemTuru == StokIslemTuru.DepoTransferi)
                return $"{DepoAdi} ➔ {HedefDepoAdi}";
            if (HareketTuru == StokHareketTuru.Giris)
                return string.IsNullOrWhiteSpace(CariUnvan) || CariUnvan == "-" ? $"{DepoAdi} (Giriş)" : $"{CariUnvan} ➔ {DepoAdi}";
            return string.IsNullOrWhiteSpace(CariUnvan) || CariUnvan == "-" ? $"{DepoAdi} (Çıkış)" : $"{DepoAdi} ➔ {CariUnvan}";
        }
    }
}
