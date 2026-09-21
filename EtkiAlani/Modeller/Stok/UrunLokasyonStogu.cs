using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Stok;

public class UrunLokasyonStogu : TemelVarlik
{
    public int UrunId { get; set; }
    public Urun? Urun { get; set; }

    public int DepoId { get; set; }
    public Depo? Depo { get; set; }

    public decimal MevcutMiktar { get; set; } = 0;
    public decimal RezerveMiktar { get; set; } = 0;
}
