using EtkiAlani.Modeller.Cari;

namespace EtkiAlani.Modeller.Finans;

public enum CekSenetTuru
{
    AlinanCek = 1,
    VerilenCek = 2,
    AlinanSenet = 3,
    VerilenSenet = 4
}

public enum CekSenetDurumu
{
    Portfoyde = 1,
    CiroEdildi = 2,
    TahsileVerildi = 3,
    TeminataVerildi = 4,
    TahsilEdildi = 5,
    Odendi = 6,
    Karsiliksiz = 7,
    IadeEdildi = 8
}

public sealed class CekSenet
{
    public int Id { get; set; }
    public Guid? SirketId { get; set; }
    
    public CekSenetTuru Tur { get; set; }
    public CekSenetDurumu Durum { get; set; }
    
    // Temel Bilgiler
    public string BelgeNo { get; set; } = string.Empty; // Çek seri numarası vb.
    public DateTime VadeTarihi { get; set; }
    public DateTime DuzenlemeTarihi { get; set; }
    
    // Tutar Bilgileri
    public decimal Tutar { get; set; }
    public int? ParaBirimiId { get; set; }
    public decimal Kur { get; set; } = 1;
    public decimal DovizTutar { get; set; }
    
    // Keşideci ve Banka Bilgileri
    public string Kesideci { get; set; } = string.Empty; // Asıl borçlu
    public string? BankaAdi { get; set; }
    public string? SubeAdi { get; set; }
    public string? HesapNo { get; set; }
    
    // İlişkiler
    public int CariKartId { get; set; } // Kimden alındı / Kime verildi
    public CariKart? CariKart { get; set; }
    
    public string Aciklama { get; set; } = string.Empty;
    
    // Denetim İzi (Soft Delete için)
    public bool SilindiMi { get; set; }
    public string? SilmeNedeni { get; set; }
}
