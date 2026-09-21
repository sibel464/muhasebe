using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Irsaliye;

public class IrsaliyeKalemiStokDagilimi : TemelVarlik
{
    public int IrsaliyeKalemiId { get; set; }
    public IrsaliyeKalemi? IrsaliyeKalemi { get; set; }
    public int DepoId { get; set; }
    public Depo? Depo { get; set; }
    public decimal Miktar { get; set; }
}
