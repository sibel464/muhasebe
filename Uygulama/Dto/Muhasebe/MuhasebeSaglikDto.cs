namespace Uygulama.Dto.Muhasebe;

public class MuhasebeSaglikDto
{
    public int DengesizFisSayisi { get; set; }
    public int EksikHesapEslameSayisi { get; set; }
    public int MuhasebelesmemisBelgeSayisi { get; set; }
    public bool KapaliDonemVarMi { get; set; }
    public string SonYevmiyeNo { get; set; } = string.Empty;
    public DateTime? SonFisTarihi { get; set; }
    public decimal ToplamYevmiyeBorc { get; set; }
    public decimal ToplamYevmiyeAlacak { get; set; }
    public bool SaglikliMi => DengesizFisSayisi == 0 && Math.Abs(ToplamYevmiyeBorc - ToplamYevmiyeAlacak) < 0.001m;
}

public class MuhasebelesmemisBelgeDto
{
    public string BelgeTipi { get; set; } = string.Empty; // "Fatura", "Tahsilat", "Odeme", "Kasa", "Banka", "POS", "Cek", "Senet"
    public int BelgeId { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
    public string CariUnvan { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public string Aciklama { get; set; } = string.Empty;
}
