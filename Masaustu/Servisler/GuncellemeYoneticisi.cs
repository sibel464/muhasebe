using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using EtkiAlani.Lisanslama;

namespace Masaustu.Servisler;

// Kurulum sürümünde otomatik güncelleme: ayarlardaki GuncellemeAdresi'nden imzalı guncelleme.json okunur,
// daha yeni sürüm varsa kullanıcıya sorulur, MSI indirilip SHA-256 özeti doğrulanır. Kurulum program kapanırken,
// veritabanı sunucusu durduktan sonra başlatılır (App.OnExit); kurulum bitince program yeniden açılır.
// Yeni sürüm veritabanı şemasını değiştiriyorsa açılışta yükseltmeden önce yedek alınır (App.VeritabaniSaglikKontroluAsync).
public static class GuncellemeYoneticisi
{
    private static readonly HttpClient Istemci = new() { Timeout = TimeSpan.FromMinutes(10) };

    // İndirilmiş ve doğrulanmış, program kapanınca kurulacak paket.
    public static string? BekleyenPaket { get; private set; }

    public static Version MevcutSurum => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0);

    public static async Task<GuncellemeBilgisi?> YeniSurumVarMiAsync(string? bildirimAdresi, CancellationToken iptal = default)
    {
        if (!LisansYoneticisi.DenetimGerekli || string.IsNullOrWhiteSpace(bildirimAdresi)) return null;
        using var zaman = CancellationTokenSource.CreateLinkedTokenSource(iptal);
        zaman.CancelAfter(TimeSpan.FromSeconds(20));
        var metin = await Istemci.GetStringAsync(bildirimAdresi, zaman.Token);
        var bilgi = Guncelleme.Dogrula(metin, LisansYoneticisi.AcikAnahtarBase64);
        return bilgi != null && Guncelleme.DahaYeniMi(bilgi, MevcutSurum) ? bilgi : null;
    }

    public static async Task IndirAsync(GuncellemeBilgisi bilgi, CancellationToken iptal = default)
    {
        var klasor = Path.Combine(Path.GetTempPath(), "MuhasebeYonetimGuncelleme");
        Directory.CreateDirectory(klasor);
        var hedef = Path.Combine(klasor, $"MuhasebeYonetim-{bilgi.Surum}.msi");
        var gecici = hedef + ".indiriliyor";
        await using (var kaynak = await Istemci.GetStreamAsync(bilgi.IndirmeAdresi, iptal))
        await using (var dosya = File.Create(gecici))
            await kaynak.CopyToAsync(dosya, iptal);

        bool dogru;
        await using (var okunan = File.OpenRead(gecici)) dogru = Guncelleme.PaketDogruMu(okunan, bilgi.Sha256);
        if (!dogru)
        {
            File.Delete(gecici);
            throw new InvalidDataException("İndirilen güncelleme paketi doğrulanamadı (dosya bozuk veya değiştirilmiş). Güncelleme yapılmadı.");
        }
        File.Move(gecici, hedef, overwrite: true);
        BekleyenPaket = hedef;
    }

    // App.OnExit'ten, veritabanı sunucusu durdurulduktan sonra çağrılır.
    public static void BekleyenPaketiKur()
    {
        if (BekleyenPaket == null || !File.Exists(BekleyenPaket)) return;
        var exe = Environment.ProcessPath;
        // Programın tamamen kapanması için kısa bekleme, ardından kurulum (Windows yönetici onayı ister) ve yeniden açılış.
        var komut = $"/c timeout /t 3 /nobreak >nul & msiexec /i \"{BekleyenPaket}\" /passive /norestart"
                    + (exe != null ? $" & start \"\" \"{exe}\"" : "");
        try { Process.Start(new ProcessStartInfo("cmd.exe", komut) { UseShellExecute = false, CreateNoWindow = true }); }
        catch (Exception ex) { Debug.WriteLine($"Güncelleme başlatılamadı: {ex.Message}"); }
    }
}
