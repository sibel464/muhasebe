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
    public static string? GomuluPostgresBin
    {
        get
        {
            var bin = Path.Combine(AppContext.BaseDirectory, "pgsql", "bin");
            return File.Exists(Path.Combine(bin, "postgres.exe")) ? bin : null;
        }
    }

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
