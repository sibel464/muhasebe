using EtkiAlani.Modeller.Stok;
using Uygulama.Dto.StokOperasyon;

namespace Uygulama.Arayuzler;

public interface IStokOperasyonServisi
{
    Task<List<StokOperasyonMatrisOzetDto>> GetMatrisOzetAsync(Guid sirketId, string? arama = null, int? depoId = null, bool sadeceKritik = false, CancellationToken cancellationToken = default);
    Task StokGirisiYapAsync(StokGirisIstegi istek, Guid sirketId, int kullaniciId);
    Task StokCikisiYapAsync(StokCikisIstegi istek, Guid sirketId, int kullaniciId);
    Task<DepoTransferFisi> TransferOlusturAsync(StokTransferIstegi istek, Guid sirketId, int kullaniciId);
    Task TransferIptalEtAsync(int transferId, Guid sirketId, int kullaniciId, string neden);
    Task<StokSayimi> SayimOlusturAsync(StokSayimIstegi istek, Guid sirketId, int kullaniciId);
    Task SayimOnaylaAsync(int sayimId, Guid sirketId, int kullaniciId);
    Task SayimIptalEtAsync(int sayimId, Guid sirketId, int kullaniciId);
}
