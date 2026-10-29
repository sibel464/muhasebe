namespace Uygulama.Dto.Muhasebe;

public class PersonelListeDto
{
    public int Id { get; set; }
    public string Kod { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public string? Gorev { get; set; }
    public string? Departman { get; set; }
    public DateTime IseGirisTarihi { get; set; }
    public DateTime? IstenCikisTarihi { get; set; }
    public decimal NetUcret { get; set; }
    public string? Iban { get; set; }
    // Tahakkuk edilmiş ama ödenmemiş net ücret (335 alt hesabı).
    public decimal OdenecekBakiye { get; set; }
    // Verilmiş, henüz maaştan düşülmemiş avans (196 alt hesabı).
    public decimal AvansBakiye { get; set; }
    public string Durum => IstenCikisTarihi != null ? $"Ayrıldı ({IstenCikisTarihi:dd.MM.yyyy})" : "Çalışıyor";
}

public class MaasTahakkukSatiriDto
{
    public int PersonelId { get; set; }
    public string Kod { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public string GiderHesapKodu { get; set; } = "770";
    public decimal NetUcret { get; set; }
    public decimal SgkIsciPayi { get; set; }
    public decimal IssizlikIsciPayi { get; set; }
    public decimal GelirVergisi { get; set; }
    public decimal DamgaVergisi { get; set; }
    public decimal SgkIsverenPayi { get; set; }
    public decimal IssizlikIsverenPayi { get; set; }
    public decimal AvansKesintisi { get; set; }
    public decimal AvansBakiye { get; set; }
    public decimal BrutUcret => NetUcret + SgkIsciPayi + IssizlikIsciPayi + GelirVergisi + DamgaVergisi;
    public decimal IsverenMaliyeti => BrutUcret + SgkIsverenPayi + IssizlikIsverenPayi;
    public decimal OdenecekNet => NetUcret - AvansKesintisi;
}

public class MaasDonemDto
{
    public int Yil { get; set; }
    public int Ay { get; set; }
    // Bu dönem için henüz tahakkuk edilmemiş çalışanlar (kartlarındaki tutarlarla önerilir).
    public List<MaasTahakkukSatiriDto> Bekleyenler { get; set; } = new();
    // Bu dönemde tahakkuk edilmiş satırlar.
    public List<MaasTahakkukSatiriDto> Yapilanlar { get; set; } = new();
    public List<string> FisNolari { get; set; } = new();
    public List<string> Engeller { get; set; } = new();
}

public class PersonelEkstreSatiriDto
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public string Islem { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public string Hesap { get; set; } = "";
    // Personele borç (tahakkuk edilen net) ve ödenen / avans.
    public decimal Tahakkuk { get; set; }
    public decimal Odenen { get; set; }
    public decimal Bakiye { get; set; }
    public bool IptalEdilebilir { get; set; }
}

public class OdemeHesabiDto
{
    public string Kaynak { get; set; } = "";
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public decimal Bakiye { get; set; }
    public override string ToString() => $"{Kaynak}: {Ad} ({Bakiye:N2} TL)";
}

public class VergiSgkBakiyeDto
{
    // 360: gelir vergisi, damga vergisi, ödenecek KDV, tevkifat. 361: SGK primleri ve işsizlik sigortası.
    public decimal OdenecekVergi { get; set; }
    public decimal OdenecekSgk { get; set; }
}
