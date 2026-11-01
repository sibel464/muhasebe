using EtkiAlani.Modeller.Muhasebe;
using Uygulama.Dto.Muhasebe;

namespace Uygulama.Arayuzler;

public interface IGiderPusulasiServisi
{
    Task<List<GiderPusulasiListeDto>> ListeAsync(DateTime baslangic, DateTime bitis, bool iptallerDahil = true, Guid sirketId = default);
    Task<GiderPusulasi?> GetirAsync(int id, Guid sirketId = default);
    Task<List<OdemeHesabiDto>> OdemeHesaplariAsync(Guid sirketId = default);
    // Kaydeder, fişler ve kasa/bankadan öder; belge numarasını döner.
    Task<GiderPusulasi> KaydetAsync(GiderPusulasiGirisDto giris, Guid sirketId = default);
    Task IptalAsync(int id, string neden, Guid sirketId = default);
    // KVKK: yasal saklama süresi (belge yılı sonundan itibaren 10 yıl) dolan belgelerde satıcının kişisel verileri silinir; tutarlar kalır.
    Task<int> SuresiDolanlariAnonimlestirAsync(DateTime bugun, Guid sirketId = default);
}
