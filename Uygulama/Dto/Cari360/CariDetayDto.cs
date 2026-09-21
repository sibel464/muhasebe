using System;

namespace Uygulama.Dto.Cari360;

public class CariDetayDto
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string CariTipi { get; set; } = string.Empty;
    public string GrupKodu { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Soyad { get; set; } = string.Empty;
    public string FirmaUnvani { get; set; } = string.Empty;
    public string YetkiliKisi { get; set; } = string.Empty;
    public string Tckn { get; set; } = string.Empty;
    public string Vkn { get; set; } = string.Empty;
    public string VergiDairesi { get; set; } = string.Empty;
    public string Telefon { get; set; } = string.Empty;
    public string Telefon2 { get; set; } = string.Empty;
    public string CepTelefonu { get; set; } = string.Empty;
    public string Eposta { get; set; } = string.Empty;
    public string Il { get; set; } = string.Empty;
    public string Ilce { get; set; } = string.Empty;
    public string AcikAdres { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public DateTime OlusturmaTarihi { get; set; }
    public bool IsKurum { get; set; }
    public string Firma { get; set; } = "";
    public string Sube { get; set; } = "";
    public string Olusturan { get; set; } = "";
    public string Guncelleyen { get; set; } = "";
    public DateTime? GuncellemeTarihi { get; set; }
    public decimal? RiskLimiti { get; set; }
    public int? VadeGunu { get; set; }
    public string Baslik => IsKurum ? FirmaUnvani : (Ad + " " + Soyad).Trim();
}
