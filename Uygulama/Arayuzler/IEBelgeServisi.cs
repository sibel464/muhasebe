using EtkiAlani.Modeller.Fatura;

namespace Uygulama.Arayuzler;

// Hazırlanan belgenin entegratöre iletilmesi. Entegratör seçilince (ör. özel entegratör web servisi) bu arayüzün bir
// uygulaması eklenir; diğer her şey (doğrulama, UBL-TR üretimi, durum takibi) aynı kalır.
public interface IEBelgeEntegratoru
{
    string Ad { get; }

    // true: belge gerçekten GİB'e iletilir (durum "Gönderildi" olur). false: yalnız dosya üretir, kullanıcı portala yükler.
    bool DogrudanGonderir { get; }

    Task<EBelgeGonderimSonucu> GonderAsync(EBelgePaketi paket, CancellationToken cancellationToken = default);
}

public sealed record EBelgePaketi(string FaturaNo, Guid Ettn, EBelgeTuru Tur, string AliciVknTckn, string? AliciEtiketi, string Xml);

public sealed record EBelgeGonderimSonucu(bool Basarili, EBelgeDurumu Durum, string? Mesaj = null, string? DosyaYolu = null);

public sealed record EBelgeOnKontrol(
    int FaturaId,
    string FaturaNo,
    EBelgeTuru Tur,
    EFaturaSenaryosu Senaryo,
    EBelgeDurumu MevcutDurum,
    string AliciUnvan,
    string AliciVknTckn,
    bool IadeBilgisiGerekli,
    string? IadeEdilenFaturaNo,
    DateTime? IadeEdilenFaturaTarihi,
    string? SonMesaj,
    IReadOnlyList<string> Hatalar)
{
    public bool Hazirlanabilir => Hatalar.Count == 0;
}

public sealed record EBelgeHazirlamaIstegi(int FaturaId, EFaturaSenaryosu? Senaryo = null, string? IadeEdilenFaturaNo = null, DateTime? IadeEdilenFaturaTarihi = null);

public interface IEBelgeServisi
{
    string EntegratorAdi { get; }
    bool DogrudanGonderir { get; }

    // Hazırlamadan önce kontrol; istekteki senaryo ve iade bilgisi kaydedilmeden denetime katılır.
    Task<EBelgeOnKontrol> OnKontrolAsync(EBelgeHazirlamaIstegi istek, CancellationToken cancellationToken = default);

    // Belgeyi doğrular, ETTN verir, UBL-TR XML üretir ve entegratöre iletir (dosya modunda klasöre yazar).
    Task<EBelgeGonderimSonucu> HazirlaVeGonderAsync(EBelgeHazirlamaIstegi istek, CancellationToken cancellationToken = default);

    // Dosya modunda portala yüklenen belgenin durumunu kullanıcı işaretler (gönderildi, kabul, red, iptal).
    Task DurumIsaretleAsync(int faturaId, EBelgeDurumu durum, string? aciklama = null, CancellationToken cancellationToken = default);

    Task<string?> SonXmlGetirAsync(int faturaId, CancellationToken cancellationToken = default);
}
