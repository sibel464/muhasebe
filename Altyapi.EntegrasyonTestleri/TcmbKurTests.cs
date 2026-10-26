using System.Net;
using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// TCMB yanıtı sahte sunucudan gelir; internet bağlantısına bağlı değildir.
[Collection("PostgreSQL integration")]
public sealed class TcmbKurTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public TcmbKurTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private sealed class SahteTcmb(Func<HttpRequestMessage, HttpResponseMessage> yanit) : HttpMessageHandler
    {
        public int CagriSayisi;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CagriSayisi);
            return Task.FromResult(yanit(request));
        }
    }

    private static HttpResponseMessage Xml(DateTime bulten, decimal usd) => new(HttpStatusCode.OK)
    {
        Content = new StringContent($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Tarih_Date Tarih=""{bulten:dd.MM.yyyy}"" Date=""{bulten:MM/dd/yyyy}"">
  <Currency CrossOrder=""0"" Kod=""USD"" CurrencyCode=""USD"">
    <ForexBuying>{usd.ToString(System.Globalization.CultureInfo.InvariantCulture)}</ForexBuying>
    <ForexSelling>{(usd + 0.1m).ToString(System.Globalization.CultureInfo.InvariantCulture)}</ForexSelling>
  </Currency>
</Tarih_Date>")
    };

    // Kur tablosu firmalar arası ortak; her test kendi uzak tarihini kullanır.
    private static DateTime BenzersizGun() =>
        DateTime.SpecifyKind(new DateTime(2090, 1, 1).AddDays(Random.Shared.Next(0, 20000)), DateTimeKind.Utc);

    private KurServisi KurServisi(MuhasebeBaglami db, HttpMessageHandler handler, Func<DateTime> simdi) =>
        new(db, new TcmbKurServisi(db, new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) }, simdi), new Stok360TestKullanici());

    [Fact]
    public async Task Internet_yokken_elle_girilen_kur_kullanilir_ve_tek_deneme_yapilir()
    {
        _fixture.RequireDatabase();
        var gun = BenzersizGun();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        db.DovizKurlari.Add(new DovizKuru { ParaBirimiKodu = "USD", Tarih = gun, KurTarihi = gun, DovizAlis = 41m, DovizSatis = 41.1m, Kaynak = "MANUEL" });
        await db.SaveChangesAsync();

        var tcmb = new SahteTcmb(_ => throw new HttpRequestException("bağlantı yok"));
        var kur = await KurServisi(db, tcmb, () => gun.AddHours(10)).GetirGecerliKurAsync("USD", gun);

        Assert.Equal(41m, kur!.DovizAlis);
        Assert.Equal(2, tcmb.CagriSayisi); // aynı gün bir kez tekrar; eskiden 7 gün geriye ayrı ayrı deneniyordu
    }

    [Fact]
    public async Task Zaman_asiminda_anlasilir_hata_verilir()
    {
        _fixture.RequireDatabase();
        var gun = BenzersizGun();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var tcmb = new SahteTcmb(_ => throw new TaskCanceledException("zaman aşımı"));

        var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => KurServisi(db, tcmb, () => gun.AddHours(10)).GetirGecerliKurAsync("USD", gun));
        Assert.Contains("manuel kur", hata.Message);
    }

    [Fact]
    public async Task Elle_girilen_kur_sonradan_alinan_TCMB_kurunun_onune_gecer()
    {
        _fixture.RequireDatabase();
        var gun = BenzersizGun();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        db.DovizKurlari.Add(new DovizKuru { ParaBirimiKodu = "USD", Tarih = gun, KurTarihi = gun, DovizAlis = 41m, DovizSatis = 41.1m, Kaynak = "MANUEL" });
        await db.SaveChangesAsync();

        var tcmb = new SahteTcmb(_ => Xml(gun, 40m));
        var servis = KurServisi(db, tcmb, () => gun.AddHours(10));
        Assert.Equal(41m, (await servis.GetirGecerliKurAsync("USD", gun))!.DovizAlis);
        Assert.Equal(41m, (await servis.GetirGecerliKurAsync("USD", gun))!.DovizAlis); // TCMB önbellekteyken de
    }

    [Fact]
    public async Task Sabah_alinan_onceki_gun_kuru_yayin_saatinden_sonra_yenilenir()
    {
        _fixture.RequireDatabase();
        var gun = BenzersizGun();
        var simdi = gun.AddHours(10);
        await using var db = new MuhasebeBaglami(_fixture.Options);
        // Sabah today.xml önceki iş gününün bültenini döner
        var tcmb = new SahteTcmb(_ => simdi.TimeOfDay < TcmbKurServisi.YayinSaati ? Xml(gun.AddDays(-1), 40m) : Xml(gun, 42m));
        var servis = KurServisi(db, tcmb, () => simdi);

        var sabah = await servis.GetirGecerliKurAsync("USD", gun);
        Assert.Equal(40m, sabah!.DovizAlis);
        Assert.Equal(gun.AddDays(-1), sabah.KurTarihi.Date);

        simdi = gun.AddHours(16);
        var aksam = await servis.GetirGecerliKurAsync("USD", gun);
        Assert.Equal(42m, aksam!.DovizAlis);
        Assert.Equal(gun, aksam.KurTarihi.Date);

        // Yenilenen kur önbellekte kalır; tekrar internete gidilmez
        var cagri = tcmb.CagriSayisi;
        Assert.Equal(42m, (await servis.GetirGecerliKurAsync("USD", gun))!.DovizAlis);
        Assert.Equal(cagri, tcmb.CagriSayisi);
    }

    [Fact]
    public async Task Hafta_sonu_dosya_yoksa_onceki_is_gunu_kuru_alinir()
    {
        _fixture.RequireDatabase();
        var gun = BenzersizGun();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var cuma = gun.AddDays(-2);
        var tcmb = new SahteTcmb(r => r.RequestUri!.AbsolutePath.EndsWith($"{cuma:ddMMyyyy}.xml") ? Xml(cuma, 39m) : new HttpResponseMessage(HttpStatusCode.NotFound));

        var kur = await KurServisi(db, tcmb, () => gun.AddDays(30)).GetirGecerliKurAsync("USD", gun);
        Assert.Equal(39m, kur!.DovizAlis);
        Assert.Equal(cuma, kur.KurTarihi.Date);
        Assert.Equal(gun, kur.Tarih.Date);
    }
}
