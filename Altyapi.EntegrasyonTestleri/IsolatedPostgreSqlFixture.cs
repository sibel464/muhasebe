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

    // Denetim veritabanı kanıt olarak korunur; mevcut veritabanları silinmez.
    public Task DisposeAsync() => Task.CompletedTask;

    public void RequireDatabase()
    {
        if (!IsAvailable)
            throw SkipException.ForSkip($"{ConnectionStringVariable} tanımlı değil; test DB'ye fallback yapılmadı.");
    }
}

[CollectionDefinition("PostgreSQL integration", DisableParallelization = true)]
public sealed class PostgreSqlIntegrationCollection : ICollectionFixture<IsolatedPostgreSqlFixture> { }
