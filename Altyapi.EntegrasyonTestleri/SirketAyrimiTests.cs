using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Şirket ayrımı: oturumdaki aktif firma yalnız kendi kayıtlarını görür, yeni kayıtlar aktif firmaya yazılır.
[Collection("PostgreSQL integration")]
public sealed class SirketAyrimiTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public SirketAyrimiTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private MuhasebeBaglami Baglam(Guid? aktif) { _fixture.RequireDatabase(); return new MuhasebeBaglami(_fixture.Options) { AktifSirketId = aktif }; }

    private async Task<(Guid A, Guid B)> IkiFirmaAsync()
    {
        await using var db = Baglam(null);
        var a = new Sirket { Id = Guid.NewGuid(), Unvan = "A Firması", SirketKodu = $"A-{Guid.NewGuid():N}"[..10] };
        var b = new Sirket { Id = Guid.NewGuid(), Unvan = "B Firması", SirketKodu = $"B-{Guid.NewGuid():N}"[..10] };
        db.Sirketler.AddRange(a, b);
        await db.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    [Fact]
    public async Task Her_firma_yalniz_kendi_cari_urun_kasa_kayitlarini_gorur()
    {
        var (a, b) = await IkiFirmaAsync();
        await using (var db = Baglam(a))
        {
            db.CariKartlar.Add(new CariKart { Kod = $"SA-{Guid.NewGuid():N}"[..12], Unvan = "A Carisi", Aktif = true });
            db.Urunler.Add(new Urun { Kod = $"SA-{Guid.NewGuid():N}"[..12], Ad = "A Ürünü", Birim = "Adet", Aktif = true });
            db.Kasalar.Add(new Kasa { Kod = $"SA-{Guid.NewGuid():N}"[..12], Ad = "A Kasası", AktifMi = true });
            await db.SaveChangesAsync();
        }
        await using (var db = Baglam(b))
        {
            db.CariKartlar.Add(new CariKart { Kod = $"SB-{Guid.NewGuid():N}"[..12], Unvan = "B Carisi", Aktif = true });
            await db.SaveChangesAsync();
        }

        await using var dbA = Baglam(a);
        var carilerA = await dbA.CariKartlar.ToListAsync();
        Assert.All(carilerA, c => Assert.Equal(a, c.SirketId));
        Assert.Contains(carilerA, c => c.Unvan == "A Carisi");
        Assert.DoesNotContain(carilerA, c => c.Unvan == "B Carisi");
        Assert.All(await dbA.Urunler.ToListAsync(), u => Assert.Equal(a, u.SirketId));
        Assert.All(await dbA.Kasalar.ToListAsync(), k => Assert.Equal(a, k.SirketId));

        await using var dbB = Baglam(b);
        Assert.Equal(new[] { "B Carisi" }, await dbB.CariKartlar.Select(c => c.Unvan).ToListAsync());
        Assert.Empty(await dbB.Urunler.ToListAsync());
        Assert.Empty(await dbB.Kasalar.ToListAsync());
    }

    [Fact]
    public async Task Ikinci_firmanin_faturasi_kendi_hesap_planina_yazilir()
    {
        var (_, b) = await IkiFirmaAsync();
        int faturaId;
        await using (var db = Baglam(b))
        {
            // B firmasının kendi şubesi ve mağazası (A'nın / varsayılan şirketin depoları görünmez).
            var sube = new Sube { SirketId = b, SubeKodu = "B-MRK", SubeAdi = "B Merkez", AktifMi = true };
            db.Subeler.Add(sube);
            await db.SaveChangesAsync();
            var depo = new Depo { SubeId = sube.Id, DepoKodu = $"BD-{Guid.NewGuid():N}"[..10], DepoAdi = "B Mağaza", DepoTuru = DepoTuru.Magaza, AktifMi = true };
            db.Depolar.Add(depo);
            await db.SaveChangesAsync();
            Assert.DoesNotContain(await db.Depolar.Select(d => d.Id).ToListAsync(), id => id == 1);
            var cari = new CariKart { Kod = $"FB-{Guid.NewGuid():N}"[..12], Unvan = "B Müşteri", Aktif = true };
            var urun = new Urun { Kod = $"FB-{Guid.NewGuid():N}"[..12], Ad = "B Ürün", Birim = "Adet", Aktif = true };
            db.AddRange(cari, urun);
            await db.SaveChangesAsync();
            db.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun.Id, DepoId = depo.Id, MevcutMiktar = 10 });
            db.StokHareketleri.Add(new StokHareketi { UrunId = urun.Id, DepoId = depo.Id, Miktar = 10, HareketTuru = StokHareketTuru.Giris, Tarih = DateTime.UtcNow, Aciklama = "Açılış" });
            await db.SaveChangesAsync();
            var fatura = new Fatura
            {
                CariKartId = cari.Id, FaturaNo = $"SB-{Guid.NewGuid():N}"[..14], Tarih = DateTime.UtcNow, Tur = FaturaTuru.Satis,
                GenelToplam = 120, KdvToplam = 20,
                Kalemler = [new FaturaKalemi { UrunId = urun.Id, UrunKodu = urun.Kod, UrunAd = urun.Ad, Birim = "Adet", Miktar = 1, BirimFiyat = 100, KdvOrani = 20,
                    StokDagilimlari = [new FaturaKalemiStokDagilimi { DepoId = depo.Id, Miktar = 1 }] }]
            };
            db.Faturalar.Add(fatura);
            await db.SaveChangesAsync();
            faturaId = fatura.Id;
            Assert.Equal(b, fatura.SirketId);
            await new FinancialTransactionService(db, new Cari360TestKullanici { AktifYetkiler = new() { FaturaOnayla = true } }).CreateFromInvoiceAsync(faturaId);
        }

        await using var kontrol = Baglam(b);
        var fis = await kontrol.YevmiyeFisleri.Include(f => f.Satirlar).ThenInclude(s => s.Hesap).SingleAsync(f => f.BelgeTipi == "Fatura" && f.BelgeId == faturaId);
        Assert.Equal(b, fis.SirketId);
        Assert.All(fis.Satirlar, s => Assert.Equal(b, s.Hesap!.SirketId));
        Assert.Contains(fis.Satirlar, s => s.Hesap!.HesapKodu == "600" && s.Alacak == 100);
        await using var varsayilan = Baglam(MuhasebeBaglami.VarsayilanSirketId);
        Assert.False(await varsayilan.YevmiyeFisleri.AnyAsync(f => f.BelgeTipi == "Fatura" && f.BelgeId == faturaId));
        Assert.False(await varsayilan.Faturalar.AnyAsync(f => f.Id == faturaId));
    }

    [Fact]
    public async Task Tahsilat_aktif_firmanin_tek_kasasina_baglanir_diger_firmanin_kasasini_gormez()
    {
        var (a, b) = await IkiFirmaAsync();
        await using (var db = Baglam(a))
        {
            db.Kasalar.Add(new Kasa { Kod = $"KA-{Guid.NewGuid():N}"[..12], Ad = "A Kasası", AktifMi = true });
            await db.SaveChangesAsync();
        }
        await using var dbB = Baglam(b);
        var kasaB = new Kasa { Kod = $"KB-{Guid.NewGuid():N}"[..12], Ad = "B Kasası", AktifMi = true };
        var cari = new CariKart { Kod = $"TB-{Guid.NewGuid():N}"[..12], Unvan = "B Tahsilat Carisi", Aktif = true, Tur = CariTuru.Musteri };
        dbB.AddRange(kasaB, cari);
        await dbB.SaveChangesAsync();

        // B firmasında tek aktif kasa var: kasa seçilmeden de ona bağlanır (A'nın kasası sayılmaz).
        var kayit = await new TahsilatOdemeServisi(dbB, new Cari360TestKullanici()).KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 50m, DateTime.UtcNow, "B tahsilat"));
        var hareket = await dbB.KasaHareketleri.SingleAsync(h => h.Id == kayit.HareketId);
        Assert.Equal(kasaB.Id, hareket.KasaId);
        Assert.Equal(b, hareket.SirketId);
        var fis = await dbB.YevmiyeFisleri.Include(f => f.Satirlar).ThenInclude(s => s.Hesap).SingleAsync(f => f.BelgeTipi == "TahsilatOdeme" && f.BelgeId == kayit.HareketId);
        Assert.All(fis.Satirlar, s => Assert.Equal(b, s.Hesap!.SirketId));
    }

    [Fact]
    public async Task Aktif_firma_yokken_filtre_uygulanmaz()
    {
        var (a, b) = await IkiFirmaAsync();
        await using (var db = Baglam(a)) { db.CariKartlar.Add(new CariKart { Kod = $"NA-{Guid.NewGuid():N}"[..12], Unvan = "NA", Aktif = true }); await db.SaveChangesAsync(); }
        await using (var db = Baglam(b)) { db.CariKartlar.Add(new CariKart { Kod = $"NB-{Guid.NewGuid():N}"[..12], Unvan = "NB", Aktif = true }); await db.SaveChangesAsync(); }
        await using var tum = Baglam(null);
        var sirketler = await tum.CariKartlar.Where(c => c.SirketId == a || c.SirketId == b).Select(c => c.SirketId).Distinct().ToListAsync();
        Assert.Equal(2, sirketler.Count);
    }
    [Fact]
    public async Task Cari_hareket_irsaliye_ve_siparis_carinin_firmasini_alir_ve_digerinde_gorunmez()
    {
        var (a, b) = await IkiFirmaAsync();
        int cariId;
        await using (var db = Baglam(a))
        {
            var cari = new CariKart { Kod = $"HI-{Guid.NewGuid():N}"[..12], Unvan = "A Hareket Carisi", Aktif = true };
            db.CariKartlar.Add(cari);
            await db.SaveChangesAsync();
            cariId = cari.Id;
        }
        // Aktif firma belirtilmeden (bakım işi) yazılan kayıt da carinin firmasına bağlanır.
        await using (var db = Baglam(null))
        {
            db.CariHareketler.Add(new CariHareket { CariKartId = cariId, Tarih = DateTime.UtcNow, HareketTuru = CariHareketTuru.Devir, Borc = 10, Aciklama = "Devir" });
            db.Irsaliyeler.Add(new EtkiAlani.Modeller.Irsaliye.Irsaliye { CariKartId = cariId, IrsaliyeNo = $"HI-{Guid.NewGuid():N}"[..14] });
            db.Siparisler.Add(new EtkiAlani.Modeller.Siparis.Siparis { CariKartId = cariId, SiparisNo = $"HI-{Guid.NewGuid():N}"[..14], ParaBirimiId = 1, Kur = 1, OlusturanKullaniciId = 1 });
            await db.SaveChangesAsync();
        }
        await using var dbA = Baglam(a);
        Assert.Equal(a, (await dbA.CariHareketler.SingleAsync(h => h.CariKartId == cariId)).SirketId);
        Assert.Equal(a, (await dbA.Irsaliyeler.SingleAsync(h => h.CariKartId == cariId)).SirketId);
        Assert.Equal(a, (await dbA.Siparisler.SingleAsync(h => h.CariKartId == cariId)).SirketId);
        await using var dbB = Baglam(b);
        Assert.False(await dbB.CariHareketler.AnyAsync(h => h.CariKartId == cariId));
        Assert.False(await dbB.Irsaliyeler.AnyAsync(h => h.CariKartId == cariId));
        Assert.False(await dbB.Siparisler.AnyAsync(h => h.CariKartId == cariId));
    }
    [Fact]
    public async Task Yeni_firma_sube_magaza_kasa_ve_mali_donemle_acilir()
    {
        await using var db = Baglam(null);
        var servis = new SirketServisi(db, new Cari360TestKullanici { AktifYetkiler = new() { AyarlariYonet = true } });
        var firma = await servis.KaydetAsync(new Sirket { Unvan = "Yeni Firma Test" }, otomatikKod: true);

        await using var dbF = Baglam(firma.Id);
        var depolar = await dbF.Depolar.Include(d => d.Sube).ToListAsync();
        var magaza = Assert.Single(depolar);
        Assert.Equal(DepoTuru.Magaza, magaza.DepoTuru);
        Assert.Equal(firma.Id, magaza.Sube!.SirketId);
        Assert.Equal("Merkez Kasa", Assert.Single(await dbF.Kasalar.ToListAsync()).Ad);
        Assert.Single(await dbF.MaliDonemler.Where(d => d.SirketId == firma.Id).ToListAsync());

        // Tekrar çalıştırmak ikinci şube/kasa açmaz.
        await SirketServisi.EksikFirmaTanimlariniTamamlaAsync(db);
        Assert.Single(await dbF.Kasalar.ToListAsync());
        Assert.Single(await dbF.Depolar.ToListAsync());
    }
}
