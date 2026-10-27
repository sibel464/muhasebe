using EtkiAlani.Modeller.Fatura;

namespace Uygulama.Arayuzler;

public sealed class TekrarlayanFaturaListeDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public string OrnekFaturaNo { get; set; } = "";
    public string CariUnvan { get; set; } = "";
    public FaturaTuru Tur { get; set; }
    public decimal Tutar { get; set; }
    public int PeriyotAy { get; set; }
    public string PeriyotAdi => PeriyotAy switch { 1 => "Her ay", 2 => "2 ayda bir", 3 => "3 ayda bir", 6 => "6 ayda bir", 12 => "Her yıl", _ => $"{PeriyotAy} ayda bir" };
    public DateTime BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public DateTime SiradakiTarih { get; set; }
    public int? VadeGunu { get; set; }
    public bool Onayla { get; set; }
    public bool Aktif { get; set; }
    public int OlusturulanAdet { get; set; }
    // Bugüne kadar kesilmesi gerekip henüz kesilmemiş dönem sayısı.
    public int BekleyenAdet { get; set; }
    public string? SonFaturaNo { get; set; }
    public string? SonHata { get; set; }
    public string Durum => !Aktif ? "Durduruldu" : BittiMi ? "Bitti" : BekleyenAdet > 0 ? $"{BekleyenAdet} fatura bekliyor" : "Zamanı gelmedi";
    public bool BittiMi { get; set; }
}

public sealed record TekrarlayanFaturaSonucu(int PlanId, string PlanAdi, DateTime Tarih, string? FaturaNo, string? Hata);

public interface ITekrarlayanFaturaServisi
{
    Task<List<TekrarlayanFaturaListeDto>> ListeAsync(DateTime? bugun = null);
    Task<TekrarlayanFatura?> GetirAsync(int id);
    // Yeni planda OrnekFaturaId ve BaslangicTarihi zorunlu; kayıtlı planda kesilmiş dönemler korunur.
    Task<TekrarlayanFatura> KaydetAsync(TekrarlayanFatura plan);
    Task SilAsync(int id);
    // Zamanı gelmiş (tarihi bugün ya da önce) her dönem için ayrı fatura keser; planId verilirse yalnız o plan.
    Task<List<TekrarlayanFaturaSonucu>> BekleyenleriKesAsync(DateTime? bugun = null, int? planId = null);
    Task<int> BekleyenSayisiAsync(DateTime? bugun = null);
}
