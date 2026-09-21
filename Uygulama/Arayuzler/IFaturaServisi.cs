using EtkiAlani.Modeller.Fatura;

namespace Uygulama.Arayuzler;

public interface IFaturaServisi
{
    Task<List<Fatura>> TumunuGetirAsync(FaturaTuru? turFilter = null);
    Task<Fatura?> IdIleGetirAsync(int id);
    Task<Fatura?> FaturaNoIleGetirAsync(string faturaNo);
    Task<Fatura> EkleAsync(Fatura fatura);
    Task<Fatura> GuncelleAsync(Fatura fatura);
    Task<bool> SilAsync(int id);
    Task<Fatura> OnaylaAsync(int id);
    Task<Fatura> PosSatisiniTamamlaAsync(Fatura fatura, string odemeYontemi, int? bekleyenFaturaId = null);
    Task<Fatura> IptalEtAsync(int id);
    Task<List<Fatura>> AraAsync(string aramaMetni, FaturaTuru? turFilter = null);
    Task<bool> FaturaNoMevcutMuAsync(string faturaNo, int? haricId = null);
    Task<Fatura> KaynaktanFaturaOlusturAsync(Fatura fatura);
    Task<List<Fatura>> SiparisFaturalariniGetirAsync(int siparisId);
    Task<List<Fatura>> IrsaliyeFaturalariniGetirAsync(int irsaliyeId);
}
