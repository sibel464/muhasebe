using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public class KasaBankaIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;

    public KasaBankaIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private async Task<(Sirket sirket, CariKart cari)> SeedSirketVeCariAsync(MuhasebeBaglami db)
    {
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Finans Test A.Ş.", SirketKodu = $"FN-{Guid.NewGuid():N}" };
        db.Sirketler.Add(sirket);
        await db.SaveChangesAsync();

        var cari = new CariKart { SirketId = sirket.Id, Kod = $"C-FN-{Guid.NewGuid():N}", Unvan = "Finans Cari Ltd.", Aktif = true };
        db.CariKartlar.Add(cari);
        await db.SaveChangesAsync();

        return (sirket, cari);
    }

    [Fact]
    public async Task Kasa_Gelir_Gider_Hareketi_Kaydeder_Ve_Varsayilan_Kasa_Olusturur()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (_, cari) = await SeedSirketVeCariAsync(db);

        var kasaServisi = new KasaServisi(db, StabilizasyonTestOturumu.Olustur());

        var gelir = new KasaHareketi
        {
            Tur = HareketTuru.Gelir,
            FisTuru = KasaFisTuru.Tahsilat,
            Tutar = 1500m,
            Aciklama = "Nakit Tahsilat",
            Tarih = DateTime.UtcNow,
            CariKartId = cari.Id
        };

        var eklenenGelir = await kasaServisi.EkleAsync(gelir);
        Assert.NotNull(eklenenGelir);
        Assert.True(eklenenGelir.Id > 0);
        Assert.True(eklenenGelir.KasaId.HasValue);

        var tumHareketler = await kasaServisi.TumunuGetirAsync();
        Assert.Contains(tumHareketler, x => x.Id == eklenenGelir.Id && x.Tutar == 1500m);
    }

    [Fact]
    public async Task Banka_Gelir_Gider_Hareketi_Kaydeder_Ve_Tarih_Filtresiyle_Listeler()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (_, cari) = await SeedSirketVeCariAsync(db);

        var bankaHesabi = new BankaHesabi
        {
            Kod = $"BNK-{Guid.NewGuid():N}",
            Ad = "Test Bankası",
            HesapTuru = BankaHesapTuru.VadesizTL,
            HesapNo = "1234567890",
            Iban = "TR2600061000123456789005001",
            AktifMi = true
        };
        db.BankaHesaplari.Add(bankaHesabi);
        await db.SaveChangesAsync();

        var bankaServisi = new BankaServisi(db, StabilizasyonTestOturumu.Olustur());

        var bankaHareketi = new BankaHareketi
        {
            BankaHesabiId = bankaHesabi.Id,
            BankaAdi = bankaHesabi.Ad,
            HesapNo = bankaHesabi.HesapNo,
            Tur = HareketTuru.Gelir,
            FisTuru = BankaFisTuru.GelenHavale,
            Tutar = 5000m,
            Aciklama = "Havale Tahsilatı",
            Tarih = DateTime.UtcNow,
            CariKartId = cari.Id,
            OdemeYontemi = OdemeYontemi.Havale
        };

        var eklenen = await bankaServisi.EkleAsync(bankaHareketi);
        Assert.NotNull(eklenen);
        Assert.True(eklenen.Id > 0);
        Assert.Equal(bankaHesabi.Id, eklenen.BankaHesabiId);

        var filtrelenmis = await bankaServisi.TarihAraligindaGetirAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        Assert.Contains(filtrelenmis, x => x.Id == eklenen.Id && x.Tutar == 5000m);
    }
}
