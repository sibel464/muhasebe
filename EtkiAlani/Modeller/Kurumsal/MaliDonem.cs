using System.ComponentModel.DataAnnotations.Schema;
using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Kurumsal;

public class MaliDonem : TemelVarlik
{
    public Guid SirketId { get; set; }
    public Sirket? Sirket { get; set; }
    public string DonemAdi { get; set; } = string.Empty;
    public DateTime Baslangic { get; set; }
    public DateTime Bitis { get; set; }
    public bool KilitliMi { get; set; }
    [NotMapped]
    public bool Kapali { get => KilitliMi; set => KilitliMi = value; }
}
