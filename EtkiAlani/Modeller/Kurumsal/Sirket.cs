namespace EtkiAlani.Modeller.Kurumsal;

public class Sirket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SirketKodu { get; set; } = string.Empty;
    public string Unvan { get; set; } = string.Empty;
    public string? VergiNo { get; set; }
    public string? VergiDairesi { get; set; }
    public string? Telefon { get; set; }
    public string? Email { get; set; }
    public string? Adres { get; set; }
    public bool AktifMi { get; set; } = true;
    public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
}
