using EtkiAlani.Modeller.Ortak;
using System.Collections.Generic;

namespace EtkiAlani.Modeller.Finans;

public enum BankaHesapTuru
{
    VadesizTL,
    VadesizDoviz,
    POSHesabi,
    KrediHesabi,
    VadeliMevduat
}

public class BankaHesabi : TemelVarlik
{
    public Guid? SirketId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty; // Örn: Ziraat Bankası Ticari
    public BankaHesapTuru HesapTuru { get; set; } = BankaHesapTuru.VadesizTL;
    public string Sube { get; set; } = string.Empty;
    public string HesapNo { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string ParaBirimi { get; set; } = "TRY";
    public decimal Bakiye { get; set; } = 0;
    public decimal BlokeBakiye { get; set; } = 0; // POS hesabı blokeli bakiyesi
    public decimal PosKomisyonOrani { get; set; } = 0; // Örn: %1.85
    public int ValorGunu { get; set; } = 1; // POS hesabı blokeli kalma gün sayısı
    public decimal KrediLimiti { get; set; } = 0;
    public string MuhasebeKodu { get; set; } = "102.01.001"; // Tek Düzen Hesap Kodu
    public bool AktifMi { get; set; } = true;
    
    public ICollection<BankaHareketi> Hareketler { get; set; } = new List<BankaHareketi>();
}

