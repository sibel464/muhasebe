using Altyapi.Veri;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Yedekten geri yükleme: yedek yeni veritabanına açılır, doğrulanır ve kullanılan veritabanıyla yer değiştirir;
// eski veritabanı silinmeden saklanır. Test yalnız kendi oluşturduğu geçici veritabanlarını kullanır.
[Collection("PostgreSQL integration")]
public sealed class GeriYuklemeTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public GeriYuklemeTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Yedek_yeni_veritabanina_acilip_yer_degistirir_eski_veritabani_saklanir()
    {
        _fixture.RequireDatabase();
        string kaynakBaglanti;
        await using (var db = new MuhasebeBaglami(_fixture.Options)) kaynakBaglanti = db.Database.GetConnectionString()!;
        var klasor = Path.Combine(Path.GetTempPath(), $"gy-{Guid.NewGuid():N}");
        var hedefAd = $"GeriYuklemeHedef_{Guid.NewGuid():N}"[..30];
        var hedefBaglanti = new NpgsqlConnectionStringBuilder(kaynakBaglanti) { Database = hedefAd, Pooling = false }.ConnectionString;
        string? eskiAd = null;
        try
        {
            // Test veritabanının yedeği = geri yüklenecek dosya
            var kaynakServis = new VeritabaniYedekServisi(kaynakBaglanti, klasor, 30, Yetkili());
            var yedek = await kaynakServis.ElleYedekAlAsync();

            // Boş bir hedef veritabanı: "kullanılan" veritabanı rolünde
            await SistemAsync(kaynakBaglanti, $"CREATE DATABASE \"{hedefAd}\"");
            var hedefServis = new VeritabaniYedekServisi(hedefBaglanti, Path.Combine(klasor, "hedef"), 30, Yetkili());

            eskiAd = await hedefServis.GeriYukleAsync(yedek.TamYol);

            await using var hedef = new NpgsqlConnection(hedefBaglanti);
            await hedef.OpenAsync();
            await using var komut = new NpgsqlCommand("SELECT count(*) FROM \"Kullanicilar\"", hedef);
            Assert.True((long)(await komut.ExecuteScalarAsync())! > 0);
            Assert.True(await VeritabaniVarMiAsync(kaynakBaglanti, eskiAd));
            // Geri yüklemeden önce hedefin kendi yedeği de alınmıştır
            Assert.NotEmpty(hedefServis.YedekleriListele());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            foreach (var ad in new[] { hedefAd, eskiAd })
                if (ad != null) await SistemAsync(kaynakBaglanti, $"DROP DATABASE IF EXISTS \"{ad}\" WITH (FORCE)");
            try { Directory.Delete(klasor, true); } catch { }
        }
    }

    [Fact]
    public async Task Bu_programa_ait_olmayan_yedek_reddedilir_ve_veritabanina_dokunulmaz()
    {
        _fixture.RequireDatabase();
        string kaynakBaglanti;
        await using (var db = new MuhasebeBaglami(_fixture.Options)) kaynakBaglanti = db.Database.GetConnectionString()!;
        var klasor = Path.Combine(Path.GetTempPath(), $"gy-{Guid.NewGuid():N}");
        var yabanciAd = $"GeriYuklemeYabanci_{Guid.NewGuid():N}"[..30];
        var hedefAd = $"GeriYuklemeHedef2_{Guid.NewGuid():N}"[..30];
        try
        {
            // İçinde bu programın tabloları olmayan bir veritabanının yedeği
            await SistemAsync(kaynakBaglanti, $"CREATE DATABASE \"{yabanciAd}\"");
            var yabanci = new NpgsqlConnectionStringBuilder(kaynakBaglanti) { Database = yabanciAd, Pooling = false }.ConnectionString;
            await using (var b = new NpgsqlConnection(yabanci)) { await b.OpenAsync(); await new NpgsqlCommand("CREATE TABLE t(x int)", b).ExecuteNonQueryAsync(); }
            var yedek = await new VeritabaniYedekServisi(yabanci, klasor, 30, Yetkili()).ElleYedekAlAsync();

            await SistemAsync(kaynakBaglanti, $"CREATE DATABASE \"{hedefAd}\"");
            var hedefBaglanti = new NpgsqlConnectionStringBuilder(kaynakBaglanti) { Database = hedefAd, Pooling = false }.ConnectionString;
            await using (var b = new NpgsqlConnection(hedefBaglanti)) { await b.OpenAsync(); await new NpgsqlCommand("CREATE TABLE korunan(x int); INSERT INTO korunan VALUES (42)", b).ExecuteNonQueryAsync(); }

            await Assert.ThrowsAsync<InvalidOperationException>(() => new VeritabaniYedekServisi(hedefBaglanti, Path.Combine(klasor, "h"), 30, Yetkili()).GeriYukleAsync(yedek.TamYol));

            await using var kontrol = new NpgsqlConnection(hedefBaglanti);
            await kontrol.OpenAsync();
            Assert.Equal(42, (int)(await new NpgsqlCommand("SELECT x FROM korunan", kontrol).ExecuteScalarAsync())!);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            foreach (var ad in new[] { yabanciAd, hedefAd })
                await SistemAsync(kaynakBaglanti, $"DROP DATABASE IF EXISTS \"{ad}\" WITH (FORCE)");
            try { Directory.Delete(klasor, true); } catch { }
        }
    }

    private static Cari360TestKullanici Yetkili() => new() { AktifYetkiler = new() { AyarlariYonet = true } };

    private static async Task SistemAsync(string baglanti, string sql)
    {
        await using var b = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(baglanti) { Database = "postgres", Pooling = false }.ConnectionString);
        await b.OpenAsync();
        await new NpgsqlCommand(sql, b).ExecuteNonQueryAsync();
    }

    private static async Task<bool> VeritabaniVarMiAsync(string baglanti, string ad)
    {
        await using var b = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(baglanti) { Database = "postgres", Pooling = false }.ConnectionString);
        await b.OpenAsync();
        await using var k = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @a", b);
        k.Parameters.AddWithValue("a", ad);
        return await k.ExecuteScalarAsync() != null;
    }
}
