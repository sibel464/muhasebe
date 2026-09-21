using System;

namespace Uygulama.Dto.Cari360;

public class CariIadeDto
{
    public int Id { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public string Tur { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
    public string KaynakFatura { get; set; } = string.Empty;
    public decimal Toplam { get; set; }
    public string Durum { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
}
