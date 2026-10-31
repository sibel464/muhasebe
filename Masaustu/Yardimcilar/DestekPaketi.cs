using System.IO;
using System.IO.Compression;
using System.Text;
using Altyapi.Veri;

namespace Masaustu.Yardimcilar;

// Destek talebi için tek dosya: program/Windows sürümü, lisans ve ağ durumu, disk alanı ve son 14 günün log dosyaları.
// Muhasebe verisi, veritabanı şifresi (appsettings.local.json) ve yedekler pakete girmez.
public static class DestekPaketi
{
    public static string Olustur(string? hedefKlasor = null)
    {
        hedefKlasor ??= Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Directory.CreateDirectory(hedefKlasor);
        var yol = Path.Combine(hedefKlasor, $"MuhasebeDestek_{DateTime.Now:yyyy-MM-dd_HHmm}.zip");
        if (File.Exists(yol)) File.Delete(yol);

        using var zip = ZipFile.Open(yol, ZipArchiveMode.Create);
        var bilgi = zip.CreateEntry("bilgi.txt");
        using (var yazici = new StreamWriter(bilgi.Open(), new UTF8Encoding(true)))
            yazici.Write(BilgiMetni());

        var logKlasoru = Path.GetDirectoryName(UygulamaKlasorleri.LogYolu("x.log"))!;
        var sinir = DateTime.Now.AddDays(-14);
        foreach (var dosya in Directory.Exists(logKlasoru) ? Directory.GetFiles(logKlasoru) : [])
        {
            var fi = new FileInfo(dosya);
            if (fi.LastWriteTime < sinir || fi.Length > 20 * 1024 * 1024) continue;
            try
            {
                // Program açıkken log dosyası yazılıyor olabilir: paylaşımlı okunur.
                using var kaynak = new FileStream(dosya, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var hedef = zip.CreateEntry("Loglar/" + fi.Name, CompressionLevel.Optimal).Open();
                kaynak.CopyTo(hedef);
            }
            catch (IOException)
            {
            }
        }
        return yol;
    }

    public static string BilgiMetni()
    {
        var s = new StringBuilder();
        void Satir(string ad, Func<object?> deger)
        {
            string metin;
            try { metin = deger()?.ToString() ?? "-"; }
            catch (Exception ex) { metin = "(alınamadı: " + ex.Message + ")"; }
            s.AppendLine($"{ad,-22}: {metin}");
        }
        var asm = typeof(DestekPaketi).Assembly.GetName();
        Satir("Oluşturma", () => DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
        Satir("Program sürümü", () => asm.Version);
        Satir("Kurulum sürümü", () => Servisler.LisansYoneticisi.DenetimGerekli ? "evet (gömülü veritabanı)" : "hayır (geliştirme)");
        Satir("Program klasörü", () => AppContext.BaseDirectory);
        Satir("Windows", () => Environment.OSVersion.VersionString + (Environment.Is64BitOperatingSystem ? " 64 bit" : " 32 bit"));
        Satir(".NET", () => Environment.Version);
        Satir("Bilgisayar adı", () => Environment.MachineName);
        Satir("Makine kodu", () => Servisler.LisansYoneticisi.MakineKodu());
        Satir("Lisans", () => Servisler.LisansYoneticisi.DurumGetir().Aciklama);
        Satir("Ağ modu", () => App.AgModu);
        Satir("Veri klasörü", () => UygulamaKlasorleri.VeriKoku);
        Satir("Boş disk (veri)", () =>
        {
            var kok = Path.GetPathRoot(UygulamaKlasorleri.VeriKoku)!;
            return $"{new DriveInfo(kok).AvailableFreeSpace / 1024d / 1024 / 1024:N1} GB ({kok})";
        });
        Satir("Bellek (program)", () => $"{Environment.WorkingSet / 1024 / 1024} MB");
        Satir("Bölge / dil", () => System.Globalization.CultureInfo.CurrentCulture.Name);
        Satir("Ekran", () => $"{System.Windows.SystemParameters.PrimaryScreenWidth:0}x{System.Windows.SystemParameters.PrimaryScreenHeight:0}");
        return s.ToString();
    }
}
