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
    public string? FisHeader { get; set; }
    public string? FisFooter { get; set; } = "Bizi tercih ettiğiniz için teşekkür ederiz!";
    public decimal VarsayilanKdv { get; set; } = 20.00m;
    public string ParaBirimiSembolu { get; set; } = "₺";
    public string ParaBirimiKodu { get; set; } = "TRY";
    public int VarsayilanVadeGunu { get; set; } = 30;
    public string? FaturaNotu { get; set; } = "Mal tesliminde faturanın ibrazı zorunludur.";
    public string? IrsaliyeNotu { get; set; } = "İrsaliye muhteviyatı ürünler eksiksiz teslim alınmıştır.";
}
