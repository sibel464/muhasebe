using Uygulama.Dto.Finans360;

namespace Uygulama.Arayuzler;

public interface IFinansMerkeziServisi
{
    Task<FinansDashboardOzetDto> GetDashboardOzetAsync(Guid sirketId, CancellationToken cancellationToken = default);
    Task<List<KasaOzetDto>> GetKasalarAsync(Guid sirketId, CancellationToken cancellationToken = default);
    Task<List<BankaHesapOzetDto>> GetBankaHesaplariAsync(Guid sirketId, CancellationToken cancellationToken = default);
    Task<List<YuruyenFinansSatirDto>> GetKasaHareketleri360Async(int kasaId, Guid sirketId, CancellationToken cancellationToken = default);
    Task<List<YuruyenFinansSatirDto>> GetBankaHareketleri360Async(int bankaHesapId, Guid sirketId, CancellationToken cancellationToken = default);
    Task<List<PosIslemOzetDto>> GetPosIslemleriAsync(Guid sirketId, CancellationToken cancellationToken = default);
    Task PosIslemiHesabaGecirAsync(int posIslemId, Guid sirketId, int kullaniciId, CancellationToken cancellationToken = default);
    Task<List<VadeKalemOzetDto>> GetVadeMerkeziAsync(Guid sirketId, CancellationToken cancellationToken = default);
    Task KasaMutabakatKaydetAsync(KasaMutabakatKayitDto dto, Guid sirketId, int kullaniciId, CancellationToken cancellationToken = default);
    Task KasaBankaTransferiYapAsync(KasaBankaTransferIstegiDto dto, Guid sirketId, int kullaniciId, CancellationToken cancellationToken = default);
    Task BankaKasaTransferiYapAsync(BankaKasaTransferIstegiDto dto, Guid sirketId, int kullaniciId, CancellationToken cancellationToken = default);
    Task BankaVirmaniYapAsync(BankaVirmanIstegiDto dto, Guid sirketId, int kullaniciId, CancellationToken cancellationToken = default);
    Task HesapTransferiYapAsync(HesapTransferIstegiDto dto, Guid sirketId, int kullaniciId, CancellationToken cancellationToken = default);
    Task FinansIslemIptalEtAsync(int hareketId, string kaynak, Guid sirketId, int kullaniciId, string neden, CancellationToken cancellationToken = default);
    Task<SayfaliFinansHareketDto> GetGlobalFinansHareketleriAsync(Guid sirketId, GlobalFinansFiltreDto filtre, CancellationToken cancellationToken = default);
}
