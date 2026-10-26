using Uygulama.Dto.Muhasebe;

namespace Uygulama.Arayuzler;

public interface IYilSonuServisi
{
    Task<YilSonuOnizlemeDto> OnizleAsync(int yil, Guid sirketId = default);
    Task<YilSonuSonucDto> KapatAsync(int yil, Guid sirketId = default);
    Task GeriAlAsync(int yil, Guid sirketId = default);
}
