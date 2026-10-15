using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Fatura stok düşümü sabit depo numarasına (1-2-3) değil, aktif depolara göre yapılmalı.
[Collection("PostgreSQL integration")]
public sealed class FaturaDepoKurallariIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public FaturaDepoKurallariIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Depo_secilmeyen_kalem_ilk_aktif_magazadan_duser_sahte_stok_acilmaz()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var varsayilan = await DepoKurallari.VarsayilanDepoAsync(db);
        var (fatura, urun) = await FaturaHazirlaAsync(db, varsayilanDepoStogu: 5, satilan: 2, depoId: null);

        await new FinancialTransactionService(db, StabilizasyonTestOturumuOnayli()).CreateFromInvoiceAsync(fatura.Id);

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        var hareket = await kontrol.StokHareketleri.SingleAsync(h => h.IlgiliBelgeId == fatura.Id);
        Assert.Equal(varsayilan.Id, hareket.DepoId);
        Assert.Equal(3m, (await kontrol.UrunLokasyonStoklari.SingleAsync(l => l.UrunId == urun.Id && l.DepoId == varsayilan.Id)).MevcutMiktar);
        // Başka depoda kayıt kendiliğinden (toplam stokla) açılmamalı.
        Assert.Equal(1, await kontrol.UrunLokasyonStoklari.CountAsync(l => l.UrunId == urun.Id));
    }

    [Fact]
    public async Task Pasif_depodan_stok_dusulemez()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var ornek = await db.Depolar.FirstAsync();
        var pasif = new Depo { SubeId = ornek.SubeId, DepoKodu = "PSF-" + Guid.NewGuid().ToString("N")[..6], DepoAdi = "Pasif Test Deposu", AktifMi = false };
        db.Depolar.Add(pasif);
        await db.SaveChangesAsync();
        var (fatura, _) = await FaturaHazirlaAsync(db, varsayilanDepoStogu: 5, satilan: 1, depoId: pasif.Id);

        var hata = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new FinancialTransactionService(db, StabilizasyonTestOturumuOnayli()).CreateFromInvoiceAsync(fatura.Id));
        Assert.Contains("pasif", hata.Message);
    }

    [Fact]
    public async Task Fatura_fisinin_120_satiri_cariye_baglanir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (fatura, _) = await FaturaHazirlaAsync(db, varsayilanDepoStogu: 5, satilan: 1, depoId: null);

        var fis = await new FinancialTransactionService(db, StabilizasyonTestOturumuOnayli()).CreateFromInvoiceAsync(fatura.Id);

        var kodlar = await db.HesapPlanlari.ToDictionaryAsync(h => h.Id, h => h.HesapKodu);
        Assert.Equal(fatura.CariKartId, fis.Satirlar.Single(s => kodlar[s.HesapId] == "120").CariKartId);
        Assert.All(fis.Satirlar.Where(s => kodlar[s.HesapId] != "120"), s => Assert.Null(s.CariKartId));
    }

    private static async Task<(Fatura Fatura, Urun Urun)> FaturaHazirlaAsync(MuhasebeBaglami db, decimal varsayilanDepoStogu, decimal satilan, int? depoId)
    {
        var sirketId = MuhasebeBaglami.VarsayilanSirketId;
        if (!await db.Sirketler.AnyAsync(x => x.Id == sirketId))
        {
            db.Sirketler.Add(new Sirket { Id = sirketId, Unvan = "Varsayılan Şirket", SirketKodu = "DEF-01" });
            await db.SaveChangesAsync();
        }
        var varsayilan = await DepoKurallari.VarsayilanDepoAsync(db);
        var cari = new CariKart { SirketId = sirketId, Kod = $"FD-{Guid.NewGuid():N}", Unvan = "Depo Test Cari", Aktif = true };
        var urun = new Urun { SirketId = sirketId, Kod = $"FD-{Guid.NewGuid():N}", Ad = "Depo Test Ürün", Birim = "Adet", Aktif = true, StokMiktari = 100 };
        db.AddRange(cari, urun);
        await db.SaveChangesAsync();
        db.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun.Id, DepoId = varsayilan.Id, MevcutMiktar = varsayilanDepoStogu });
        var fatura = new Fatura
        {
            SirketId = sirketId, CariKartId = cari.Id, FaturaNo = $"FD-{Guid.NewGuid():N}", Tarih = DateTime.UtcNow, Tur = FaturaTuru.Satis,
            GenelToplam = 120, KdvToplam = 20,
            Kalemler = [new FaturaKalemi
            {
                UrunId = urun.Id, UrunKodu = urun.Kod, UrunAd = urun.Ad, Birim = urun.Birim, Miktar = satilan, BirimFiyat = 100, KdvOrani = 20,
                StokDagilimlari = depoId is int d ? [new FaturaKalemiStokDagilimi { DepoId = d, Miktar = satilan }] : []
            }]
        };
        db.Faturalar.Add(fatura);
        await db.SaveChangesAsync();
        return (fatura, urun);
    }

    private static Uygulama.Arayuzler.IKullaniciServisi StabilizasyonTestOturumuOnayli()
    {
        var oturum = (Cari360TestKullanici)StabilizasyonTestOturumu.Olustur();
        oturum.AktifYetkiler!.FaturaOnayla = true;
        return oturum;
    }
}
