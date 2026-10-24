using System.Diagnostics;
using Npgsql;
using Uygulama.Arayuzler;

namespace Altyapi.Veri;

// pg_dump ile sıkıştırılmış (custom format) tam veritabanı yedeği alır, eski yedekleri temizler ve
// seçilen yedeği güvenli biçimde (yeni veritabanına açıp yer değiştirerek) geri yükler.
public sealed class VeritabaniYedekServisi : IVeritabaniYedekServisi
{
    private static readonly TimeSpan GunlukAralik = TimeSpan.FromHours(24);
    private static readonly TimeSpan YedekZamanAsimi = TimeSpan.FromMinutes(10);
    private static readonly SemaphoreSlim YedekKilidi = new(1, 1);

    private readonly NpgsqlConnectionStringBuilder _baglanti;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public string YedekKlasoru { get; }
    public int SaklanacakYedekSayisi { get; }
    public string? IkinciYedekKlasoru { get; private set; }
    public string IkinciKopyaDurumu { get; private set; } = string.Empty;

    // İkinci konum makineye özeldir (bu bilgisayarın USB/harici diski); veritabanına değil yedek klasörüne yazılır,
    // böylece eski bir yedek geri yüklenince ayar değişmez.
    private string IkinciKonumAyarDosyasi => Path.Combine(YedekKlasoru, "ikinci-yedek-konumu.txt");

    public VeritabaniYedekServisi(string baglantiDizesi, string? yedekKlasoru = null, int saklanacakYedekSayisi = 30, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglanti = new NpgsqlConnectionStringBuilder(baglantiDizesi);
        _kullaniciServisi = kullaniciServisi;
        YedekKlasoru = string.IsNullOrWhiteSpace(yedekKlasoru)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Muhasebe Yedekleri")
            : Environment.ExpandEnvironmentVariables(yedekKlasoru.Trim());
        SaklanacakYedekSayisi = Math.Clamp(saklanacakYedekSayisi, 3, 365);
        try
        {
            if (File.Exists(IkinciKonumAyarDosyasi))
                IkinciYedekKlasoru = File.ReadAllText(IkinciKonumAyarDosyasi).Trim() is { Length: > 0 } k ? k : null;
        }
        catch { }
    }

    public void IkinciYedekKlasoruAyarla(string? klasor)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama"))
            .YetkiKontrolEt(y => y.AyarlariYonet, "Yedek Konumu Değiştirme");
        klasor = string.IsNullOrWhiteSpace(klasor) ? null : Environment.ExpandEnvironmentVariables(klasor.Trim());
        if (klasor != null)
        {
            if (!Path.IsPathFullyQualified(klasor))
                throw new ArgumentException(@"İkinci yedek konumu tam bir klasör yolu olmalıdır (ör. E:\Muhasebe Yedekleri veya \\sunucu\yedek).");
            if (string.Equals(Path.GetFullPath(klasor).TrimEnd('\\'), Path.GetFullPath(YedekKlasoru).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("İkinci yedek konumu asıl yedek klasöründen farklı olmalıdır.");
            if (string.Equals(Path.GetPathRoot(Path.GetFullPath(klasor)), Path.GetPathRoot(Path.GetFullPath(YedekKlasoru)), StringComparison.OrdinalIgnoreCase))
                IkinciKopyaDurumu = "⚠ İkinci konum asıl yedekle aynı diskte; disk bozulursa ikisi birden kaybolur. Harici disk, USB bellek veya ağ klasörü önerilir.";
            else
                IkinciKopyaDurumu = string.Empty;
            // Yazılabilir mi? (USB takılı değilse ya da yetki yoksa şimdi söylenir, ilk yedekte değil)
            try
            {
                Directory.CreateDirectory(klasor);
                var deneme = Path.Combine(klasor, $".yazma-denemesi-{Guid.NewGuid():N}");
                File.WriteAllText(deneme, "ok");
                File.Delete(deneme);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"İkinci yedek konumuna yazılamıyor: {ex.Message}", ex);
            }
        }
        else
            IkinciKopyaDurumu = string.Empty;

        Directory.CreateDirectory(YedekKlasoru);
        if (klasor == null) File.Delete(IkinciKonumAyarDosyasi);
        else File.WriteAllText(IkinciKonumAyarDosyasi, klasor);
        IkinciYedekKlasoru = klasor;
    }

    private string DosyaOneki => $"{_baglanti.Database}_";

