using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Altyapi.Veri;

namespace TestKonsolu;

public static class CheckDbUsers
{
    public static async Task TestAsync()
    {
        Console.WriteLine("\n=== DB USERS CHECK ===");
        var connStr = "Host=127.0.0.1;Port=5432;Database=Muhasebe;Username=postgres;";
        var opts = new DbContextOptionsBuilder<MuhasebeBaglami>().UseNpgsql(connStr).Options;

        await using var baglam = new MuhasebeBaglami(opts);

        var kullanicilar = await baglam.Kullanicilar.IgnoreQueryFilters().ToListAsync();
        Console.WriteLine($"Total Users in DB: {kullanicilar.Count}");

        foreach (var u in kullanicilar)
        {
            Console.WriteLine($"\nUser ID: {u.Id}");
            Console.WriteLine($"  KullaniciAdi: '{u.KullaniciAdi}'");
            Console.WriteLine($"  Ad: '{u.Ad}', Soyad: '{u.Soyad}'");
            Console.WriteLine($"  Aktif: {u.Aktif}");
            Console.WriteLine($"  Silindi: {u.Silindi}");
            Console.WriteLine($"  SifreHash: '{u.SifreHash}'");

            bool testAdmin = false;
            bool test123 = false;
            try { testAdmin = BCrypt.Net.BCrypt.Verify("admin", u.SifreHash); } catch (Exception ex) { Console.WriteLine($"  BCrypt 'admin' verify ex: {ex.Message}"); }
            try { test123 = BCrypt.Net.BCrypt.Verify("123456", u.SifreHash); } catch (Exception ex) { Console.WriteLine($"  BCrypt '123456' verify ex: {ex.Message}"); }

            Console.WriteLine($"  BCrypt.Verify('admin'): {testAdmin}");
            Console.WriteLine($"  BCrypt.Verify('123456'): {test123}");
        }
        Console.WriteLine("\n=== END DB USERS CHECK ===");
    }
}
