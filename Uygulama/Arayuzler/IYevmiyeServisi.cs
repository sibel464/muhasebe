using EtkiAlani.Modeller.Ortak;
using Uygulama.Dto.Muhasebe;

namespace Uygulama.Arayuzler;

public interface IYevmiyeServisi
{
    Task<YevmiyeFisDto> FisOlusturAsync(YevmiyeFisDto dto, Guid sirketId = default, int kullaniciId = 1);
    Task<YevmiyeFisDto> FisOnaylaAsync(int fisId, Guid sirketId = default);
    Task<YevmiyeFisDto> FisIptalEtAsync(int fisId, string iptalNedeni, Guid sirketId = default);
    Task<YevmiyeFisDto?> FisIptalEtTersKayitUretAsync(int fisId, string iptalNedeni, Guid sirketId = default);
    Task<YevmiyeFisDto?> GetirFisByIdAsync(int fisId, Guid sirketId = default);
    Task<SayfaliSonuc<YevmiyeSatiriDto>> YevmiyeDefteriSorgulaAsync(YevmiyeFiltreDto filtre);
    Task<SayfaliSonuc<YevmiyeFisDto>> FisleriGetirAsync(YevmiyeFiltreDto filtre);
    Task<YevmiyeFisDto?> GetirBelgeFisiAsync(string belgeTipi, int belgeId, Guid sirketId = default);
    Task<YevmiyeFisDto> OtomatikMuhasebelestirAsync(string belgeTipi, int belgeId, Guid sirketId = default, int kullaniciId = 1);
}
