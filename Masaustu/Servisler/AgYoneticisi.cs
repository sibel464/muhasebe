using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Altyapi.Veri;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Masaustu.Servisler;

// Çok kullanıcılı kurulum (bkz. AgAyarlari). Sunucu yapma/kaldırma Windows yönetici yetkisi ister: program kendini
// "--ag-sunucu-kur" / "--ag-sunucu-kaldir" ile yönetici olarak yeniden çalıştırır, adımlar Loglar\ag-kurulum.log'a yazılır.
public static class AgYoneticisi
{
    public const string SunucuKurArgumani = "--ag-sunucu-kur";
    public const string SunucuKaldirArgumani = "--ag-sunucu-kaldir";
    public const string IstemciSifreAnahtari = "VeritabaniAyarlari:IstemciSifreKorumali";
    private const string IstemciBaglantiAnahtari = "VeritabaniAyarlari:IstemciBaglantiDizesi";
    // NT AUTHORITY\NetworkService: PostgreSQL hizmeti yönetici olmayan bu hesapla çalışır.
    private const string HizmetHesabi = @"NT AUTHORITY\NetworkService";
    private const string HizmetHesabiSid = "*S-1-5-20";
    // Yöneticiler ve sistem tam yetkili; oturum açmış kullanıcılar hizmeti başlatıp durdurabilir (program açılışta başlatabilsin).
    private const string HizmetGuvenligi = "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWRPWPLOCRRC;;;AU)";

    public static string KurulumLogu => UygulamaKlasorleri.LogYolu("ag-kurulum.log");

    public static AgModu Mod(IConfiguration ayarlar) => AgAyarlari.ModCoz(ayarlar[AgAyarlari.ModAnahtari]);

    // İstemcide sunucunun bağlantı dizesi ve şifresi ayrı anahtarlarda durur; tek bilgisayara dönünce yerel ayarlar bozulmamış olur.
    public static string? IstemciBaglantiDizesi(IConfiguration ayarlar) => ayarlar[IstemciBaglantiAnahtari];

    // ---- Yönetici olarak çalışan adımlar ----

