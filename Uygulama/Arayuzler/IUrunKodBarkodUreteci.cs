namespace Uygulama.Arayuzler;

public interface IUrunKodBarkodUreteci
{
    Task<string> SonrakiKodUretAsync(CancellationToken cancellationToken = default);
    Task<string> SonrakiBarkodUretAsync(CancellationToken cancellationToken = default);
    bool Ean13BarkodGecerliMi(string barkod);
}
