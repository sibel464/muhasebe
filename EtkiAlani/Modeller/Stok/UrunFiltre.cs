namespace EtkiAlani.Modeller.Stok;

public class UrunFiltre
{
    public string AramaMetni { get; set; } = string.Empty;
    public string Kategori { get; set; } = string.Empty;
    public bool SadeceKritikStok { get; set; }
    public bool SadeceAktifler { get; set; } = true;
}
