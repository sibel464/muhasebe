using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Kurumsal;

public class KdvOrani : TemelVarlik
{
    public string Kod { get; set; } = string.Empty;
    public decimal Oran { get; set; }
    public bool AktifMi { get; set; } = true;
}
