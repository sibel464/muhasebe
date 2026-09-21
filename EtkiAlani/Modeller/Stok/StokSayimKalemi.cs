using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Stok;

public class StokSayimKalemi : TemelVarlik
{
    public int StokSayimiId { get; set; }
    public StokSayimi? StokSayimi { get; set; }

    public int UrunId { get; set; }
    public Urun? Urun { get; set; }

    public decimal SistemMiktari { get; set; } = 0;
    public decimal SayilanMiktar { get; set; } = 0;
    public decimal FarkMiktari => SayilanMiktar - SistemMiktari;
    public decimal BirimFiyat { get; set; } = 0;
    public decimal ToplamFarkTutari => FarkMiktari * BirimFiyat;
}
