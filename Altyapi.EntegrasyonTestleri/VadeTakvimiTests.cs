using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kurumsal;
using Uygulama.Dto.Rapor;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Vade takvimi: açık alacaklar (FIFO), açık tedarikçi borçları (aynı FIFO'nun aynası) ve açık çek / senetler.
[Collection("PostgreSQL integration")]
public sealed class VadeTakvimiTests(IsolatedPostgreSqlFixture fixture)
{
    private static DateTime G(int fark) => DateTime.SpecifyKind(DateTime.Today.AddDays(fark), DateTimeKind.Utc);

    [Fact]
    public async Task Alacak_borc_ve_cekler_vadeye_gore_listelenir_cift_sayilmaz()
    {
        fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(fixture.Options);
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Vade Takvimi Test", SirketKodu = $"VT-{Guid.NewGuid():N}"[..10] };
        db.Sirketler.Add(sirket);
        var sube = new Sube { SirketId = sirket.Id, SubeKodu = $"S-{Guid.NewGuid():N}"[..6], SubeAdi = "Merkez", AktifMi = true };
        db.Subeler.Add(sube);
        await db.SaveChangesAsync();
        CariKart Cari(string ad, CariTuru tur) => new() { SirketId = sirket.Id, Kod = $"{ad[..2]}-{Guid.NewGuid():N}"[..10], Unvan = ad, Tur = tur, Aktif = true };
        var musteri = Cari("Müşteri A", CariTuru.Musteri);
        var tedarikci = Cari("Tedarikçi B", CariTuru.Tedarikci);
        var ikili = Cari("İkili C", CariTuru.MusteriTedarikci);
        var fazla = Cari("Fazla Ödeyen D", CariTuru.Musteri);
        db.CariKartlar.AddRange(musteri, tedarikci, ikili, fazla);
        await db.SaveChangesAsync();

        Fatura F(CariKart c, FaturaTuru tur, int vade, decimal tutar) => new()
        {
            SirketId = sirket.Id, SubeId = sube.Id, CariKartId = c.Id, FaturaNo = $"VT{Guid.NewGuid():N}"[..14], Tur = tur,
            Durum = BelgeDurumu.Onaylandi, Tarih = G(-20), VadeTarihi = G(vade), GenelToplam = tutar
        };
        var s1 = F(musteri, FaturaTuru.Satis, 5, 1000);
        var s2 = F(musteri, FaturaTuru.Satis, -10, 500);
        var s3 = F(musteri, FaturaTuru.Satis, 45, 700);   // ufuk dışı
        var a1 = F(tedarikci, FaturaTuru.Alis, 3, 800);
        var i1 = F(ikili, FaturaTuru.Satis, 4, 1000);
        var i2 = F(ikili, FaturaTuru.Alis, 6, 800);
        db.Faturalar.AddRange(s1, s2, s3, a1, i1, i2);
        await db.SaveChangesAsync();
        CariHareket H(CariKart c, Fatura? f, CariHareketTuru tur, decimal borc, decimal alacak) => new()
        {
            CariKartId = c.Id, SirketId = sirket.Id, Tarih = G(-20), HareketTuru = tur, BelgeNo = f?.FaturaNo, IlgiliBelgeId = f?.Id, Borc = borc, Alacak = alacak
        };
        db.CariHareketler.AddRange(
            H(musteri, s1, CariHareketTuru.SatisFaturasi, 1000, 0), H(musteri, s2, CariHareketTuru.SatisFaturasi, 500, 0), H(musteri, s3, CariHareketTuru.SatisFaturasi, 700, 0),
            H(tedarikci, a1, CariHareketTuru.AlisFaturasi, 0, 800), H(tedarikci, null, CariHareketTuru.Odeme, 300, 0),
            H(ikili, i1, CariHareketTuru.SatisFaturasi, 1000, 0), H(ikili, i2, CariHareketTuru.AlisFaturasi, 0, 800),
            H(fazla, null, CariHareketTuru.Tahsilat, 0, 100));
        db.CekSenetler.AddRange(
            new CekSenet { SirketId = sirket.Id, Tur = CekSenetTuru.AlinanCek, Durum = CekSenetDurumu.Portfoyde, BelgeNo = "AC-1", VadeTarihi = G(7), DuzenlemeTarihi = G(-5), Tutar = 1500, CariKartId = musteri.Id, Kesideci = "X" },
            new CekSenet { SirketId = sirket.Id, Tur = CekSenetTuru.VerilenCek, Durum = CekSenetDurumu.Portfoyde, BelgeNo = "VC-1", VadeTarihi = G(2), DuzenlemeTarihi = G(-5), Tutar = 400, CariKartId = tedarikci.Id, Kesideci = "Biz" },
            new CekSenet { SirketId = sirket.Id, Tur = CekSenetTuru.AlinanCek, Durum = CekSenetDurumu.TahsilEdildi, BelgeNo = "AC-2", VadeTarihi = G(1), DuzenlemeTarihi = G(-5), Tutar = 999, CariKartId = musteri.Id, Kesideci = "X" },
            new CekSenet { SirketId = sirket.Id, Tur = CekSenetTuru.AlinanSenet, Durum = CekSenetDurumu.Portfoyde, BelgeNo = "AS-9", VadeTarihi = G(60), DuzenlemeTarihi = G(-5), Tutar = 999, CariKartId = musteri.Id, Kesideci = "X" });
        await db.SaveChangesAsync();

        var liste = await StabilizasyonTestOturumu.Rapor(db, sirket.Id).GetVadeTakvimiAsync(new SatisRaporFiltreDto { BitisTarihi = DateTime.Today.AddDays(30) });

        Assert.Equal(new[] { "s2", "VC-1", "a1", "i1", "s1", "AC-1" }, liste.Select(k => k.BelgeNo switch
        {
            var n when n == s1.FaturaNo => "s1", var n when n == s2.FaturaNo => "s2", var n when n == a1.FaturaNo => "a1", var n when n == i1.FaturaNo => "i1", var n => n
        }));
        var gecmis = liste[0];
        Assert.True(gecmis.Tahsilat);
        Assert.Equal(10, gecmis.GecikmeGun);
        var borc = liste.Single(k => k.BelgeNo == a1.FaturaNo);
        Assert.Equal((false, 500m, "Tedarikçi B"), (borc.Tahsilat, borc.Tutar, borc.CariUnvan));
        Assert.Equal(200m, liste.Single(k => k.BelgeNo == i1.FaturaNo).Tutar); // ikili caride 1.000 alacak - 800 borç
        Assert.DoesNotContain(liste, k => k.BelgeNo == i2.FaturaNo);
        Assert.DoesNotContain(liste, k => k.CariUnvan == "Fazla Ödeyen D");
        Assert.Equal((false, "Verilen çek"), (liste[1].Tahsilat, liste[1].Kaynak));
        Assert.Equal(3200m, liste.Where(k => k.Tahsilat).Sum(k => k.Tutar));  // 500 + 200 + 1.000 + 1.500
        Assert.Equal(900m, liste.Where(k => !k.Tahsilat).Sum(k => k.Tutar));  // 400 + 500
    }
}
