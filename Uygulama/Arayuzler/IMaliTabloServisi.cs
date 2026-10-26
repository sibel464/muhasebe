using Uygulama.Dto.Muhasebe;

namespace Uygulama.Arayuzler;

public interface IMaliTabloServisi
{
    Task<MaliTabloDto> BilancoAsync(DateTime tarih, Guid sirketId = default);
    Task<MaliTabloDto> GelirTablosuAsync(DateTime baslangic, DateTime bitis, Guid sirketId = default);
}
