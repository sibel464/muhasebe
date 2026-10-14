using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Npgsql;

namespace Altyapi.Veri;

public static class PostgreSqlSunucuYoneticisi
{
    private static Process? _postgresSureci;
    private static string? _baslatilanVeriKlasoru;
    private static string? _baslatilanPostgresExe;
    private static readonly object _kilit = new();

    public static async Task SunucuyuHazirlaAsync(string baglantiDizesi)
    {
        var csb = new NpgsqlConnectionStringBuilder(baglantiDizesi);
        var host = csb.Host ?? "127.0.0.1";
        var port = csb.Port > 0 ? csb.Port : 5432;
        var hedefVeritabani = csb.Database ?? "Muhasebe";

        // Yalnızca localhost / 127.0.0.1 için yerel başlatma uygulanır
        if (host != "127.0.0.1" && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return;

        if (!await SunucuCevapVeriyorMuAsync(baglantiDizesi))
        {
            lock (_kilit)
            {
                if (!PortAcikMi(host, port))
                {
                    string? dbDataKlasoru = DbDataKlasorunuBul();
                    if (!string.IsNullOrEmpty(dbDataKlasoru) && Directory.Exists(dbDataKlasoru))
                    {
                        string? postgresExe = PostgresExeBul();
                        if (!string.IsNullOrEmpty(postgresExe) && File.Exists(postgresExe))
                        {
                            var startInfo = new ProcessStartInfo
                            {
                                FileName = postgresExe,
                                Arguments = $"-D \"{dbDataKlasoru}\" -p {port}",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden,
                                RedirectStandardOutput = false,
                                RedirectStandardError = false
                            };

                            try
                            {
                                _baslatilanVeriKlasoru = dbDataKlasoru;
                                _baslatilanPostgresExe = postgresExe;
                                _postgresSureci = Process.Start(startInfo);
                                if (_postgresSureci != null)
                                {
                                    AppDomain.CurrentDomain.ProcessExit += (_, _) => SunucuyuDurdur();
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"PostgreSQL başlatma hatası: {ex.Message}");
                            }
                        }
                    }
                }
            }

            // Sunucunun ayağa kalkmasını bekle (maksimum 25 saniye)
            for (int i = 0; i < 50; i++)
            {
                if (await SunucuCevapVeriyorMuAsync(baglantiDizesi))
                    break;

                await Task.Delay(500);
            }
        }

        // Hedef veritabanı yoksa otomatik oluştur
        await VeritabaniYoksaOlusturAsync(baglantiDizesi, hedefVeritabani);
    }

    public static void SunucuyuDurdur()
    {
        lock (_kilit)
        {
            try
            {
                if (_postgresSureci != null && !_postgresSureci.HasExited)
                {
                    if (string.IsNullOrWhiteSpace(_baslatilanVeriKlasoru) || string.IsNullOrWhiteSpace(_baslatilanPostgresExe))
                        return;
                    var pgCtl = Path.Combine(Path.GetDirectoryName(_baslatilanPostgresExe)!, "pg_ctl.exe");
                    if (!File.Exists(pgCtl))
                    {
                        Debug.WriteLine("PostgreSQL kapatılamadı: pg_ctl bulunamadı. Sunucu çalışır durumda bırakıldı.");
                        return;
                    }
                    var stopInfo = new ProcessStartInfo(pgCtl)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    foreach (var arg in new[] { "-D", _baslatilanVeriKlasoru, "stop", "-m", "fast", "-w", "-t", "15" })
                        stopInfo.ArgumentList.Add(arg);
                    using var stop = Process.Start(stopInfo);
                    if (stop == null || !stop.WaitForExit(20000) || stop.ExitCode != 0)
                    {
                        Debug.WriteLine("PostgreSQL normal kapanışı doğrulanamadı. Süreç zorla sonlandırılmadı.");
                        return;
                    }
                    _postgresSureci.Dispose();
                    _postgresSureci = null;
                }
            }
            catch
            {
            }
        }
    }

    private static bool PortAcikMi(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var sonuc = client.BeginConnect(host, port, null, null);
            var basarili = sonuc.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(400));
            if (!basarili) return false;
            client.EndConnect(sonuc);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SunucuCevapVeriyorMuAsync(string baglantiDizesi)
    {
        try
        {
            var csb = new NpgsqlConnectionStringBuilder(baglantiDizesi)
            {
                Timeout = 2,
                CommandTimeout = 2,
                Database = "postgres" // Sistem veritabanı üzerinden test edilir
            };

            await using var conn = new NpgsqlConnection(csb.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1;";
            await cmd.ExecuteScalarAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task VeritabaniYoksaOlusturAsync(string baglantiDizesi, string veritabaniAdi)
    {
        try
        {
            var csb = new NpgsqlConnectionStringBuilder(baglantiDizesi)
            {
                Timeout = 5,
                CommandTimeout = 5,
                Database = "postgres"
            };

            await using var conn = new NpgsqlConnection(csb.ConnectionString);
            await conn.OpenAsync();

            await using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT 1 FROM pg_database WHERE datname = @db;";
            checkCmd.Parameters.AddWithValue("db", veritabaniAdi);
            var varMi = await checkCmd.ExecuteScalarAsync();

            if (varMi == null)
            {
                await using var createCmd = conn.CreateCommand();
                createCmd.CommandText = $"CREATE DATABASE \"{veritabaniAdi}\" WITH ENCODING = 'UTF8';";
                await createCmd.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Veritabanı oluşturma kontrolü: {ex.Message}");
        }
    }

    private static string? DbDataKlasorunuBul()
    {
        // 1. Mevcut çalışma dizininden yukarı doğru ara
        var mevcutDizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (mevcutDizin != null)
        {
            var aday = Path.Combine(mevcutDizin.FullName, "db-data");
            if (Directory.Exists(aday) && File.Exists(Path.Combine(aday, "PG_VERSION")))
                return aday;

            mevcutDizin = mevcutDizin.Parent;
        }

        // 2. Proje kök dizini tahmini
        var ortamDizin = Directory.GetCurrentDirectory();
        var aday2 = Path.Combine(ortamDizin, "db-data");
        if (Directory.Exists(aday2) && File.Exists(Path.Combine(aday2, "PG_VERSION")))
            return aday2;

        return null;
    }

    // pg_dump, pg_restore gibi PostgreSQL araçlarını sunucuyla aynı bin klasöründe arar.
    public static string? AracBul(string exeAdi)
    {
        var postgres = _baslatilanPostgresExe ?? PostgresExeBul();
        if (postgres == null)
            return null;
        var aday = Path.Combine(Path.GetDirectoryName(postgres)!, exeAdi);
        return File.Exists(aday) ? aday : null;
    }

    private static string? PostgresExeBul()
    {
        string[] bilinenYollar =
        {
            @"C:\Program Files\PostgreSQL\16\bin\postgres.exe",
            @"C:\Program Files\PostgreSQL\15\bin\postgres.exe",
            @"C:\Program Files\PostgreSQL\17\bin\postgres.exe",
            @"C:\Program Files\PostgreSQL\14\bin\postgres.exe"
        };

        foreach (var yol in bilinenYollar)
        {
            if (File.Exists(yol))
                return yol;
        }

        try
        {
            var pgAnaKlasor = @"C:\Program Files\PostgreSQL";
            if (Directory.Exists(pgAnaKlasor))
            {
                var altKlasorler = Directory.GetDirectories(pgAnaKlasor);
                foreach (var klasor in altKlasorler)
                {
                    var aday = Path.Combine(klasor, "bin", "postgres.exe");
                    if (File.Exists(aday))
                        return aday;
                }
            }
        }
        catch
        {
        }

        return null;
    }
}
