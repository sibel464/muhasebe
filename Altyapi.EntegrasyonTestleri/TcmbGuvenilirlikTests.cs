using Altyapi.Servisler;
using Altyapi.Veri;
using Microsoft.EntityFrameworkCore;
using System.Net;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class TcmbGuvenilirlikTests(IsolatedPostgreSqlFixture fixture)
{
    [Fact]
    public async Task Erisilemeyen_Tcmb_uydurma_kur_kaydetmez()
    {
        await using var db = new MuhasebeBaglami(fixture.Options);
        var tarih = new DateTime(2041, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        using var client = new HttpClient(new BasarisizSunucu());
        var servis = new TcmbKurServisi(db, client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.GunlukKurlariGuncelleVeyaGetirAsync(tarih));
        Assert.False(await db.DovizKurlari.AnyAsync(k => k.Tarih == tarih));
    }

    [Fact]
    public async Task Iptal_istegi_kur_fallback_ile_yutulmaz()
    {
        await using var db = new MuhasebeBaglami(fixture.Options);
        using var client = new HttpClient(new BasarisizSunucu());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var servis = new TcmbKurServisi(db, client);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            servis.GunlukKurlariGuncelleVeyaGetirAsync(new DateTime(2041, 1, 4, 0, 0, 0, DateTimeKind.Utc), cancellation.Token));
    }

    private sealed class BasarisizSunucu : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
