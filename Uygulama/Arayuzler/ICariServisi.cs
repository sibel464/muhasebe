using EtkiAlani.Modeller.Cari;

namespace Uygulama.Arayuzler;

public interface ICariServisi
{
    Task<List<CariKart>> TumunuGetirAsync(CariTuru? turFilter = null);
    Task<CariKart?> IdIleGetirAsync(int id);
    Task<CariKart?> KodIleGetirAsync(string kod);
    Task<CariKart> EkleAsync(CariKart cari);
    Task<CariKart> GuncelleAsync(CariKart cari);
    Task<bool> SilAsync(int id);
    Task<List<CariKart>> AraAsync(string aramaMetni, CariTuru? turFilter = null);
    Task<bool> KodMevcutMuAsync(string kod, int? haricId = null);
    Task<bool> IslemGormusMuAsync(int id);
}
