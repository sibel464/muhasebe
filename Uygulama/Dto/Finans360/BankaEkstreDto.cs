namespace Uygulama.Dto.Finans360;

// Banka ekstresinden okunan bir satır. Tutar işaretlidir: + hesaba giriş, - hesaptan çıkış.
public class EkstreSatiriDto
{
    public int SatirNo { get; set; }
    public DateTime Tarih { get; set; }
    public string Aciklama { get; set; } = "";
    public decimal Tutar { get; set; }
    public decimal? Bakiye { get; set; }
    public string? DekontNo { get; set; }
}

public class EkstreOkumaSonucuDto
{
    public List<EkstreSatiriDto> Satirlar { get; set; } = new();
    public string SutunBilgisi { get; set; } = "";
    public List<string> Uyarilar { get; set; } = new();
}

public enum EkstreIslemTuru
{
    Atla = 0,
    Tahsilat = 1,       // cariden gelen havale / EFT
    Odeme = 2,          // cariye giden havale / EFT
    BankaMasrafi = 3,   // 780
    FaizGeliri = 4,     // 642
    KrediOdemesi = 5    // 300
}

public class EkstreEslesmeDto
{
    public EkstreSatiriDto Satir { get; set; } = new();
    public EkstreIslemTuru Islem { get; set; }
    public int? CariId { get; set; }
    public string CariUnvan { get; set; } = "";
    public bool ZatenKayitli { get; set; }
    public string Durum { get; set; } = "";
    public bool Secili { get; set; }
    public string Sonuc { get; set; } = "";
}

public class EkstreKayitSonucuDto
{
    public int Kaydedilen { get; set; }
    public int Hatali { get; set; }
    public List<string> Hatalar { get; set; } = new();
}
