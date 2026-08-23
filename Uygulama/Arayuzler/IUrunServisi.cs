using EtkiAlani.Modeller.Stok;

namespace Uygulama.Arayuzler;

public interface IUrunServisi
{
    Task<List<Urun>> TumunuGetirAsync();
    Task<Urun?> IdIleGetirAsync(int id);
    Task<Urun?> KodIleGetirAsync(string kod);
    Task<Urun> EkleAsync(Urun urun);
    Task<Urun> GuncelleAsync(Urun urun);
    Task<bool> SilAsync(int id);
    Task<List<Urun>> AraAsync(string aramaMetni);
    Task<bool> KodMevcutMuAsync(string kod, int? haricId = null);
}
