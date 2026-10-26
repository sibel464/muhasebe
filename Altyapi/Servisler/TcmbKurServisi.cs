using System.Globalization;
using System.Xml.Linq;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Doviz;

namespace Altyapi.Servisler;

public class TcmbKurServisi : ITcmbKurServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly HttpClient _httpClient;

    // Servis her ekranda yeniden oluşturulur; HttpClient paylaşılır (her seferinde yeni bağlantı havuzu açılmasın).
    // TCMB'ye ilk (soğuk) bağlantı 9-10 sn sürebiliyor; 5 sn'lik süre programın ilk kur çekişini düşürüyordu.
    private static readonly HttpClient OrtakHttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    // TCMB günün kurunu 15:30'da yayımlar; bu saatten sonra bugüne önbelleğe alınmış önceki gün kuru yenilenir.
    public static readonly TimeSpan YayinSaati = new(15, 45, 0);

    private readonly Func<DateTime> _simdi;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<DateTime, DateTime> SonYenilemeDenemesi = new();

    public TcmbKurServisi(MuhasebeBaglami baglam, HttpClient? httpClient = null, Func<DateTime>? simdi = null)
    {
        _baglam = baglam;
        _httpClient = httpClient ?? OrtakHttpClient;
        _simdi = simdi ?? (() => DateTime.Now);
    }

    public async Task<List<DovizKuruDto>> GunlukKurlariGuncelleVeyaGetirAsync(DateTime? tarih = null, CancellationToken cancellationToken = default)
    {
        // Kur günü yerel takvim günüdür (KurServisi ile aynı).
        var simdi = _simdi();
        var hedefTarih = DateTime.SpecifyKind((tarih ?? simdi).Date, DateTimeKind.Utc);

        // Check DB Cache first
        var cached = await _baglam.DovizKurlari
            .Where(k => k.Tarih.Date == hedefTarih && k.Kaynak == "TCMB" && !k.Silindi)
            .ToListAsync(cancellationToken);

        // Bugün sabah alınan kur önceki iş gününün kurudur; yayın saatinden sonra bir kez yenilenir.
        // Eski kurla yapılmış işlemler etkilenmez (kur işleme kaydedilirken kopyalanır).
        // Hafta sonu/tatilde yeni bülten çıkmaz; her sorguda internete gidilmesin diye 30 dakikada bir denenir.
        var bayatBugunKuru = cached.Count > 0 && hedefTarih == simdi.Date && simdi.TimeOfDay >= YayinSaati
            && cached.All(k => k.KurTarihi.Date < hedefTarih)
            && (!SonYenilemeDenemesi.TryGetValue(hedefTarih, out var son) || simdi - son >= TimeSpan.FromMinutes(30));
        if (bayatBugunKuru)
            SonYenilemeDenemesi[hedefTarih] = simdi;
        if (cached.Count > 0 && !bayatBugunKuru)
        {
            return cached.Select(MapToDto).ToList();
        }

        if (bayatBugunKuru)
        {
            List<DovizKuruDto> yeni;
            DateTime yeniTarih;
            try
            {
                (yeni, yeniTarih) = await TcmbXmlCekAndParseAsync(hedefTarih, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Bağlantı yoksa elimizdeki (önceki iş günü) kur kullanılmaya devam eder.
                return cached.Select(MapToDto).ToList();
            }
            if (yeniTarih.Date <= cached.Max(k => k.KurTarihi.Date))
                return cached.Select(MapToDto).ToList();
            foreach (var eski in cached)
            {
                eski.Silindi = true;
                eski.GuncellemeTarihi = DateTime.UtcNow;
            }
            return await KaydetAsync(yeni, yeniTarih, hedefTarih, cancellationToken);
        }

        // Fetch from TCMB
        var (parsedKurlar, kurTarihi) = await TcmbXmlCekAndParseAsync(hedefTarih, cancellationToken);
        return await KaydetAsync(parsedKurlar, kurTarihi, hedefTarih, cancellationToken);
    }

    private async Task<List<DovizKuruDto>> KaydetAsync(List<DovizKuruDto> parsedKurlar, DateTime kurTarihi, DateTime hedefTarih, CancellationToken cancellationToken)
    {
        kurTarihi = DateTime.SpecifyKind(kurTarihi.Date, DateTimeKind.Utc);
        var list = new List<DovizKuru>();
        foreach (var p in parsedKurlar)
        {
            var entity = new DovizKuru
            {
                ParaBirimiKodu = p.ParaBirimiKodu,
                Tarih = hedefTarih,
                KurTarihi = kurTarihi,
                DovizAlis = p.DovizAlis,
                DovizSatis = p.DovizSatis,
                EfektifAlis = p.EfektifAlis,
                EfektifSatis = p.EfektifSatis,
                Kaynak = "TCMB",
                OlusturmaTarihi = DateTime.UtcNow,
                KullaniciId = 1,
                Aciklama = hedefTarih.Date == kurTarihi.Date ? "TCMB Günlük Resmi Kur" : $"TCMB Hafta Sonu/Tatil Fallback Kuru (İş Günü: {kurTarihi:dd.MM.yyyy})"
            };
            _baglam.DovizKurlari.Add(entity);
            list.Add(entity);
        }

        await _baglam.SaveChangesAsync(cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task<DovizKuruDto?> GetirKurAsync(string paraBirimiKodu, DateTime? tarih = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paraBirimiKodu) || paraBirimiKodu.Equals("TRY", StringComparison.OrdinalIgnoreCase))
        {
            return new DovizKuruDto
            {
                ParaBirimiKodu = "TRY",
                Tarih = (tarih ?? DateTime.UtcNow).Date,
                KurTarihi = (tarih ?? DateTime.UtcNow).Date,
                DovizAlis = 1.0m,
                DovizSatis = 1.0m,
                EfektifAlis = 1.0m,
                EfektifSatis = 1.0m,
                Kaynak = "SİSTEM"
            };
        }

        var kurlar = await GunlukKurlariGuncelleVeyaGetirAsync(tarih, cancellationToken);
        return kurlar.FirstOrDefault(k => k.ParaBirimiKodu.Equals(paraBirimiKodu, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(List<DovizKuruDto> Kurlar, DateTime KurTarihi)> TcmbXmlCekAndParseAsync(DateTime hedefTarih, CancellationToken cancellationToken)
    {
        DateTime aktifTarih = hedefTarih.Date;
        int maxFallbackDays = 7;
        var bugun = _simdi().Date;

        for (int i = 0; i < maxFallbackDays; i++)
        {
            string url;
            if (aktifTarih.Date >= bugun)
            {
                url = "https://www.tcmb.gov.tr/kurlar/today.xml";
            }
            else
            {
                url = $"https://www.tcmb.gov.tr/kurlar/{aktifTarih:yyyyMM}/{aktifTarih:ddMMyyyy}.xml";
            }

            try
            {
                using var response = await GetirTekrarliAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var xmlStr = await response.Content.ReadAsStringAsync(cancellationToken);
                    var kurlar = XmlParse(xmlStr);
                    if (kurlar.Count > 0)
                    {
                        // today.xml yayın saatinden önce önceki iş gününün bültenidir; gerçek bülten tarihi XML'den okunur.
                        var bultenTarihi = XmlTarihi(xmlStr) ?? aktifTarih;
                        return (kurlar, bultenTarihi > aktifTarih ? aktifTarih : bultenTarihi);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                // Bağlantı yok veya zaman aşımı (zaman aşımı TaskCanceledException olarak gelir): diğer günleri denemek
                // yalnız bekletir (7 × 15 sn). Tatil/hafta sonu için geri gitme yalnız sunucu "dosya yok" dediğinde yapılır.
                break;
            }

            aktifTarih = aktifTarih.AddDays(-1);
        }

        // Doğrulanmış veri yoksa finansal kur üretilmez.
        throw new InvalidOperationException("TCMB doğrulanmış kur verisi alınamadı. Bağlantınızı kontrol edin veya yetkili kullanıcı ile manuel kur girin.");
    }

    // Hızlı biten bağlantı hatası (bağlantı koptu/sıfırlandı) bir kez daha denenir. Zaman aşımı tekrarlanmaz:
    // kullanıcı ikinci kez 15 sn beklemesin.
    private async Task<HttpResponseMessage> GetirTekrarliAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return await _httpClient.GetAsync(url, cancellationToken);
        }
    }

    // Kök öğedeki bülten tarihi: Tarih="17.10.2026" (gg.aa.yyyy) veya Date="10/17/2026" (aa/gg/yyyy).
    public static DateTime? XmlTarihi(string xmlContent)
    {
        try
        {
            var kok = XDocument.Parse(xmlContent).Root;
            var tr = kok?.Attribute("Tarih")?.Value;
            if (DateTime.TryParseExact(tr, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t1))
                return DateTime.SpecifyKind(t1, DateTimeKind.Utc);
            var en = kok?.Attribute("Date")?.Value;
            if (DateTime.TryParseExact(en, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t2))
                return DateTime.SpecifyKind(t2, DateTimeKind.Utc);
        }
        catch
        {
        }
        return null;
    }

    public static List<DovizKuruDto> XmlParse(string xmlContent)
    {
        var list = new List<DovizKuruDto>();
        if (string.IsNullOrWhiteSpace(xmlContent)) return list;

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var currencies = doc.Root?.Elements("Currency");
            if (currencies == null) return list;

            foreach (var el in currencies)
            {
                var kod = el.Attribute("CurrencyCode")?.Value ?? el.Attribute("Kod")?.Value ?? "";
                if (string.IsNullOrWhiteSpace(kod)) continue;

                decimal ParseDec(string name)
                {
                    var val = el.Element(name)?.Value;
                    if (string.IsNullOrWhiteSpace(val)) return 0m;
                    return decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
                }

                decimal alis = ParseDec("ForexBuying");
                decimal satis = ParseDec("ForexSelling");
                decimal efAlis = ParseDec("BanknoteBuying");
                decimal efSatis = ParseDec("BanknoteSelling");

                if (efAlis <= 0m) efAlis = alis;
                if (efSatis <= 0m) efSatis = satis;

                if (alis > 0m || satis > 0m)
                {
                    list.Add(new DovizKuruDto
                    {
                        ParaBirimiKodu = kod.ToUpperInvariant(),
                        DovizAlis = alis,
                        DovizSatis = satis > 0 ? satis : alis,
                        EfektifAlis = efAlis,
                        EfektifSatis = efSatis > 0 ? efSatis : (satis > 0 ? satis : alis),
                        Kaynak = "TCMB"
                    });
                }
            }
        }
        catch
        {
            // Ignore parse exception
        }

        return list;
    }

    private static DovizKuruDto MapToDto(DovizKuru entity)
    {
        return new DovizKuruDto
        {
            Id = entity.Id,
            ParaBirimiKodu = entity.ParaBirimiKodu,
            Tarih = entity.Tarih,
            KurTarihi = entity.KurTarihi,
            DovizAlis = entity.DovizAlis,
            DovizSatis = entity.DovizSatis,
            EfektifAlis = entity.EfektifAlis,
            EfektifSatis = entity.EfektifSatis,
            Kaynak = entity.Kaynak,
            OlusturmaTarihi = entity.OlusturmaTarihi,
            KullaniciId = entity.KullaniciId,
            Aciklama = entity.Aciklama
        };
    }
}
