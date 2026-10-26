namespace Uygulama.Arayuzler;

public sealed record EpostaEki(string DosyaAdi, byte[] Icerik, string IcerikTuru = "application/pdf");

public sealed class EpostaMesaji
{
    public string Alicilar { get; set; } = "";
    public string Konu { get; set; } = "";
    public string Metin { get; set; } = "";
    public List<EpostaEki> Ekler { get; set; } = new();
}

public interface IEpostaServisi
{
    Task<bool> YapilandirildiMiAsync();
    Task GonderAsync(EpostaMesaji mesaj, CancellationToken iptal = default);
    Task<byte[]> HtmldenPdfAsync(string html, CancellationToken iptal = default);
}
