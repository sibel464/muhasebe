using Uygulama.Dto.Muhasebe;

namespace Uygulama.Arayuzler;

public interface IMuhasebeBaglantiServisi
{
    Task<List<MuhasebeBaglantiKoduDto>> GetirTumBaglantilarAsync(Guid sirketId = default);
    Task GuncelleBaglantiAsync(MuhasebeBaglantiKoduDto dto, Guid sirketId = default);
    Task<string> GetirHesapKoduForKategoriAsync(string kategoriKodu, Guid sirketId = default);

    Task<List<MuhasebeBaglantiKoduDto>> GetirBaglantiKodlariAsync(Guid sirketId = default);
    Task<bool> GuncelleBaglantiKoduAsync(MuhasebeBaglantiKoduDto dto, Guid sirketId = default);
}
