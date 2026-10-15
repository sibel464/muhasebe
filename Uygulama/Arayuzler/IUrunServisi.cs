using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Kurumsal;

namespace Uygulama.Arayuzler;

public interface IUrunServisi
{
    Task<List<Urun>> TumunuGetirAsync();
    Task<Urun?> IdIleGetirAsync(int id);
    Task<Urun?> KodIleGetirAsync(string kod);
    Task<Urun> EkleAsync(Urun urun);
    Task<Urun> GuncelleAsync(Urun urun);
    Task<Urun> KartGuncelleAsync(Urun urun, List<UrunSatisFiyati> fiyatlar);
    Task<bool> SilAsync(int id, string? silmeNedeni = null);
    Task<List<Urun>> SilinenleriGetirAsync();
    Task GeriAlAsync(int id);
    Task<List<Urun>> AraAsync(string aramaMetni);
    Task<List<Urun>> FiltreliAraAsync(UrunFiltre filtre);
    Task<bool> KodMevcutMuAsync(string kod, int? haricId = null);
    Task<bool> BarkodMevcutMuAsync(string barkod, int? haricId = null);
    Task<Urun?> BarkodIleGetirAsync(string barkod);
    Task<string> OtomatikKodOnizleAsync();
    Task<string> OtomatikBarkodOnizleAsync();
    Task<Urun> GuvenliEkleAsync(Urun urun, bool otomatikKod, bool otomatikBarkod);
    Task<List<string>> KullanilanKodlariGetirAsync();
    Task<List<string>> KullanilanBarkodlariGetirAsync();

    // ── Taksitli Fiyat Yönetimi ──
    Task<List<UrunSatisFiyati>> FiyatlariGetirAsync(int urunId);
    Task FiyatlariKaydetAsync(int urunId, List<UrunSatisFiyati> fiyatlar);

    // ── Lokasyon Stok Yönetimi ── (stok yalnızca stok fişleriyle değişir; kartta açılış stoğu dışında yazılmaz)
    Task<List<UrunLokasyonStogu>> LokasyonStoklariniGetirAsync(int urunId);

    // ── Depo Listesi ──
    Task<List<Depo>> DepolariGetirAsync();
}
