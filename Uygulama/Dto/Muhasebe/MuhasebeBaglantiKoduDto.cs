namespace Uygulama.Dto.Muhasebe;

public class MuhasebeBaglantiKoduDto
{
    public int Id { get; set; }
    public Guid SirketId { get; set; }
    public string KategoriKodu { get; set; } = string.Empty;
    public string KategoriAdi { get; set; } = string.Empty;
    public string VarSayilanHesapKodu { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
    public string? MevcutHesapAdi { get; set; }
}
