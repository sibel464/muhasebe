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

    public TcmbKurServisi(MuhasebeBaglami baglam, HttpClient? httpClient = null)
    {
        _baglam = baglam;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task<List<DovizKuruDto>> GunlukKurlariGuncelleVeyaGetirAsync(DateTime? tarih = null, CancellationToken cancellationToken = default)
    {
        var hedefTarih = (tarih ?? DateTime.UtcNow).Date;

        // Check DB Cache first
        var cached = await _baglam.DovizKurlari
            .AsNoTracking()
            .Where(k => k.Tarih.Date == hedefTarih && k.Kaynak == "TCMB" && !k.Silindi)
            .ToListAsync(cancellationToken);

        if (cached.Count > 0)
        {
            return cached.Select(MapToDto).ToList();
        }

        // Fetch from TCMB
        var (parsedKurlar, kurTarihi) = await TcmbXmlCekAndParseAsync(hedefTarih, cancellationToken);

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

        for (int i = 0; i < maxFallbackDays; i++)
        {
            string url;
            if (aktifTarih.Date == DateTime.UtcNow.Date)
            {
                url = "https://www.tcmb.gov.tr/kurlar/today.xml";
            }
            else
            {
                url = $"https://www.tcmb.gov.tr/kurlar/{aktifTarih:yyyyMM}/{aktifTarih:ddMMyyyy}.xml";
            }

            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var xmlStr = await response.Content.ReadAsStringAsync(cancellationToken);
                    var kurlar = XmlParse(xmlStr);
                    if (kurlar.Count > 0)
                    {
                        return (kurlar, aktifTarih);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException)
            {
                // Network error or timeout, try fallback date
            }

            aktifTarih = aktifTarih.AddDays(-1);
        }

        // Doğrulanmış veri yoksa finansal kur üretilmez.
        throw new InvalidOperationException("TCMB doğrulanmış kur verisi alınamadı. Bağlantınızı kontrol edin veya yetkili kullanıcı ile manuel kur girin.");
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
