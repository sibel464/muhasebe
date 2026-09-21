using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Stok;

public enum DepoTransferDurumu
{
    Taslak = 1,
    SevkEdildi = 2,
    Tamamlandi = 3,
    Iptal = 4
}

public class DepoTransferFisi : TemelVarlik
{
    public Guid? SirketId { get; set; }
    public Sirket? Sirket { get; set; }

    public string FisNo { get; set; } = string.Empty;
    public DateTime Tarih { get; set; } = DateTime.Today;

    public int KaynakDepoId { get; set; }
    public Depo? KaynakDepo { get; set; }

    public int HedefDepoId { get; set; }
    public Depo? HedefDepo { get; set; }

    public DepoTransferDurumu Durum { get; set; } = DepoTransferDurumu.Tamamlandi;

    public string Aciklama { get; set; } = string.Empty;
    public string? SevkEdenPersonel { get; set; }
    public string? TeslimAlanPersonel { get; set; }
    public string? BelgeNo { get; set; }

    public decimal ToplamMiktar { get; set; }
    public decimal ToplamTutar { get; set; }

    public List<DepoTransferKalemi> Kalemler { get; set; } = new();
}
