using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Kurumsal;

public enum DepoTuru
{
    Magaza = 1,
    Depo = 2
}

public class Depo : TemelVarlik
{
    public int SubeId { get; set; }
    public Sube? Sube { get; set; }
    public string DepoKodu { get; set; } = string.Empty;
    public string DepoAdi { get; set; } = string.Empty;
    public DepoTuru DepoTuru { get; set; } = DepoTuru.Depo;
    public string? Sorumlu { get; set; }
    public string? Telefon { get; set; }
    public string? Adres { get; set; }
    public string? MuhasebeKodu { get; set; } = "153.01.001";
    public bool AktifMi { get; set; } = true;
}

