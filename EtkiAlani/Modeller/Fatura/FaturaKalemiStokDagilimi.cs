using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Fatura;

public class FaturaKalemiStokDagilimi : TemelVarlik
{
    public int FaturaKalemiId { get; set; }
    public FaturaKalemi? FaturaKalemi { get; set; }

    public int DepoId { get; set; }
    public Depo? Depo { get; set; }

    public decimal Miktar { get; set; } = 0;
}
