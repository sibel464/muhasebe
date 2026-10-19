namespace Altyapi.Veri;

// Program Files'a kurulan uygulama kendi klasörüne yazamaz. Ayarlar, loglar ve (kurulum sürümünde) veritabanı
// bilgisayardaki tüm kullanıcıların ortak klasöründe tutulur: C:\ProgramData\MuhasebeYonetim.
public static class UygulamaKlasorleri
{
    public const string KlasorAdi = "MuhasebeYonetim";

    public static string VeriKoku => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), KlasorAdi);

    // Kurulum sürümünde veritabanı dosyaları (ayarda VeriKlasoru verilmemişse).
    public static string VarsayilanVeriKlasoru => Path.Combine(VeriKoku, "db-data");

    // Kuruluma özgü ayarlar (ör. korumalı veritabanı şifresi); kurulum klasöründeki appsettings.json'ın üzerine yazılır.
    public static string YerelAyarDosyasi => Path.Combine(VeriKoku, "appsettings.local.json");

    // Uygulamayla birlikte gelen PostgreSQL (kurulum paketi): <uygulama>\pgsql\bin. Geliştirme ortamında yoktur.
    // PostgreSQL kurulum (initdb) sırasında kendi klasör yolunu SQL'e yazar; yolda Türkçe karakter varsa (ör. "Yönetim")
    // UTF-8 hatasıyla durur. Bu durumda klasörün Windows kısa adı (8.3, yalnız ASCII) kullanılır.
    public static string? GomuluPostgresBin
    {
        get
        {
            var bin = Path.Combine(AppContext.BaseDirectory, "pgsql", "bin");
            return File.Exists(Path.Combine(bin, "postgres.exe")) ? AsciiYol(bin) : null;
        }
    }

    public static string AsciiYol(string yol)
    {
        if (yol.All(c => c < 128) || !OperatingSystem.IsWindows()) return yol;
        var tampon = new System.Text.StringBuilder(1024);
        var uzunluk = GetShortPathName(yol, tampon, tampon.Capacity);
        return uzunluk > 0 && uzunluk < tampon.Capacity && tampon.ToString().All(c => c < 128) ? tampon.ToString() : yol;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int GetShortPathName(string uzunYol, System.Text.StringBuilder kisaYol, int boyut);

    // Log dosyası yolu: ortak klasör yazılamıyorsa geçici klasöre düşer (log yazılamadığı için uygulama durmamalı).
    public static string LogYolu(string dosyaAdi)
    {
        foreach (var aday in new[] { Path.Combine(VeriKoku, "Loglar"), Path.Combine(Path.GetTempPath(), KlasorAdi) })
        {
            try
            {
                Directory.CreateDirectory(aday);
                return Path.Combine(aday, dosyaAdi);
            }
            catch
            {
            }
        }
        return Path.Combine(Path.GetTempPath(), dosyaAdi);
    }
}
