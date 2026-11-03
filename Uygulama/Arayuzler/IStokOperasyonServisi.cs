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

    // Yarıda bırakılan sayım: "Sayımda" durumunda saklanır, sonra kaldığı yerden sürdürülür (stok değişmez).
    // sayimId verilirse o taslağın satırları gönderilenlerle değiştirilir.
    Task<StokSayimi> SayimTaslakKaydetAsync(int? sayimId, StokSayimIstegi istek, Guid sirketId, int kullaniciId);
    Task<List<StokSayimOzetiDto>> SayimlariListeleAsync(Guid sirketId, CancellationToken cancellationToken = default);
    // Satırlar ürün ve depo bilgisiyle (tutanak ve taslağa devam için).
    Task<StokSayimi?> SayimGetirAsync(int sayimId, Guid sirketId);
}
