using System;

namespace Uygulama.Dto.Cari360;

public class CariIrsaliyeDto
{
    public int Id { get; set; }
    public string IrsaliyeNo { get; set; } = string.Empty;
    public string Tur { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
    public string KaynakSiparis { get; set; } = string.Empty;
    public decimal ToplamMiktar { get; set; }
    public string Durum { get; set; } = string.Empty;
    public bool FaturalandiMi { get; set; }
}
