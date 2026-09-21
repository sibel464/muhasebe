using Altyapi.Veri;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using System.Text.Json;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
string connection = "";
string source = "";
string environment = "Production (Release default)";
foreach (var path in new[] {"Masaustu/appsettings.json", "Masaustu/appsettings.local.json"})
{
    var full = Path.Combine(root, path);
    if (!File.Exists(full)) continue;
    using var config = JsonDocument.Parse(File.ReadAllText(full));
    if (config.RootElement.TryGetProperty("VeritabaniAyarlari", out var settings) && settings.TryGetProperty("BaglantiDizesi", out var cs))
    { connection = cs.GetString()!; source = path; }
    if (config.RootElement.TryGetProperty("UygulamaAyarlari", out var app) && app.TryGetProperty("Ortam", out var env))
        environment = env.GetString()!;
}
var builder = new NpgsqlConnectionStringBuilder(connection);
await using var db = new MuhasebeBaglami(new DbContextOptionsBuilder<MuhasebeBaglami>().UseNpgsql(connection).Options);
var model = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
await using var conn = new NpgsqlConnection(connection);
await conn.OpenAsync();
await using var tx = await conn.BeginTransactionAsync();
await using (var ro = new NpgsqlCommand("SET TRANSACTION READ ONLY", conn, tx)) await ro.ExecuteNonQueryAsync();
var columns = new Dictionary<string, string>();
await using (var cmd = new NpgsqlCommand("""
SELECT c.relname, a.attname, format_type(a.atttypid,a.atttypmod), a.attnotnull
FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_attribute a ON a.attrelid=c.oid
WHERE n.nspname='public' AND c.relkind='r' AND a.attnum>0 AND NOT a.attisdropped
""", conn, tx))
await using (var reader = await cmd.ExecuteReaderAsync())
while (await reader.ReadAsync()) columns[reader.GetString(0)+"."+reader.GetString(1)] = reader.GetString(2)+"|nullable="+!reader.GetBoolean(3);
var differences = new List<string>();
var expectedKeys = new HashSet<string>();
foreach (var table in model.Tables)
foreach (var column in table.Columns)
{
    var key = table.Name+"."+column.Name;
    expectedKeys.Add(key);
    if (column.Name == "xmin") continue;
    var expected = column.StoreType+"|nullable="+column.IsNullable;
    if (!columns.TryGetValue(key,out var actual)) differences.Add("MISSING COLUMN: "+key+" expected "+expected);
    else if (actual != expected) differences.Add("COLUMN: "+key+" expected "+expected+" actual "+actual);
}
foreach (var key in columns.Keys.Where(k => !expectedKeys.Contains(k) && !k.StartsWith("__EFMigrationsHistory.")))
    differences.Add("EXTRA COLUMN: "+key);
var actualObjects = new HashSet<string>();
await using (var cmd = new NpgsqlCommand("""
SELECT 'FK|'||c.relname||'|'||con.conname FROM pg_constraint con JOIN pg_class c ON c.oid=con.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' AND con.contype='f'
UNION ALL SELECT 'INDEX|'||tablename||'|'||indexname FROM pg_indexes WHERE schemaname='public'
""",conn,tx))
await using (var reader = await cmd.ExecuteReaderAsync())
while (await reader.ReadAsync()) actualObjects.Add(reader.GetString(0));
var expectedObjects = new HashSet<string>();
foreach (var table in model.Tables)
{
    foreach (var fk in table.ForeignKeyConstraints) expectedObjects.Add("FK|"+table.Name+"|"+fk.Name);
    foreach (var index in table.Indexes) expectedObjects.Add("INDEX|"+table.Name+"|"+index.Name);
    foreach (var unique in table.UniqueConstraints) expectedObjects.Add("INDEX|"+table.Name+"|"+unique.Name);
}
foreach (var item in expectedObjects.Except(actualObjects)) differences.Add("MISSING OBJECT: "+item);
foreach (var item in actualObjects.Except(expectedObjects).Where(x=>!x.Contains("__EFMigrationsHistory"))) differences.Add("EXTRA OBJECT: "+item);
var history = (await db.Database.GetAppliedMigrationsAsync()).ToHashSet();
var code = db.Database.GetMigrations().ToHashSet();
var matrix = code.Union(history).OrderBy(x=>x).Select(id=>new {MigrationId=id,Code=code.Contains(id),History=history.Contains(id),HistoricalSchemaEffect="NOT VERIFIED individually; final model/catalog comparison recorded"});
var report = new {Host=builder.Host,Database=builder.Database,Environment=environment,ConfigSource=source,PendingModelChanges=db.Database.HasPendingModelChanges(),PendingMigrations=code.Except(history),ModelTables=model.Tables.Count(),ColumnTypeAndNullableDifferences=differences,MigrationMatrix=matrix};
Console.WriteLine(JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
