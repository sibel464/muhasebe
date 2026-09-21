using EtkiAlani.Modeller.Ortak;
using Uygulama.Dto.Doviz;

namespace Uygulama.Arayuzler;

public interface IRevaluasyonServisi
{
    Task<RevaluasyonOnizleDto> RevaluasyonOnizleAsync(Guid sirketId, string paraBirimiKodu, DateTime? degerlemeTarihi = null);
    Task<RevaluasyonKaydiDto> RevaluasyonUygulaAsync(RevaluasyonOnizleDto dto, int kullaniciId = 0);
    Task<RevaluasyonKaydiDto> RevaluasyonIptalEtAsync(int revaluasyonKaydiId, string iptalNedeni, Guid sirketId = default);
    Task<SayfaliSonuc<RevaluasyonKaydiDto>> GetirRevaluasyonGecmisiAsync(Guid sirketId, int sayfa = 1, int sayfaBoyutu = 20);
    Task<List<DovizliBakiyeOzetDto>> GetirDovizliBakiyeOzetleriAsync(Guid sirketId);
}
