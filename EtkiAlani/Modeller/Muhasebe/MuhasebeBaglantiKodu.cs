using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Muhasebe;

public class MuhasebeBaglantiKodu : TemelVarlik
{
    public Guid SirketId { get; set; }
    public Sirket? Sirket { get; set; }
    public string KategoriKodu { get; set; } = string.Empty; // e.g. "KASA", "BANKA", "ALICILAR", "SATICILAR", "YURTICI_SATIS", "SATIS_KDV", "INDIRILECEK_KDV", "POS", "KOMISYON", "SATIS_IADE", "ALIS_IADE", "STOK", "STMM"
    public string KategoriAdi { get; set; } = string.Empty;
    public string VarSayilanHesapKodu { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
}
