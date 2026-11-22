using Altyapi.Veri;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using Xunit.Sdk;

namespace Altyapi.EntegrasyonTestleri;

public sealed class IsolatedPostgreSqlFixture : IAsyncLifetime
{
    public const string ConnectionStringVariable = "MUHASEBE_TEST_CONNECTION_STRING";
    private string? _connectionString;

    public bool IsAvailable => _connectionString is not null;
    public DbContextOptions<MuhasebeBaglami> Options => new DbContextOptionsBuilder<MuhasebeBaglami>()
        .UseNpgsql(_connectionString!)

        .Options;

    public async Task InitializeAsync()
    {
        var value = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(value))
            value = "Host=127.0.0.1;Port=5432;Database=Muhasebe_Test;Username=postgres;";

        var builder = new NpgsqlConnectionStringBuilder(value);
        if (string.IsNullOrWhiteSpace(builder.Database) || !builder.Database.EndsWith("_Test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Entegrasyon testi yalnızca adı _Test ile biten PostgreSQL veritabanında çalışabilir.");

        builder.Database = "Muhasebe_Audit_" + Guid.NewGuid().ToString("N") + "_Test";
        _connectionString = builder.ConnectionString;
        await using var context = new MuhasebeBaglami(Options);

        await context.Database.MigrateAsync();
    }

    // Test bitince yalnız bu çalışmanın açtığı geçici veritabanı silinir (eskiden hepsi kalıyordu: yüzlerce kopya birikip
    // sunucunun açılışını dakikalarca uzatıyordu). İncelemek için MUHASEBE_TEST_DB_KORU=1 verilirse korunur.
    // Yalnız "Muhasebe_Audit_..._Test" adı silinir; başka veritabanına dokunulmaz.
    public async Task DisposeAsync()
    {
        if (_connectionString is null || Environment.GetEnvironmentVariable("MUHASEBE_TEST_DB_KORU") == "1")
            return;
        var builder = new NpgsqlConnectionStringBuilder(_connectionString);
        var ad = builder.Database!;
        if (!ad.StartsWith("Muhasebe_Audit_", StringComparison.Ordinal) || !ad.EndsWith("_Test", StringComparison.Ordinal))
            return;
        NpgsqlConnection.ClearAllPools();
        builder.Database = "postgres";
        builder.Pooling = false;
        try
        {
            await using var baglanti = new NpgsqlConnection(builder.ConnectionString);
            await baglanti.OpenAsync();
            await using var komut = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{ad}\" WITH (FORCE)", baglanti);
            await komut.ExecuteNonQueryAsync();
        }
        catch
        {
            // Silinemezse test sonucu etkilenmez; bir sonraki temizlikte silinir.
        }
    }

    public void RequireDatabase()
    {
        if (!IsAvailable)
            throw SkipException.ForSkip($"{ConnectionStringVariable} tanımlı değil; test DB'ye fallback yapılmadı.");
    }
}

[CollectionDefinition("PostgreSQL integration", DisableParallelization = true)]
public sealed class PostgreSqlIntegrationCollection : ICollectionFixture<IsolatedPostgreSqlFixture> { }
