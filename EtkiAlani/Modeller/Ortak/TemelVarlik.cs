namespace EtkiAlani.Modeller.Ortak;

public abstract class TemelVarlik
{
    public int Id { get; set; }
    public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
    public DateTime? GuncellemeTarihi { get; set; }
    public bool Silindi { get; set; } = false;
    public DateTime? SilinmeTarihi { get; set; }
    public string? SilinmeNedeni { get; set; }
}
