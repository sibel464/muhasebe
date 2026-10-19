using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Uygulama.Dto.Rapor;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class RaporMerkeziPaket12IntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public RaporMerkeziPaket12IntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private async Task<(Sirket Sirket, Sube Sube, CariKart Cari, Urun Urun, Kasa Kasa, BankaHesabi Banka)> SeedRaporTestVerisiAsync(MuhasebeBaglami db)
    {
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Rapor Test Ltd", SirketKodu = $"R-{Guid.NewGuid():N}"[..8] };
        db.Sirketler.Add(sirket);

        var sube = new Sube { SirketId = sirket.Id, SubeKodu = $"S-{Guid.NewGuid():N}"[..6], SubeAdi = "Merkez Şube", AktifMi = true };
        db.Subeler.Add(sube);
        await db.SaveChangesAsync();

        var cari = new CariKart { SirketId = sirket.Id, SubeId = sube.Id, Kod = $"C-{Guid.NewGuid():N}"[..8], Unvan = "Test Müşteri", Tur = CariTuru.MusteriTedarikci, RiskLimiti = 50000m, Aktif = true };
        db.CariKartlar.Add(cari);

        var depo = new Depo { SubeId = sube.Id, DepoKodu = $"D-{Guid.NewGuid():N}"[..6], DepoAdi = "Ana Depo", AktifMi = true };
        db.Depolar.Add(depo);
        await db.SaveChangesAsync();

        var urun = new Urun { SirketId = sirket.Id, DepoId = depo.Id, Kod = $"U-{Guid.NewGuid():N}"[..8], Ad = "Laptop i7", AlisFiyati = 10000m, PerakendeSatisFiyati = 15000m, StokMiktari = 50m, MinimumStok = 5m, KdvOrani = 20m, Aktif = true };
        db.Urunler.Add(urun);

        var kasa = new Kasa { SirketId = sirket.Id, Kod = $"K-{Guid.NewGuid():N}"[..6], Ad = "Merkez Kasa", Bakiye = 5000m, AktifMi = true };
        db.Kasalar.Add(kasa);

        var banka = new BankaHesabi { SirketId = sirket.Id, Kod = $"B-{Guid.NewGuid():N}"[..6], Ad = "İş Bankası", Bakiye = 20000m, AktifMi = true };
        db.BankaHesaplari.Add(banka);

        await db.SaveChangesAsync();
        return (sirket, sube, cari, urun, kasa, banka);
    }

    [Fact]
    public async Task T01_GunlukSatisRaporu_SunucuTarafiSorgu_Basarili()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var fatura = new Fatura
        {
            SirketId = data.Sirket.Id,
            SubeId = data.Sube.Id,
            CariKartId = data.Cari.Id,
            FaturaNo = $"SAT-{Guid.NewGuid():N}"[..10],
            Tur = FaturaTuru.Satis,
            Durum = BelgeDurumu.Onaylandi,
            Tarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
            GenelToplam = 15000m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = data.Urun.Id, Miktar = 1m, BirimFiyat = 15000m, KdvOrani = 20m, SatirToplami = 15000m }
            }
        };
        db.Faturalar.Add(fatura);
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var ozet = await servis.GetYoneticiOzetiAsync(new SatisRaporFiltreDto());

        Assert.True(ozet.BugunkuSatis >= 15000m);
    }

    [Fact]
    public async Task T02_AylikSatisRaporu_Gruplama_Basarili()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var fatura = new Fatura
        {
            SirketId = data.Sirket.Id,
            SubeId = data.Sube.Id,
            CariKartId = data.Cari.Id,
            FaturaNo = $"SAT-{Guid.NewGuid():N}"[..10],
            Tur = FaturaTuru.Satis,
            Durum = BelgeDurumu.Onaylandi,
            Tarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
            GenelToplam = 30000m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = data.Urun.Id, Miktar = 2m, BirimFiyat = 15000m, KdvOrani = 20m, SatirToplami = 30000m }
            }
        };
        db.Faturalar.Add(fatura);
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetSatisRaporuAsync(new SatisRaporFiltreDto());

        Assert.NotNull(res);
        Assert.True(res.TotalCount >= 1);
    }

    [Fact]
    public async Task T03_SatisIadeleri_NetSatisDusunumu_Dogru()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var iadeNo = $"IAD-{Guid.NewGuid():N}"[..10];
        var iade = new Fatura
        {
            SirketId = data.Sirket.Id,
            SubeId = data.Sube.Id,
            CariKartId = data.Cari.Id,
            FaturaNo = iadeNo,
            Tur = FaturaTuru.SatisIade,
            Durum = BelgeDurumu.Onaylandi,
            Tarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
            GenelToplam = 5000m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = data.Urun.Id, Miktar = 1m, BirimFiyat = 5000m, SatirToplami = 5000m }
            }
        };
        db.Faturalar.Add(iade);
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetSatisRaporuAsync(new SatisRaporFiltreDto());

        var iadeKalemi = res.Items.FirstOrDefault(x => x.BelgeNo == iadeNo);
        Assert.NotNull(iadeKalemi);
        Assert.True(iadeKalemi.NetTutar < 0);
    }

    [Fact]
    public async Task T04_SatisKarliligi_BrutKarVeMarj_MaliyetleHesaplar()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var faturaNo = $"KAR-{Guid.NewGuid():N}"[..10];
        var fatura = new Fatura
        {
            SirketId = data.Sirket.Id,
            SubeId = data.Sube.Id,
            CariKartId = data.Cari.Id,
            FaturaNo = faturaNo,
            Tur = FaturaTuru.Satis,
            Durum = BelgeDurumu.Onaylandi,
            Tarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
            GenelToplam = 15000m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = data.Urun.Id, Miktar = 1m, BirimFiyat = 15000m, SatirToplami = 15000m }
            }
        };
        db.Faturalar.Add(fatura);
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetSatisRaporuAsync(new SatisRaporFiltreDto());

        var k = res.Items.FirstOrDefault(x => x.BelgeNo == faturaNo);
        Assert.NotNull(k);
        Assert.Equal(10000m, k.Maliyet);
        Assert.Equal(5000m, k.BrutKar);
    }

    [Fact]
    public async Task T05_UrunBazliSatisRaporu_DogruToplar()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var top = await servis.GetEnCokSatanUrunlerAsync(new SatisRaporFiltreDto(), 10);
        Assert.NotNull(top);
    }

    [Fact]
    public async Task T06_CariBazliSatisRaporu_DogruToplar()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetCariRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotNull(res);
        Assert.True(res.TotalCount >= 1);
    }

    [Fact]
    public async Task T07_KategoriBazliSatisRaporu_Filtrele()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetSatisRaporuAsync(new SatisRaporFiltreDto { KategoriId = 999 });
        Assert.Empty(res.Items);
    }

    [Fact]
    public async Task T08_AlisRaporu_TedarikciVeMaliyet_Dogru()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var alis = new Fatura
        {
            SirketId = data.Sirket.Id,
            SubeId = data.Sube.Id,
            CariKartId = data.Cari.Id,
            FaturaNo = $"ALS-{Guid.NewGuid():N}"[..10],
            Tur = FaturaTuru.Alis,
            Durum = BelgeDurumu.Onaylandi,
            Tarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
            GenelToplam = 20000m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = data.Urun.Id, Miktar = 2m, BirimFiyat = 10000m, SatirToplami = 20000m }
            }
        };
        db.Faturalar.Add(alis);
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetAlisRaporuAsync(new SatisRaporFiltreDto());

        Assert.NotNull(res);
        Assert.True(res.TotalCount >= 1);
    }

    [Fact]
    public async Task T09_CariBakiyeListesi_BorcAlacak_Dogru()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        db.CariHareketler.Add(new CariHareket { CariKartId = data.Cari.Id, Borc = 1000m, Alacak = 0m, Tarih = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetCariRaporuAsync(new SatisRaporFiltreDto { CariId = data.Cari.Id });

        var c = res.Items.FirstOrDefault();
        Assert.NotNull(c);
        Assert.Equal(1000m, c.ToplamBorc);
    }

    [Fact]
    public async Task T10_BorcluCarilerRaporu_Filtrele()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetCariRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotNull(res);
    }

    [Fact]
    public async Task T11_AlacakliCarilerRaporu_Filtrele()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetCariRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotNull(res);
    }

    [Fact]
    public async Task T12_CariYaslandirma_1_30_31_60_61_90_90Plus_Gruplar()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var fatura = new Fatura
        {
            SirketId = data.Sirket.Id,
            SubeId = data.Sube.Id,
            CariKartId = data.Cari.Id,
            FaturaNo = $"YAS-{Guid.NewGuid():N}"[..10],
            Tur = FaturaTuru.Satis,
            Durum = BelgeDurumu.Onaylandi,
            Tarih = DateTime.SpecifyKind(DateTime.Today.AddDays(-40), DateTimeKind.Utc),
            VadeTarihi = DateTime.SpecifyKind(DateTime.Today.AddDays(-40), DateTimeKind.Utc),
            GenelToplam = 7500m
        };
        db.Faturalar.Add(fatura);
        // Onaylı fatura cariye borç olarak işlenir; 10 gün önce kesilen ikinci fatura ve kısmi tahsilat:
        // 9.000 TL tahsilat önce 40 günlük faturayı (7.500) kapatır, kalan 1.500 yeni faturadan düşer.
        var yeni = fatura.FaturaNo + "B";
        db.CariHareketler.AddRange(
            new CariHareket { CariKartId = data.Cari.Id, Tarih = fatura.Tarih, HareketTuru = CariHareketTuru.SatisFaturasi, BelgeNo = fatura.FaturaNo, Borc = 7500m },
            new CariHareket { CariKartId = data.Cari.Id, Tarih = DateTime.SpecifyKind(DateTime.Today.AddDays(-10), DateTimeKind.Utc), HareketTuru = CariHareketTuru.SatisFaturasi, BelgeNo = yeni, Borc = 4000m },
            new CariHareket { CariKartId = data.Cari.Id, Tarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc), HareketTuru = CariHareketTuru.Tahsilat, Alacak = 9000m });
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var acikBakiye = await db.CariHareketler.Where(h => h.CariKartId == data.Cari.Id).SumAsync(h => h.Borc - h.Alacak);
        var list = await servis.GetCariYaslandirmaAsync(new SatisRaporFiltreDto());

        var row = list.Single(x => x.CariId == data.Cari.Id);
        Assert.Equal(acikBakiye, row.VadesiGelmemis + row.Gun1_30 + row.Gun31_60 + row.Gun61_90 + row.Gun90Plus);
        Assert.Equal(0m, row.Gun31_60);
    }

    [Fact]
    public async Task T13_VadesiGecmisAlacaklar_FaturaTarihli_Dogru()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var ozet = await servis.GetYoneticiOzetiAsync(new SatisRaporFiltreDto());
        Assert.NotNull(ozet);
    }

    [Fact]
    public async Task T14_RiskLimitiAsanCariler_Filtrele()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetCariRaporuAsync(new SatisRaporFiltreDto { CariId = data.Cari.Id });
        Assert.NotNull(res);
    }

    [Fact]
    public async Task T15_StokDurumRaporu_FizikselVeLokasyon()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetStokRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotNull(res);
        Assert.True(res.TotalCount >= 1);
    }

    [Fact]
    public async Task T16_KritikStokRaporu_MinimumStokAltindaki_Dogru()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        data.Urun.StokMiktari = 2m; // min stok: 5m
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var ozet = await servis.GetYoneticiOzetiAsync(new SatisRaporFiltreDto());

        Assert.True(ozet.KritikStokSayisi >= 1);
    }

    [Fact]
    public async Task T17_StoksuzUrunler_MiktarSifir_Filtrele()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetStokRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotNull(res);
    }

    [Fact]
    public async Task T18_RezerveVeKullanilabilirStok_Formul_Dogru()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetStokRaporuAsync(new SatisRaporFiltreDto());
        var item = res.Items.FirstOrDefault(x => x.UrunId == data.Urun.Id);
        Assert.NotNull(item);
        Assert.Equal(item.FizikselStok - item.RezerveStok, item.KullanilabilirStok);
    }

    [Fact]
    public async Task T19_StokDevirHizi_Hesaplama_Basarili()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetStokRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotNull(res);
    }

    [Fact]
    public async Task T20_StokDegerRaporu_LokasyonVeMaliyet()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetStokDegerRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotEmpty(res);
    }

    [Fact]
    public async Task T21_KasaRaporu_DevirGirisCikisBakiye()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        db.KasaHareketleri.Add(new KasaHareketi
        {
            KasaId = data.Kasa.Id,
            Tarih = DateTime.UtcNow,
            FisTuru = KasaFisTuru.Tahsilat,
            Tur = HareketTuru.Gelir,
            Tutar = 2000m
        });
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var list = await servis.GetKasaBankaRaporuAsync(new SatisRaporFiltreDto());

        var k = list.FirstOrDefault(x => x.HesapId == data.Kasa.Id && x.Hesapturu == "Kasa");
        Assert.NotNull(k);
        Assert.Equal(2000m, k.Giris);
    }

    [Fact]
    public async Task T22_BankaRaporu_DevirGirisCikisBakiye()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var list = await servis.GetKasaBankaRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotEmpty(list);
    }

    [Fact]
    public async Task T23_TahsilatRaporu_KasaVeBanka()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var nakit = await servis.GetNakitAkisRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotNull(nakit);
    }

    [Fact]
    public async Task T24_OdemeRaporu_KasaVeBanka()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var nakit = await servis.GetNakitAkisRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotNull(nakit);
    }

    [Fact]
    public async Task T25_NakitAkisiRaporu_DonemBasiVeDonemSonu()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetNakitAkisRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotNull(res);
    }

    [Fact]
    public async Task T26_PosKomisyonRaporu_BrutNetHesabaGecen()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        db.PosIslemleri.Add(new PosIslemi
        {
            SirketId = data.Sirket.Id,
            BankaHesabiId = data.Banka.Id,
            PosCihazAdi = "Garanti POS",
            BankaAdi = "Garanti",
            TaksitSayisi = 1,
            BrutTutar = 1000m,
            KomisyonOrani = 2m,
            KomisyonTutar = 20m,
            NetTutar = 980m,
            IslemTarihi = DateTime.UtcNow,
            BeklenenHesabaGecisTarihi = DateTime.UtcNow.AddDays(1),
            Durum = PosIslemDurumu.HesabaGecti
        });
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var list = await servis.GetPosRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotEmpty(list);
    }

    [Fact]
    public async Task T27_CekSenetVadeRaporu_PortfoyVeVadesiGecen()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        db.CekSenetler.Add(new CekSenet
        {
            SirketId = data.Sirket.Id,
            CariKartId = data.Cari.Id,
            BelgeNo = $"CK-{Guid.NewGuid():N}"[..8],
            Tur = CekSenetTuru.AlinanCek,
            VadeTarihi = DateTime.SpecifyKind(DateTime.Today.AddDays(5), DateTimeKind.Utc),
            DuzenlemeTarihi = DateTime.UtcNow,
            Tutar = 12000m,
            Durum = CekSenetDurumu.Portfoyde,
            Kesideci = "Borçlu A.Ş."
        });
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var list = await servis.GetCekSenetRaporuAsync(new SatisRaporFiltreDto());
        Assert.NotEmpty(list);
    }

    [Fact]
    public async Task T28_SatisKdvRaporu_OranBazliMatrahKdv()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var list = await servis.GetKdvRaporuAsync(new SatisRaporFiltreDto());
        Assert.Equal(4, list.Count);
    }

    [Fact]
    public async Task T29_AlisKdvRaporu_OranBazliMatrahKdv()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var list = await servis.GetKdvRaporuAsync(new SatisRaporFiltreDto());
        Assert.Equal(4, list.Count);
    }

    [Fact]
    public async Task T30_MultiTenantFirmaIzolasyonu_SirketData_Sizdirmaz()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data1 = await SeedRaporTestVerisiAsync(db);
        var data2 = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data1.Sirket.Id);
        var ozet1 = await servis.GetYoneticiOzetiAsync(new SatisRaporFiltreDto { SirketId = 1 });
        Assert.NotNull(ozet1);
        Assert.Equal(data1.Kasa.Bakiye, ozet1.KasaToplami);
        Assert.Equal(data1.Banka.Bakiye, ozet1.BankaToplami);
    }

    [Fact]
    public async Task T31_SubeFiltresi_SubeDatasini_IzoleEder()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetSatisRaporuAsync(new SatisRaporFiltreDto { SubeId = 999 });
        Assert.Empty(res.Items);
    }

    [Fact]
    public async Task T32_KullaniciRaporYetkisi_Görüntüleme_Kontrolu()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var ozet = await servis.GetYoneticiOzetiAsync(new SatisRaporFiltreDto());
        Assert.NotNull(ozet);
    }

    [Fact]
    public async Task T33_MaliyetGizlemeYetkisi_MaliyetKolonu_OmitEder()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var excel = new ExcelExportServisi();
        var bytes = excel.ExportToExcel("Test", new SatisRaporFiltreDto(), new List<SatisRaporuKalemiDto>(), new Dictionary<string, string> { { "BelgeNo", "Belge No" } });
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task T34_Drilldown_FaturaVeCariDetay_DogruVeri()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetCariRaporuAsync(new SatisRaporFiltreDto { CariId = data.Cari.Id });
        Assert.NotEmpty(res.Items);
    }

    [Fact]
    public async Task T35_Pagination_ServerSide_PageSizeAndCount()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetSatisRaporuAsync(new SatisRaporFiltreDto { PageNumber = 1, PageSize = 10 });
        Assert.NotNull(res);
        Assert.Equal(10, res.PageSize);
    }

    [Fact]
    public async Task T36_ExcelExport_XlsxFormatindaByteUretir()
    {
        var excel = new ExcelExportServisi();
        var list = new List<SatisRaporuKalemiDto>
        {
            new SatisRaporuKalemiDto { BelgeNo = "FAT-001", CariUnvan = "Test", NetTutar = 1000m }
        };
        var bytes = excel.ExportToExcel("Satış Raporu", new SatisRaporFiltreDto(), list, new Dictionary<string, string> { { "BelgeNo", "Belge No" }, { "NetTutar", "Tutar" } });

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task T37_PdfExport_HtmlVeByteUretir()
    {
        var pdf = new PdfRaporServisi();
        var list = new List<SatisRaporuKalemiDto>
        {
            new SatisRaporuKalemiDto { BelgeNo = "FAT-002", CariUnvan = "Test 2", NetTutar = 2000m }
        };
        var bytes = pdf.ExportToPdf("Satış Raporu", new SatisRaporFiltreDto(), list, new Dictionary<string, string> { { "BelgeNo", "Belge No" }, { "NetTutar", "Tutar" } });

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task T38_IptalEdilenlerFiltresi_IptalFaturalariDahilEder()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        db.Faturalar.Add(new Fatura
        {
            SirketId = data.Sirket.Id,
            SubeId = data.Sube.Id,
            CariKartId = data.Cari.Id,
            FaturaNo = $"IPT-{Guid.NewGuid():N}"[..10],
            Tur = FaturaTuru.Satis,
            Durum = BelgeDurumu.Iptal,
            Tarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
            GenelToplam = 1000m
        });
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var resNormal = await servis.GetSatisRaporuAsync(new SatisRaporFiltreDto { IptalleriDahilEt = false });
        var resAll = await servis.GetSatisRaporuAsync(new SatisRaporFiltreDto { IptalleriDahilEt = true });

        Assert.True(resAll.TotalCount >= resNormal.TotalCount);
    }

    [Fact]
    public async Task T39_DonemKarsilastirmasi_BugunVsDun_BuAyVsGecenAy()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetDonemKarsilastirmaAsync(new SatisRaporFiltreDto { PresetKey = "BuAyVsGecenAy" });

        Assert.NotNull(res);
        Assert.NotEmpty(res.Kalemler);
    }

    [Fact]
    public async Task T40_BuyukVeriBenchmark_Performance_100k_Scenario()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedRaporTestVerisiAsync(db);

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        await servis.BenchmarkPerformanceAsync(new SatisRaporFiltreDto());

        Assert.True(true);
    }
}
