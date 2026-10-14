using System.Diagnostics;
using Npgsql;
using Uygulama.Arayuzler;

namespace Altyapi.Veri;

// pg_dump ile sıkıştırılmış (custom format) tam veritabanı yedeği alır ve eski yedekleri temizler.
// Geri yükleme pg_restore ile yapılır; README'deki adımlara bakın.
public sealed class VeritabaniYedekServisi : IVeritabaniYedekServisi
{
    private static readonly TimeSpan GunlukAralik = TimeSpan.FromHours(24);
    private static readonly TimeSpan YedekZamanAsimi = TimeSpan.FromMinutes(10);
    private static readonly SemaphoreSlim YedekKilidi = new(1, 1);

    private readonly NpgsqlConnectionStringBuilder _baglanti;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public string YedekKlasoru { get; }
    public int SaklanacakYedekSayisi { get; }

    public VeritabaniYedekServisi(string baglantiDizesi, string? yedekKlasoru = null, int saklanacakYedekSayisi = 30, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglanti = new NpgsqlConnectionStringBuilder(baglantiDizesi);
        _kullaniciServisi = kullaniciServisi;
        YedekKlasoru = string.IsNullOrWhiteSpace(yedekKlasoru)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Muhasebe Yedekleri")
            : Environment.ExpandEnvironmentVariables(yedekKlasoru.Trim());
        SaklanacakYedekSayisi = Math.Clamp(saklanacakYedekSayisi, 3, 365);
    }

    private string DosyaOneki => $"{_baglanti.Database}_";

    public Task<YedekDosyasi> ElleYedekAlAsync(CancellationToken cancellationToken = default)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama"))
            .YetkiKontrolEt(y => y.AyarlariYonet, "Veritabanı Yedeği Alma");
        return YedekAlAsync(cancellationToken);
    }

    public async Task<YedekDosyasi?> GerekirseGunlukYedekAlAsync(CancellationToken cancellationToken = default)
    {
        var sonYedek = YedekleriListele().FirstOrDefault();
        if (sonYedek != null && DateTime.Now - sonYedek.OlusturmaTarihi < GunlukAralik)
            return null;
        return await YedekAlAsync(cancellationToken);
    }

    public IReadOnlyList<YedekDosyasi> YedekleriListele()
    {
        if (!Directory.Exists(YedekKlasoru))
            return Array.Empty<YedekDosyasi>();

        return new DirectoryInfo(YedekKlasoru)
            .GetFiles($"{DosyaOneki}*.dump")
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new YedekDosyasi(f.Name, f.FullName, f.LastWriteTime, f.Length))
            .ToList();
    }

    private async Task<YedekDosyasi> YedekAlAsync(CancellationToken cancellationToken)
    {
        await YedekKilidi.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(YedekKlasoru);
            YarimKalanlariTemizle();

            var pgDump = PostgreSqlSunucuYoneticisi.AracBul("pg_dump.exe")
                ?? throw new InvalidOperationException("pg_dump.exe bulunamadı. PostgreSQL kurulumunu kontrol edin.");

            var hedef = Path.Combine(YedekKlasoru, $"{DosyaOneki}{DateTime.Now:yyyy-MM-dd_HHmmss_fff}.dump");
            // Önce geçici dosyaya yazılır; yarım kalan bir yedek hiçbir zaman geçerli yedek gibi görünmez.
            var gecici = hedef + ".tmp";

            var bilgi = new ProcessStartInfo(pgDump)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            bilgi.ArgumentList.Add("-h"); bilgi.ArgumentList.Add(_baglanti.Host ?? "127.0.0.1");
            bilgi.ArgumentList.Add("-p"); bilgi.ArgumentList.Add(_baglanti.Port.ToString());
            bilgi.ArgumentList.Add("-U"); bilgi.ArgumentList.Add(_baglanti.Username ?? "postgres");
            bilgi.ArgumentList.Add("-d"); bilgi.ArgumentList.Add(_baglanti.Database ?? "Muhasebe");
            bilgi.ArgumentList.Add("-Fc");
            bilgi.ArgumentList.Add("--no-password");
            bilgi.ArgumentList.Add("-f"); bilgi.ArgumentList.Add(gecici);
            if (!string.IsNullOrEmpty(_baglanti.Password))
                bilgi.Environment["PGPASSWORD"] = _baglanti.Password;

            using var surec = Process.Start(bilgi) ?? throw new InvalidOperationException("pg_dump başlatılamadı.");
            var hataCiktisi = surec.StandardError.ReadToEndAsync(cancellationToken);
            using var zamanAsimi = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            zamanAsimi.CancelAfter(YedekZamanAsimi);
            try
            {
                await surec.WaitForExitAsync(zamanAsimi.Token);
            }
            catch (OperationCanceledException)
            {
                try { surec.Kill(entireProcessTree: true); } catch { }
                File.Delete(gecici);
                throw new TimeoutException("Veritabanı yedeği zaman aşımına uğradı.");
            }

            if (surec.ExitCode != 0 || !File.Exists(gecici) || new FileInfo(gecici).Length == 0)
            {
                if (File.Exists(gecici)) File.Delete(gecici);
                var hata = (await hataCiktisi).Trim();
                throw new InvalidOperationException($"Veritabanı yedeği alınamadı: {(hata.Length > 0 ? hata : $"pg_dump çıkış kodu {surec.ExitCode}")}");
            }

            File.Move(gecici, hedef);
            EskiYedekleriTemizle();

            var dosya = new FileInfo(hedef);
            return new YedekDosyasi(dosya.Name, dosya.FullName, dosya.LastWriteTime, dosya.Length);
        }
        finally
        {
            YedekKilidi.Release();
        }
    }

    private void YarimKalanlariTemizle()
    {
        foreach (var dosya in Directory.GetFiles(YedekKlasoru, $"{DosyaOneki}*.dump.tmp"))
        {
            try { File.Delete(dosya); } catch { }
        }
    }

    private void EskiYedekleriTemizle()
    {
        foreach (var eski in YedekleriListele().Skip(SaklanacakYedekSayisi))
        {
            try { File.Delete(eski.TamYol); } catch { }
        }
    }
}