    public static int YukseltilmisIslem(string arguman)
    {
        try
        {
            File.WriteAllText(KurulumLogu, $"{DateTime.Now:dd.MM.yyyy HH:mm:ss} {arguman}{Environment.NewLine}");
            var ayarlar = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false).AddJsonFile(UygulamaKlasorleri.YerelAyarDosyasi, optional: true).Build();
            var bin = UygulamaKlasorleri.GomuluPostgresBin ?? throw new InvalidOperationException("Bu işlem yalnız kurulum paketiyle kurulmuş programda yapılabilir.");
            var veri = string.IsNullOrWhiteSpace(ayarlar["VeritabaniAyarlari:VeriKlasoru"]) ? UygulamaKlasorleri.VarsayilanVeriKlasoru : ayarlar["VeritabaniAyarlari:VeriKlasoru"]!;
            var port = new NpgsqlConnectionStringBuilder(ayarlar["VeritabaniAyarlari:BaglantiDizesi"]).Port;
            if (arguman == SunucuKurArgumani) SunucuKur(bin, veri, port);
            else SunucuKaldir(bin, veri);
            Log("Tamamlandı.");
            return 0;
        }
        catch (Exception ex)
        {
            Log($"HATA: {ex.Message}");
            return 1;
        }
    }

    private static void SunucuKur(string bin, string veri, int port)
    {
        if (!File.Exists(Path.Combine(veri, "PG_VERSION"))) throw new InvalidOperationException($"Veritabanı bulunamadı: {veri}");
        var pgCtl = Path.Combine(bin, "pg_ctl.exe");

        Log("1/6 Çalışan veritabanı durduruluyor");
        if (Calistir(pgCtl, "status", "-D", veri).Kod == 0)
            Gerekli(Calistir(pgCtl, "stop", "-D", veri, "-m", "fast", "-w", "-t", "60"), "Veritabanı durdurulamadı");

        Log("2/6 Ağ erişimi açılıyor (yalnız yerel ağ, şifreli giriş)");
        DosyaDuzenle(Path.Combine(veri, "postgresql.conf"), AgAyarlari.KonfigurasyonaAgBloguYaz);
        DosyaDuzenle(Path.Combine(veri, "pg_hba.conf"), AgAyarlari.ErisimeAgBloguYaz);

        Log("3/6 Veritabanı klasörü izinleri");
        Gerekli(Calistir("icacls.exe", veri, "/grant", $"{HizmetHesabiSid}:(OI)(CI)F", "/T", "/Q"), "Klasör izni verilemedi");

        Log("4/6 Windows hizmeti kaydediliyor");
        if (Calistir("sc.exe", "query", AgAyarlari.HizmetAdi).Kod != 0)
            Gerekli(Calistir(pgCtl, "register", "-N", AgAyarlari.HizmetAdi, "-U", HizmetHesabi, "-D", veri, "-S", "auto",
                "-o", $"-p {port} -c logging_collector=on"), "Hizmet kaydedilemedi");
        Calistir("sc.exe", "sdset", AgAyarlari.HizmetAdi, HizmetGuvenligi);
        Calistir("sc.exe", "description", AgAyarlari.HizmetAdi, "Muhasebe Yönetim Sistemi veritabanı (ağ sunucusu)");
        Calistir("sc.exe", "failure", AgAyarlari.HizmetAdi, "reset=", "86400", "actions=", "restart/10000/restart/30000//");

        Log("5/6 Hizmet başlatılıyor");
        Calistir("sc.exe", "start", AgAyarlari.HizmetAdi);
        if (!HazirOlanaKadarBekle(bin, port, TimeSpan.FromSeconds(120)))
            throw new InvalidOperationException("Veritabanı hizmeti başlamadı. Ayrıntı için veritabanı klasöründeki log klasörüne bakın.");

        Log("6/6 Güvenlik duvarı izni (yalnız özel/etki alanı ağları)");
        Calistir("netsh.exe", "advfirewall", "firewall", "delete", "rule", $"name={AgAyarlari.GuvenlikDuvariKurali}");
        Gerekli(Calistir("netsh.exe", "advfirewall", "firewall", "add", "rule", $"name={AgAyarlari.GuvenlikDuvariKurali}", "dir=in", "action=allow",
            "protocol=TCP", $"localport={port}", "profile=private,domain"), "Güvenlik duvarı izni verilemedi");

        VeritabaniKurulumu.AyarYaz(AgAyarlari.ModAnahtari, nameof(AgModu.Sunucu));
    }

    private static void SunucuKaldir(string bin, string veri)
    {
        Log("1/3 Veritabanı hizmeti durduruluyor ve kaldırılıyor");
        Calistir("sc.exe", "stop", AgAyarlari.HizmetAdi);
        for (var i = 0; i < 60 && Calistir(Path.Combine(bin, "pg_ctl.exe"), "status", "-D", veri).Kod == 0; i++) Thread.Sleep(1000);
        Calistir(Path.Combine(bin, "pg_ctl.exe"), "unregister", "-N", AgAyarlari.HizmetAdi);

        Log("2/3 Ağ erişimi kapatılıyor");
        DosyaDuzenle(Path.Combine(veri, "postgresql.conf"), AgAyarlari.BlokuKaldir);
        DosyaDuzenle(Path.Combine(veri, "pg_hba.conf"), AgAyarlari.BlokuKaldir);
        Calistir("netsh.exe", "advfirewall", "firewall", "delete", "rule", $"name={AgAyarlari.GuvenlikDuvariKurali}");

        Log("3/3 Tek bilgisayar moduna dönülüyor");
        VeritabaniKurulumu.AyarYaz(AgAyarlari.ModAnahtari, nameof(AgModu.Tek));
    }

    // Program yönetici yetkisiyle yeniden çalıştırılır; kullanıcı Windows onayını reddederse false.
    public static async Task<bool> YoneticiOlarakCalistirAsync(string arguman)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Program yolu bulunamadı.");
        try
        {
            using var surec = Process.Start(new ProcessStartInfo(exe, arguman) { UseShellExecute = true, Verb = "runas" })
                ?? throw new InvalidOperationException("İşlem başlatılamadı.");
            await surec.WaitForExitAsync();
            if (surec.ExitCode != 0)
                throw new InvalidOperationException("Ağ ayarı tamamlanamadı:\n" + (File.Exists(KurulumLogu) ? await File.ReadAllTextAsync(KurulumLogu) : ""));
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return false; // kullanıcı yönetici onayını vermedi
        }
    }

    // Sunucu modunda hizmet herhangi bir nedenle durmuşsa program açılışta başlatmayı dener (yetkisi kurulumda verildi).
    public static void HizmetiBaslatmayiDene(int port)
    {
        if (PortAcik(port)) return;
        if (Calistir("sc.exe", "start", AgAyarlari.HizmetAdi).Kod != 0) return;
        // Hizmet başlarken program kendi kopyasını başlatmasın diye port açılana kadar beklenir (en çok 60 sn).
        for (var i = 0; i < 120 && !PortAcik(port); i++) Thread.Sleep(500);
    }

    private static bool PortAcik(int port)
    {
        try
        {
            using var istemci = new TcpClient();
            return istemci.ConnectAsync("127.0.0.1", port).Wait(400) && istemci.Connected;
        }
        catch { return false; }
    }

    // ---- Sunucu: istemcilere verilecek kod ----

    public static string BaglantiKoduUret(string sunucuBaglantiDizesi)
    {
        var csb = new NpgsqlConnectionStringBuilder(sunucuBaglantiDizesi);
        var adresler = new[] { Environment.MachineName }.Concat(YerelIpAdresleri()).Distinct().ToArray();
        return AgAyarlari.KodUret(new AgAyarlari.BaglantiKodu(adresler, csb.Port, csb.Database ?? "Muhasebe", csb.Username ?? "postgres", csb.Password ?? ""));
    }

    public static IEnumerable<string> YerelIpAdresleri() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.ToString());

    // ---- İstemci ----

    // Koddaki adresler sırayla denenir; bağlanılan adres ayarlara yazılır. Sunucuya ulaşılamazsa hata.
    public static async Task<string> IstemciOlarakBaglanAsync(string kodMetni)
    {
        var kod = AgAyarlari.KodCoz(kodMetni) ?? throw new ArgumentException("Bağlantı kodu geçersiz. Kodu sunucu bilgisayardaki Ayarlar > Ağ bölümünden eksiksiz kopyalayın.");
        var hatalar = new List<string>();
        foreach (var adres in kod.Adresler)
        {
            var csb = new NpgsqlConnectionStringBuilder
            {
                Host = adres, Port = kod.Port, Database = kod.Veritabani, Username = kod.Kullanici, Password = kod.Sifre, Timeout = 4, Pooling = false
            };
            try
            {
                await using var baglanti = new NpgsqlConnection(csb.ConnectionString);
                await baglanti.OpenAsync();
                csb.Password = null; csb.Timeout = 15; csb.Pooling = true;
                VeritabaniKurulumu.AyarYaz(IstemciBaglantiAnahtari, csb.ConnectionString);
                VeritabaniKurulumu.AyarYaz(IstemciSifreAnahtari, VeritabaniKurulumu.SifreKoru(kod.Sifre));
                VeritabaniKurulumu.AyarYaz(AgAyarlari.ModAnahtari, nameof(AgModu.Istemci));
                return adres;
            }
            catch (Exception ex) when (ex is NpgsqlException or SocketException or TimeoutException)
            {
                hatalar.Add($"{adres}: {ex.Message}");
            }
        }
        throw new InvalidOperationException("Sunucuya bağlanılamadı. Sunucu bilgisayarın açık, programın sunucu modunda ve iki bilgisayarın aynı ağda olduğundan emin olun.\n\n" + string.Join("\n", hatalar));
    }

    public static void TekBilgisayaraDon() => VeritabaniKurulumu.AyarYaz(AgAyarlari.ModAnahtari, nameof(AgModu.Tek));

    // ---- yardımcılar ----

    private static bool HazirOlanaKadarBekle(string bin, int port, TimeSpan sure)
    {
        var bitis = DateTime.UtcNow + sure;
        while (DateTime.UtcNow < bitis)
        {
            if (Calistir(Path.Combine(bin, "pg_isready.exe"), "-h", "127.0.0.1", "-p", port.ToString()).Kod == 0) return true;
            Thread.Sleep(1000);
        }
        return false;
    }

    private static void DosyaDuzenle(string yol, Func<string, string> donustur) => File.WriteAllText(yol, donustur(File.ReadAllText(yol)));

    private static void Gerekli((int Kod, string Cikti) sonuc, string mesaj)
    {
        if (sonuc.Kod != 0) throw new InvalidOperationException($"{mesaj}: {sonuc.Cikti.Trim()}");
    }

    private static (int Kod, string Cikti) Calistir(string exe, params string[] argumanlar)
    {
        var bilgi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in argumanlar) bilgi.ArgumentList.Add(a);
        using var surec = Process.Start(bilgi) ?? throw new InvalidOperationException($"{exe} başlatılamadı.");
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        surec.WaitForExit(180_000);
        var metin = cikti.Result + hata.Result;
        Log($"   {Path.GetFileName(exe)} {string.Join(' ', argumanlar)} -> {surec.ExitCode} {metin.Trim()}");
        return (surec.ExitCode, metin);
    }

    private static void Log(string satir)
    {
        try { File.AppendAllText(KurulumLogu, satir + Environment.NewLine); } catch { }
    }
}
