using EtkiAlani.Modeller.Finans;

namespace Uygulama.Arayuzler;

public record AcikBelge(int CariHareketId, DateTime Tarih, string Aciklama, string? BelgeNo, decimal ToplamTutar, decimal AcikTutar, decimal DovizAcikTutar, bool BorcMu);

public interface IBelgeKapamaServisi
{
    Task<IReadOnlyList<AcikBelge>> AcikBelgeleriGetirAsync(int cariKartId);
    Task<BelgeKapama> KapatAsync(int odenenCariHareketId, int faturaCariHareketId, decimal tutar, decimal dovizTutar = 0);
    Task IptalEtAsync(int kapamaId);
}
