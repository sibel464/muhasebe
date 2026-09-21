using System;

namespace Uygulama.Dto.Cari360;

public class CariSiparisDto
{
    public int Id { get; set; }
    public string SiparisNo { get; set; } = string.Empty;
    public string Tur { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
    public DateTime? TeslimTarihi { get; set; }
    public decimal Toplam { get; set; }
    public string SevkDurumu { get; set; } = string.Empty;
    public string FaturaDurumu { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
}
