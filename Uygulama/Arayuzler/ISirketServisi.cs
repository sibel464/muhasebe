using EtkiAlani.Modeller.Kurumsal;

namespace Uygulama.Arayuzler;

public interface ISirketServisi
{
    Task<List<Sirket>> ListeleAsync(string? arama = null, bool? aktif = null);
    Task<string> SonrakiKodAsync();
    Task<Sirket> KaydetAsync(Sirket sirket, bool otomatikKod);
    Task DurumDegistirAsync(Guid id, bool aktif);
    Task<List<Sube>> SubeleriGetirAsync(Guid sirketId);
    Task<List<MaliDonem>> DonemleriGetirAsync(Guid sirketId);
    Task<Sube> SubeKaydetAsync(Sube sube);
    Task SubeDurumDegistirAsync(int subeId, bool aktif);
    Task<MaliDonem> MaliDonemKaydetAsync(MaliDonem donem);
    Task MaliDonemKilitleAsync(int donemId, bool kilitli);
    Task<List<BelgeSerisi>> BelgeSerileriniGetirAsync(Guid sirketId);
    Task<BelgeSerisi> BelgeSerisiKaydetAsync(BelgeSerisi seri);
    Task<string> SonrakiBelgeNoAsync(int belgeSerisiId);
}
