using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Ortak;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System;
using System.Threading.Tasks;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class CariDetayIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public CariDetayIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Cari_GenelBilgileri_Cekilebilir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        
        var sirket = new EtkiAlani.Modeller.Kurumsal.Sirket { Id = Guid.NewGuid(), Unvan = "Test Sirket 1", SirketKodu = $"TST1-{Guid.NewGuid():N}" };
        db.Sirketler.Add(sirket);
        
        var cariTipi = new CariTipiTanimi { Kod = "TEST_TIP", Ad = "Test Tipi" };
        db.CariTipiTanimlari.Add(cariTipi);
        
        var cari = new CariKart 
        { 
            SirketId = sirket.Id, 
            Kod = "TEST-001",
            Ad = "Ahmet",
            Soyad = "Yilmaz",
            CariTipi = cariTipi,
            Unvan = ""
        };
        db.CariKartlar.Add(cari);
        await db.SaveChangesAsync();

        var servis = Cari360TestKullanici.Servis(db, cari.SirketId!.Value);
        var dto = await servis.GetCariGenelBilgiAsync(cari.Id, sirket.Id);

        Assert.NotNull(dto);
        Assert.Equal("Ahmet", dto.Ad);
        Assert.Equal("TEST-001", dto.Kod);
        Assert.Equal("Test Tipi", dto.CariTipi);
    }
    
    [Fact]
    public async Task Baska_Sirkenin_Carisine_Erisilemez()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        
        var sirket1 = new EtkiAlani.Modeller.Kurumsal.Sirket { Id = Guid.NewGuid(), Unvan = "Test Sirket 1", SirketKodu = $"TST1-{Guid.NewGuid():N}" };
        var sirket2 = new EtkiAlani.Modeller.Kurumsal.Sirket { Id = Guid.NewGuid(), Unvan = "Test Sirket 2", SirketKodu = $"TST2-{Guid.NewGuid():N}" };
        db.Sirketler.AddRange(sirket1, sirket2);
        
        var cariTipi = new CariTipiTanimi { Kod = "TEST_TIP2", Ad = "Test Tipi2" };
        db.CariTipiTanimlari.Add(cariTipi);
        
        var cari = new CariKart 
        { 
            SirketId = sirket1.Id, 
            Kod = "TEST-002",
            CariTipi = cariTipi
        };
        db.CariKartlar.Add(cari);
        await db.SaveChangesAsync();

        var servis = Cari360TestKullanici.Servis(db, cari.SirketId!.Value);
        
        // Firma2 olarak giriş yapmış birisi Firma1 carisini çekmeye çalışıyor
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => servis.GetCariGenelBilgiAsync(cari.Id, sirket2.Id));
        Assert.Equal("Seçilen cari aktif firmaya ait değil.", ex.Message);
    }

    [Fact]
    public async Task CariHareket_YuruyenBakiye_Hesabi_Dogrudur()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        
        var sirket = new EtkiAlani.Modeller.Kurumsal.Sirket { Id = Guid.NewGuid(), Unvan = "Test Sirket 3", SirketKodu = $"TST3-{Guid.NewGuid():N}" };
        db.Sirketler.Add(sirket);

        var cariTipi = new CariTipiTanimi { Kod = "TEST_TIP3", Ad = "Test Tipi3" };
        db.CariTipiTanimlari.Add(cariTipi);
        
        var cari = new CariKart { SirketId = sirket.Id, Kod = "TEST-003", CariTipi = cariTipi };
        db.CariKartlar.Add(cari);
        await db.SaveChangesAsync();

        db.CariHareketler.AddRange(
            new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow.AddDays(-2), Borc = 1000, Alacak = 0, HareketTuru = CariHareketTuru.SatisFaturasi },
            new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow.AddDays(-1), Borc = 0, Alacak = 400, HareketTuru = CariHareketTuru.Tahsilat }
        );
        await db.SaveChangesAsync();

        var servis = Cari360TestKullanici.Servis(db, cari.SirketId!.Value);
        var hareketler = await servis.GetCariHareketlerAsync(cari.Id, sirket.Id);

        Assert.Equal(2, hareketler.Count);
        
        // Tarihe göre artan (asc) işlendiğinde:
        // 1. Hareket: Borç 1000 -> Bakiye 1000 Borçlu
        // 2. Hareket: Alacak 400 -> Bakiye 600 Borçlu
        // Ancak DTO listesi OrderByDescending(Tarih) ile geri dönüyor.
        
        var sonHareket = hareketler[0]; // En yeni hareket (Alacak 400)
        Assert.Equal(600, sonHareket.Bakiye);
        Assert.Equal("Borç", sonHareket.BakiyeYonu);

        var ilkHareket = hareketler[1]; // Eski hareket (Borc 1000)
        Assert.Equal(1000, ilkHareket.Bakiye);
    }
}
