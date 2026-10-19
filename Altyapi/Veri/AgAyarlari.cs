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

    // postgresql.conf: tüm ağ arayüzlerinden dinle. Önceki blok varsa yenisiyle değiştirilir.
    public static string KonfigurasyonaAgBloguYaz(string icerik) =>
        BlokuKaldir(icerik).TrimEnd() + Environment.NewLine + Environment.NewLine +
        string.Join(Environment.NewLine, BlokBasi, "listen_addresses = '*'", BlokSonu) + Environment.NewLine;

    // pg_hba.conf: yalnız sunucunun bağlı olduğu yerel ağlardan (samenet), şifreyle (scram) bağlantı.
    public static string ErisimeAgBloguYaz(string icerik) =>
        BlokuKaldir(icerik).TrimEnd() + Environment.NewLine + Environment.NewLine +
        string.Join(Environment.NewLine, BlokBasi, "host    all    all    samenet    scram-sha-256", BlokSonu) + Environment.NewLine;

    public static string BlokuKaldir(string icerik)
    {
        var bas = icerik.IndexOf(BlokBasi, StringComparison.Ordinal);
        if (bas < 0) return icerik;
        var son = icerik.IndexOf(BlokSonu, bas, StringComparison.Ordinal);
        son = son < 0 ? icerik.Length : son + BlokSonu.Length;
        return (icerik[..bas].TrimEnd() + Environment.NewLine + icerik[son..].TrimStart('\r', '\n')).TrimEnd() + Environment.NewLine;
    }
}
