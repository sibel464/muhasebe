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

// Ağ modunda birden çok kullanıcının aynı anda çalıştığı durum: her işlem kendi bağlamında, eşzamanlı.
// Sonunda stok, kasa, cari, yevmiye ve numaraların birbiriyle tutarlı kaldığı denetlenir.
[Collection("PostgreSQL integration")]
public sealed class EszamanliYukTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public EszamanliYukTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Eszamanli_satis_tahsilat_ve_odemeler_sonunda_kayitlar_tutarli_kalir()
    {
        _fixture.RequireDatabase();
        var sirketId = MuhasebeBaglami.VarsayilanSirketId;
        int cariId, urunId, kasaId, depoId;
        await using (var db = new MuhasebeBaglami(_fixture.Options))
        {
            if (!await db.Sirketler.AnyAsync(x => x.Id == sirketId))
            {
                db.Sirketler.Add(new Sirket { Id = sirketId, Unvan = "Varsayılan Şirket", SirketKodu = "DEF-01" });
                await db.SaveChangesAsync();
            }
            var cari = new CariKart { SirketId = sirketId, Kod = $"YUK-{Guid.NewGuid():N}", Unvan = "Yük Test Cari", Aktif = true, Tur = CariTuru.MusteriTedarikci };
            var urun = new Urun { SirketId = sirketId, Kod = $"YUK-{Guid.NewGuid():N}", Ad = "Yük Test Ürün", Birim = "Adet", Aktif = true, StokMiktari = 30 };
            var kasa = new Kasa { SirketId = sirketId, Kod = $"Y-{Guid.NewGuid():N}"[..12], Ad = "Yük Kasa", AktifMi = true };
            db.AddRange(cari, urun, kasa);
            await db.SaveChangesAsync();
            var depo = await DepoKurallari.VarsayilanDepoAsync(db);
            db.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun.Id, DepoId = depo.Id, MevcutMiktar = 30 });
            await db.SaveChangesAsync();
            (cariId, urunId, kasaId, depoId) = (cari.Id, urun.Id, kasa.Id, depo.Id);
        }

        // 12 fatura × 3 adet = 36 adet talep, stok 30: en çok 10 fatura onaylanabilmeli.
        async Task<int?> SatisAsync()
        {
            await using var db = new MuhasebeBaglami(_fixture.Options);
            var servis = new FaturaServisi(db, Oturum());
            var fatura = await servis.EkleAsync(new Fatura
            {
                CariKartId = cariId, Tarih = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc), Tur = FaturaTuru.Satis, FaturaNo = string.Empty,
                Kalemler = [new FaturaKalemi { UrunId = urunId, Miktar = 3, BirimFiyat = 100, KdvOrani = 20 }]
            });
            try { await servis.OnaylaAsync(fatura.Id); return fatura.Id; }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException) { return null; }
        }

        async Task<bool> KasaIslemiAsync(FinansIslemTuru tur)
        {
            await using var db = new MuhasebeBaglami(_fixture.Options);
            try
            {
                await new TahsilatOdemeServisi(db, Oturum()).KaydetAsync(new TahsilatOdemeTalebi(tur, OdemeYontemi.Nakit, cariId, 100m,
                    DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc), $"Yük {tur}", KasaId: kasaId));
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }

        var satislar = Enumerable.Range(0, 12).Select(_ => SatisAsync()).ToList();
        var tahsilatlar = Enumerable.Range(0, 8).Select(_ => KasaIslemiAsync(FinansIslemTuru.Tahsilat)).ToList();
        var odemeler = Enumerable.Range(0, 8).Select(_ => KasaIslemiAsync(FinansIslemTuru.Odeme)).ToList();
        await Task.WhenAll(satislar.Cast<Task>().Concat(tahsilatlar).Concat(odemeler));

        var onaylanan = satislar.Select(t => t.Result).Where(id => id.HasValue).Select(id => id!.Value).ToList();
        var tahsilatSayisi = tahsilatlar.Count(t => t.Result);
        var odemeSayisi = odemeler.Count(t => t.Result);

        await using var k = new MuhasebeBaglami(_fixture.Options);

        // Stok: eksiye düşmez, yetecek kadar satış onaylanır, ürün/depo/hareket toplamı birbirini tutar.
        Assert.Equal(10, onaylanan.Count);
        var urunSon = await k.Urunler.AsNoTracking().SingleAsync(u => u.Id == urunId);
        var lokasyon = await k.UrunLokasyonStoklari.AsNoTracking().SingleAsync(x => x.UrunId == urunId && x.DepoId == depoId);
        Assert.Equal(0m, urunSon.StokMiktari);
        Assert.Equal(0m, lokasyon.MevcutMiktar);
        var stokHareketleri = await k.StokHareketleri.AsNoTracking().Where(h => h.UrunId == urunId).ToListAsync();
        Assert.Equal(30m, stokHareketleri.Sum(h => h.Miktar));

        // Kasa: tüm tahsilatlar girer, ödemeler kasa bakiyesini hiçbir an eksiye düşürmez.
        Assert.Equal(8, tahsilatSayisi);
        var kasaHareketleri = await k.KasaHareketleri.AsNoTracking().Where(h => h.KasaId == kasaId).ToListAsync();
        Assert.Equal(tahsilatSayisi + odemeSayisi, kasaHareketleri.Count);
        var bakiye = 0m;
        foreach (var h in kasaHareketleri.OrderBy(h => h.Id))
        {
            bakiye += h.Tur == HareketTuru.Gelir ? h.Tutar : -h.Tutar;
            Assert.True(bakiye >= 0m, $"Kasa bakiyesi {h.Id} numaralı harekette eksiye düştü: {bakiye}");
        }
        Assert.Equal((tahsilatSayisi - odemeSayisi) * 100m, bakiye);

        // Cari: bakiye alanı hareket toplamıyla aynı.
        var cariSon = await k.CariKartlar.AsNoTracking().SingleAsync(c => c.Id == cariId);
        var cariHareketleri = await k.CariHareketler.AsNoTracking().Where(h => h.CariKartId == cariId).ToListAsync();
        var beklenenCari = onaylanan.Count * 360m - tahsilatSayisi * 100m + odemeSayisi * 100m;
        Assert.Equal(beklenenCari, cariHareketleri.Sum(h => h.Borc - h.Alacak));
        Assert.Equal(beklenenCari, cariSon.Bakiye);

        // Numaralar: faturalar ve yevmiye fişleri çakışmaz.
        var faturaNolari = await k.Faturalar.AsNoTracking().Where(f => f.CariKartId == cariId).Select(f => f.FaturaNo).ToListAsync();
        Assert.Equal(12, faturaNolari.Count);
        Assert.Equal(faturaNolari.Count, faturaNolari.Distinct().Count());
        var fisNolari = await k.YevmiyeFisleri.AsNoTracking().Where(f => f.SirketId == sirketId).Select(f => f.FisNo).ToListAsync();
        Assert.Equal(fisNolari.Count, fisNolari.Distinct().Count());

        // Yevmiye: her fatura ve kasa hareketinin tek, denk fişi var.
        var kasaHareketIdleri = kasaHareketleri.Select(h => h.Id).ToList();
        var fisler = await k.YevmiyeFisleri.AsNoTracking().Include(f => f.Satirlar)
            .Where(f => (f.BelgeTipi == "Fatura" && onaylanan.Contains(f.BelgeId)) || (f.BelgeTipi == "TahsilatOdeme" && kasaHareketIdleri.Contains(f.BelgeId) && f.Satirlar.Any(s => s.CariKartId == cariId)))
            .ToListAsync();
        Assert.Equal(onaylanan.Count + kasaHareketleri.Count, fisler.Count);
        Assert.All(fisler, f => Assert.Equal(f.Satirlar.Sum(s => s.Borc), f.Satirlar.Sum(s => s.Alacak)));
    }

    [Fact]
    public async Task Fatura_stok_cikisi_ve_transfer_ayni_depo_stoguna_eszamanli_dokununca_dusum_kaybolmaz()
    {
        _fixture.RequireDatabase();
        var sirketId = MuhasebeBaglami.VarsayilanSirketId;
        int cariId, urunId, depoA, depoB;
        await using (var db = new MuhasebeBaglami(_fixture.Options))
        {
            if (!await db.Sirketler.AnyAsync(x => x.Id == sirketId))
            {
                db.Sirketler.Add(new Sirket { Id = sirketId, Unvan = "Varsayılan Şirket", SirketKodu = "DEF-01" });
                await db.SaveChangesAsync();
            }
            var a = await DepoKurallari.VarsayilanDepoAsync(db);
            var b = new Depo { SubeId = a.SubeId, DepoKodu = "YUKB-" + Guid.NewGuid().ToString("N")[..6], DepoAdi = "Yük Depo B", DepoTuru = DepoTuru.Depo, AktifMi = true };
            var cari = new CariKart { SirketId = sirketId, Kod = $"YUK-{Guid.NewGuid():N}", Unvan = "Yük Test Cari 2", Aktif = true, Tur = CariTuru.Musteri };
            var urun = new Urun { SirketId = sirketId, Kod = $"YUK-{Guid.NewGuid():N}", Ad = "Yük Test Ürün 2", Birim = "Adet", Aktif = true, StokMiktari = 40 };
            db.AddRange(b, cari, urun);
            await db.SaveChangesAsync();
            db.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun.Id, DepoId = a.Id, MevcutMiktar = 40 });
            await db.SaveChangesAsync();
            (cariId, urunId, depoA, depoB) = (cari.Id, urun.Id, a.Id, b.Id);
        }

        var simdi = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
        // Talep tam 40: 6 satış × 3 + 6 stok çıkışı × 2 + 5 transfer × 2. Hepsi hatasız geçmeli, hiçbir düşüm kaybolmamalı.
        async Task SatisAsync()
        {
            await using var db = new MuhasebeBaglami(_fixture.Options);
            var servis = new FaturaServisi(db, Oturum());
            var f = await servis.EkleAsync(new Fatura
            {
                CariKartId = cariId, Tarih = simdi, Tur = FaturaTuru.Satis, FaturaNo = string.Empty,
                Kalemler = [new FaturaKalemi { UrunId = urunId, Miktar = 3, BirimFiyat = 100, KdvOrani = 20, StokDagilimlari = [new FaturaKalemiStokDagilimi { DepoId = depoA, Miktar = 3 }] }]
            });
            await servis.OnaylaAsync(f.Id);
        }
        async Task CikisAsync()
        {
            await using var db = new MuhasebeBaglami(_fixture.Options);
            await new StokOperasyonServisi(db, Oturum()).StokCikisiYapAsync(new Uygulama.Dto.StokOperasyon.StokCikisIstegi(urunId, depoA, 2, 10, simdi, "Fire", null, null), sirketId, 1);
        }
        async Task TransferAsync()
        {
            await using var db = new MuhasebeBaglami(_fixture.Options);
            await new StokOperasyonServisi(db, Oturum()).TransferOlusturAsync(new Uygulama.Dto.StokOperasyon.StokTransferIstegi(depoA, depoB, urunId, 2, 10, simdi, null, null), sirketId, 1);
        }

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => SatisAsync())
            .Concat(Enumerable.Range(0, 6).Select(_ => CikisAsync()))
            .Concat(Enumerable.Range(0, 5).Select(_ => TransferAsync())));

        await using var k = new MuhasebeBaglami(_fixture.Options);
        var lokasyonlar = await k.UrunLokasyonStoklari.AsNoTracking().Where(x => x.UrunId == urunId).ToDictionaryAsync(x => x.DepoId, x => x.MevcutMiktar);
        Assert.Equal(0m, lokasyonlar[depoA]);
        Assert.Equal(10m, lokasyonlar[depoB]);
        Assert.Equal(10m, (await k.Urunler.AsNoTracking().SingleAsync(u => u.Id == urunId)).StokMiktari);
        // Depo stoğu, o depodaki stok hareketlerinin toplamına eşit (açılış 40 hareketsiz girildi).
        var hareketler = await k.StokHareketleri.AsNoTracking().Where(h => h.UrunId == urunId).ToListAsync();
        decimal Net(int depo) => hareketler.Where(h => h.DepoId == depo).Sum(h => h.HareketTuru == StokHareketTuru.Giris ? h.Miktar : -h.Miktar);
        Assert.Equal(-40m, Net(depoA));
        Assert.Equal(10m, Net(depoB));
    }

    private static Uygulama.Arayuzler.IKullaniciServisi Oturum()
    {
        var oturum = (Cari360TestKullanici)StabilizasyonTestOturumu.Olustur();
        oturum.AktifYetkiler!.FaturaOlustur = true;
        oturum.AktifYetkiler.FaturaOnayla = true;
        return oturum;
    }
}
