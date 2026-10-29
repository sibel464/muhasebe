using EtkiAlani.Modeller.Muhasebe;
using Uygulama.Dto.Muhasebe;

namespace Uygulama.Arayuzler;

public interface IPersonelServisi
{
    Task<List<PersonelListeDto>> ListeAsync(bool ayrilanlarDahil = false, Guid sirketId = default);
    Task<Personel?> GetirAsync(int id, Guid sirketId = default);
    Task<Personel> KaydetAsync(Personel personel, Guid sirketId = default);
    Task SilAsync(int id, Guid sirketId = default);

    Task<MaasDonemDto> DonemAsync(int yil, int ay, Guid sirketId = default);
    Task<string> TahakkukEtAsync(int yil, int ay, IReadOnlyList<MaasTahakkukSatiriDto> satirlar, Guid sirketId = default);
    Task TahakkukGeriAlAsync(int yil, int ay, Guid sirketId = default);

    Task<List<OdemeHesabiDto>> OdemeHesaplariAsync(Guid sirketId = default);
    Task<int> OdemeYapAsync(int personelId, decimal tutar, DateTime tarih, string kaynak, int hesapId, string? aciklama = null, Guid sirketId = default);
    Task<int> TopluOdemeAsync(DateTime tarih, string kaynak, int hesapId, Guid sirketId = default);
    Task<int> AvansVerAsync(int personelId, decimal tutar, DateTime tarih, string kaynak, int hesapId, string? aciklama = null, Guid sirketId = default);
    Task HareketIptalAsync(int personelHareketiId, string neden, Guid sirketId = default);
    Task<List<PersonelEkstreSatiriDto>> EkstreAsync(int personelId, Guid sirketId = default);

    Task<VergiSgkBakiyeDto> VergiSgkBakiyeAsync(Guid sirketId = default);
    Task VergiSgkOdeAsync(string hesapKodu, decimal tutar, DateTime tarih, string kaynak, int hesapId, string? aciklama = null, Guid sirketId = default);
}
