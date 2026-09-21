using System;
using System.Linq;
using System.Threading.Tasks;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;

namespace TestKonsolu;

public static class SeedOmerYeter
{
    public static async Task RunAsync()
    {
        var connectionString = "Host=127.0.0.1;Port=5432;Database=Muhasebe;Username=postgres;";

        var optionsBuilder = new DbContextOptionsBuilder<MuhasebeBaglami>();
        optionsBuilder.UseNpgsql(connectionString);

        using var db = new MuhasebeBaglami(optionsBuilder.Options);
        var sirketId = MuhasebeBaglami.VarsayilanSirketId;
        
        await ZenginTestVerisiYukleyici.YukleAsync(db);
        Console.WriteLine("✅ Zengin ürün ve çoklu depo verileri başarıyla yüklendi!");
        
        var cari = await db.CariKartlar.FirstOrDefaultAsync(c => (c.Unvan ?? "").ToLower().Contains("ömer") || (c.Ad ?? "").ToLower().Contains("ömer"));
        
        if (cari == null)
        {
            var cariTipi = await db.CariTipiTanimlari.FirstOrDefaultAsync();
            if (cariTipi == null)
            {
                cariTipi = new CariTipiTanimi { Kod = "PRK", Ad = "Perakende" };
                db.CariTipiTanimlari.Add(cariTipi);
            }
            
            cari = new CariKart 
            { 
                SirketId = sirketId, 
                Kod = "CARI-0001",
                Ad = "Ömer",
                Soyad = "Yeter",
                Unvan = "Ömer Yeter Perakende Müşterisi",
                CariTipi = cariTipi,
                Tur = CariTuru.Musteri
            };
            db.CariKartlar.Add(cari);
            await db.SaveChangesAsync();
        }

        var urun1 = await db.Urunler.FirstOrDefaultAsync(u => u.Kod == "URUN-001");
        if (urun1 == null) 
        {
            urun1 = new Urun { SirketId = sirketId, Kod = "URUN-001", Ad = "Dizüstü Bilgisayar", Barkod = "12345" };
            db.Urunler.Add(urun1);
        }
        var urun2 = await db.Urunler.FirstOrDefaultAsync(u => u.Kod == "URUN-002");
        if (urun2 == null) 
        {
            urun2 = new Urun { SirketId = sirketId, Kod = "URUN-002", Ad = "Kablosuz Mouse", Barkod = "67890" };
            db.Urunler.Add(urun2);
        }
        await db.SaveChangesAsync();

        var fatura1 = new Fatura 
        { 
            SirketId = sirketId,
            CariKartId = cari.Id,
            FaturaNo = "FAT-2026-001",
            Tur = FaturaTuru.Satis,
            Tarih = DateTime.UtcNow.AddDays(-10),
            VadeTarihi = DateTime.UtcNow.AddDays(20),
            Durum = BelgeDurumu.Onaylandi,
            Kalemler = new System.Collections.Generic.List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = urun1.Id, UrunKodu = urun1.Kod, UrunAd = urun1.Ad, Birim = "Adet", Miktar = 2, BirimFiyat = 15000, KdvOrani = 20, SatirToplami = 36000 }
            },
            GenelToplam = 36000,
            Aciklama = "Laptop satışı"
        };
        db.Faturalar.Add(fatura1);

        var fatura2 = new Fatura 
        { 
            SirketId = sirketId,
            CariKartId = cari.Id,
            FaturaNo = "FAT-2026-002",
            Tur = FaturaTuru.Satis,
            Tarih = DateTime.UtcNow.AddDays(-5),
            VadeTarihi = DateTime.UtcNow.AddDays(25),
            Durum = BelgeDurumu.Onaylandi,
            Kalemler = new System.Collections.Generic.List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = urun2.Id, UrunKodu = urun2.Kod, UrunAd = urun2.Ad, Birim = "Adet", Miktar = 5, BirimFiyat = 500, KdvOrani = 20, SatirToplami = 3000 }
            },
            GenelToplam = 3000,
            Aciklama = "Mouse satışı"
        };
        db.Faturalar.Add(fatura2);

        db.CariHareketler.Add(new CariHareket { CariKartId = cari.Id, Tarih = fatura1.Tarih, BelgeNo = fatura1.FaturaNo, Borc = 36000, Alacak = 0, HareketTuru = CariHareketTuru.SatisFaturasi, Aciklama = "Fatura İşlemi" });
        db.CariHareketler.Add(new CariHareket { CariKartId = cari.Id, Tarih = fatura2.Tarih, BelgeNo = fatura2.FaturaNo, Borc = 3000, Alacak = 0, HareketTuru = CariHareketTuru.SatisFaturasi, Aciklama = "Fatura İşlemi" });

        db.CariHareketler.Add(new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow.AddDays(-2), BelgeNo = "TAH-001", Borc = 0, Alacak = 10000, HareketTuru = CariHareketTuru.Tahsilat, Aciklama = "Kredi Kartı ile Kısmi ödeme" });

        await db.SaveChangesAsync();

        Console.WriteLine($"[BAŞARILI] Ömer Yeter (ID: {cari.Id}) için dummy veriler eklendi.");
    }
}
