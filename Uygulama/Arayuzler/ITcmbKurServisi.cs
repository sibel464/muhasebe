using Uygulama.Dto.Doviz;

namespace Uygulama.Arayuzler;

public interface ITcmbKurServisi
{
    Task<List<DovizKuruDto>> GunlukKurlariGuncelleVeyaGetirAsync(DateTime? tarih = null, CancellationToken cancellationToken = default);
    Task<DovizKuruDto?> GetirKurAsync(string paraBirimiKodu, DateTime? tarih = null, CancellationToken cancellationToken = default);
}
