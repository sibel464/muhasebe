using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Altyapi.Veri;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Masaustu.Servisler;

// Kurulum paketinde PostgreSQL uygulamayla birlikte gelir (<uygulama>\pgsql). İlk açılışta veritabanı yoksa
// ortak veri klasöründe (C:\ProgramData\MuhasebeYonetim\db-data) Türkçe sıralamalı yeni bir veritabanı kurulur.
// Veritabanı şifresi rastgele üretilir ve Windows DPAPI (makine kapsamı) ile şifrelenmiş olarak ayarlarda saklanır;
// şifre düz metin olarak hiçbir dosyada bulunmaz. Geliştirme ortamında (gömülü PostgreSQL yoksa) hiçbir şey değişmez.
public static class VeritabaniKurulumu
{
    private const string KorumaliSifreAnahtari = "VeritabaniAyarlari:SifreKorumali";
    private static readonly byte[] Entropi = Encoding.UTF8.GetBytes("MuhasebeYonetim.Veritabani.v1");

    public sealed record Sonuc(string BaglantiDizesi, string? VeriKlasoru);

    public static async Task<Sonuc> HazirlaAsync(IConfiguration ayarlar)
    {
        var csb = new NpgsqlConnectionStringBuilder(ayarlar["VeritabaniAyarlari:BaglantiDizesi"]
            ?? throw new InvalidOperationException("Veritabanı bağlantı dizesi bulunamadı."));
        var veriKlasoru = ayarlar["VeritabaniAyarlari:VeriKlasoru"];
        var gomuluBin = UygulamaKlasorleri.GomuluPostgresBin;

        if (gomuluBin != null && string.IsNullOrWhiteSpace(veriKlasoru))
            veriKlasoru = UygulamaKlasorleri.VarsayilanVeriKlasoru;

        if (ayarlar[KorumaliSifreAnahtari] is { Length: > 0 } korumali)
            csb.Password = SifreCoz(korumali);

        if (gomuluBin != null && veriKlasoru != null && !File.Exists(Path.Combine(veriKlasoru, "PG_VERSION")))
        {
            var sifre = SifreUret();
            await IlkKurulumAsync(gomuluBin, veriKlasoru, csb.Username ?? "postgres", sifre);
            AyarYaz(KorumaliSifreAnahtari, SifreKoru(sifre));
            csb.Password = sifre;
        }

        await PostgreSqlSunucuYoneticisi.SunucuyuHazirlaAsync(csb.ConnectionString, veriKlasoru);
        return new Sonuc(csb.ConnectionString, veriKlasoru);
    }

    private static async Task IlkKurulumAsync(string bin, string veriKlasoru, string kullanici, string sifre)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(veriKlasoru)!);
        if (Directory.Exists(veriKlasoru) && Directory.EnumerateFileSystemEntries(veriKlasoru).Any())
            throw new InvalidOperationException($"Veritabanı klasörü boş değil ama veritabanı dosyası (PG_VERSION) yok: {veriKlasoru}. " +
                                                "Klasör elle silinmiş ya da bozulmuş olabilir; yedekten geri yükleme için destek alın.");

        var sifreDosyasi = Path.Combine(Path.GetTempPath(), $"mhs-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(sifreDosyasi, sifre);
            var bilgi = new ProcessStartInfo(Path.Combine(bin, "initdb.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[]
                     {
                         "-D", veriKlasoru, "-U", kullanici, "--pwfile", sifreDosyasi, "--auth", "scram-sha-256",
                         "--encoding", "UTF8", "--locale", "C", "--locale-provider", "icu", "--icu-locale", "tr-TR"
                     })
                bilgi.ArgumentList.Add(arg);
            using var surec = Process.Start(bilgi) ?? throw new InvalidOperationException("Veritabanı kurulumu (initdb) başlatılamadı.");
            var cikti = surec.StandardOutput.ReadToEndAsync();
            var hata = surec.StandardError.ReadToEndAsync();
            await surec.WaitForExitAsync();
            if (surec.ExitCode != 0)
                throw new InvalidOperationException($"Veritabanı kurulamadı (initdb {surec.ExitCode}): {await hata}{await cikti}");
        }
        finally
        {
            try { File.Delete(sifreDosyasi); } catch { }
        }
    }

    private static string SifreUret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', 'x').Replace('/', 'y').TrimEnd('=');

    // Makine kapsamı: bilgisayardaki her Windows kullanıcısı uygulamayı açabilir; dosya başka bilgisayara kopyalanırsa çözülemez.
    private static string SifreKoru(string sifre) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(sifre), Entropi, DataProtectionScope.LocalMachine));

    private static string SifreCoz(string korumali) =>
        Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(korumali), Entropi, DataProtectionScope.LocalMachine));

    private static void AyarYaz(string anahtar, string deger)
    {
        var yol = UygulamaKlasorleri.YerelAyarDosyasi;
        Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
        var kok = File.Exists(yol) ? JsonNode.Parse(File.ReadAllText(yol))?.AsObject() ?? new JsonObject() : new JsonObject();
        var parcalar = anahtar.Split(':');
        var dugum = kok;
        foreach (var parca in parcalar[..^1])
        {
            if (dugum[parca] is not JsonObject alt) { alt = new JsonObject(); dugum[parca] = alt; }
            dugum = alt;
        }
        dugum[parcalar[^1]] = deger;
        File.WriteAllText(yol, kok.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
