using EtkiAlani.Modeller.Finans;

namespace Uygulama.Arayuzler;

public record CekSenetEkleTalebi(
    CekSenetTuru Tur,
    string BelgeNo,
    DateTime VadeTarihi,
    DateTime DuzenlemeTarihi,
    decimal Tutar,
    int CariKartId,
    string Kesideci,
    string? BankaAdi = null,
    string? SubeAdi = null,
    string? HesapNo = null,
    string? Aciklama = null,
    int? ParaBirimiId = null,
    decimal Kur = 1,
    decimal DovizTutar = 0);

public interface ICekSenetServisi
{
    Task<CekSenet> EkleAsync(CekSenetEkleTalebi talep);
    Task<IReadOnlyList<CekSenet>> TumunuGetirAsync(CekSenetTuru? tur = null, CekSenetDurumu? durum = null);
    Task DurumGuncelleAsync(int cekSenetId, CekSenetDurumu yeniDurum, int? kasaId = null, int? bankaHesabiId = null, string? aciklama = null);
    Task SilAsync(int id, string neden);
}
