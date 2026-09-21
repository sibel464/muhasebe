using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Ayar;

public class SistemAyarlari : TemelVarlik
{
    public string FirmaUnvani { get; set; } = "Örnek Ticaret Ltd. Şti.";
    public string? VergiDairesi { get; set; } = "Kadıköy V.D.";
    public string? VergiNo { get; set; } = "1234567890";
    public string? Telefon { get; set; } = "0216 555 00 00";
    public string? Eposta { get; set; } = "info@ornekticaret.com";
    public string? WebSitesi { get; set; } = "www.ornekticaret.com";
    public string? Adres { get; set; } = "Atatürk Cad. No: 15/A Kadıköy / İstanbul";
    public string? FisHeader { get; set; } = "Örnek Ticaret Ltd. Şti.";
    public string? FisFooter { get; set; } = "Bizi tercih ettiğiniz için teşekkür ederiz!";
    public decimal VarsayilanKdv { get; set; } = 20.00m;
    public string ParaBirimiSembolu { get; set; } = "₺";
    public string ParaBirimiKodu { get; set; } = "TRY";
    public int VarsayilanVadeGunu { get; set; } = 30;
    public string? FaturaNotu { get; set; } = "Mal tesliminde faturanın ibrazı zorunludur.";
    public string? IrsaliyeNotu { get; set; } = "İrsaliye muhteviyatı ürünler eksiksiz teslim alınmıştır.";
}
