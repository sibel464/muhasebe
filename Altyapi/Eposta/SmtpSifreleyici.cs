using System.Security.Cryptography;
using System.Text;

namespace Altyapi.Eposta;

// SMTP şifresi veritabanında düz metin durmasın diye AES ile şifrelenir. Ağ kurulumunda her bilgisayar aynı ayarı
// okuyabilsin diye anahtar programa gömülüdür; bu, veritabanı yedeğine bakan birinin şifreyi doğrudan görmesini engeller.
public static class SmtpSifreleyici
{
    private const string Onek = "v1:";
    private static readonly byte[] Anahtar = SHA256.HashData(Encoding.UTF8.GetBytes("MuhasebeYonetim.Smtp.v1/2026"));

    public static string? Sifrele(string? sifre)
    {
        if (string.IsNullOrEmpty(sifre)) return null;
        using var aes = Aes.Create();
        aes.Key = Anahtar;
        aes.GenerateIV();
        var sifreli = aes.EncryptCbc(Encoding.UTF8.GetBytes(sifre), aes.IV);
        return Onek + Convert.ToBase64String(aes.IV.Concat(sifreli).ToArray());
    }

    public static string? Coz(string? kayit)
    {
        if (string.IsNullOrEmpty(kayit)) return null;
        if (!kayit.StartsWith(Onek, StringComparison.Ordinal)) return kayit;
        var veri = Convert.FromBase64String(kayit[Onek.Length..]);
        using var aes = Aes.Create();
        aes.Key = Anahtar;
        return Encoding.UTF8.GetString(aes.DecryptCbc(veri[16..], veri[..16]));
    }
}
