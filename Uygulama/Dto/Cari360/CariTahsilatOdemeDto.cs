using System;

namespace Uygulama.Dto.Cari360;

public class CariTahsilatOdemeDto
{
    public int Id { get; set; }
    public string IslemNo { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
    public string Tur { get; set; } = string.Empty; // Tahsilat veya Ödeme
    public string OdemeYontemi { get; set; } = string.Empty;
    public string KasaBanka { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = string.Empty;
    public string Referans { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
}
