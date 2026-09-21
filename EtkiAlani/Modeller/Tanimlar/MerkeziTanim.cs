using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Tanimlar;

public static class TanimGruplari
{
    public const string UrunKategorisi = "URUN_KATEGORISI";
    public const string Marka = "MARKA";
    public const string Birim = "BIRIM";
}

public class MerkeziTanim : TemelVarlik
{
    public string Grup { get; set; } = string.Empty;
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string? KisaAd { get; set; }
    public string? Aciklama { get; set; }
    public bool AktifMi { get; set; } = true;
    public int SiraNo { get; set; }
}
