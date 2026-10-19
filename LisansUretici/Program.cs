using System.Security.Cryptography;
using EtkiAlani.Lisanslama;

// Kullanım:
//   dotnet run --project LisansUretici -- anahtar-olustur
//   dotnet run --project LisansUretici -- makine-kodu          (bu bilgisayarın kodu; deneme için)
//   dotnet run --project LisansUretici -- uret --musteri "ABC Ltd." --makine ABCD-EFGH-JKLM-NPQR [--vkn 1234567890] [--bitis 2027-12-31] [--cikti lisans.lic]
//   dotnet run --project LisansUretici -- dogrula --dosya lisans.lic --makine ABCD-EFGH-JKLM-NPQR
//   dotnet run --project LisansUretici -- guncelleme --msi MuhasebeYonetim-1.0.1.msi --surum 1.0.1 --adres https://.../MuhasebeYonetim-1.0.1.msi [--notlar "..."] [--cikti guncelleme.json]
//     (çıkan guncelleme.json ve MSI, programdaki GuncellemeAdresi'nin gösterdiği yere yüklenir)
// Özel anahtar --anahtar ile verilmezse %USERPROFILE%\MuhasebeLisansAnahtari\ozel-anahtar.pem kullanılır.
// Özel anahtar kaybolursa yeni lisans üretilemez; başkasının eline geçerse herkes lisans üretebilir.
var ayarlar = Argumanlar(args.Skip(1).ToArray());
var varsayilanAnahtar = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MuhasebeLisansAnahtari", "ozel-anahtar.pem");
var anahtarYolu = ayarlar.GetValueOrDefault("anahtar") ?? varsayilanAnahtar;

switch (args.FirstOrDefault())
{
    case "anahtar-olustur":
        if (File.Exists(anahtarYolu))
        {
            Console.Error.WriteLine($"Anahtar zaten var: {anahtarYolu}. Üzerine yazılmaz (eski lisanslar geçersiz olurdu).");
            return 1;
        }
        using (var yeni = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(anahtarYolu)!);
            File.WriteAllText(anahtarYolu, yeni.ExportECPrivateKeyPem());
            Console.WriteLine($"Özel anahtar yazıldı: {anahtarYolu}");
            Console.WriteLine("Bu dosyayı güvenli bir yere (şifreli USB vb.) yedekleyin ve kimseyle paylaşmayın.");
            Console.WriteLine("Programa gömülecek açık anahtar:");
            Console.WriteLine(Convert.ToBase64String(yeni.ExportSubjectPublicKeyInfo()));
        }
        return 0;

    case "makine-kodu":
        // Bu bilgisayarın makine kodu (programın Lisans penceresinde gösterdiğiyle aynı hesap).
        using (var anahtar = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64)
                   .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
            Console.WriteLine(Lisans.MakineKoduUret(anahtar?.GetValue("MachineGuid") as string ?? Environment.MachineName));
        return 0;

    case "acik-anahtar":
        using (var mevcut = AnahtarOku(anahtarYolu))
            Console.WriteLine(Convert.ToBase64String(mevcut.ExportSubjectPublicKeyInfo()));
        return 0;

    case "uret":
    {
        var musteri = Zorunlu(ayarlar, "musteri");
        var makine = Zorunlu(ayarlar, "makine");
        DateOnly? bitis = ayarlar.TryGetValue("bitis", out var b) ? DateOnly.Parse(b) : null;
        var bilgi = new LisansBilgisi(
            LisansNo: $"LSN-{DateTime.Now:yyyyMMddHHmmss}",
            Musteri: musteri,
            VergiNo: ayarlar.GetValueOrDefault("vkn"),
            MakineKodu: makine.Trim().ToUpperInvariant(),
            Baslangic: DateOnly.FromDateTime(DateTime.Today),
            Bitis: bitis);
        using var anahtar = AnahtarOku(anahtarYolu);
        var cikti = ayarlar.GetValueOrDefault("cikti") ?? $"lisans-{string.Concat(musteri.Where(char.IsLetterOrDigit))}.lic";
        File.WriteAllText(cikti, Lisans.DosyaMetni(Lisans.Imzala(bilgi, anahtar)));
        Console.WriteLine($"Lisans üretildi: {Path.GetFullPath(cikti)}");
        Console.WriteLine($"  {bilgi.LisansNo} • {bilgi.Musteri} • makine {bilgi.MakineKodu} • {(bitis is null ? "süresiz" : $"{bitis:dd.MM.yyyy} tarihine kadar")}");
        return 0;
    }

    case "dogrula":
    {
        using var anahtar = AnahtarOku(anahtarYolu);
        var sonuc = Lisans.Dogrula(File.ReadAllText(Zorunlu(ayarlar, "dosya")), Convert.ToBase64String(anahtar.ExportSubjectPublicKeyInfo()),
            Zorunlu(ayarlar, "makine"), DateOnly.FromDateTime(DateTime.Today));
        Console.WriteLine(sonuc.Aciklama);
        return sonuc.GecerliMi ? 0 : 2;
    }

    case "guncelleme":
    {
        var msi = Zorunlu(ayarlar, "msi");
        string ozet;
        using (var akis = File.OpenRead(msi)) ozet = Convert.ToHexString(SHA256.HashData(akis));
        var bilgi = new GuncellemeBilgisi(Zorunlu(ayarlar, "surum"), Zorunlu(ayarlar, "adres"), ozet,
            ayarlar.GetValueOrDefault("notlar") ?? "", DateOnly.FromDateTime(DateTime.Today));
        using var anahtar = AnahtarOku(anahtarYolu);
        var metin = Guncelleme.BildirimMetni(bilgi, anahtar);
        if (Guncelleme.Dogrula(metin, Convert.ToBase64String(anahtar.ExportSubjectPublicKeyInfo())) == null)
        {
            Console.Error.WriteLine("Bildirim doğrulanamadı: sürüm (ör. 1.0.1) ve https ile başlayan adres girin.");
            return 2;
        }
        var cikti = ayarlar.GetValueOrDefault("cikti") ?? "guncelleme.json";
        File.WriteAllText(cikti, metin);
        Console.WriteLine($"Güncelleme bildirimi yazıldı: {Path.GetFullPath(cikti)}  (sürüm {bilgi.Surum}, SHA-256 {ozet[..12]}...)");
        return 0;
    }

    default:
        Console.WriteLine("Komutlar: anahtar-olustur | makine-kodu | acik-anahtar | uret --musteri .. --makine .. [--vkn ..] [--bitis yyyy-aa-gg] [--cikti dosya] | dogrula --dosya .. --makine .. | guncelleme --msi .. --surum .. --adres https://.. [--notlar ..]");
        return 1;
}

static ECDsa AnahtarOku(string yol)
{
    if (!File.Exists(yol)) throw new FileNotFoundException("Özel anahtar bulunamadı. Önce 'anahtar-olustur' çalıştırın.", yol);
    var anahtar = ECDsa.Create();
    anahtar.ImportFromPem(File.ReadAllText(yol));
    return anahtar;
}

static Dictionary<string, string> Argumanlar(string[] a)
{
    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < a.Length - 1; i++)
        if (a[i].StartsWith("--")) d[a[i][2..]] = a[++i];
    return d;
}

static string Zorunlu(Dictionary<string, string> d, string ad) =>
    d.TryGetValue(ad, out var v) && !string.IsNullOrWhiteSpace(v) ? v : throw new ArgumentException($"--{ad} zorunludur.");
