using Uygulama.Dto.Stok360;

namespace Uygulama.Arayuzler;

public interface IStok360Servisi
{
    Task<UrunStokOzetDto?> GetUrunOzetAsync(int urunId, Guid sirketId, CancellationToken cancellationToken = default);
    Task<List<LokasyonStokOzetDto>> GetLokasyonStoklariAsync(int urunId, Guid sirketId, CancellationToken cancellationToken = default);
    Task<List<YuruyenStokSatirDto>> GetYuruyenStokHareketleriAsync(int urunId, Guid sirketId, UrunStok360Sorgu sorgu, CancellationToken cancellationToken = default);
    Task<List<UrunSatisGecmisDto>> GetSatisGecmisiAsync(int urunId, Guid sirketId, UrunStok360Sorgu sorgu, CancellationToken cancellationToken = default);
    Task<List<UrunAlisGecmisDto>> GetAlisGecmisiAsync(int urunId, Guid sirketId, UrunStok360Sorgu sorgu, CancellationToken cancellationToken = default);
    Task<List<UrunSiparisRezervasyonDto>> GetSiparisRezervasyonlariAsync(int urunId, Guid sirketId, CancellationToken cancellationToken = default);
    Task<List<YuruyenStokSatirDto>> GetTransferGecmisiAsync(int urunId, Guid sirketId, CancellationToken cancellationToken = default);
    Task<List<YuruyenStokSatirDto>> GetSayimGecmisiAsync(int urunId, Guid sirketId, CancellationToken cancellationToken = default);
    Task<UrunMaliyetKarlilikDto> GetMaliyetKarlilikAsync(int urunId, Guid sirketId, CancellationToken cancellationToken = default);
}
