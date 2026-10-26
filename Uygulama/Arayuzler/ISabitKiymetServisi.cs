using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Yardimcilar;
using Uygulama.Dto.Muhasebe;

namespace Uygulama.Arayuzler;

public interface ISabitKiymetServisi
{
    Task<List<SabitKiymetListeDto>> ListeAsync(bool eldenCikanlarDahil = false, Guid sirketId = default);
    Task<SabitKiymet?> GetirAsync(int id, Guid sirketId = default);
    Task<SabitKiymet> KaydetAsync(SabitKiymet kiymet, Guid sirketId = default);
    Task SilAsync(int id, Guid sirketId = default);
    Task<List<AmortismanSatiri>> PlanAsync(int id, Guid sirketId = default);
    Task<AmortismanYilOnizlemeDto> YilOnizleAsync(int yil, Guid sirketId = default);
    Task<string> AmortismanAyirAsync(int yil, Guid sirketId = default);
    Task AmortismanGeriAlAsync(int yil, Guid sirketId = default);
    Task<string> EldenCikarAsync(int id, DateTime tarih, string aciklama, Guid sirketId = default);
}
