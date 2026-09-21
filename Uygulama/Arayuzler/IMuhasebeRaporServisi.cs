using Uygulama.Dto.Muhasebe;

namespace Uygulama.Arayuzler;

public interface IMuhasebeRaporServisi
{
    Task<MizanRaporDto> MizanAlAsync(Guid sirketId = default, DateTime? baslangic = null, DateTime? bitis = null, MizanTuru tur = MizanTuru.Genel);
    Task<MuavinRaporDto> MuavinAlAsync(int hesapId, Guid sirketId = default, DateTime? baslangic = null, DateTime? bitis = null);
    Task<List<BuyukDefterSatirDto>> BuyukDefterAlAsync(Guid sirketId = default, DateTime? baslangic = null, DateTime? bitis = null);
    Task<MuhasebeSaglikDto> SaglikPaneliGetirAsync(Guid sirketId = default);
    Task<List<MuhasebelesmemisBelgeDto>> GetirMuhasebelesmemisBelgelerAsync(Guid sirketId = default);
}
