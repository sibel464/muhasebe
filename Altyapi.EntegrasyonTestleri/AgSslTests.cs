using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Altyapi.Veri;
using Npgsql;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Ağ sunucusu ayarları gerçek bir PostgreSQL örneğinde denenir: bağlantı SSL ile şifrelenir, ağdan şifresiz bağlantı reddedilir.
public sealed class AgSslTests
{
    private static string? PostgresBin() =>
        Directory.Exists(@"C:\Program Files\PostgreSQL")
            ? Directory.GetDirectories(@"C:\Program Files\PostgreSQL").Select(d => Path.Combine(d, "bin"))
                .Where(b => File.Exists(Path.Combine(b, "initdb.exe"))).OrderDescending().FirstOrDefault()
            : null;

    // pg_ctl start çıktısı yönlendirilirse başlattığı sunucu yönlendirmeyi devralır ve okuma hiç bitmez: çıktısız çalıştırılır.
    private static int CiktisizCalistir(string exe, params string[] argumanlar)
    {
        var bilgi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in argumanlar) bilgi.ArgumentList.Add(a);
        using var p = Process.Start(bilgi)!;
        p.WaitForExit(120_000);
        return p.ExitCode;
    }

    private static (int Kod, string Cikti) Calistir(string exe, params string[] argumanlar)
    {
        var bilgi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in argumanlar) bilgi.ArgumentList.Add(a);
        using var p = Process.Start(bilgi)!;
        var cikti = p.StandardOutput.ReadToEndAsync();
        var hata = p.StandardError.ReadToEndAsync();
        p.WaitForExit(120_000);
        return (p.ExitCode, cikti.Result + hata.Result);
    }

    [Fact]
    public void Sertifika_pem_bicimindedir()
    {
        var (sertifika, anahtar) = AgAyarlari.SunucuSertifikasiUret("MUHASEBE-PC");
        Assert.StartsWith("-----BEGIN CERTIFICATE-----", sertifika);
        Assert.StartsWith("-----BEGIN PRIVATE KEY-----", anahtar);
        Assert.Contains("hostssl", AgAyarlari.ErisimeAgBloguYaz(""));
        Assert.Contains("ssl = on", AgAyarlari.KonfigurasyonaAgBloguYaz(""));
    }

    [Fact]
    public async Task Sunucu_modunda_baglanti_ssl_ile_sifrelenir_agdan_sifresiz_baglanti_reddedilir()
    {
        var bin = PostgresBin();
        if (bin == null) return; // PostgreSQL kurulu olmayan makinede denenemez

        var kok = Path.Combine(Path.GetTempPath(), "mhs-ssl-" + Guid.NewGuid().ToString("N")[..8]);
        var veri = Path.Combine(kok, "data");
        Directory.CreateDirectory(kok);
        const string sifre = "Ssl-Deneme-2026";
        var port = new Random().Next(56000, 56900);
        var pgCtl = Path.Combine(bin, "pg_ctl.exe");
        try
        {
            var pw = Path.Combine(kok, "pw.txt");
            await File.WriteAllTextAsync(pw, sifre);
            var init = Calistir(Path.Combine(bin, "initdb.exe"), "-D", veri, "-U", "postgres", "--pwfile", pw, "--auth", "scram-sha-256", "--encoding", "UTF8", "--locale", "C");
            Assert.True(init.Kod == 0, init.Cikti);

            var (sertifika, anahtar) = AgAyarlari.SunucuSertifikasiUret(Environment.MachineName);
            await File.WriteAllTextAsync(Path.Combine(veri, AgAyarlari.SertifikaDosyasi), sertifika);
            await File.WriteAllTextAsync(Path.Combine(veri, AgAyarlari.AnahtarDosyasi), anahtar);
            var conf = Path.Combine(veri, "postgresql.conf");
            await File.WriteAllTextAsync(conf, AgAyarlari.KonfigurasyonaAgBloguYaz(await File.ReadAllTextAsync(conf)));
            var hba = Path.Combine(veri, "pg_hba.conf");
            await File.WriteAllTextAsync(hba, AgAyarlari.ErisimeAgBloguYaz(await File.ReadAllTextAsync(hba)));

            var bas = CiktisizCalistir(pgCtl, "start", "-D", veri, "-w", "-t", "90", "-l", Path.Combine(kok, "pg.log"), "-o", $"-p {port}");
            if (bas != 0)
            {
                using var log = new StreamReader(new FileStream(Path.Combine(kok, "pg.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                Assert.Fail("Sunucu başlamadı: " + log.ReadToEnd());
            }

            var csb = new NpgsqlConnectionStringBuilder { Host = "127.0.0.1", Port = port, Username = "postgres", Password = sifre, Database = "postgres", SslMode = SslMode.Prefer, Pooling = false };
            await using (var baglanti = new NpgsqlConnection(csb.ConnectionString))
            {
                await baglanti.OpenAsync();
                await using var komut = new NpgsqlCommand("SELECT ssl FROM pg_stat_ssl WHERE pid = pg_backend_pid()", baglanti);
                Assert.True((bool)(await komut.ExecuteScalarAsync())!);
            }

            // Yerel ağ adresinden şifresiz bağlanma denemesi reddedilir (pg_hba'da yalnız hostssl var).
            var agAdresi = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !a.Address.ToString().StartsWith("169.254."))
                .Select(a => a.Address.ToString()).FirstOrDefault();
            if (agAdresi != null)
            {
                csb.Host = agAdresi;
                csb.SslMode = SslMode.Disable;
                await using var sifresiz = new NpgsqlConnection(csb.ConnectionString);
                var hata = await Assert.ThrowsAsync<PostgresException>(() => sifresiz.OpenAsync());
                Assert.Equal("28000", hata.SqlState); // pg_hba: bağlantıya izin yok

                csb.SslMode = SslMode.Prefer;
                await using var sifreli = new NpgsqlConnection(csb.ConnectionString);
                await sifreli.OpenAsync();
            }
        }
        finally
        {
            if (Directory.Exists(veri)) CiktisizCalistir(pgCtl, "stop", "-D", veri, "-m", "immediate", "-w");
            try { Directory.Delete(kok, true); } catch { }
        }
    }
}
