using System.IO;
using System.Security.Cryptography;
using System.Text;
using Altyapi.Veri;
using EtkiAlani.Lisanslama;
using Microsoft.Win32;

namespace Masaustu.Servisler;

// Kurulum sürümünde lisans denetimi: geçerli imzalı lisans dosyası yoksa ilk açılıştan itibaren 30 gün deneme.
// Deneme bitince program yalnız lisans penceresini açar; veriler silinmez, yedek klasörüne erişim sürer.
// Geliştirme ortamında (gömülü PostgreSQL yokken) denetim yapılmaz.
public static class LisansYoneticisi
{
    // LisansUretici "anahtar-olustur" çıktısındaki açık anahtar. Özel anahtar yalnız satıcıdadır.
    private const string AcikAnahtar = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJ0n8zVKs3oB42FWEt9cJ3W3KjQyOo5YQ5oWVXcR2TGdeOifAwb6UXvJt4d+uq2uvW6Ji00b28eOzPcyhkHunSA==";
    public const int DenemeGunu = 30;
    private static readonly byte[] Entropi = Encoding.UTF8.GetBytes("MuhasebeYonetim.Deneme.v1");

    public static bool DenetimGerekli => UygulamaKlasorleri.GomuluPostgresBin != null;
    public static string LisansDosyaYolu => Path.Combine(UygulamaKlasorleri.VeriKoku, "lisans.lic");
    private static string DenemeDosyaYolu => Path.Combine(UygulamaKlasorleri.VeriKoku, "deneme.dat");

    public sealed record Durum(bool Kullanilabilir, LisansSonucu? Lisans, int? KalanDenemeGunu)
    {
        public string Aciklama => Lisans?.GecerliMi == true
            ? Lisans.Aciklama
            : KalanDenemeGunu is > 0 ? $"Deneme sürümü: {KalanDenemeGunu} gün kaldı."
            : (Lisans != null ? Lisans.Aciklama + " " : "") + "Deneme süresi doldu; devam etmek için lisans dosyası yükleyin.";
    }

    public static string MakineKodu()
    {
        string? kimlik = null;
        try
        {
            using var anahtar = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            kimlik = anahtar?.GetValue("MachineGuid") as string;
        }
        catch
        {
        }
        return Lisans.MakineKoduUret(kimlik ?? Environment.MachineName);
    }

    public static Durum DurumGetir()
    {
        var bugun = DateOnly.FromDateTime(DateTime.Today);
        LisansSonucu? lisans = null;
        if (File.Exists(LisansDosyaYolu))
        {
            lisans = Lisans.Dogrula(File.ReadAllText(LisansDosyaYolu), AcikAnahtar, MakineKodu(), bugun);
            if (lisans.GecerliMi) return new Durum(true, lisans, null);
        }
        var kalan = DenemeGunu - (bugun.DayNumber - DenemeBaslangici().DayNumber);
        return new Durum(kalan > 0, lisans, Math.Max(kalan, 0));
    }

    // Seçilen lisans dosyasını doğrular; geçerliyse ortak klasöre kopyalar.
    public static LisansSonucu Yukle(string dosyaYolu)
    {
        var metin = File.ReadAllText(dosyaYolu);
        var sonuc = Lisans.Dogrula(metin, AcikAnahtar, MakineKodu(), DateOnly.FromDateTime(DateTime.Today));
        if (sonuc.GecerliMi)
        {
            Directory.CreateDirectory(UygulamaKlasorleri.VeriKoku);
            File.WriteAllText(LisansDosyaYolu, metin);
        }
        return sonuc;
    }

    // Deneme başlangıcı ilk açılışta yazılır; tarih Windows DPAPI ile korunduğu için elle düzenlenemez,
    // bozulmuş dosya "bitmiş deneme" sayılır. Dosya tamamen silinirse deneme yeniden başlar: internetsiz
    // denemenin bilinen sınırı (asıl koruma lisans dosyasının imzası ve makineye bağlı olmasıdır).
    private static DateOnly DenemeBaslangici()
    {
        try
        {
            if (File.Exists(DenemeDosyaYolu))
            {
                var acik = ProtectedData.Unprotect(File.ReadAllBytes(DenemeDosyaYolu), Entropi, DataProtectionScope.LocalMachine);
                return DateOnly.ParseExact(Encoding.UTF8.GetString(acik), "yyyy-MM-dd");
            }
        }
        catch
        {
            return DateOnly.MinValue;
        }
        var bugun = DateOnly.FromDateTime(DateTime.Today);
        try
        {
            Directory.CreateDirectory(UygulamaKlasorleri.VeriKoku);
            File.WriteAllBytes(DenemeDosyaYolu, ProtectedData.Protect(Encoding.UTF8.GetBytes(bugun.ToString("yyyy-MM-dd")), Entropi, DataProtectionScope.LocalMachine));
        }
        catch
        {
        }
        return bugun;
    }
}
