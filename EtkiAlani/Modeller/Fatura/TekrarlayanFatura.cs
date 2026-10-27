using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Fatura;

// Kira, aidat, bakım sözleşmesi gibi düzenli kesilen faturalar: örnek faturanın cari, kalem ve tutarları her dönem
// yeni bir faturaya kopyalanır. n. faturanın tarihi BaslangicTarihi + n * PeriyotAy olarak hesaplanır (ayın 31'i kaymaz).
public class TekrarlayanFatura : TemelVarlik
{
    public Guid? SirketId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int OrnekFaturaId { get; set; }
    public Fatura? OrnekFatura { get; set; }
    public int PeriyotAy { get; set; } = 1;
    public DateTime BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    // Fatura tarihinden itibaren vade (gün); null = vadesiz.
    public int? VadeGunu { get; set; }
    // true: kesilen fatura hemen onaylanır (cariye ve deftere işlenir); false: taslak olarak kalır, kullanıcı kontrol edip onaylar.
    public bool Onayla { get; set; }
    public bool Aktif { get; set; } = true;
    public int OlusturulanAdet { get; set; }
    public int? SonFaturaId { get; set; }
    public string? SonHata { get; set; }

    public DateTime SiradakiTarih => BaslangicTarihi.AddMonths(OlusturulanAdet * Math.Max(1, PeriyotAy));
    public bool BittiMi => BitisTarihi is DateTime b && SiradakiTarih.Date > b.Date;
}
