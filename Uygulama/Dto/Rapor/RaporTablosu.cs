namespace Uygulama.Dto.Rapor;

// Excel / PDF / yazdırma çıktısının ortak biçimi: ekranda görünen başlıklar ve hücre metinleri.
// Sayı hücreleri Excel'e sayı olarak yazılsın diye Sayi ayrıca taşınır.
public sealed class RaporTablosu
{
    public string Baslik { get; set; } = "";
    public string AltBaslik { get; set; } = "";
    public List<RaporKolonu> Kolonlar { get; set; } = new();
    public List<RaporHucresi[]> Satirlar { get; set; } = new();
}

public sealed record RaporKolonu(string Baslik, bool Sayisal = false, bool Ondalikli = true);

public sealed record RaporHucresi(string Metin, decimal? Sayi = null);
