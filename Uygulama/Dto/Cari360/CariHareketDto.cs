using System;

namespace Uygulama.Dto.Cari360;

public class CariHareketDto
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public string IslemTuru { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
    public decimal Bakiye { get; set; }
    public string BakiyeYonu { get; set; } = string.Empty;
    public string Kaynak { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
}
