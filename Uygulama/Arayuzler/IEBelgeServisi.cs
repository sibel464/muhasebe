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

// Web servisiyle doğrudan çalışan entegratörlerin ek yetenekleri (durum sorgulama, alıcı sorgulama, gelen kutusu).
public interface IEBelgeCevrimiciEntegrator : IEBelgeEntegratoru
{
    // Kullanıcı adı/şifre doğruysa entegratördeki firma unvanı ve vergi no'su döner.
    Task<EntegratorHesapBilgisi> BaglantiDeneAsync(CancellationToken cancellationToken = default);

    // Alıcı GİB e-Fatura sistemine kayıtlıysa posta kutusu etiketini (PK), değilse null döner.
    Task<string?> AliciEtiketiAsync(string vknTckn, CancellationToken cancellationToken = default);

    // Gönderilmiş belgenin entegratördeki durumu; entegratör belgeyi tanımıyorsa null.
    Task<EBelgeDurumBilgisi?> DurumSorgulaAsync(Guid ettn, EBelgeTuru tur, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GelenEBelgeOzeti>> GelenListeAsync(DateTime baslangic, DateTime bitis, CancellationToken cancellationToken = default);
    Task<string> GelenXmlAsync(string ettn, CancellationToken cancellationToken = default);

    // Gönderilmiş belgenin entegratörün ürettiği resmî PDF görüntüsü.
    Task<byte[]> PdfAsync(Guid ettn, CancellationToken cancellationToken = default);

    // e-Arşiv fatura iptali (GİB'e raporlanır). e-Fatura bu yolla iptal edilemez.
    Task EArsivIptalAsync(Guid ettn, DateTime iptalTarihi, CancellationToken cancellationToken = default);

    // Vergi/T.C. kimlik no'dan GİB kayıtlı ünvan, vergi dairesi ve adres; bulunamazsa null.
    Task<FirmaSorguSonucu?> FirmaSorgulaAsync(string vknTckn, CancellationToken cancellationToken = default);

    Task<EBelgeDurumBilgisi?> IrsaliyeDurumSorgulaAsync(Guid ettn, CancellationToken cancellationToken = default);
}

public sealed record FirmaSorguSonucu(string VknTckn, string Unvan, string? Ad, string? Soyad, string? VergiDairesi, string? Adres, string? Il, string? Ilce, bool Faal)
{
    // e-Fatura mükellefiyse GİB posta kutusu etiketi (servis doldurur).
    public string? EFaturaEtiketi { get; init; }
    public bool GercekKisi => VknTckn.Length == 11 || (!string.IsNullOrWhiteSpace(Ad) && string.IsNullOrWhiteSpace(Unvan));
}

// Ana sayfa uyarısı: sorunlu (red/hata) belgeler, e-belgesi kesilmemiş satışlar, aktarılmamış gelen faturalar.
public sealed record EBelgeUyariDto(int SorunluBelge, int GonderilmemisSatis, int YeniGelen, int DurumuGuncellenen)
{
    public string Metin()
    {
        var parcalar = new List<string>();
        if (SorunluBelge > 0) parcalar.Add($"{SorunluBelge} e-belge reddedildi ya da hata aldı");
        if (GonderilmemisSatis > 0) parcalar.Add($"{GonderilmemisSatis} onaylı satış faturasının e-belgesi henüz gönderilmedi (e-Arşiv 7 gün içinde düzenlenmelidir)");
        if (YeniGelen > 0) parcalar.Add($"entegratörde programa aktarılmamış {YeniGelen} gelen e-Fatura var");
        return parcalar.Count == 0 ? string.Empty : "e-Belge: " + string.Join(" • ", parcalar) + ".";
    }
}

public sealed record EntegratorHesapBilgisi(string Unvan, string VknTckn, bool EFatura, bool EArsiv);
public sealed record EBelgeDurumBilgisi(EBelgeDurumu Durum, string Aciklama);
public sealed record GelenEBelgeOzeti(string Ettn, string FaturaNo, DateTime Tarih, string GonderenUnvan, string GonderenVknTckn, decimal Tutar, string ParaBirimi, string Durum)
{
    public bool Aktarildi { get; set; }
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
    // Entegratör web servisi bağlı mı (durum sorgulama ve gelen kutusu yalnız bu durumda çalışır).
    bool CevrimiciMi { get; }

    // Ayarlar ekranındaki "Bağlantıyı Dene": şifre boşsa kayıtlı şifre kullanılır.
    Task<EntegratorHesapBilgisi> BaglantiDeneAsync(int entegrator, string kullanici, string? sifre, bool testOrtami, CancellationToken cancellationToken = default);
    Task<EBelgeDurumBilgisi> DurumSorgulaAsync(int faturaId, CancellationToken cancellationToken = default);
    Task<int> BekleyenDurumlariGuncelleAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GelenEBelgeOzeti>> GelenListeAsync(DateTime baslangic, DateTime bitis, CancellationToken cancellationToken = default);
    Task<string> GelenXmlIndirAsync(string ettn, CancellationToken cancellationToken = default);
    // Entegratörün ürettiği resmî fatura PDF'i (yalnız entegratör bağlıyken).
    Task<byte[]> PdfGetirAsync(int faturaId, CancellationToken cancellationToken = default);
    // Gönderilmiş e-Arşiv faturayı entegratör üzerinden iptal eder (GİB'e raporlanır); e-belge durumu İptal edildi olur.
    Task EArsivIptalAsync(int faturaId, CancellationToken cancellationToken = default);
    // Vergi/T.C. kimlik no'dan GİB kayıtlı ünvan ve adres (entegratör bağlı değilse null).
    Task<FirmaSorguSonucu?> FirmaSorgulaAsync(string vknTckn, CancellationToken cancellationToken = default);
    Task<EBelgeDurumBilgisi> IrsaliyeDurumSorgulaAsync(int irsaliyeId, CancellationToken cancellationToken = default);
    // Ana sayfa için; entegratör bağlıysa önce son 30 günde yanıt bekleyen belgelerin durumunu günceller (bağlantı hatası uyarıyı engellemez).
    Task<EBelgeUyariDto> UyariAsync(CancellationToken cancellationToken = default);
    // "Onaylanınca otomatik gönder" açıksa ve entegratör bağlıysa onaylı satış faturasının / alış iadesinin e-belgesini gönderir.
    // Ayar kapalıysa, belge türü uygun değilse ya da zaten gönderilmişse null döner; eksik bilgi hata olarak değil sonuç olarak döner.
    Task<EBelgeGonderimSonucu?> OtomatikGonderAsync(int faturaId, CancellationToken cancellationToken = default);

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

    // e-İrsaliye (sevk irsaliyesi). Ön kontrol eksikleri listeler; hazırlama ilk seferde GİB biçiminde numara ve ETTN verir.
    Task<IReadOnlyList<string>> IrsaliyeOnKontrolAsync(int irsaliyeId, CancellationToken cancellationToken = default);
    // e-İrsaliyenin istediği taşıma bilgisi (gönderilmemiş irsaliyede düzeltilebilir).
    Task IrsaliyeTasimaBilgisiKaydetAsync(int irsaliyeId, string aracPlaka, string soforAdi, string soforTckn, CancellationToken cancellationToken = default);
    Task<EBelgeGonderimSonucu> IrsaliyeHazirlaAsync(int irsaliyeId, CancellationToken cancellationToken = default);
    Task IrsaliyeDurumIsaretleAsync(int irsaliyeId, EBelgeDurumu durum, string? aciklama = null, CancellationToken cancellationToken = default);
}
