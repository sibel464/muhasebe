using Uygulama.Dto.Muhasebe;

namespace Uygulama.Arayuzler;

public interface IHesapPlaniServisi
{
    Task<List<HesapPlaniDto>> GetirTumHesaplarAsync(Guid sirketId);
    Task<List<HesapPlaniDto>> GetirHiyerarşikHesaplarAsync(Guid sirketId);
    Task<HesapPlaniDto?> GetirHesapByIdAsync(int id, Guid sirketId);
    Task<HesapPlaniDto?> GetirHesapByKodAsync(string hesapKodu, Guid sirketId);
    Task<Hesap360Dto> GetirHesap360Async(int hesapId, Guid sirketId, DateTime? baslangic = null, DateTime? bitis = null);
    Task<HesapPlaniDto> EkleHesapAsync(HesapPlaniDto dto, Guid sirketId);
    Task GuncelleHesapAsync(HesapPlaniDto dto, Guid sirketId);
    Task SilVeyaPasifeAlAsync(int hesapId, Guid sirketId);

    Task<List<HesapPlaniDto>> HesapPlaniGetirAsync(string aramaMetni = "", bool sadeceAktifler = true, Guid sirketId = default);
    Task<Hesap360Dto> Hesap360GetirAsync(int hesapId, DateTime? baslangic = null, DateTime? bitis = null, Guid sirketId = default);
    Task<bool> HesapDurumDegistirAsync(int hesapId, bool aktifMi, Guid sirketId = default);
}
