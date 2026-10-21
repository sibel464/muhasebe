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

// Tedarikçiden gelen e-Fatura XML'inin aktarım önizlemesi. Hatalar varsa aktarım yapılmaz; uyarılar bilgi amaçlıdır.
public sealed record GelenFaturaOnizleme(
    string FaturaNo,
    Guid Ettn,
    DateTime Tarih,
    string TurAciklamasi,
    string SaticiUnvan,
    string SaticiVknTckn,
    string? EslesenCari,
    decimal OdenecekTutar,
    IReadOnlyList<GelenFaturaSatirOnizleme> Satirlar,
    IReadOnlyList<string> Uyarilar,
    IReadOnlyList<string> Hatalar)
{
    public bool Aktarilabilir => Hatalar.Count == 0;
}

public sealed record GelenFaturaSatirOnizleme(
    int SiraNo, string UrunAdi, string? SaticiKodu, decimal Miktar, string Birim, decimal BirimFiyat, decimal IskontoOrani,
    decimal KdvOrani, decimal Tutar, string EslesenUrun);

public sealed record EBelgeTopluSonuc(int FaturaId, string FaturaNo, string CariUnvan, bool Basarili, string Mesaj);

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

    // Tarih aralığındaki, e-belgesi hazırlanmamış ya da hatalı onaylı satış faturaları ve alış iadeleri tek tek hazırlanır;
    // biri hatalı olsa da diğerleri devam eder.
    Task<GelenFaturaOnizleme> GelenFaturaOnizleAsync(string xml, CancellationToken cancellationToken = default);

    // Gelen faturayı taslak alış faturası (müşteri iadesiyse taslak satış iadesi) olarak kaydeder. Tedarikçi cari kartı ve
    // eşleşmeyen ürünler için yeni kart açılır. Aynı ETTN ikinci kez aktarılamaz. Fatura onaylanınca stok/cari işlenir.
    Task<Fatura> GelenFaturaAktarAsync(string xml, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EBelgeTopluSonuc>> TopluHazirlaAsync(DateTime baslangic, DateTime bitis, CancellationToken cancellationToken = default);
}
