using EtkiAlani.Modeller.Ortak;
using System.Collections.Generic;

namespace EtkiAlani.Modeller.Finans;

public class Kasa : TemelVarlik
{
    public Guid? SirketId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string ParaBirimi { get; set; } = "TRY";
    public decimal Bakiye { get; set; } = 0;
    public string MuhasebeKodu { get; set; } = "100.01.001";
    public string Aciklama { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
    
    public ICollection<KasaHareketi> Hareketler { get; set; } = new List<KasaHareketi>();
}

