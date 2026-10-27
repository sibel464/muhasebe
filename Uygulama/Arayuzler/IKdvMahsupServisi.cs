using Uygulama.Dto.Muhasebe;

namespace Uygulama.Arayuzler;

public interface IKdvMahsupServisi
{
    Task<List<KdvMahsupAyDto>> YilAsync(int yil, Guid sirketId = default);
    Task<KdvMahsupOnizlemeDto> OnizleAsync(int yil, int ay, Guid sirketId = default);
    Task<string> MahsupEtAsync(int yil, int ay, Guid sirketId = default);
    Task GeriAlAsync(int yil, int ay, Guid sirketId = default);
}
