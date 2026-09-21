using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Kurumsal;

public class ParaBirimi : TemelVarlik
{
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Sembol { get; set; } = string.Empty;
    public bool VarsayilanMi { get; set; }
}
