using EtkiAlani.Modeller.Fatura;

namespace Uygulama.Arayuzler;

public interface IFaturaServisi
{
    Task<List<Fatura>> TumunuGetirAsync(FaturaTuru? turFilter = null);
    Task<Fatura?> IdIleGetirAsync(int id);
    // Liste ekranları için: kalemsiz, salt okunur, yeniden eskiye. Yıllar içinde on binlerce fatura birikince
    // kalemleriyle birlikte yüklemek saniyeler sürüyordu. Kalemler gerekince DetayGetirAsync.
    Task<List<Fatura>> ListeGetirAsync(FaturaTuru? turFilter = null, BelgeDurumu? durum = null);
    // Kalemleri ve depo dağılımlarıyla, veritabanından taze (izlenmeyen) okunur.
    Task<Fatura?> DetayGetirAsync(int id);
    Task<int> SayAsync(DateTime baslangic);
    Task<Fatura?> FaturaNoIleGetirAsync(string faturaNo);
    Task<Fatura> EkleAsync(Fatura fatura);
    Task<Fatura> GuncelleAsync(Fatura fatura);
    Task<bool> SilAsync(int id);
    Task<Fatura> OnaylaAsync(int id);
    // Onay + peşin ödemenin (nakit/kart/banka) kasa/banka ve cari kaydı tek işlemde.
    Task<Fatura> OnaylaVeOdemeyiKaydetAsync(int id);
    Task<Fatura> PosSatisiniTamamlaAsync(Fatura fatura, string odemeYontemi, int? bekleyenFaturaId = null);
    Task<Fatura> IptalEtAsync(int id, string? neden = null);
    Task<List<Fatura>> AraAsync(string aramaMetni, FaturaTuru? turFilter = null);
    Task<bool> FaturaNoMevcutMuAsync(string faturaNo, int? haricId = null);
    Task<Fatura> KaynaktanFaturaOlusturAsync(Fatura fatura);
    Task<List<Fatura>> SiparisFaturalariniGetirAsync(int siparisId);
    Task<List<Fatura>> IrsaliyeFaturalariniGetirAsync(int irsaliyeId);
}
