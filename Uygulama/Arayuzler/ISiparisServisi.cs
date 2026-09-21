using EtkiAlani.Modeller.Siparis;

namespace Uygulama.Arayuzler;

public interface ISiparisServisi
{
    Task<List<Siparis>> ListeleAsync(SiparisFiltre? filtre = null);
    Task<Siparis?> GetirAsync(int id);
    Task<Siparis> EkleAsync(Siparis siparis, bool taslak);
    Task<Siparis> GuncelleAsync(Siparis siparis);
    Task<Siparis> DurumDegistirAsync(int id, SiparisDurumu yeniDurum, int versiyon);
    Task<Siparis> IptalEtAsync(int id, int versiyon);
    Task<bool> SiparisNoMevcutMuAsync(string siparisNo, int? haricId = null);
}
