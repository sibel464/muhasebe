using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Ayar;

public class SistemAyarlari : TemelVarlik
{
    public string FirmaUnvani { get; set; } = string.Empty;
    public string? VergiDairesi { get; set; }
    public string? VergiNo { get; set; }
    public string? MersisNo { get; set; }
    public string? Telefon { get; set; }
    public string? Eposta { get; set; }
    public string? WebSitesi { get; set; }
    public string? Adres { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public string? PostaKodu { get; set; }

    // e-Fatura / e-Arşiv
    public string? EBelgeKlasoru { get; set; }                    // Boşsa Belgelerim\MuhasebeYonetim\e-Belge
    public EtkiAlani.Modeller.Fatura.EFaturaSenaryosu EFaturaVarsayilanSenaryo { get; set; } = EtkiAlani.Modeller.Fatura.EFaturaSenaryosu.Temel;
    // Gönderim yolu: 0 = XML dosyası (portala elle yükleme), 1 = Uyumsoft web servisi (doğrudan gönderim).
    public int EBelgeEntegrator { get; set; }
    public string? EBelgeKullanici { get; set; }
    public string? EBelgeSifre { get; set; }                    // Şifrelenmiş saklanır (SmtpSifreleyici)
    public bool EBelgeTestOrtami { get; set; }
    // E-posta (SMTP) gönderimi; şifre SmtpSifre alanında şifrelenmiş saklanır.
    public string? SmtpSunucu { get; set; }
    public int SmtpPort { get; set; } = 587;
    public int SmtpGuvenlik { get; set; }                       // 0 otomatik, 1 SSL (465), 2 STARTTLS (587), 3 yok
    public string? SmtpKullanici { get; set; }
    public string? SmtpSifre { get; set; }
    public string? EpostaGonderenAdi { get; set; }
    public string? FisHeader { get; set; }
    public string? FisFooter { get; set; } = "Bizi tercih ettiğiniz için teşekkür ederiz!";
    public decimal VarsayilanKdv { get; set; } = 20.00m;
    public string ParaBirimiSembolu { get; set; } = "₺";
    public string ParaBirimiKodu { get; set; } = "TRY";
    public int VarsayilanVadeGunu { get; set; } = 30;
    public string? FaturaNotu { get; set; } = "Mal tesliminde faturanın ibrazı zorunludur.";
    public string? IrsaliyeNotu { get; set; } = "İrsaliye muhteviyatı ürünler eksiksiz teslim alınmıştır.";
}
