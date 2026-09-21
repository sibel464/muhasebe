using EtkiAlani.Modeller.Muhasebe;

namespace Uygulama.Dto.Muhasebe;

public class YevmiyeFisDto
{
    public int Id { get; set; }
    public Guid SirketId { get; set; }
    public string FisNo { get; set; } = string.Empty;
    public FisTuru FisTuru { get; set; } = FisTuru.Mahsup;
    public string FisTuruStr => FisTuru switch
    {
        FisTuru.Mahsup => "Mahsup Fişi",
        FisTuru.Tahsil => "Tahsil Fişi",
        FisTuru.Tediye => "Tediye Fişi",
        FisTuru.Acilis => "Açılış Fişi",
        FisTuru.Kapanis => "Kapanış Fişi",
        _ => "Fiş"
    };
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public int? MaliDonemId { get; set; }
    public int? SubeId { get; set; }
    public string BelgeTipi { get; set; } = string.Empty;
    public int BelgeId { get; set; }
    public string Referans { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public YevmiyeDurumu Durum { get; set; } = YevmiyeDurumu.Onaylandi;
    public string DurumStr => Durum switch
    {
        YevmiyeDurumu.Taslak => "Taslak",
        YevmiyeDurumu.Onaylandi => "Onaylı",
        YevmiyeDurumu.Iptal => "İptal",
        _ => "Belirsiz"
    };
    public int? TersFisId { get; set; }
    public int? IptalEdilenFisId { get; set; }
    public int KullaniciId { get; set; }
    public string OlusturanKullaniciAdi { get; set; } = string.Empty;
    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public decimal Fark => Math.Abs(ToplamBorc - ToplamAlacak);
    public bool DengedeMi => Math.Abs(ToplamBorc - ToplamAlacak) < 0.001m;
    public List<YevmiyeSatiriDto> Satirlar { get; set; } = new();
}
