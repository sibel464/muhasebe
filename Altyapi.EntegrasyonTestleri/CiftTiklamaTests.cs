using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Irsaliye;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.StokOperasyon;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Aynı belgeye iki kullanıcının (ya da çift tıklamanın) aynı anda aynı işlemi yapması: işlem yalnız bir kez
// gerçekleşmeli; stok, kasa, cari ve yevmiye ikinci kez etkilenmemeli.
[Collection("PostgreSQL integration")]
public sealed class CiftTiklamaTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public CiftTiklamaTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;
    private static Guid SirketId => MuhasebeBaglami.VarsayilanSirketId;
    private static DateTime Simdi => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);

    [Fact]
    public async Task Ayni_fatura_ayni_anda_iki_kez_onaylanamaz()
    {
        var v = await HazirlaAsync(stok: 20);
        int faturaId;
        await using (var db = Yeni()) faturaId = (await new FaturaServisi(db, new TamYetkili()).EkleAsync(SatisFaturasi(v, 4))).Id;

        var basari = await IkiKezAsync(async db => { await new FaturaServisi(db, new TamYetkili()).OnaylaAsync(faturaId); });

        Assert.Equal(1, basari);
        await using var k = Yeni();
        Assert.Equal(1, await k.YevmiyeFisleri.CountAsync(f => f.BelgeTipi == "Fatura" && f.BelgeId == faturaId));
        Assert.Equal(16m, await DepoStoguAsync(k, v));
        Assert.Equal(16m, (await k.Urunler.AsNoTracking().SingleAsync(u => u.Id == v.UrunId)).StokMiktari);
        Assert.Equal(480m, (await k.CariKartlar.AsNoTracking().SingleAsync(c => c.Id == v.CariId)).Bakiye);
    }

    [Fact]
    public async Task Ayni_fatura_ayni_anda_iki_kez_iptal_edilemez()
    {
        var v = await HazirlaAsync(stok: 20);
        int faturaId;
        await using (var db = Yeni())
        {
            var s = new FaturaServisi(db, new TamYetkili());
            faturaId = (await s.EkleAsync(SatisFaturasi(v, 4))).Id;
            await s.OnaylaAsync(faturaId);
        }

        var basari = await IkiKezAsync(async db => { await new FaturaServisi(db, new TamYetkili()).IptalEtAsync(faturaId); });

        Assert.Equal(1, basari);
        await using var k = Yeni();
        Assert.Equal(20m, await DepoStoguAsync(k, v));
        Assert.Equal(20m, (await k.Urunler.AsNoTracking().SingleAsync(u => u.Id == v.UrunId)).StokMiktari);
        Assert.Equal(0m, (await k.CariKartlar.AsNoTracking().SingleAsync(c => c.Id == v.CariId)).Bakiye);
    }

    [Fact]
    public async Task Ayni_tahsilat_ayni_anda_iki_kez_iptal_edilemez()
    {
        var v = await HazirlaAsync(stok: 0);
        int hareketId;
        await using (var db = Yeni())
            hareketId = (await new TahsilatOdemeServisi(db, new TamYetkili()).KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit,
                v.CariId, 250m, Simdi, "Çift iptal", KasaId: v.KasaId))).HareketId;

        var basari = await IkiKezAsync(async db => { await new TahsilatOdemeServisi(db, new TamYetkili()).IptalEtAsync("Kasa", hareketId, "Yanlış"); });

        Assert.Equal(1, basari);
        await using var k = Yeni();
        Assert.Equal(0m, await KasaBakiyesiAsync(k, v.KasaId));
        Assert.Equal(2, await k.KasaHareketleri.CountAsync(h => h.KasaId == v.KasaId));
        Assert.Equal(0m, (await k.CariKartlar.AsNoTracking().SingleAsync(c => c.Id == v.CariId)).Bakiye);
    }

    [Fact]
    public async Task Ayni_irsaliye_ayni_anda_iki_kez_onaylanamaz_ve_iptal_edilemez()
    {
        var v = await HazirlaAsync(stok: 20);
        int irsaliyeId;
        await using (var db = Yeni())
            irsaliyeId = (await new IrsaliyeServisi(db, new TamYetkili()).EkleAsync(new Irsaliye
            {
                CariKartId = v.CariId, Tur = IrsaliyeTuru.Satis, Tarih = Simdi, SevkTarihi = Simdi,
                Kalemler = [new IrsaliyeKalemi { UrunId = v.UrunId, Miktar = 5, BirimFiyat = 10, StokDagilimlari = [new IrsaliyeKalemiStokDagilimi { DepoId = v.DepoId, Miktar = 5 }] }]
            })).Id;

        Assert.Equal(1, await IkiKezAsync(async db => { await new IrsaliyeServisi(db, new TamYetkili()).OnaylaAsync(irsaliyeId); }));
        await using (var k = Yeni()) Assert.Equal(15m, await DepoStoguAsync(k, v));

        Assert.Equal(1, await IkiKezAsync(async db => { await new IrsaliyeServisi(db, new TamYetkili()).IptalEtAsync(irsaliyeId); }));
        await using (var k = Yeni())
        {
            Assert.Equal(20m, await DepoStoguAsync(k, v));
            Assert.Equal(20m, (await k.Urunler.AsNoTracking().SingleAsync(u => u.Id == v.UrunId)).StokMiktari);
        }
    }

    [Fact]
    public async Task Ayni_transfer_ayni_anda_iki_kez_iptal_edilemez()
    {
        var v = await HazirlaAsync(stok: 20);
        int hedefId, transferId;
        await using (var db = Yeni())
        {
            var hedef = new Depo { SubeId = (await db.Depolar.SingleAsync(d => d.Id == v.DepoId)).SubeId, DepoKodu = "CT-" + Guid.NewGuid().ToString("N")[..6], DepoAdi = "Çift Hedef", DepoTuru = DepoTuru.Depo, AktifMi = true };
            db.Depolar.Add(hedef);
            await db.SaveChangesAsync();
            hedefId = hedef.Id;
            // Hedefte başka stok da var: ikinci iptal "hedefte yeterli stok yok" kuralına takılarak tesadüfen engellenmesin.
            db.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = v.UrunId, DepoId = hedefId, MevcutMiktar = 50 });
            await db.SaveChangesAsync();
            transferId = (await new StokOperasyonServisi(db, new TamYetkili()).TransferOlusturAsync(new StokTransferIstegi(v.DepoId, hedefId, v.UrunId, 6, 10, Simdi, null, null), SirketId, 1)).Id;
        }

        var basari = await IkiKezAsync(async db => { await new StokOperasyonServisi(db, new TamYetkili()).TransferIptalEtAsync(transferId, SirketId, 1, "Yanlış"); });

        Assert.Equal(1, basari);
        await using var k = Yeni();
        Assert.Equal(20m, await DepoStoguAsync(k, v));
        Assert.Equal(50m, await k.UrunLokasyonStoklari.Where(x => x.UrunId == v.UrunId && x.DepoId == hedefId).Select(x => x.MevcutMiktar).SingleAsync());
        Assert.Equal(20m, (await k.Urunler.AsNoTracking().SingleAsync(u => u.Id == v.UrunId)).StokMiktari);
    }

    [Fact]
    public async Task Ayni_sayim_ayni_anda_iki_kez_onaylanamaz()
    {
        var v = await HazirlaAsync(stok: 20);
        int sayimId;
        await using (var db = Yeni())
            sayimId = (await new StokOperasyonServisi(db, new TamYetkili()).SayimOlusturAsync(new StokSayimIstegi(v.DepoId, Simdi, "Çift sayım", [new StokSayimKalemIstegi(v.UrunId, 17)]), SirketId, 1)).Id;

        var basari = await IkiKezAsync(async db => { await new StokOperasyonServisi(db, new TamYetkili()).SayimOnaylaAsync(sayimId, SirketId, 1); });

        Assert.Equal(1, basari);
        await using var k = Yeni();
        Assert.Equal(17m, await DepoStoguAsync(k, v));
        Assert.Equal(17m, (await k.Urunler.AsNoTracking().SingleAsync(u => u.Id == v.UrunId)).StokMiktari);
        Assert.Equal(1, await k.StokHareketleri.CountAsync(h => h.UrunId == v.UrunId && h.IlgiliBelgeId == sayimId));
    }

    [Fact]
    public async Task Ayni_cek_ayni_anda_iki_kez_tahsil_edilemez()
    {
        var v = await HazirlaAsync(stok: 0);
        int cekId;
        await using (var db = Yeni())
            cekId = (await new CekSenetServisi(db, new TamYetkili()).EkleAsync(new CekSenetEkleTalebi(CekSenetTuru.AlinanCek, "CT-" + Guid.NewGuid().ToString("N")[..8],
                Simdi.AddDays(10), Simdi, 700m, v.CariId, "Keşideci"))).Id;

        // İkinci istek kilitte bekler, çeki zaten tahsil edilmiş bulur ve hiçbir şey yapmadan döner.
        await IkiKezAsync(async db => { await new CekSenetServisi(db, new TamYetkili()).DurumGuncelleAsync(cekId, CekSenetDurumu.TahsilEdildi, kasaId: v.KasaId); });

        await using var k = Yeni();
        Assert.Equal(700m, await KasaBakiyesiAsync(k, v.KasaId));
    }

    [Fact]
    public async Task Ayni_pos_islemi_ayni_anda_iki_kez_hesaba_gecirilemez()
    {
        var v = await HazirlaAsync(stok: 20);
        int posHesapId, vadesizId, faturaId;
        await using (var db = Yeni())
        {
            var pos = new BankaHesabi { SirketId = SirketId, Kod = $"CTP-{Guid.NewGuid():N}"[..20], Ad = "Çift POS", HesapTuru = BankaHesapTuru.POSHesabi, PosKomisyonOrani = 2, MuhasebeKodu = "108" };
            var vadesiz = new BankaHesabi { SirketId = SirketId, Kod = $"CTV-{Guid.NewGuid():N}"[..20], Ad = "Çift Vadesiz", HesapTuru = BankaHesapTuru.VadesizTL, MuhasebeKodu = "102" };
            db.AddRange(pos, vadesiz);
            await db.SaveChangesAsync();
            (posHesapId, vadesizId) = (pos.Id, vadesiz.Id);
            var f = SatisFaturasi(v, 1);
            f.FaturaNo = "CTP-" + Guid.NewGuid().ToString("N")[..10];
            faturaId = (await new FaturaServisi(db, new TamYetkili()).PosSatisiniTamamlaAsync(f, "Kredi Kartı")).Id;
        }
        int posIslemId;
        await using (var db = Yeni()) posIslemId = (await db.PosIslemleri.SingleAsync(p => p.FaturaId == faturaId)).Id;

        var basari = await IkiKezAsync(async db => { await new FinansMerkeziServisi(db, new TamYetkili()).PosIslemiHesabaGecirAsync(posIslemId, SirketId, 1); });

        Assert.Equal(1, basari);
        await using var k = Yeni();
        var islem = await k.PosIslemleri.AsNoTracking().SingleAsync(p => p.Id == posIslemId);
        Assert.Equal(1, await k.BankaHareketleri.CountAsync(h => h.FisTuru == BankaFisTuru.PosHesabaGecis && h.BankaHesabiId == islem.BankaHesabiId && h.Tur == HareketTuru.Gider && h.Tutar == islem.BrutTutar && h.Tarih == islem.HesabaGecisTarihi));
    }

    [Fact]
    public async Task Ayni_muhasebe_fisi_ayni_anda_iki_kez_iptal_edilemez()
    {
        _fixture.RequireDatabase();
        int fisId;
        await using (var db = Yeni())
        {
            await HazirlaAsync(stok: 0);
            var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == SirketId && h.HesapKodu == "100");
            var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == SirketId && h.HesapKodu == "102");
            fisId = (await YevmiyeServisi(db).FisOlusturAsync(new Uygulama.Dto.Muhasebe.YevmiyeFisDto
            {
                SirketId = SirketId, FisTuru = EtkiAlani.Modeller.Muhasebe.FisTuru.Mahsup, Tarih = Simdi, Aciklama = "Çift iptal fişi",
                Durum = EtkiAlani.Modeller.Muhasebe.YevmiyeDurumu.Onaylandi,
                Satirlar = [new() { HesapId = h100.Id, Borc = 300m, Aciklama = "B" }, new() { HesapId = h102.Id, Alacak = 300m, Aciklama = "A" }]
            }, SirketId, 1)).Id;
        }

        var basari = await IkiKezAsync(async db => await YevmiyeServisi(db).FisIptalEtTersKayitUretAsync(fisId, "Yanlış", SirketId));

        Assert.Equal(1, basari);
        await using var k = Yeni();
        Assert.Equal(1, await k.YevmiyeFisleri.CountAsync(f => f.IptalEdilenFisId == fisId));
    }

    [Fact]
    public async Task Ayni_anda_girilen_elle_muhasebe_fisleri_farkli_numara_alir()
    {
        _fixture.RequireDatabase();
        await HazirlaAsync(stok: 0);
        int h100, h102;
        await using (var db = Yeni())
        {
            h100 = (await db.HesapPlanlari.FirstAsync(h => h.SirketId == SirketId && h.HesapKodu == "100")).Id;
            h102 = (await db.HesapPlanlari.FirstAsync(h => h.SirketId == SirketId && h.HesapKodu == "102")).Id;
        }
        var numaralar = new System.Collections.Concurrent.ConcurrentBag<string>();

        var basari = await IkiKezAsync(async db => numaralar.Add((await YevmiyeServisi(db).FisOlusturAsync(new Uygulama.Dto.Muhasebe.YevmiyeFisDto
        {
            SirketId = SirketId, FisTuru = EtkiAlani.Modeller.Muhasebe.FisTuru.Tediye, Tarih = Simdi, Aciklama = "Eşzamanlı elle fiş",
            Durum = EtkiAlani.Modeller.Muhasebe.YevmiyeDurumu.Onaylandi,
            Satirlar = [new() { HesapId = h102, Borc = 10m, Aciklama = "B" }, new() { HesapId = h100, Alacak = 10m, Aciklama = "A" }]
        }, SirketId, 1)).FisNo));

        Assert.Equal(6, basari);
        Assert.Equal(6, numaralar.Distinct().Count());
    }

    [Fact]
    public async Task Ayni_anda_girilen_irsaliyeler_farkli_numara_alir()
    {
        var v = await HazirlaAsync(stok: 50);
        var numaralar = new System.Collections.Concurrent.ConcurrentBag<string>();

        var basari = await IkiKezAsync(async db => numaralar.Add((await new IrsaliyeServisi(db, new TamYetkili()).EkleAsync(new Irsaliye
        {
            CariKartId = v.CariId, Tur = IrsaliyeTuru.Satis, Tarih = Simdi, SevkTarihi = Simdi,
            Kalemler = [new IrsaliyeKalemi { UrunId = v.UrunId, Miktar = 1, BirimFiyat = 10, StokDagilimlari = [new IrsaliyeKalemiStokDagilimi { DepoId = v.DepoId, Miktar = 1 }] }]
        })).IrsaliyeNo));

        Assert.Equal(6, basari);
        Assert.Equal(6, numaralar.Distinct().Count());
    }

    [Fact]
    public async Task Ayni_belgeye_ayni_anda_yapilan_kapamalar_acik_tutari_asamaz()
    {
        var v = await HazirlaAsync(stok: 0);
        int odenenId, faturaHareketId;
        await using (var db = Yeni())
        {
            await new TahsilatOdemeServisi(db, new TamYetkili()).KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, v.CariId, 5000m, Simdi, "Kapama", KasaId: v.KasaId));
            odenenId = (await db.CariHareketler.SingleAsync(x => x.CariKartId == v.CariId && x.HareketTuru == CariHareketTuru.Tahsilat)).Id;
            var fh = new CariHareket { CariKartId = v.CariId, Tarih = Simdi, HareketTuru = CariHareketTuru.SatisFaturasi, Aciklama = "Kapama faturası", Borc = 1000m };
            db.CariHareketler.Add(fh);
            await db.SaveChangesAsync();
            faturaHareketId = fh.Id;
        }

        // Her biri tek başına geçerli (600 ≤ 1000) ama ikisi birlikte açık tutarı aşar.
        var basari = await IkiKezAsync(async db => await new BelgeKapamaServisi(db).KapatAsync(odenenId, faturaHareketId, 600m));

        Assert.Equal(1, basari);
        await using var k = Yeni();
        Assert.Equal(600m, (await k.CariHareketler.AsNoTracking().SingleAsync(x => x.Id == faturaHareketId)).KapaliTutar);
        Assert.Equal(1, await k.BelgeKapamalar.CountAsync(x => x.FaturaCariHareketId == faturaHareketId));
    }

    private static YevmiyeServisi YevmiyeServisi(MuhasebeBaglami db) => new(db, new TamYetkili(), new MuhasebeBaglantiServisi(db, new TamYetkili()));

    // Altı eşzamanlı deneme (çift tıklama + başka bilgisayardan aynı işlem); kaç tanesinin hatasız bittiğini döner.
    // Hepsi aynı anda başlasın diye bağlantılar önce açılır, işlemler tek işaretle salınır. Beklenen hata iş kuralı/çakışmadır.
    private async Task<int> IkiKezAsync(Func<MuhasebeBaglami, Task> islem)
    {
        var baslat = new TaskCompletionSource();
        async Task<bool> Dene()
        {
            await using var db = Yeni();
            await db.Database.OpenConnectionAsync();
            await baslat.Task;
            try { await islem(db); return true; }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
            {
                // Engellenen kullanıcı EF/PostgreSQL'in İngilizce teknik metnini değil Türkçe açıklama görmeli.
                Assert.DoesNotContain("database operation", ex.Message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("duplicate key", ex.Message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("See the inner exception", ex.Message, StringComparison.OrdinalIgnoreCase);
                return false;
            }
        }
        var gorevler = Enumerable.Range(0, 6).Select(_ => Task.Run(Dene)).ToList();
        await Task.Delay(300);
        baslat.SetResult();
        var sonuc = await Task.WhenAll(gorevler);
        return sonuc.Count(x => x);
    }

    private sealed record Veri(int CariId, int UrunId, int DepoId, int KasaId);

    private async Task<Veri> HazirlaAsync(decimal stok)
    {
        await using var db = Yeni();
        if (!await db.Sirketler.AnyAsync(x => x.Id == SirketId))
        {
            db.Sirketler.Add(new Sirket { Id = SirketId, Unvan = "Varsayılan Şirket", SirketKodu = "DEF-01" });
            await db.SaveChangesAsync();
        }
        var cari = new CariKart { SirketId = SirketId, Kod = $"CT-{Guid.NewGuid():N}", Unvan = "Çift Tıklama Cari", Aktif = true, Tur = CariTuru.MusteriTedarikci };
        var urun = new Urun { SirketId = SirketId, Kod = $"CT-{Guid.NewGuid():N}", Ad = "Çift Tıklama Ürün", Birim = "Adet", Aktif = true, StokMiktari = stok, KdvOrani = 20 };
        var kasa = new Kasa { SirketId = SirketId, Kod = $"CT-{Guid.NewGuid():N}"[..12], Ad = "Çift Kasa", AktifMi = true };
        db.AddRange(cari, urun, kasa);
        await db.SaveChangesAsync();
        var depo = await DepoKurallari.VarsayilanDepoAsync(db);
        db.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun.Id, DepoId = depo.Id, MevcutMiktar = stok });
        await db.SaveChangesAsync();
        return new Veri(cari.Id, urun.Id, depo.Id, kasa.Id);
    }

    private static Fatura SatisFaturasi(Veri v, decimal miktar) => new()
    {
        CariKartId = v.CariId, Tarih = Simdi, Tur = FaturaTuru.Satis, FaturaNo = string.Empty,
        Kalemler = [new FaturaKalemi { UrunId = v.UrunId, Miktar = miktar, BirimFiyat = 100, KdvOrani = 20, StokDagilimlari = [new FaturaKalemiStokDagilimi { DepoId = v.DepoId, Miktar = miktar }] }]
    };

    private static Task<decimal> DepoStoguAsync(MuhasebeBaglami k, Veri v) =>
        k.UrunLokasyonStoklari.Where(x => x.UrunId == v.UrunId && x.DepoId == v.DepoId).Select(x => x.MevcutMiktar).SingleAsync();

    private static async Task<decimal> KasaBakiyesiAsync(MuhasebeBaglami k, int kasaId) =>
        (await k.KasaHareketleri.AsNoTracking().Where(h => h.KasaId == kasaId && h.IslemDurumu == FinansIslemDurumu.Tamamlandi).ToListAsync())
            .Sum(h => h.Tur == HareketTuru.Gelir ? h.Tutar : -h.Tutar);

    private MuhasebeBaglami Yeni() { _fixture.RequireDatabase(); return new(_fixture.Options); }

    private sealed class TamYetkili : IKullaniciServisi
    {
        public Kullanici? AktifKullanici { get; } = new() { Id = 1, KullaniciAdi = "admin" };
        public KullaniciYetkileri? AktifYetkiler => null;
        public Task<bool> GirisYapAsync(string a, string s, CancellationToken c = default) => Task.FromResult(true);
        public void CikisYap() { }
        public bool YetkiliMi(Func<KullaniciYetkileri, bool> f) => true;
        public void YetkiKontrolEt(Func<KullaniciYetkileri, bool> f, string i) { }
        public Task<List<Kullanici>> TumunuGetirAsync() => throw new NotSupportedException();
        public Task<Kullanici?> GetirAsync(int id) => throw new NotSupportedException();
        public Task<Kullanici> EkleAsync(Kullanici k, string s) => throw new NotSupportedException();
        public Task GuncelleAsync(Kullanici k) => throw new NotSupportedException();
        public Task SifreDegistirAsync(int id, string s) => throw new NotSupportedException();
        public Task SilAsync(int id) => throw new NotSupportedException();
    }
}
