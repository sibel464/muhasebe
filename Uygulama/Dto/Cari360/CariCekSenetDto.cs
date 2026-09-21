using System;

namespace Uygulama.Dto.Cari360;

public class CariCekSenetDto
{
    public int Id { get; set; }
    public string Tur { get; set; } = string.Empty;
    public string BelgeNo { get; set; } = string.Empty;
    public string CekSenetNo { get; set; } = string.Empty;
    public DateTime Vade { get; set; }
    public decimal Tutar { get; set; }
    public string Durum { get; set; } = string.Empty;
}
