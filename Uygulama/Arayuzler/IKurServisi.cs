using EtkiAlani.Modeller.Ortak;
using Uygulama.Dto.Doviz;

namespace Uygulama.Arayuzler;

public interface IKurServisi
{
    Task<List<DovizKuruDto>> GetirGunlukKurlarAsync(DateTime? tarih = null);
    Task<DovizKuruDto?> GetirGecerliKurAsync(string paraBirimiKodu, DateTime? tarih = null);
    Task<decimal> GetirIslemKuruAsync(string paraBirimiKodu, DateTime? tarih, KurTipiPolicy policy);
    Task<DovizKuruDto> KaydetManuelKurAsync(ManuelKurGirisDto dto);
    Task<SayfaliSonuc<DovizKuruDto>> GetirKurGecmisiAsync(KurFiltreDto filtre);
}
