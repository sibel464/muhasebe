using System;

namespace Uygulama.Dto.Cari360;

public class CariFaturaDto
{
    public int Id { get; set; }
    public string FaturaNo { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
    public string Tur { get; set; } = string.Empty;
    public decimal Net { get; set; }
    public decimal Kdv { get; set; }
    public decimal GenelToplam { get; set; }
    public decimal Odenen { get; set; }
    public decimal Kalan => GenelToplam - Odenen;
    public string Durum { get; set; } = string.Empty;
    public string Kaynak { get; set; } = string.Empty;
}
