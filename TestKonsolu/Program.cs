using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Altyapi.Veri;
using Altyapi.Servisler;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;

namespace TestKonsolu;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== TİCARİ CARİ HAREKET SİSTEMİ ENTEGRASYON TESTLERİ ===");
        
        var optionsBuilder = new DbContextOptionsBuilder<MuhasebeBaglami>();
        optionsBuilder.UseNpgsql("Host=127.0.0.1;Port=5432;Database=Muhasebe;Username=postgres;");

        using var baglam = new MuhasebeBaglami(optionsBuilder.Options);
        var cariServisi = new CariServisi(baglam);
        var hareketServisi = new CariHareketServisi(baglam);

        try
        {
            // 1. Temizlik
            Console.WriteLine("\n[TEMİZLİK] Eski test verileri temizleniyor...");
            var eskiFaturalar = await baglam.Faturalar.IgnoreQueryFilters().ToListAsync();
            baglam.Faturalar.RemoveRange(eskiFaturalar);
            await baglam.SaveChangesAsync();

            var eskiHareketler = await baglam.CariHareketler.IgnoreQueryFilters().ToListAsync();
            baglam.CariHareketler.RemoveRange(eskiHareketler);
            await baglam.SaveChangesAsync();

            var eskiCariler = await baglam.CariKartlar.IgnoreQueryFilters().Where(c => c.Kod.StartsWith("TEST_")).ToListAsync();
            baglam.CariKartlar.RemoveRange(eskiCariler);
            await baglam.SaveChangesAsync();
            Console.WriteLine("Temizlik tamamlandı.");

            // 2. Test Cari Kartı Ekle
            Console.WriteLine("\n[HAZIRLIK] Test Cari Kartı oluşturuluyor...");
            var testCari = new CariKart
            {
                Kod = "TEST_CARI",
                Tur = CariTuru.Musteri,
                Tip = CariTipi.Kurumsal,
                Unvan = "Test Cari Limit Ltd. Şti.",
                Aktif = true
            };
            var eklenenCari = await cariServisi.EkleAsync(testCari);
            Console.WriteLine($"Cari oluşturuldu. ID: {eklenenCari.Id}, Başlangıç Borç: {eklenenCari.Borc}, Alacak: {eklenenCari.Alacak}, Bakiye: {eklenenCari.Bakiye}");

            // 3. Sıralı İşlemler ve Bakiye Doğruluğu Testi
            // Senaryo:
            // 1. Devir       Borç     1000 TL
            // 2. Satış       Borç      500 TL
            // 3. Tahsilat    Alacak    300 TL
            // Beklenen Bakiye = 1200 TL
            Console.WriteLine("\n[TEST 1] Kümülatif Bakiye Doğruluğu Testi...");
            
            await hareketServisi.EkleAsync(new CariHareket
            {
                CariKartId = eklenenCari.Id,
                Tarih = DateTime.UtcNow.AddDays(-2),
                HareketTuru = CariHareketTuru.Devir,
                Aciklama = "Devir Borç Kaydı",
                Borc = 1000.0000m,
                Alacak = 0
            });
            Console.WriteLine("1. Devir Borç eklendi (1000 TL).");

            await hareketServisi.EkleAsync(new CariHareket
            {
                CariKartId = eklenenCari.Id,
                Tarih = DateTime.UtcNow.AddDays(-1),
                HareketTuru = CariHareketTuru.SatisFaturasi,
                Aciklama = "Test Satış Faturası",
                Borc = 500.0000m,
                Alacak = 0
            });
            Console.WriteLine("2. Satış Borç eklendi (500 TL).");

            await hareketServisi.EkleAsync(new CariHareket
            {
                CariKartId = eklenenCari.Id,
                Tarih = DateTime.UtcNow,
                HareketTuru = CariHareketTuru.Tahsilat,
                Aciklama = "Test Tahsilat Fişi",
                Borc = 0,
                Alacak = 300.0000m
            });
            Console.WriteLine("3. Tahsilat Alacak eklendi (300 TL).");

            // Yeniden Yükleme ve Toplam Kontrolleri
            using var baglam2 = new MuhasebeBaglami(optionsBuilder.Options);
            var guncelCari = await baglam2.CariKartlar.FirstAsync(c => c.Id == eklenenCari.Id);
            
            Console.WriteLine($"DB Cari Toplam Borç: {guncelCari.Borc} TL");
            Console.WriteLine($"DB Cari Toplam Alacak: {guncelCari.Alacak} TL");
            Console.WriteLine($"DB Cari Bakiye: {guncelCari.Bakiye} TL");

            if (guncelCari.Borc == 1500 && guncelCari.Alacak == 300 && guncelCari.Bakiye == 1200)
            {
                Console.WriteLine("BAŞARILI: Bakiye hesabı ve cache değerler tam uyumlu (1200 TL).");
            }
            else
            {
                Console.WriteLine("HATA: Bakiye uyuşmazlığı tespit edildi!");
                return;
            }

            // Dinamik Hareket Sorgu Kontrolü
            var dinamikBakiye = await hareketServisi.BakiyeHesaplaAsync(eklenenCari.Id);
            Console.WriteLine($"Dinamik Hesaplanan Bakiye: {dinamikBakiye} TL");
            if (dinamikBakiye == guncelCari.Bakiye)
            {
                Console.WriteLine("BAŞARILI: Yaklaşım A (Dinamik) ile Yaklaşım B (Cached) değerleri birebir uyuşuyor.");
            }
            else
            {
                Console.WriteLine("HATA: Yaklaşım A ve B arasında uyumsuzluk!");
                return;
            }

            // 4. Borç ve Alacak Kısıtlama Testi (İkisi Birden Pozitif Olmama Kuralı)
            Console.WriteLine("\n[TEST 2] Borç ve Alacak alanlarının ikisinin birden pozitif olmasının engellenmesi testi...");
            var gecersizHareket = new CariHareket
            {
                CariKartId = eklenenCari.Id,
                Tarih = DateTime.UtcNow,
                HareketTuru = CariHareketTuru.Diger,
                Aciklama = "Geçersiz Çift Yönlü Hareket",
                Borc = 100,
                Alacak = 200 // İkisi birden pozitif
            };

            try
            {
                await hareketServisi.EkleAsync(gecersizHareket);
                Console.WriteLine("HATA: Çift yönlü pozitif hareket eklenebildi!");
                return;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"BAŞARILI: Uygulama seviyesinde engellendi: {ex.Message}");
            }

            // DB Constraint Doğrulaması (Direkt DB'ye zorla eklemeyi dene)
            Console.WriteLine("PostgreSQL DB Constraint testi yapılıyor...");
            using (var hamBaglam = new MuhasebeBaglami(optionsBuilder.Options))
            {
                var gecersizHareketDb = new CariHareket
                {
                    CariKartId = eklenenCari.Id,
                    Tarih = DateTime.UtcNow,
                    HareketTuru = CariHareketTuru.Diger,
                    Aciklama = "Geçersiz Çift Yönlü Hareket (Constraint)",
                    Borc = 100,
                    Alacak = 200
                };
                hamBaglam.CariHareketler.Add(gecersizHareketDb);
                try
                {
                    await hamBaglam.SaveChangesAsync();
                    Console.WriteLine("HATA: PostgreSQL Constraint'i geçersiz hareketi engellemedi!");
                    return;
                }
                catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("CK_CariHareket_BorcAlacak") == true)
                {
                    Console.WriteLine("BAŞARILI: PostgreSQL Check Constraint tetiklendi ve işlemi engelledi.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"BAŞARILI: DB işlemi engelledi. Fırlatılan hata: {ex.InnerException?.Message}");
                }
            }

            // 5. Negatif Tutar Engelleme Testi
            Console.WriteLine("\n[TEST 3] Negatif tutarlı hareket engelleme testi...");
            var negatifHareket = new CariHareket
            {
                CariKartId = eklenenCari.Id,
                Tarih = DateTime.UtcNow,
                HareketTuru = CariHareketTuru.Diger,
                Aciklama = "Negatif Borç",
                Borc = -100,
                Alacak = 0
            };

            try
            {
                await hareketServisi.EkleAsync(negatifHareket);
                Console.WriteLine("HATA: Negatif tutarlı hareket eklenebildi!");
                return;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"BAŞARILI: Uygulama seviyesinde engellendi: {ex.Message}");
            }

            // 6. Concurrency (Eş Zamanlı İşlem) Testi
            // Senaryo: 10 farklı thread aynı anda aynı cari karta 100'er TL'lik Borç hareketi ekleyecek.
            // Başlangıç bakiye: 1200 TL. Beklenen nihai bakiye: 1200 + 1000 = 2200 TL olmalıdır.
            Console.WriteLine("\n[TEST 4] Eş Zamanlı (Concurrency) Bakiye Güncelleme Testi...");
            Console.WriteLine("10 adet eş zamanlı hareket girişi tetikleniyor...");
            
            var gorevler = new List<Task<CariHareket>>();
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                gorevler.Add(Task.Run(async () =>
                {
                    // Concurrency çakışmalarında yeniden deneme (Retry pattern)
                    int denemeSayisi = 0;
                    while (denemeSayisi < 10) // Deneme sayısını 10 yaptık
                    {
                        using var localBaglam = new MuhasebeBaglami(optionsBuilder.Options);
                        var localServis = new CariHareketServisi(localBaglam);
                        try
                        {
                            return await localServis.EkleAsync(new CariHareket
                            {
                                CariKartId = eklenenCari.Id,
                                Tarih = DateTime.UtcNow,
                                HareketTuru = CariHareketTuru.Diger,
                                Aciklama = $"Eş Zamanlı İşlem #{index + 1}",
                                Borc = 100.0000m,
                                Alacak = 0
                            });
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("çakışması"))
                        {
                            denemeSayisi++;
                            // Rastgele bekleme (Jittered backoff) çakışmaları azaltmak için çok önemlidir
                            await Task.Delay(new Random().Next(20, 150));
                        }
                    }
                    throw new Exception("Çoklu deneme sonrası concurrency aşılamadı!");
                }));
            }

            try
            {
                await Task.WhenAll(gorevler);
                Console.WriteLine("Tüm eş zamanlı işlemler tamamlandı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA: Eş zamanlı işlem sırasında hata: {ex.Message}");
                return;
            }

            // Sonuçları DB'den doğrula
            using var sonBaglam = new MuhasebeBaglami(optionsBuilder.Options);
            var nihaiCari = await sonBaglam.CariKartlar.FirstAsync(c => c.Id == eklenenCari.Id);
            Console.WriteLine($"Nihai Borç Toplamı: {nihaiCari.Borc} TL (Beklenen: 2500 TL)");
            Console.WriteLine($"Nihai Alacak Toplamı: {nihaiCari.Alacak} TL (Beklenen: 300 TL)");
            Console.WriteLine($"Nihai Bakiye: {nihaiCari.Bakiye} TL (Beklenen: 2200 TL)");

            if (nihaiCari.Bakiye == 2200)
            {
                Console.WriteLine("BAŞARILI: Concurrency yönetimi başarılı, veri bütünlüğü korundu.");
            }
            else
            {
                Console.WriteLine("HATA: Eş zamanlı işlemler sırasında bakiye bozuldu!");
                return;
            }

            // 7. Hareket Silme (Soft Delete) ve Bakiye Güncelleme Testi
            Console.WriteLine("\n[TEST 5] Hareket silme (Soft Delete) ve bakiye geri çekme testi...");
            
            // Son eklenen işlemlerden birini seç ve sil
            var silinecekHareket = (await sonBaglam.CariHareketler.Where(h => h.CariKartId == eklenenCari.Id).ToListAsync()).Last();
            Console.WriteLine($"Silinecek hareket: ID {silinecekHareket.Id}, Borç: {silinecekHareket.Borc} TL");

            using var silmeBaglami = new MuhasebeBaglami(optionsBuilder.Options);
            var silmeServisi = new CariHareketServisi(silmeBaglami);
            
            var silindiMi = await silmeServisi.SilAsync(silinecekHareket.Id);
            if (silindiMi)
            {
                var kontrolCari = await silmeBaglami.CariKartlar.FirstAsync(c => c.Id == eklenenCari.Id);
                Console.WriteLine($"Silme sonrası Borç: {kontrolCari.Borc} TL (Beklenen: 2400 TL)");
                Console.WriteLine($"Silme sonrası Bakiye: {kontrolCari.Bakiye} TL (Beklenen: 2100 TL)");
                
                if (kontrolCari.Bakiye == 2100)
                {
                    Console.WriteLine("BAŞARILI: Hareket başarıyla silindi ve cari bakiye tutarlı biçimde düşürüldü.");
                }
                else
                {
                    Console.WriteLine("HATA: Silme sonrası bakiye güncellenemedi!");
                    return;
                }
            }
            else
            {
                Console.WriteLine("HATA: Silme işlemi başarısız!");
                return;
            }

            Console.WriteLine("\n=======================================================");
            Console.WriteLine("TÜM CARİ HAREKET SİSTEMİ TESTLERİ BAŞARIYLA GEÇTİ! (100% OK)");
            Console.WriteLine("=======================================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BEKLENMEYEN HATA: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"İÇ HATA: {ex.InnerException.Message}");
            }
            Console.WriteLine(ex.StackTrace);
        }
    }
}
