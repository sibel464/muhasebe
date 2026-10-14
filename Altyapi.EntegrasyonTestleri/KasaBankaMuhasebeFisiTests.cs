using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class KasaBankaMuhasebeFisiTests(IsolatedPostgreSqlFixture fixture)
{
    private static async Task<decimal> HesapNetiAsync(MuhasebeBaglami db, Guid sirketId, string hesapKodu) =>
        await db.YevmiyeSatirlari
            .Where(s => s.Fis!.SirketId == sirketId && s.Hesap!.HesapKodu == hesapKodu)
            .SumAsync(s => (decimal?)(s.Borc - s.Alacak)) ?? 0;

    [Fact]
    public async Task Kasa_ve_banka_islemleri_mizanla_tutarli_fis_uretir()
    {
        fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(fixture.Options);
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Fiş Test", SirketKodu = $"FIS-{Guid.NewGuid():N}"[..12] };
        var cari = new CariKart { SirketId = sirket.Id, Kod = $"C-{Guid.NewGuid():N}"[..12], Unvan = "Fiş Cari", Aktif = true };
        var kasa = new Kasa { SirketId = sirket.Id, Kod = $"K-{Guid.NewGuid():N}"[..12], Ad = "Fiş Kasa", AktifMi = true };
        var banka = new BankaHesabi { SirketId = sirket.Id, Kod = $"B-{Guid.NewGuid():N}"[..12], Ad = "Fiş Banka", Iban = "TR330006100519786457841326", AktifMi = true };
        db.AddRange(sirket, cari, kasa, banka);
        await db.SaveChangesAsync();

        var oturum = StabilizasyonTestOturumu.Olustur();
        var kasaServisi = new KasaServisi(db, oturum);
        var bankaServisi = new BankaServisi(db, oturum);

        await kasaServisi.TahsilatEkleAsync(kasa.Id, cari.Id, 1000m, "Tahsilat");
        await kasaServisi.EkleAsync(new KasaHareketi { KasaId = kasa.Id, FisTuru = KasaFisTuru.Gider, Tutar = 200m, Aciklama = "Kırtasiye" });
        await kasaServisi.BankayaParaYatirAsync(kasa.Id, banka.Id, 300m, "Yatırma");
        await bankaServisi.EkleAsync(new BankaHareketi { BankaHesabiId = banka.Id, FisTuru = BankaFisTuru.BankaGiderMasraf, Tutar = 10m, Aciklama = "EFT masrafı" });

        // Kasa ve banka bakiyesi, mizandaki 100 ve 102 hesaplarıyla aynı olmalı.
        Assert.Equal(await kasaServisi.KasaBakiyeHesaplaAsync(kasa.Id), await HesapNetiAsync(db, sirket.Id, "100"));
        Assert.Equal(await bankaServisi.HesapBakiyeHesaplaAsync(banka.Id), await HesapNetiAsync(db, sirket.Id, "102"));
        Assert.Equal(-1000m, await HesapNetiAsync(db, sirket.Id, "120"));
        Assert.Equal(200m, await HesapNetiAsync(db, sirket.Id, "770"));
        Assert.Equal(10m, await HesapNetiAsync(db, sirket.Id, "780"));

        // Silinen hareketin fişi de kalkar; mizan yine kasa/banka bakiyesiyle tutar.
        var gider = await db.KasaHareketleri.SingleAsync(h => h.KasaId == kasa.Id && h.FisTuru == KasaFisTuru.Gider);
        await kasaServisi.SilAsync(gider.Id);
        Assert.Equal(700m, await HesapNetiAsync(db, sirket.Id, "100"));
        Assert.Equal(0m, await HesapNetiAsync(db, sirket.Id, "770"));
        Assert.Equal(await kasaServisi.KasaBakiyeHesaplaAsync(kasa.Id), await HesapNetiAsync(db, sirket.Id, "100"));

        // Her fiş borç = alacak dengesinde olmalı.
        var dengesiz = await db.YevmiyeFisleri.Where(f => f.SirketId == sirket.Id)
            .AnyAsync(f => f.Satirlar.Sum(s => s.Borc) != f.Satirlar.Sum(s => s.Alacak));
        Assert.False(dengesiz);
    }

    [Fact]
    public async Task Eski_fissiz_hareketler_tamamlanir_ve_tekrar_calistirmak_cift_kayit_uretmez()
    {
        fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(fixture.Options);
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Eski Kayıt", SirketKodu = $"ESK-{Guid.NewGuid():N}"[..12] };
        var cari = new CariKart { SirketId = sirket.Id, Kod = $"C-{Guid.NewGuid():N}"[..12], Unvan = "Eski Cari", Aktif = true };
        var kasa = new Kasa { SirketId = sirket.Id, Kod = $"K-{Guid.NewGuid():N}"[..12], Ad = "Eski Kasa", AktifMi = true };
        var banka = new BankaHesabi { SirketId = sirket.Id, Kod = $"B-{Guid.NewGuid():N}"[..12], Ad = "Eski Banka", Iban = "TR330006100519786457841326", AktifMi = true };
        db.AddRange(sirket, cari, kasa, banka);
        await db.SaveChangesAsync();

        // Otomatik fiş özelliğinden önceki gibi doğrudan (fişsiz) kayıtlar
        db.KasaHareketleri.AddRange(
            new KasaHareketi { SirketId = sirket.Id, KasaId = kasa.Id, Tur = HareketTuru.Gelir, FisTuru = KasaFisTuru.Gelir, Tutar = 5000m, Aciklama = "Sermaye" },
            new KasaHareketi { SirketId = sirket.Id, KasaId = kasa.Id, Tur = HareketTuru.Gider, FisTuru = KasaFisTuru.Gider, Tutar = 1200m, Aciklama = "Kırtasiye" },
            new KasaHareketi { SirketId = sirket.Id, KasaId = kasa.Id, Tur = HareketTuru.Gelir, FisTuru = KasaFisTuru.Tahsilat, Tutar = 220m, Aciklama = "Nakit satış", CariKartId = cari.Id });
        db.BankaHareketleri.Add(new BankaHareketi { SirketId = sirket.Id, BankaHesabiId = banka.Id, Tur = HareketTuru.Gelir, FisTuru = BankaFisTuru.GelenHavale, Tutar = 25000m, Aciklama = "POS gün sonu" });
        await db.SaveChangesAsync();

        Assert.True(await FinansMuhasebeKaydi.EksikFisleriTamamlaAsync(db) >= 4);
        var kasaServisi = new KasaServisi(db, StabilizasyonTestOturumu.Olustur());
        var bankaServisi = new BankaServisi(db, StabilizasyonTestOturumu.Olustur());
        Assert.Equal(await kasaServisi.KasaBakiyeHesaplaAsync(kasa.Id), await HesapNetiAsync(db, sirket.Id, "100"));
        Assert.Equal(await bankaServisi.HesapBakiyeHesaplaAsync(banka.Id), await HesapNetiAsync(db, sirket.Id, "102"));
        Assert.Equal(-220m, await HesapNetiAsync(db, sirket.Id, "120"));

        var fisSayisi = await db.YevmiyeFisleri.CountAsync(f => f.SirketId == sirket.Id);
        await FinansMuhasebeKaydi.EksikFisleriTamamlaAsync(db);
        Assert.Equal(fisSayisi, await db.YevmiyeFisleri.CountAsync(f => f.SirketId == sirket.Id));
    }

    [Fact]
    public async Task Cek_tahsilati_kasaya_girer_ve_alinan_cekler_hesabindan_duser()
    {
        fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(fixture.Options);
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Çek Test", SirketKodu = $"CEK-{Guid.NewGuid():N}"[..12] };
        var cari = new CariKart { SirketId = sirket.Id, Kod = $"C-{Guid.NewGuid():N}"[..12], Unvan = "Çek Cari", Aktif = true };
        var kasa = new Kasa { SirketId = sirket.Id, Kod = $"K-{Guid.NewGuid():N}"[..12], Ad = "Çek Kasa", AktifMi = true };
        db.AddRange(sirket, cari, kasa);
        await db.SaveChangesAsync();
        var cek = new CekSenet { SirketId = sirket.Id, CariKartId = cari.Id, Tur = CekSenetTuru.AlinanCek, Durum = CekSenetDurumu.Portfoyde,
            BelgeNo = "CK-1", Tutar = 750m, VadeTarihi = DateTime.UtcNow, DuzenlemeTarihi = DateTime.UtcNow };
        db.CekSenetler.Add(cek);
        await db.SaveChangesAsync();

        await new CekSenetServisi(db, StabilizasyonTestOturumu.Olustur()).DurumGuncelleAsync(cek.Id, CekSenetDurumu.TahsilEdildi, kasaId: kasa.Id);

        Assert.Equal(750m, await HesapNetiAsync(db, sirket.Id, "100"));
        Assert.Equal(-750m, await HesapNetiAsync(db, sirket.Id, "101"));
    }
}
