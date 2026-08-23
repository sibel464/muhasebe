using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Altyapi.Veri;
using Altyapi.Servisler;
using EtkiAlani.Modeller.Stok;

namespace TestKonsolu;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== TİCARİ MUHASEBE VERİTABANI VE İŞ KURALLARI TEST PROGRAMI ===");
        
        var optionsBuilder = new DbContextOptionsBuilder<MuhasebeBaglami>();
        optionsBuilder.UseNpgsql("Host=127.0.0.1;Port=5432;Database=Muhasebe;Username=postgres;");

        using var baglam = new MuhasebeBaglami(optionsBuilder.Options);
        var urunServisi = new UrunServisi(baglam);

        try
        {
            // 1. PostgreSQL Bağlantı Kontrolü
            Console.WriteLine("\n[TEST 1] PostgreSQL Bağlantısı kuruluyor...");
            var baglantiOk = await baglam.Database.CanConnectAsync();
            if (!baglantiOk)
            {
                Console.WriteLine("HATA: Veritabanına bağlanılamadı!");
                return;
            }
            Console.WriteLine("BAŞARILI: Bağlantı kuruldu.");

            // Temizlik (Önceki testlerden kalan TEST001 varsa temizle)
            var eskiUrun = await baglam.Urunler.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Kod == "TEST001");
            if (eskiUrun != null)
            {
                baglam.Urunler.Remove(eskiUrun);
                await baglam.SaveChangesAsync();
            }

            // 2. Ürün Ekleme (Create)
            Console.WriteLine("\n[TEST 2] TEST001 ürünü ekleniyor...");
            var yeniUrun = new Urun
            {
                Kod = "TEST001",
                Ad = "Ticari Test Ürünü",
                Kategori = "Test",
                Birim = "Adet",
                AlisFiyati = 100.0000m,
                SatisFiyati = 150.0000m,
                KdvOrani = 20.00m,
                StokMiktari = 10.0000m,
                MinimumStok = 2.0000m,
                Aktif = true
            };
            
            var eklenen = await urunServisi.EkleAsync(yeniUrun);
            Console.WriteLine($"BAŞARILI: Ürün veritabanına eklendi. DB ID: {eklenen.Id}");

            // 3. Kalıcılık ve Listeleme (Read)
            Console.WriteLine("\n[TEST 3] Ürün veritabanından sorgulanıyor...");
            var sorgulanan = await urunServisi.KodIleGetirAsync("TEST001");
            if (sorgulanan == null || sorgulanan.Ad != "Ticari Test Ürünü")
            {
                Console.WriteLine("HATA: Kaydedilen ürün veritabanından okunamadı veya veriler uyuşmuyor!");
                return;
            }
            Console.WriteLine($"BAŞARILI: Veri kalıcılığı doğrulandı. Fiyat: {sorgulanan.SatisFiyati} TL, Stok: {sorgulanan.StokMiktari}");

            // 4. Güncelleme (Update)
            Console.WriteLine("\n[TEST 4] Ürün güncelleniyor (Fiyat 150 -> 175)...");
            sorgulanan.SatisFiyati = 175.0000m;
            sorgulanan.Ad = "Ticari Test Ürünü (Geliştirilmiş)";
            await urunServisi.GuncelleAsync(sorgulanan);
            
            var guncellenen = await urunServisi.IdIleGetirAsync(sorgulanan.Id);
            if (guncellenen == null || guncellenen.SatisFiyati != 175.0000m || guncellenen.Ad != "Ticari Test Ürünü (Geliştirilmiş)")
            {
                Console.WriteLine("HATA: Ürün güncellemesi başarısız oldu!");
                return;
            }
            Console.WriteLine("BAŞARILI: Güncelleme kaydedildi ve doğrulandı.");

            // 5. Benzersiz Ürün Kodu Kısıtlaması (Unique Index Constraint)
            Console.WriteLine("\n[TEST 5] Aynı ürün koduyla (TEST001) ikinci ürün eklenmeye çalışılıyor...");
            var ciftUrun = new Urun
            {
                Kod = "TEST001",
                Ad = "Mükerrer Ürün",
                Birim = "Adet",
                SatisFiyati = 10.00m
            };

            try
            {
                await urunServisi.EkleAsync(ciftUrun);
                Console.WriteLine("HATA: Aynı koda sahip ikinci ürün eklenebildi! Veritabanı benzersizlik kuralı çalışmıyor!");
                return;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"BAŞARILI: UI/Servis seviyesindeki benzersizlik doğrulaması çalıştı: {ex.Message}");
            }
            catch (DbUpdateException)
            {
                Console.WriteLine("BAŞARILI: Veritabanı seviyesindeki Unique Index hatayı yakaladı.");
            }

            // 6. Soft Delete Testi (Delete)
            Console.WriteLine("\n[TEST 6] Ürün siliniyor (Soft Delete)...");
            var silindi = await urunServisi.SilAsync(sorgulanan.Id);
            if (!silindi)
            {
                Console.WriteLine("HATA: Silme komutu başarısız döndü!");
                return;
            }

            // Normal sorguda gelmemeli
            var silinenSorguNormal = await urunServisi.IdIleGetirAsync(sorgulanan.Id);
            if (silinenSorguNormal != null)
            {
                Console.WriteLine("HATA: Soft-delete uygulanan ürün normal sorgularda hala listeleniyor! Global Query Filter çalışmıyor.");
                return;
            }
            Console.WriteLine("BAŞARILI: Silinen ürün normal sorgulardan (Global Query Filter) gizlendi.");

            // Veritabanında fiziksel olarak durmalı (IgnoreQueryFilters)
            var silinenSorguFiziksel = await baglam.Urunler
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == sorgulanan.Id);

            if (silinenSorguFiziksel == null || !silinenSorguFiziksel.Silindi)
            {
                Console.WriteLine("HATA: Kayıt veritabanından tamamen silinmiş veya Silindi flag'i güncellenmemiş!");
                return;
            }
            Console.WriteLine("BAŞARILI: Silinen ürünün veritabanında fiziksel olarak durduğu ve Silindi=True olduğu doğrulandı.");

            Console.WriteLine("\n=======================================================");
            Console.WriteLine("TÜM TEST SENARYOLARI BAŞARIYLA TAMAMLANDI! (100% OK)");
            Console.WriteLine("=======================================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BEKLENMEYEN HATA: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
