using EtkiAlani.Modeller.Ortak;
using EtkiAlani.Modeller.Cari;

namespace EtkiAlani.Modeller.Finans;

public class BelgeKapama : TemelVarlik
{
    // Hangi tahsilat/ödeme işlemiyle kapanıyor? (CariHareket tablosundaki Tahsilat/Odeme ID'si)
    public int OdenenCariHareketId { get; set; }
    public CariHareket? OdenenCariHareket { get; set; }

    // Hangi fatura kapanıyor? (CariHareket tablosundaki Fatura/Açık İşlem ID'si)
    public int FaturaCariHareketId { get; set; }
    public CariHareket? FaturaCariHareket { get; set; }

    public decimal KapananTutar { get; set; }
    public decimal KapananDovizTutar { get; set; }
    public DateTime KapamaTarihi { get; set; }
}
