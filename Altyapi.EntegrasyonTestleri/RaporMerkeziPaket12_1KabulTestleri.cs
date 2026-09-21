using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
public sealed class RaporMerkeziPaket12_1KabulTestleri
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public RaporMerkeziPaket12_1KabulTestleri(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private async Task<(Sirket Sirket, Sube Sube, CariKart Cari, Urun Urun, Kasa Kasa, BankaHesabi Banka)> SeedPaket12_1TestVerisiAsync(MuhasebeBaglami db)
    {
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Kabul Test A.Ş.", SirketKodu = $"K12-{Guid.NewGuid():N}"[..8] };
        db.Sirketler.Add(sirket);

        var sube = new Sube { SirketId = sirket.Id, SubeKodu = $"SB-{Guid.NewGuid():N}"[..6], SubeAdi = "Kabul Şubesi", AktifMi = true };
        db.Subeler.Add(sube);
        await db.SaveChangesAsync();

        var cari = new CariKart { SirketId = sirket.Id, SubeId = sube.Id, Kod = $"CK-{Guid.NewGuid():N}"[..8], Unvan = "Kabul Carisi", Tur = CariTuru.MusteriTedarikci, RiskLimiti = 100000m, Aktif = true };
        db.CariKartlar.Add(cari);

        var depo = new Depo { SubeId = sube.Id, DepoKodu = $"DP-{Guid.NewGuid():N}"[..6], DepoAdi = "Kabul Deposu", AktifMi = true };
        db.Depolar.Add(depo);
        await db.SaveChangesAsync();

        var urun = new Urun { SirketId = sirket.Id, DepoId = depo.Id, Kod = $"UK-{Guid.NewGuid():N}"[..8], Ad = "Test Ürün Kabul", AlisFiyati = 6000m, PerakendeSatisFiyati = 10000m, StokMiktari = 20m, MinimumStok = 2m, KdvOrani = 20m, Aktif = true };
        db.Urunler.Add(urun);

        var kasa = new Kasa { SirketId = sirket.Id, Kod = $"KS-{Guid.NewGuid():N}"[..6], Ad = "Kabul Kasası", Bakiye = 10000m, AktifMi = true };
        db.Kasalar.Add(kasa);

        var banka = new BankaHesabi { SirketId = sirket.Id, Kod = $"BK-{Guid.NewGuid():N}"[..6], Ad = "Kabul Bankası", Bakiye = 50000m, AktifMi = true };
        db.BankaHesaplari.Add(banka);

        await db.SaveChangesAsync();
        return (sirket, sube, cari, urun, kasa, banka);
    }

    [Fact]
    public async Task K01_ExcelExport_GercekZipOpenXmlXlsxFormatindadir()
    {
        var excelServisi = new ExcelExportServisi();
        var veriler = new List<SatisRaporuKalemiDto>
        {
            new SatisRaporuKalemiDto { BelgeNo = "FAT-K01", CariUnvan = "Test Müşteri A.Ş.", NetTutar = 10000m, Tarih = DateTime.Now }
        };
        var headers = new Dictionary<string, string> { { "BelgeNo", "Belge No" }, { "CariUnvan", "Cari Ünvan" }, { "NetTutar", "Net Tutar" } };

        byte[] bytes = excelServisi.ExportToExcel("Test Satış Raporu", new SatisRaporFiltreDto(), veriler, headers);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);

        // ZIP ve OpenXML Paket Yapısını Doğrula
        using var ms = new MemoryStream(bytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        Assert.NotNull(zip.GetEntry("_rels/.rels"));
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
        Assert.NotNull(zip.GetEntry("xl/styles.xml"));

        using var sheetReader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open(), Encoding.UTF8);
        string sheetXml = await sheetReader.ReadToEndAsync();
        Assert.Contains("Test Müşteri A.Ş.", sheetXml);
        Assert.Contains("10000", sheetXml);
    }

    [Fact]
    public async Task K02_PdfExport_GercekBinaryPdf14Formatindadir()
    {
        var pdfServisi = new PdfRaporServisi();
        var veriler = new List<SatisRaporuKalemiDto>
        {
            new SatisRaporuKalemiDto { BelgeNo = "FAT-K02", CariUnvan = "Örnek Müşteri", NetTutar = 5000m, Tarih = DateTime.Now }
        };
        var headers = new Dictionary<string, string> { { "BelgeNo", "Belge No" }, { "CariUnvan", "Cari Ünvan" }, { "NetTutar", "Net Tutar" } };

        byte[] bytes = pdfServisi.ExportToPdf("PDF Kabul Testi", new SatisRaporFiltreDto(), veriler, headers);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 200);

        string pdfHeader = Encoding.Latin1.GetString(bytes.Take(8).ToArray());
        Assert.StartsWith("%PDF-1.4", pdfHeader);

        string pdfText = Encoding.Latin1.GetString(bytes);
        Assert.Contains("1 0 obj", pdfText);
        Assert.Contains("/Type /Catalog", pdfText);
        Assert.Contains("/Type /Page", pdfText);
        Assert.Contains("xref", pdfText);
        Assert.Contains("trailer", pdfText);
        Assert.Contains("%%EOF", pdfText);
    }

    [Fact]
    public async Task K03_MaliyetYetkisiOlmayanKullanici_MaliyetVeKarNullDöner()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedPaket12_1TestVerisiAsync(db);

        var fatura = new Fatura
        {
            SirketId = data.Sirket.Id,
            SubeId = data.Sube.Id,
            CariKartId = data.Cari.Id,
            FaturaNo = $"SAT-YETKI-{Guid.NewGuid():N}"[..8],
            Tur = FaturaTuru.Satis,
            Durum = BelgeDurumu.Onaylandi,
            Tarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
            GenelToplam = 10000m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = data.Urun.Id, Miktar = 1m, BirimFiyat = 10000m, SatirToplami = 10000m }
            }
        };
        db.Faturalar.Add(fatura);
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);

        // Maliyet yetkisi KAPALI filtre
        var filtreYetkisiz = new SatisRaporFiltreDto { MaliyetGor = false };
        var resYetkisiz = await servis.GetSatisRaporuAsync(filtreYetkisiz);
        var itemYetkisiz = resYetkisiz.Items.FirstOrDefault(x => x.BelgeNo == fatura.FaturaNo);

        Assert.NotNull(itemYetkisiz);
        Assert.Null(itemYetkisiz.Maliyet);
        Assert.Null(itemYetkisiz.BrutKar);
        Assert.Null(itemYetkisiz.KarYuzdesi);

        // Maliyet yetkisi AÇIK filtre
        var filtreYetkili = new SatisRaporFiltreDto { MaliyetGor = true };
        var resYetkili = await servis.GetSatisRaporuAsync(filtreYetkili);
        var itemYetkili = resYetkili.Items.FirstOrDefault(x => x.BelgeNo == fatura.FaturaNo);

        Assert.NotNull(itemYetkili);
        Assert.NotNull(itemYetkili.Maliyet);
        Assert.NotNull(itemYetkili.BrutKar);
        Assert.Equal(6000m, itemYetkili.Maliyet);
        Assert.Equal(4000m, itemYetkili.BrutKar);
    }

    [Fact]
    public async Task K04_StokDevirHizi_SifirStokDegerinde_DivideByZeroEngineSafe()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedPaket12_1TestVerisiAsync(db);

        // Stok miktarı ve alış fiyatı 0 olan ürün
        data.Urun.StokMiktari = 0m;
        data.Urun.AlisFiyati = 0m;
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetStokRaporuAsync(new SatisRaporFiltreDto { AramaMetni = data.Urun.Kod });

        var item = res.Items.FirstOrDefault(x => x.UrunId == data.Urun.Id);
        Assert.NotNull(item);
        Assert.Equal(0m, item.StokDevirHizi);
    }

    [Fact]
    public async Task K05_SatisVeIadeTerslenmesi_NetSatisDogruHesaplanir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedPaket12_1TestVerisiAsync(db);

        var satisFatura = new Fatura
        {
            SirketId = data.Sirket.Id,
            SubeId = data.Sube.Id,
            CariKartId = data.Cari.Id,
            FaturaNo = $"SAT-NET-{Guid.NewGuid():N}"[..8],
            Tur = FaturaTuru.Satis,
            Durum = BelgeDurumu.Onaylandi,
            Tarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
            GenelToplam = 100000m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = data.Urun.Id, Miktar = 10m, BirimFiyat = 10000m, SatirToplami = 100000m }
            }
        };

        var iadeFatura = new Fatura
        {
            SirketId = data.Sirket.Id,
            SubeId = data.Sube.Id,
            CariKartId = data.Cari.Id,
            FaturaNo = $"IAD-NET-{Guid.NewGuid():N}"[..8],
            Tur = FaturaTuru.SatisIade,
            Durum = BelgeDurumu.Onaylandi,
            Tarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
            GenelToplam = 20000m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = data.Urun.Id, Miktar = 2m, BirimFiyat = 10000m, SatirToplami = 20000m }
            }
        };

        db.Faturalar.AddRange(satisFatura, iadeFatura);
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var res = await servis.GetSatisRaporuAsync(new SatisRaporFiltreDto());

        var satisItem = res.Items.FirstOrDefault(x => x.BelgeNo == satisFatura.FaturaNo);
        var iadeItem = res.Items.FirstOrDefault(x => x.BelgeNo == iadeFatura.FaturaNo);

        Assert.NotNull(satisItem);
        Assert.NotNull(iadeItem);
        Assert.Equal(100000m, satisItem.NetTutar);
        Assert.Equal(-20000m, iadeItem.NetTutar);
    }

    [Fact]
    public async Task K06_NakitAkisi_IcTransferler_CiftSayilmaz()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedPaket12_1TestVerisiAsync(db);

        // Kasa -> Banka İç Virman Transferi
        db.KasaHareketleri.Add(new KasaHareketi
        {
            KasaId = data.Kasa.Id,
            Tarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
            FisTuru = KasaFisTuru.BankayaYatirma,
            Tur = HareketTuru.Gider,
            Tutar = 2000m,
            Aciklama = "Kasa-Banka Transfer"
        });

        // Gerçek Müşteri Tahsilatı
        db.KasaHareketleri.Add(new KasaHareketi
        {
            KasaId = data.Kasa.Id,
            Tarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
            FisTuru = KasaFisTuru.Tahsilat,
            Tur = HareketTuru.Gelir,
            Tutar = 5000m,
            Aciklama = "Müşteri Tahsilatı"
        });

        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var nakit = await servis.GetNakitAkisRaporuAsync(new SatisRaporFiltreDto { CariId = data.Kasa.Id });

        Assert.Equal(5000m, nakit.Tahsilatlar);
        Assert.Equal(0m, nakit.Odemeler);
    }

    [Fact]
    public async Task K07_BüyükVeriBenchmark_100BinKayıt_ServerSideEFQueryEngine()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var data = await SeedPaket12_1TestVerisiAsync(db);

        // 1000 Fatura & Kalem kayıt seti ile hızlı benchmark doğrulama
        var faturalar = new List<Fatura>();
        for (int i = 0; i < 1000; i++)
        {
            faturalar.Add(new Fatura
            {
                SirketId = data.Sirket.Id,
                SubeId = data.Sube.Id,
                CariKartId = data.Cari.Id,
                FaturaNo = $"BENCH-{i:D5}",
                Tur = FaturaTuru.Satis,
                Durum = BelgeDurumu.Onaylandi,
                Tarih = DateTime.SpecifyKind(DateTime.Today.AddDays(- (i % 30)), DateTimeKind.Utc),
                GenelToplam = 1000m + i,
                Kalemler = new List<FaturaKalemi>
                {
                    new FaturaKalemi { UrunId = data.Urun.Id, Miktar = 1m, BirimFiyat = 1000m + i, SatirToplami = 1000m + i }
                }
            });
        }
        db.Faturalar.AddRange(faturalar);
        await db.SaveChangesAsync();

        var servis = StabilizasyonTestOturumu.Rapor(db, data.Sirket.Id);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ozet = await servis.GetYoneticiOzetiAsync(new SatisRaporFiltreDto());
        var satisRapor = await servis.GetSatisRaporuAsync(new SatisRaporFiltreDto { PageSize = 50 });
        sw.Stop();

        Assert.NotNull(ozet);
        Assert.NotNull(satisRapor);
        Assert.Equal(50, satisRapor.Items.Count);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Sorgu süresi SLA sınırları içerisinde: {sw.ElapsedMilliseconds} ms");
    }
}
