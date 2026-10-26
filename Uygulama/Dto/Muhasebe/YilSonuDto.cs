namespace Uygulama.Dto.Muhasebe;

// Yıl sonu kapanışının önizlemesi: kapatılacak tutarlar, engeller (kapanışı durduran) ve uyarılar.
public class YilSonuOnizlemeDto
{
    public int Yil { get; set; }
    public bool Kapatildi { get; set; }
    public decimal GelirToplami { get; set; }
    public decimal GiderToplami { get; set; }
    public decimal DonemKari => GelirToplami - GiderToplami;
    public decimal VergiKarsiligi { get; set; }
    public decimal NetKar => DonemKari - VergiKarsiligi;
    public List<YansitmaKalemiDto> Yansitmalar { get; set; } = new();
    public int BilancoHesapSayisi { get; set; }
    public List<string> Engeller { get; set; } = new();
    public List<string> Uyarilar { get; set; } = new();
    public bool KapatilabilirMi => !Kapatildi && Engeller.Count == 0;
}

public class YansitmaKalemiDto
{
    public string GiderHesabi { get; set; } = "";
    public string YansitmaHesabi { get; set; } = "";
    public string HedefHesap { get; set; } = "";
    public decimal Tutar { get; set; }
}

public class YilSonuSonucDto
{
    public int Yil { get; set; }
    public List<string> FisNolari { get; set; } = new();
    public decimal NetKar { get; set; }
}
