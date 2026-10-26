namespace Uygulama.Dto.Muhasebe;

// Bilanço / gelir tablosu satırı. Seviye 0: ana bölüm (I. DÖNEN VARLIKLAR), 1: grup (A. Hazır Değerler),
// 2: hesap (100 Kasa), 3: ara toplam / sonuç satırı (NET SATIŞLAR, AKTİF TOPLAMI).
public class MaliTabloSatiriDto
{
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public int Seviye { get; set; }
    public decimal Tutar { get; set; }
    public decimal OncekiTutar { get; set; }
    public string Girinti => new(' ', Seviye == 3 ? 0 : Seviye * 4);
    public string Baslik => Girinti + (string.IsNullOrEmpty(Kod) ? Ad : $"{Kod}  {Ad}");
}

public class MaliTabloDto
{
    public string Baslik { get; set; } = "";
    public string DonemMetni { get; set; } = "";
    public string OncekiDonemMetni { get; set; } = "";
    public List<MaliTabloSatiriDto> Satirlar { get; set; } = new();
    public List<string> Uyarilar { get; set; } = new();
}
