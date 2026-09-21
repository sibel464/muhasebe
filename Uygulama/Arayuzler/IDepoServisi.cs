using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using Uygulama.Dto.Depo;

namespace Uygulama.Arayuzler;

public interface IDepoServisi
{
    Task<List<Depo>> DepolariGetirAsync();
    Task<Depo?> DepoGetirByIdAsync(int id);
    Task<Depo> DepoEkleAsync(Depo depo);
    Task DepoGuncelleAsync(Depo depo);
    Task<bool> DepoSilAsync(int id);

    Task<List<DepoStokMatrisDto>> DepoStokMatrisiGetirAsync();
    Task<List<DepoHareketDetayDto>> DepoHareketleriGetirAsync(int? depoId = null, int? urunId = null, DateTime? baslangic = null, DateTime? bitis = null);

    Task<DepoTransferFisi> TransferFisiKaydetAsync(DepoTransferFisi transferFisi);
    Task<List<DepoTransferFisi>> TransferFisleriGetirAsync(int? depoId = null);

    Task DepoSayimFarkiKaydetAsync(int depoId, int urunId, decimal sayilanMiktar, string aciklama);
    Task<decimal> DepoToplamStokDegeriHesaplaAsync(int? depoId = null);
}
