using Altyapi.Veri;
using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Siparis;
using EtkiAlani.Modeller.Stok;
using Uygulama.Dto.Rapor;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Rapor rakamlarının muhasebe kurallarına uygunluğu: KDV hariç kâr, dönemli KDV, iadeler, rezerve ve hareketsiz stok.
[Collection("PostgreSQL integration")]
public sealed class RaporDogrulukTests(IsolatedPostgreSqlFixture fixture)
{
    private static DateTime Gun(int fark) => DateTime.SpecifyKind(DateTime.Today.AddDays(fark), DateTimeKind.Utc);

    private async Task<(MuhasebeBaglami Db, Sirket Sirket, CariKart Cari, Urun Urun)> HazirlaAsync()
    {
        fixture.RequireDatabase();
        var db = new MuhasebeBaglami(fixture.Options);
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Rapor Doğruluk", SirketKodu = $"RD-{Guid.NewGuid():N}"[..10] };
        var cari = new CariKart { SirketId = sirket.Id, Kod = $"C-{Guid.NewGuid():N}"[..10], Unvan = "Müşteri", Aktif = true };
        var urun = new Urun { SirketId = sirket.Id, Kod = $"U-{Guid.NewGuid():N}"[..10], Ad = "Ürün", AlisFiyati = 60m, StokMiktari = 0m, KdvOrani = 20m, Aktif = true };
        db.AddRange(sirket, cari, urun);
        await db.SaveChangesAsync();
        return (db, sirket, cari, urun);
    }

    private static Fatura Fatura(Guid sirketId, int cariId, int urunId, FaturaTuru tur, DateTime tarih, decimal matrah, decimal oran) => new()
    {
        SirketId = sirketId, CariKartId = cariId, FaturaNo = $"F-{Guid.NewGuid():N}"[..12], Tur = tur, Durum = BelgeDurumu.Onaylandi, Tarih = tarih,
        GenelToplam = matrah * (1 + oran / 100m),
        Kalemler = [new FaturaKalemi { UrunId = urunId, Miktar = 1m, BirimFiyat = matrah, KdvOrani = oran, KdvTutari = matrah * oran / 100m, SatirToplami = matrah * (1 + oran / 100m) }]
    };

    [Fact]
    public async Task Satis_karı_KDV_haric_ve_KDV_raporu_donemli_iadeler_dusulmus()
    {
        var (db, sirket, cari, urun) = await HazirlaAsync();
        await using var _ = db;
        db.Faturalar.AddRange(
            Fatura(sirket.Id, cari.Id, urun.Id, FaturaTuru.Satis, Gun(0), 100m, 20m),
            Fatura(sirket.Id, cari.Id, urun.Id, FaturaTuru.SatisIade, Gun(0), 100m, 20m),
            Fatura(sirket.Id, cari.Id, urun.Id, FaturaTuru.Satis, Gun(0), 200m, 18m),
            Fatura(sirket.Id, cari.Id, urun.Id, FaturaTuru.Satis, Gun(-60), 1000m, 20m),
            Fatura(sirket.Id, cari.Id, urun.Id, FaturaTuru.Alis, Gun(0), 50m, 20m));
        await db.SaveChangesAsync();
        var servis = StabilizasyonTestOturumu.Rapor(db, sirket.Id);

        var satis = await servis.GetSatisRaporuAsync(new SatisRaporFiltreDto { BaslangicTarihi = DateTime.Today, BitisTarihi = DateTime.Today, MaliyetGor = true, PageSize = 50 });
        var ilk = satis.Items.Single(x => x.KdvOrani == 18m);
        Assert.Equal(200m, ilk.NetTutar);
        Assert.Equal(200m - 60m, ilk.BrutKar);
        Assert.Equal(0m, satis.Items.Where(x => x.KdvOrani == 20m).Sum(x => x.NetTutar));

        var kdv = await servis.GetKdvRaporuAsync(new SatisRaporFiltreDto { BaslangicTarihi = DateTime.Today, BitisTarihi = DateTime.Today });
        var y20 = kdv.Single(x => x.KdvOrani == 20m);
        Assert.Equal(0m, y20.SatisMatrah);
        Assert.Equal(0m, y20.SatisKdv);
        Assert.Equal(50m, y20.AlisMatrah);
        Assert.Equal(10m, y20.AlisKdv);
        var y18 = kdv.Single(x => x.KdvOrani == 18m);
        Assert.Equal(36m, y18.SatisKdv);
    }

    [Fact]
    public async Task Rezerve_yalniz_acik_satis_siparisinin_sevk_edilmemis_kismi_ve_hareketsiz_urun_son_hareketten()
    {
        var (db, sirket, cari, urun) = await HazirlaAsync();
        await using var _ = db;
        var kullanici = await db.Kullanicilar.OrderBy(k => k.Id).Select(k => k.Id).FirstOrDefaultAsync();
        Siparis Siparis(SiparisTuru tur, SiparisDurumu durum, decimal miktar, decimal sevk) => new()
        {
            SiparisNo = $"S-{Guid.NewGuid():N}"[..12], SiparisTuru = tur, Durum = durum, CariKartId = cari.Id, ParaBirimiId = 1, Kur = 1, OlusturanKullaniciId = kullanici,
            Kalemler = [new SiparisKalemi { UrunId = urun.Id, UrunKodu = urun.Kod, UrunAdi = urun.Ad, Birim = "Adet", Miktar = miktar, SevkEdilenMiktar = sevk, BirimFiyat = 1 }]
        };
        db.Siparisler.AddRange(
            Siparis(SiparisTuru.Satis, SiparisDurumu.KismenKarsilandi, 10m, 4m),
            Siparis(SiparisTuru.Satis, SiparisDurumu.Beklemede, 3m, 0m),
            Siparis(SiparisTuru.Alis, SiparisDurumu.Onaylandi, 100m, 0m),
            Siparis(SiparisTuru.Satis, SiparisDurumu.Iptal, 50m, 0m));
        db.StokHareketleri.Add(new StokHareketi { UrunId = urun.Id, SirketId = sirket.Id, Tarih = Gun(-5), HareketTuru = StokHareketTuru.Giris, Miktar = 20m });
        await db.SaveChangesAsync();
        var servis = StabilizasyonTestOturumu.Rapor(db, sirket.Id);

        var stok = await servis.GetStokRaporuAsync(new SatisRaporFiltreDto { PageSize = 50 });
        var satir = stok.Items.Single(x => x.UrunId == urun.Id);
        Assert.Equal(9m, satir.RezerveStok);
        Assert.Equal(Gun(-5).Date, satir.SonHareketTarihi!.Value.Date);

        var hareketsiz = await servis.GetHareketsizUrunlerAsync(new SatisRaporFiltreDto(), 30);
        Assert.DoesNotContain(hareketsiz, x => x.UrunId == urun.Id);
    }
}
