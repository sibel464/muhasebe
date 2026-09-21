using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Cari;

public class CariTipiTanimi : TemelVarlik
{
    public const int BireyselId = 1;
    public const int KurumsalId = 2;

    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public bool AktifMi { get; set; } = true;
    public int SiraNo { get; set; }
}