    public Task<YedekDosyasi> ElleYedekAlAsync(CancellationToken cancellationToken = default)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama"))
            .YetkiKontrolEt(y => y.AyarlariYonet, "Veritabanı Yedeği Alma");
        return YedekAlAsync(cancellationToken);
    }

    public Task<YedekDosyasi> GuncellemeOncesiYedekAlAsync(CancellationToken cancellationToken = default) => YedekAlAsync(cancellationToken);

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

            // Dosya gerçekten açılabiliyor mu? Bozuk/yarım yedek, ihtiyaç anında fark edilmesin diye şimdi reddedilir.
            try { await YedekDosyasiniDogrulaAsync(gecici, cancellationToken); }
            catch (InvalidDataException ex)
            {
                File.Delete(gecici);
                throw new InvalidOperationException($"Alınan yedek doğrulanamadı, dosya silindi. {ex.Message}", ex);
            }

            File.Move(gecici, hedef);
            EskiYedekleriTemizle();
            IkinciKonumaKopyala(hedef);

            var dosya = new FileInfo(hedef);
            return new YedekDosyasi(dosya.Name, dosya.FullName, dosya.LastWriteTime, dosya.Length);
        }
        finally
        {
            YedekKilidi.Release();
        }
    }

    public async Task<string> GeriYukleAsync(string yedekYolu, CancellationToken cancellationToken = default)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama"))
            .YetkiKontrolEt(y => y.AyarlariYonet, "Yedekten Geri Yükleme");
        if (!File.Exists(yedekYolu) || new FileInfo(yedekYolu).Length == 0)
            throw new FileNotFoundException("Yedek dosyası bulunamadı veya boş.", yedekYolu);
        var pgRestore = PostgreSqlSunucuYoneticisi.AracBul("pg_restore.exe")
            ?? throw new InvalidOperationException("pg_restore.exe bulunamadı. PostgreSQL kurulumunu kontrol edin.");

        // 1. Geri dönüş garantisi: şu anki verinin yedeği (bu da başarısızsa hiçbir şeye dokunulmaz).
        await YedekAlAsync(cancellationToken);

        var asil = _baglanti.Database ?? "Muhasebe";
        var damga = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var gecici = $"{asil}_geri_{damga}";
        var eski = $"{asil}_eski_{damga}";

        await YedekKilidi.WaitAsync(cancellationToken);
        try
        {
            // 2. Yedek ayrı (yeni) bir veritabanına açılır; kullanılan veritabanına bu aşamada dokunulmaz.
            await SistemKomutuAsync($"CREATE DATABASE \"{gecici}\" WITH TEMPLATE template0 ENCODING 'UTF8'", cancellationToken);
            try
            {
                var bilgi = new ProcessStartInfo(pgRestore)
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true
                };
                foreach (var arg in new[] { "-h", _baglanti.Host ?? "127.0.0.1", "-p", _baglanti.Port.ToString(), "-U", _baglanti.Username ?? "postgres",
                                            "-d", gecici, "--no-owner", "--exit-on-error", "--no-password", yedekYolu })
                    bilgi.ArgumentList.Add(arg);
                if (!string.IsNullOrEmpty(_baglanti.Password))
                    bilgi.Environment["PGPASSWORD"] = _baglanti.Password;
                using var surec = Process.Start(bilgi) ?? throw new InvalidOperationException("pg_restore başlatılamadı.");
                var hataCiktisi = surec.StandardError.ReadToEndAsync(cancellationToken);
                using var zamanAsimi = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                zamanAsimi.CancelAfter(YedekZamanAsimi);
                try { await surec.WaitForExitAsync(zamanAsimi.Token); }
                catch (OperationCanceledException)
                {
                    try { surec.Kill(entireProcessTree: true); } catch { }
                    throw new TimeoutException("Geri yükleme zaman aşımına uğradı.");
                }
                if (surec.ExitCode != 0)
                    throw new InvalidOperationException($"Yedek açılamadı: {(await hataCiktisi).Trim()}");

                // 3. Açılan yedek bu programın veritabanı mı? (yanlış dosya seçilmesine karşı)
                await using (var kontrol = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_baglanti.ConnectionString) { Database = gecici, Pooling = false }.ConnectionString))
                {
                    await kontrol.OpenAsync(cancellationToken);
                    await using var tabloKomutu = kontrol.CreateCommand();
                    tabloKomutu.CommandText = "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL AND to_regclass('public.\"Kullanicilar\"') IS NOT NULL";
                    var gecerli = await tabloKomutu.ExecuteScalarAsync(cancellationToken) is true;
                    if (gecerli)
                    {
                        await using var komut = kontrol.CreateCommand();
                        komut.CommandText = "SELECT (SELECT count(*) FROM \"__EFMigrationsHistory\") > 0 AND (SELECT count(*) FROM \"Kullanicilar\") > 0";
                        gecerli = await komut.ExecuteScalarAsync(cancellationToken) is true;
                    }
                    if (!gecerli)
                        throw new InvalidOperationException("Seçilen dosya bu programın geçerli bir yedeği değil.");
                }
            }
            catch
            {
                try { await SistemKomutuAsync($"DROP DATABASE IF EXISTS \"{gecici}\"", CancellationToken.None); } catch { }
                throw;
            }

            // 4. Yer değiştirme: programın açık bağlantıları kapatılır, eski veritabanı silinmeden yeniden adlandırılır.
            NpgsqlConnection.ClearAllPools();
            await SistemKomutuAsync($"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{asil}' AND pid <> pg_backend_pid()", cancellationToken);
            await SistemKomutuAsync($"ALTER DATABASE \"{asil}\" RENAME TO \"{eski}\"", cancellationToken);
            try
            {
                await SistemKomutuAsync($"ALTER DATABASE \"{gecici}\" RENAME TO \"{asil}\"", cancellationToken);
            }
            catch
            {
                await SistemKomutuAsync($"ALTER DATABASE \"{eski}\" RENAME TO \"{asil}\"", CancellationToken.None);
                throw;
            }
            return eski;
        }
        finally
        {
            YedekKilidi.Release();
        }
    }

    // Sunucu düzeyindeki komutlar (veritabanı oluşturma/yeniden adlandırma) sistem veritabanı üzerinden çalışır.
    private async Task SistemKomutuAsync(string sql, CancellationToken cancellationToken)
    {
        var cs = new NpgsqlConnectionStringBuilder(_baglanti.ConnectionString) { Database = "postgres", Pooling = false, CommandTimeout = 120 };
        await using var baglanti = new NpgsqlConnection(cs.ConnectionString);
        await baglanti.OpenAsync(cancellationToken);
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = sql;
        await komut.ExecuteNonQueryAsync(cancellationToken);
    }

    // Yedek baştan sona açılır (SQL'e çevrilip atılır): her veri bloğu okunur, yarım ya da bozuk dosya burada yakalanır.
    public static async Task YedekDosyasiniDogrulaAsync(string yol, CancellationToken cancellationToken = default)
    {
        var pgRestore = PostgreSqlSunucuYoneticisi.AracBul("pg_restore.exe");
        if (pgRestore == null) return; // Araç yoksa doğrulama atlanır; geri yükleme zaten aynı aracı ister.
        var bilgi = new ProcessStartInfo(pgRestore) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        bilgi.ArgumentList.Add("-f"); bilgi.ArgumentList.Add("-");
        bilgi.ArgumentList.Add(yol);
        using var surec = Process.Start(bilgi) ?? throw new InvalidOperationException("pg_restore başlatılamadı.");
        var cikti = surec.StandardOutput.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        var hata = surec.StandardError.ReadToEndAsync(cancellationToken);
        using var zamanAsimi = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        zamanAsimi.CancelAfter(YedekZamanAsimi);
        try { await surec.WaitForExitAsync(zamanAsimi.Token); await cikti; }
        catch (OperationCanceledException)
        {
            try { surec.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("Yedek doğrulaması zaman aşımına uğradı.");
        }
        if (surec.ExitCode != 0)
        {
            var mesaj = (await hata).Trim();
            throw new InvalidDataException($"Yedek dosyası okunamıyor (bozuk veya yarım): {(mesaj.Length > 0 ? mesaj : $"pg_restore çıkış kodu {surec.ExitCode}")}");
        }
    }

    // Asıl yedek alındıktan sonra ikinci konuma kopyalanır. Kopyalanamazsa (USB takılı değil, ağ yok) yedek yine de
    // geçerlidir; hata yalnız durum olarak gösterilir. Kopya da önce geçici adla yazılır.
    private void IkinciKonumaKopyala(string kaynak)
    {
        var klasor = IkinciYedekKlasoru;
        if (klasor == null) return;
        try
        {
            Directory.CreateDirectory(klasor);
            var hedef = Path.Combine(klasor, Path.GetFileName(kaynak));
            var gecici = hedef + ".tmp";
            File.Copy(kaynak, gecici, overwrite: true);
            if (new FileInfo(gecici).Length != new FileInfo(kaynak).Length)
                throw new IOException("Kopyalanan dosyanın boyutu asıl yedekle aynı değil.");
            File.Move(gecici, hedef, overwrite: true);
            foreach (var eski in new DirectoryInfo(klasor).GetFiles($"{DosyaOneki}*.dump").OrderByDescending(f => f.LastWriteTime).Skip(SaklanacakYedekSayisi))
            {
                try { eski.Delete(); } catch { }
            }
            IkinciKopyaDurumu = $"✅ Son kopya: {DateTime.Now:dd.MM.yyyy HH:mm} → {klasor}";
        }
        catch (Exception ex)
        {
            IkinciKopyaDurumu = $"❌ {DateTime.Now:dd.MM.yyyy HH:mm} ikinci konuma kopyalanamadı ({klasor}): {ex.Message}";
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
