using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Altyapi.Veri;

// Çok kullanıcılı kullanım: verinin durduğu bilgisayar "Sunucu" (veritabanı Windows hizmeti olarak sürekli çalışır,
// yerel ağdan bağlantı kabul eder), diğerleri "İstemci" (kendi veritabanını kullanmaz, sunucuya bağlanır).
// Varsayılan "Tek": veritabanı program açılınca başlar, kapanınca durur, ağdan erişilemez.
public enum AgModu { Tek, Sunucu, Istemci }

public static class AgAyarlari
{
    public const string ModAnahtari = "VeritabaniAyarlari:AgModu";
    public const string HizmetAdi = "MuhasebeYonetimVeritabani";
    public const string GuvenlikDuvariKurali = "Muhasebe Yönetim Veritabanı";
    private const string KodOneki = "MYS1-";
    private const string BlokBasi = "# >>> MuhasebeYonetim ağ ayarları (programca yönetilir)";
    private const string BlokSonu = "# <<< MuhasebeYonetim ağ ayarları";

    public static AgModu ModCoz(string? deger) => Enum.TryParse<AgModu>(deger, ignoreCase: true, out var mod) ? mod : AgModu.Tek;

    public static bool YerelMi(string? host) =>
        string.IsNullOrWhiteSpace(host) || host == "127.0.0.1" || host == "::1" || host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    // Sunucunun istemcilere verdiği bağlantı kodu: adres adayları (bilgisayar adı ve IP'ler), port, veritabanı ve şifre.
    // Kod veritabanına tam erişim verir; yalnız yetkili kişilere verilmelidir.
    public sealed record BaglantiKodu(string[] Adresler, int Port, string Veritabani, string Kullanici, string Sifre);

    public static string KodUret(BaglantiKodu kod) =>
        KodOneki + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(kod)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static BaglantiKodu? KodCoz(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin)) return null;
        metin = string.Concat(metin.Where(c => !char.IsWhiteSpace(c)));
        if (!metin.StartsWith(KodOneki, StringComparison.OrdinalIgnoreCase)) return null;
        var govde = metin[KodOneki.Length..].Replace('-', '+').Replace('_', '/');
        govde += new string('=', (4 - govde.Length % 4) % 4);
        try
        {
            var kod = JsonSerializer.Deserialize<BaglantiKodu>(Convert.FromBase64String(govde));
            return kod is { Adresler.Length: > 0, Port: > 0 and < 65536 } && !string.IsNullOrEmpty(kod.Sifre) ? kod : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException) { return null; }
    }

    public const string SertifikaDosyasi = "server.crt";
    public const string AnahtarDosyasi = "server.key";

    // postgresql.conf: tüm ağ arayüzlerinden dinle; ağ trafiği SSL ile şifrelenir. Önceki blok varsa yenisiyle değiştirilir.
    public static string KonfigurasyonaAgBloguYaz(string icerik) =>
        BlokuKaldir(icerik).TrimEnd() + Environment.NewLine + Environment.NewLine +
        string.Join(Environment.NewLine, BlokBasi, "listen_addresses = '*'", "ssl = on",
            $"ssl_cert_file = '{SertifikaDosyasi}'", $"ssl_key_file = '{AnahtarDosyasi}'", "ssl_min_protocol_version = 'TLSv1.2'", BlokSonu) + Environment.NewLine;

    // pg_hba.conf: yalnız sunucunun bağlı olduğu yerel ağlardan (samenet), yalnız şifreli bağlantıyla (hostssl) ve
    // parolayla (scram). Ağdaki biri trafiği dinlese de fatura, cari ve parola bilgisi okunamaz.
    public static string ErisimeAgBloguYaz(string icerik) =>
        BlokuKaldir(icerik).TrimEnd() + Environment.NewLine + Environment.NewLine +
        string.Join(Environment.NewLine, BlokBasi, "hostssl    all    all    samenet    scram-sha-256", BlokSonu) + Environment.NewLine;

    // Sunucu için kendinden imzalı sertifika (PEM). Amaç ağ trafiğini şifrelemektir; istemci kimlik doğrulamasını
    // parolayla (scram) yapar.
    public static (string Sertifika, string Anahtar) SunucuSertifikasiUret(string bilgisayarAdi)
    {
        using var rsa = RSA.Create(2048);
        var ad = string.Concat(bilgisayarAdi.Where(c => char.IsLetterOrDigit(c) || c is '-' or '.'));
        var istek = new CertificateRequest($"CN={(ad.Length > 0 ? ad : "MuhasebeYonetim")}, O=Muhasebe Yonetim", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        istek.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        istek.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        istek.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var sertifika = istek.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(30));
        return (sertifika.ExportCertificatePem(), rsa.ExportPkcs8PrivateKeyPem());
    }

    public static string BlokuKaldir(string icerik)
    {
        var bas = icerik.IndexOf(BlokBasi, StringComparison.Ordinal);
        if (bas < 0) return icerik;
        var son = icerik.IndexOf(BlokSonu, bas, StringComparison.Ordinal);
        son = son < 0 ? icerik.Length : son + BlokSonu.Length;
        return (icerik[..bas].TrimEnd() + Environment.NewLine + icerik[son..].TrimStart('\r', '\n')).TrimEnd() + Environment.NewLine;
    }
}
