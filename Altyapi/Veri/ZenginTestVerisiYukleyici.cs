using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;

namespace Altyapi.Veri;

public static class ZenginTestVerisiYukleyici
{
    public static async Task YukleAsync(MuhasebeBaglami context)
    {
        var sirket = await context.Sirketler.FirstOrDefaultAsync();
        var sirketId = sirket?.Id ?? MuhasebeBaglami.VarsayilanSirketId;

        // 1. ŞUBE & DEPOLARI GARANTİ ET
        var sube = await context.Subeler.FirstOrDefaultAsync();
        if (sube == null)
        {
            sube = new Sube { SirketId = sirketId, SubeKodu = "SUBE-01", SubeAdi = "Merkez Şube", AktifMi = true };
            context.Subeler.Add(sube);
            await context.SaveChangesAsync();
        }

        var dMagaza = await context.Depolar.FirstOrDefaultAsync(d => d.DepoKodu == "MAG-01");
        if (dMagaza == null)
        {
            dMagaza = new Depo { SubeId = sube.Id, DepoKodu = "MAG-01", DepoAdi = "Merkez Mağaza", DepoTuru = DepoTuru.Magaza, Sorumlu = "Ahmet Yılmaz (Mağaza Müdürü)", Telefon = "0212 555 1001", Adres = "Kadıköy / İstanbul", MuhasebeKodu = "153.01.001" };
            context.Depolar.Add(dMagaza);
        }

        var dDepo1 = await context.Depolar.FirstOrDefaultAsync(d => d.DepoKodu == "DEP-01");
        if (dDepo1 == null)
        {
            dDepo1 = new Depo { SubeId = sube.Id, DepoKodu = "DEP-01", DepoAdi = "Depo 1 (Ana Lojistik)", DepoTuru = DepoTuru.Depo, Sorumlu = "Mustafa Kaya (Depo Şefi)", Telefon = "0212 555 1002", Adres = "Tuzla Lojistik Üssü", MuhasebeKodu = "153.01.002" };
            context.Depolar.Add(dDepo1);
        }

        var dDepo2 = await context.Depolar.FirstOrDefaultAsync(d => d.DepoKodu == "DEP-02");
        if (dDepo2 == null)
        {
            dDepo2 = new Depo { SubeId = sube.Id, DepoKodu = "DEP-02", DepoAdi = "Depo 2 (Yedek Stok)", DepoTuru = DepoTuru.Depo, Sorumlu = "Kemal Demir (Sevkiyat Amiri)", Telefon = "0212 555 1003", Adres = "Gebze Depolama Alanı", MuhasebeKodu = "153.01.003" };
            context.Depolar.Add(dDepo2);
        }

        await context.SaveChangesAsync();

        // 2. CARİLER (TEDARİKÇİ & MÜŞTERİ)
        var cariTipi = await context.CariTipiTanimlari.FirstOrDefaultAsync();
        if (cariTipi == null)
        {
            cariTipi = new CariTipiTanimi { Kod = "TIC", Ad = "Ticari" };
            context.CariTipiTanimlari.Add(cariTipi);
            await context.SaveChangesAsync();
        }

        var tedarikci1 = await context.CariKartlar.FirstOrDefaultAsync(c => c.Kod == "TED-001");
        if (tedarikci1 == null)
        {
            tedarikci1 = new CariKart { SirketId = sirketId, Kod = "TED-001", Unvan = "İndeks Bilgisayar A.Ş.", Ad = "İndeks", Soyad = "Bilgisayar", Tur = CariTuru.Tedarikci, CariTipiId = cariTipi.Id, Telefon = "0212 331 5200", Eposta = "siparis@indeks.com.tr", Il = "İstanbul", Ilce = "Ayazağa" };
            context.CariKartlar.Add(tedarikci1);
        }

        var tedarikci2 = await context.CariKartlar.FirstOrDefaultAsync(c => c.Kod == "TED-002");
        if (tedarikci2 == null)
        {
            tedarikci2 = new CariKart { SirketId = sirketId, Kod = "TED-002", Unvan = "Penta Teknoloji Dağıtım A.Ş.", Ad = "Penta", Soyad = "Teknoloji", Tur = CariTuru.Tedarikci, CariTipiId = cariTipi.Id, Telefon = "0216 528 0000", Eposta = "kurumsal@penta.com.tr", Il = "İstanbul", Ilce = "Ümraniye" };
            context.CariKartlar.Add(tedarikci2);
        }

        var musteri1 = await context.CariKartlar.FirstOrDefaultAsync(c => c.Kod == "MUS-001");
        if (musteri1 == null)
        {
            musteri1 = new CariKart { SirketId = sirketId, Kod = "MUS-001", Unvan = "Vural Mimarlık & Tasarım Ltd. Şti.", Ad = "Vural", Soyad = "Mimarlık", Tur = CariTuru.Musteri, CariTipiId = cariTipi.Id, Telefon = "0216 444 8899", Eposta = "muhasebe@vuralmimarlik.com", Il = "İstanbul", Ilce = "Kadıköy" };
            context.CariKartlar.Add(musteri1);
        }

        var musteri2 = await context.CariKartlar.FirstOrDefaultAsync(c => c.Kod == "MUS-002");
        if (musteri2 == null)
        {
            musteri2 = new CariKart { SirketId = sirketId, Kod = "MUS-002", Unvan = "Trend Yazılım Bilişim Danışmanlık A.Ş.", Ad = "Trend", Soyad = "Yazılım", Tur = CariTuru.Musteri, CariTipiId = cariTipi.Id, Telefon = "0312 210 5050", Eposta = "info@trendyazilim.com", Il = "Ankara", Ilce = "Çankaya" };
            context.CariKartlar.Add(musteri2);
        }

        await context.SaveChangesAsync();

        // 3. GERÇEKÇİ ÜRÜN LİSTESİ
        var ornekUrunler = new (string Kod, string Ad, string Barkod, string Kategori, decimal AlisFiyat, decimal SatisFiyat, decimal Kdv, decimal MinStok, decimal MagazaStok, decimal Depo1Stok, decimal Depo2Stok)[]
        {
            ("IPHONE-15PM-256", "Apple iPhone 15 Pro Max 256GB Titanyum", "8680001001", "Akıllı Telefon", 62000, 74999, 20, 3, 5, 45, 20),
            ("SAMS-S24U-512", "Samsung Galaxy S24 Ultra 512GB Titanyum Siyah", "8680001002", "Akıllı Telefon", 58000, 69999, 20, 3, 4, 35, 15),
            ("MACBOOK-M3-16", "Apple MacBook Air M3 16GB RAM 512GB SSD Uzay Grisi", "8680001003", "Bilgisayar", 44000, 52999, 20, 2, 3, 25, 10),
            ("ASUS-ROG-G16", "Asus ROG Strix G16 i9 RTX 4070 32GB Gaming Laptop", "8680001004", "Bilgisayar", 59000, 71500, 20, 2, 2, 18, 8),
            ("DELL-U2723QE", "Dell UltraSharp 27\" 4K IPS Profesyonel Monitör", "8680001005", "Çevre Birimleri", 18500, 23499, 20, 4, 6, 40, 20),
            ("SONY-WH1000XM5", "Sony WH-1000XM5 Gürültü Engelleyici Kablosuz Kulaklık", "8680001006", "Ses Sistemleri", 12000, 15499, 20, 5, 8, 50, 25),
            ("LOGI-MX-M3S", "Logitech MX Master 3S Ergonomik Kablosuz Mouse", "8680001007", "Çevre Birimleri", 3200, 4299, 20, 10, 15, 80, 40),
            ("AIRPODS-PRO-2", "Apple AirPods Pro 2. Nesil USB-C Şarj Kutulu", "8680001008", "Ses Sistemleri", 7500, 9499, 20, 8, 12, 60, 30),
            ("EPSON-L3250", "Epson EcoTank L3250 Wi-Fi Çok Fonksiyonlu Tanklı Yazıcı", "8680001009", "Yazıcı & Tarayıcı", 5400, 6999, 20, 3, 4, 30, 15),
            ("SANDISK-EXT-1TB", "SanDisk 1TB Extreme 1050MB/s Taşınabilir Type-C SSD", "8680001010", "Depolama", 3100, 4199, 20, 10, 18, 90, 45),
            ("ANKER-735-GAN", "Anker 735 65W GaN III 3 Portlu Hızlı Şarj Adaptörü", "8680001011", "Aksesuar", 1400, 1999, 20, 15, 25, 120, 60),
            ("PHILIPS-HUE-KIT", "Philips Hue Akıllı Başlangıç Kiti RGB LED Seti", "8680001012", "Akıllı Ev", 4200, 5499, 20, 5, 7, 35, 15)
        };

        foreach (var ou in ornekUrunler)
        {
            var urun = await context.Urunler.FirstOrDefaultAsync(u => u.Kod == ou.Kod);
            var toplamStok = ou.MagazaStok + ou.Depo1Stok + ou.Depo2Stok;

            if (urun == null)
            {
                urun = new Urun
                {
                    SirketId = sirketId,
                    DepoId = dDepo1.Id,
                    Kod = ou.Kod,
                    Ad = ou.Ad,
                    Barkod = ou.Barkod,
                    Kategori = ou.Kategori,
                    Birim = "Adet",
                    AlisFiyati = ou.AlisFiyat,
                    PerakendeSatisFiyati = ou.SatisFiyat,
                    ToptanSatisFiyati = ou.SatisFiyat * 0.90m,
                    KdvOrani = ou.Kdv,
                    MinimumStok = ou.MinStok,
                    StokMiktari = toplamStok,
                    Aktif = true
                };
                context.Urunler.Add(urun);
                await context.SaveChangesAsync();
            }
            else
            {
                urun.Ad = ou.Ad;
                urun.AlisFiyati = ou.AlisFiyat;
                urun.PerakendeSatisFiyati = ou.SatisFiyat;
                urun.MinimumStok = ou.MinStok;
                urun.StokMiktari = toplamStok;
            }

            // Lokasyon Stoklarını Güncelle (Mağaza, Depo 1, Depo 2)
            await LokasyonStokAyarlaAsync(context, urun.Id, dMagaza.Id, ou.MagazaStok);
            await LokasyonStokAyarlaAsync(context, urun.Id, dDepo1.Id, ou.Depo1Stok);
            await LokasyonStokAyarlaAsync(context, urun.Id, dDepo2.Id, ou.Depo2Stok);
        }

        await context.SaveChangesAsync();

        // 4. ÖRNEK DEPOLAR ARASI TRANSFER FİŞLERİ & İZLENEBİLİRLİK
        if (!await context.DepoTransferFisleri.AnyAsync())
        {
            var u1 = await context.Urunler.FirstAsync(u => u.Kod == "IPHONE-15PM-256");
            var u2 = await context.Urunler.FirstAsync(u => u.Kod == "MACBOOK-M3-16");
            var u3 = await context.Urunler.FirstAsync(u => u.Kod == "LOGI-MX-M3S");

            var trf1 = new DepoTransferFisi
            {
                FisNo = "TRF-2026-00001",
                Tarih = DateTime.UtcNow.AddDays(-3),
                KaynakDepoId = dDepo1.Id,
                HedefDepoId = dMagaza.Id,
                Durum = DepoTransferDurumu.Tamamlandi,
                Aciklama = "Mağaza vitrin ve hafta sonu perakende stoku takviyesi",
                SevkEdenPersonel = "Mustafa Kaya",
                TeslimAlanPersonel = "Ahmet Yılmaz",
                ToplamMiktar = 15,
                ToplamTutar = (10 * u1.AlisFiyati) + (5 * u2.AlisFiyati),
                Kalemler = new List<DepoTransferKalemi>
                {
                    new DepoTransferKalemi { UrunId = u1.Id, Miktar = 10, BirimFiyat = u1.AlisFiyati, Not = "Vitrin Teşhir" },
                    new DepoTransferKalemi { UrunId = u2.Id, Miktar = 5, BirimFiyat = u2.AlisFiyati, Not = "Mağaza Stok" }
                }
            };

            var trf2 = new DepoTransferFisi
            {
                FisNo = "TRF-2026-00002",
                Tarih = DateTime.UtcNow.AddDays(-1),
                KaynakDepoId = dDepo1.Id,
                HedefDepoId = dDepo2.Id,
                Durum = DepoTransferDurumu.Tamamlandi,
                Aciklama = "Yedek depoya lojistik aktarım",
                SevkEdenPersonel = "Mustafa Kaya",
                TeslimAlanPersonel = "Kemal Demir",
                ToplamMiktar = 20,
                ToplamTutar = 20 * u3.AlisFiyati,
                Kalemler = new List<DepoTransferKalemi>
                {
                    new DepoTransferKalemi { UrunId = u3.Id, Miktar = 20, BirimFiyat = u3.AlisFiyati, Not = "Koli Sevkiyat" }
                }
            };

            context.DepoTransferFisleri.AddRange(trf1, trf2);

            // Stok Hareketleri (İzlenebilirlik)
            context.StokHareketleri.AddRange(
                new StokHareketi
                {
                    UrunId = u1.Id,
                    DepoId = dDepo1.Id,
                    CariKartId = tedarikci1.Id,
                    Tarih = DateTime.UtcNow.AddDays(-7),
                    HareketTuru = StokHareketTuru.Giris,
                    IslemTuru = StokIslemTuru.AlisFaturasi,
                    FisNo = "FAT-2026-0089",
                    BelgeNo = "İND-9921",
                    Miktar = 50,
                    BirimFiyat = u1.AlisFiyati,
                    Aciklama = $"{tedarikci1.Unvan} ➔ Depo 1 Mal Kabul Girişi"
                },
                new StokHareketi
                {
                    UrunId = u1.Id,
                    DepoId = dDepo1.Id,
                    HedefDepoId = dMagaza.Id,
                    Tarih = DateTime.UtcNow.AddDays(-3),
                    HareketTuru = StokHareketTuru.Cikis,
                    IslemTuru = StokIslemTuru.DepoTransferi,
                    FisNo = trf1.FisNo,
                    Miktar = 10,
                    BirimFiyat = u1.AlisFiyati,
                    Aciklama = $"{dDepo1.DepoAdi} ➔ {dMagaza.DepoAdi} Sevk"
                },
                new StokHareketi
                {
                    UrunId = u1.Id,
                    DepoId = dMagaza.Id,
                    HedefDepoId = dDepo1.Id,
                    Tarih = DateTime.UtcNow.AddDays(-3),
                    HareketTuru = StokHareketTuru.Giris,
                    IslemTuru = StokIslemTuru.DepoTransferi,
                    FisNo = trf1.FisNo,
                    Miktar = 10,
                    BirimFiyat = u1.AlisFiyati,
                    Aciklama = $"{dDepo1.DepoAdi} ➔ {dMagaza.DepoAdi} Kabul"
                },
                new StokHareketi
                {
                    UrunId = u1.Id,
                    DepoId = dMagaza.Id,
                    CariKartId = musteri1.Id,
                    Tarih = DateTime.UtcNow.AddDays(-2),
                    HareketTuru = StokHareketTuru.Cikis,
                    IslemTuru = StokIslemTuru.SatisFaturasi,
                    FisNo = "FAT-2026-0012",
                    Miktar = 2,
                    BirimFiyat = u1.PerakendeSatisFiyati,
                    Aciklama = $"Merkez Mağaza ➔ {musteri1.Unvan} Perakende Satış"
                }
            );

            await context.SaveChangesAsync();
        }
    }

    private static async Task LokasyonStokAyarlaAsync(MuhasebeBaglami context, int urunId, int depoId, decimal miktar)
    {
        var ls = await context.UrunLokasyonStoklari
            .FirstOrDefaultAsync(l => l.UrunId == urunId && l.DepoId == depoId);

        if (ls == null)
        {
            ls = new UrunLokasyonStogu { UrunId = urunId, DepoId = depoId, MevcutMiktar = miktar };
            context.UrunLokasyonStoklari.Add(ls);
        }
        else
        {
            ls.MevcutMiktar = miktar;
        }
    }
}
