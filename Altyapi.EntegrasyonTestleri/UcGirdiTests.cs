using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Uç girdiler: çok uzun açıklama, çok büyük tutar. Ya kayıt güvenle yapılmalı ya da anlaşılır Türkçe mesajla
// reddedilmeli; yarım kayıt kalmamalı.
[Collection("PostgreSQL integration")]
public sealed class UcGirdiTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public UcGirdiTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;
    private static Guid SirketId => MuhasebeBaglami.VarsayilanSirketId;
    private static DateTime Simdi => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);

    [Fact]
    public async Task Uzun_unvanli_cariye_uzun_aciklamali_tahsilat_ve_fatura_kaydedilir()
    {
        var (cariId, urunId, kasaId) = await HazirlaAsync(new string('Ü', 250));
        var uzun = string.Concat(Enumerable.Repeat("Uzun açıklama ", 60)); // ~840 karakter

        await using var db = new MuhasebeBaglami(_fixture.Options);
        var tahsilat = await new TahsilatOdemeServisi(db, StabilizasyonTestOturumu.Olustur()).KaydetAsync(
            new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cariId, 100m, Simdi, uzun, KasaId: kasaId));
        Assert.True(tahsilat.HareketId > 0);

        var oturum = (Cari360TestKullanici)StabilizasyonTestOturumu.Olustur();
        oturum.AktifYetkiler!.FaturaOlustur = true; oturum.AktifYetkiler.FaturaOnayla = true;
        var fs = new FaturaServisi(db, oturum);
        var f = await fs.EkleAsync(new Fatura { CariKartId = cariId, Tarih = Simdi, Tur = FaturaTuru.Satis, FaturaNo = string.Empty, Aciklama = uzun[..480],
            Kalemler = [new FaturaKalemi { UrunId = urunId, Miktar = 1, BirimFiyat = 50, KdvOrani = 20 }] });
        await fs.OnaylaAsync(f.Id);

        await using var k = new MuhasebeBaglami(_fixture.Options);
        Assert.Equal(1, await k.YevmiyeFisleri.CountAsync(x => x.BelgeTipi == "Fatura" && x.BelgeId == f.Id));
        Assert.Equal(60m - 100m, (await k.CariKartlar.AsNoTracking().SingleAsync(c => c.Id == cariId)).Bakiye);

        // Faturanın kendi açıklaması kısaltılmaz: sınırı aşarsa anlaşılır uyarıyla reddedilir.
        var hata = await Assert.ThrowsAsync<DbUpdateException>(() => new FaturaServisi(new MuhasebeBaglami(_fixture.Options), oturum).EkleAsync(new Fatura
        {
            CariKartId = cariId, Tarih = Simdi, Tur = FaturaTuru.Satis, FaturaNo = string.Empty, Aciklama = uzun,
            Kalemler = [new FaturaKalemi { UrunId = urunId, Miktar = 1, BirimFiyat = 50, KdvOrani = 20 }]
        }));
        Assert.Contains("uzunluktan fazla", hata.Message);
    }

    [Fact]
    public async Task Veritabaninin_tutamayacagi_buyuklukte_fatura_turkce_mesajla_reddedilir()
    {
        var (cariId, urunId, _) = await HazirlaAsync("Büyük Tutar Cari");
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var oturum = (Cari360TestKullanici)StabilizasyonTestOturumu.Olustur();
        oturum.AktifYetkiler!.FaturaOlustur = true;
        var hata = await Assert.ThrowsAnyAsync<Exception>(() => new FaturaServisi(db, oturum).EkleAsync(new Fatura
        {
            CariKartId = cariId, Tarih = Simdi, Tur = FaturaTuru.Satis, FaturaNo = string.Empty,
            Kalemler = [new FaturaKalemi { UrunId = urunId, Miktar = 1_000_000, BirimFiyat = 1_000_000_000, KdvOrani = 20 }]
        }));
        Assert.Contains("çok büyük", hata.Message);
    }

    private async Task<(int CariId, int UrunId, int KasaId)> HazirlaAsync(string unvan)
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        if (!await db.Sirketler.AnyAsync(x => x.Id == SirketId))
        {
            db.Sirketler.Add(new Sirket { Id = SirketId, Unvan = "Varsayılan Şirket", SirketKodu = "DEF-01" });
            await db.SaveChangesAsync();
        }
        var cari = new CariKart { SirketId = SirketId, Kod = $"UG-{Guid.NewGuid():N}", Unvan = unvan, Aktif = true, Tur = CariTuru.Musteri };
        var urun = new Urun { SirketId = SirketId, Kod = $"UG-{Guid.NewGuid():N}", Ad = "Uç Girdi Ürün", Birim = "Adet", Aktif = true, StokMiktari = 10 };
        var kasa = new Kasa { SirketId = SirketId, Kod = $"UG-{Guid.NewGuid():N}"[..12], Ad = "Uç Kasa", AktifMi = true };
        db.AddRange(cari, urun, kasa);
        await db.SaveChangesAsync();
        var depo = await DepoKurallari.VarsayilanDepoAsync(db);
        db.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun.Id, DepoId = depo.Id, MevcutMiktar = 10 });
        await db.SaveChangesAsync();
        return (cari.Id, urun.Id, kasa.Id);
    }
}
