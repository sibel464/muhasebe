using EtkiAlani.Modeller.Irsaliye;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Stok;

namespace Uygulama.Arayuzler;

public interface IIrsaliyeServisi
{
    Task<List<Irsaliye>> TumunuGetirAsync(IrsaliyeTuru? tur = null, IrsaliyeDurumu? durum = null);
    Task<Irsaliye?> IdIleGetirAsync(int id);
    Task<Irsaliye?> IrsaliyeNoIleGetirAsync(string irsaliyeNo);
    Task<Irsaliye> EkleAsync(Irsaliye irsaliye);
    Task<Irsaliye> GuncelleAsync(Irsaliye irsaliye);
    Task<bool> OnaylaAsync(int irsaliyeId);
    Task<bool> IptalEtAsync(int irsaliyeId);
    Task<bool> SilAsync(int irsaliyeId);
    Task<Fatura> FaturayaDonusturAsync(int irsaliyeId);
    Task<string> SiraliIrsaliyeNoUretAsync(IrsaliyeTuru tur, DateTime tarih);
    Task<bool> IrsaliyeNoMevcutMuAsync(string irsaliyeNo, int? haricId = null);
    Task<Irsaliye> SiparistenSevkEtAsync(Irsaliye irsaliye);
    Task<List<Irsaliye>> SiparisSevkiyatlariniGetirAsync(int siparisId);
    Task<List<UrunLokasyonStogu>> LokasyonStoklariniGetirAsync(IEnumerable<int> urunIds);
}
