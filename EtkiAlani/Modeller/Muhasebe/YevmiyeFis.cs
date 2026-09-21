using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Muhasebe;

public class YevmiyeFis : TemelVarlik
{
    public Guid SirketId { get; set; }
    public Sirket? Sirket { get; set; }
    public string FisNo { get; set; } = string.Empty;
    public FisTuru FisTuru { get; set; } = FisTuru.Mahsup;
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public int? MaliDonemId { get; set; }
    public int? SubeId { get; set; }
    public string BelgeTipi { get; set; } = string.Empty;
    public int BelgeId { get; set; }
    public string Referans { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public YevmiyeDurumu Durum { get; set; } = YevmiyeDurumu.Onaylandi;
    public int? TersFisId { get; set; }
    public int? IptalEdilenFisId { get; set; }
    public int KullaniciId { get; set; }
    public string OlusturanKullaniciAdi { get; set; } = string.Empty;
    public List<YevmiyeSatiri> Satirlar { get; set; } = new();
}
