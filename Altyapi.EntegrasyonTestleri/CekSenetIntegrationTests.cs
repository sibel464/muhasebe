using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public class CekSenetIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;

    public CekSenetIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private async Task<(Sirket sirket, CariKart cari)> SeedSirketVeCariAsync(MuhasebeBaglami db)
    {
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Çek Senet Test A.Ş.", SirketKodu = $"CS-{Guid.NewGuid():N}" };
        db.Sirketler.Add(sirket);
        await db.SaveChangesAsync();

        var cari = new CariKart { SirketId = sirket.Id, Kod = $"C-CS-{Guid.NewGuid():N}", Unvan = "Müşteri Çek A.Ş.", Aktif = true };
        db.CariKartlar.Add(cari);
        await db.SaveChangesAsync();

        return (sirket, cari);
    }

    [Fact]
    public async Task CekSenet_Ekle_DurumGuncelle_Ve_Sil_Basariyla_Calisir()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (_, cari) = await SeedSirketVeCariAsync(db);

        var servis = new CekSenetServisi(db, StabilizasyonTestOturumu.Olustur());

        // 1. Yeni Çek Ekle
        var talep = new CekSenetEkleTalebi(
            Tur: CekSenetTuru.AlinanCek,
            BelgeNo: "CK-100200",
            VadeTarihi: DateTime.UtcNow.AddDays(30),
            DuzenlemeTarihi: DateTime.UtcNow,
            Tutar: 25000m,
            CariKartId: cari.Id,
            Kesideci: "Müşteri Çek A.Ş.",
            BankaAdi: "Garanti BBVA",
            SubeAdi: "Kadıköy",
            HesapNo: "1234567890",
            Aciklama: "Entegrasyon testi çek kaydı"
        );

        var cek = await servis.EkleAsync(talep);
        Assert.NotNull(cek);
        Assert.True(cek.Id > 0);
        Assert.Equal(CekSenetDurumu.Portfoyde, cek.Durum);

        // 2. Durum Güncelle (Tahsil Edildi)
        var kasa = new Kasa { Ad = "Merkez Kasa", Kod = $"KS-{Guid.NewGuid():N}", AktifMi = true };
        db.Kasalar.Add(kasa);
        await db.SaveChangesAsync();

        await servis.DurumGuncelleAsync(cek.Id, CekSenetDurumu.TahsilEdildi, kasaId: kasa.Id, aciklama: "Nakit tahsilat");
        var guncelCek = await db.CekSenetler.FindAsync(cek.Id);
        Assert.NotNull(guncelCek);
        Assert.Equal(CekSenetDurumu.TahsilEdildi, guncelCek.Durum);

        // 3. Tahsil edilmiş çek tekrar portföye alınamaz, ikinci kez tahsil edilemez ve silinemez
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.DurumGuncelleAsync(cek.Id, CekSenetDurumu.Portfoyde));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.SilAsync(cek.Id, "Hatalı kayıt"));
        Assert.Equal(25000m, await db.KasaHareketleri.Where(h => h.KasaId == kasa.Id).SumAsync(h => h.Tutar));

        // 4. Portföydeki çek silinebilir
        var ikinci = await servis.EkleAsync(talep with { BelgeNo = "CK-100201" });
        await servis.SilAsync(ikinci.Id, "Hatalı kayıt");
        var silinmis = await db.CekSenetler.IgnoreQueryFilters().SingleAsync(x => x.Id == ikinci.Id);
        Assert.True(silinmis.SilindiMi);
        Assert.Equal("Hatalı kayıt", silinmis.SilmeNedeni);
    }

    [Fact]
    public async Task CekSenet_kurallari_yetkisiz_ve_hatali_islemleri_engeller()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (_, cari) = await SeedSirketVeCariAsync(db);
        var servis = new CekSenetServisi(db, StabilizasyonTestOturumu.Olustur());
        var cek = await servis.EkleAsync(new CekSenetEkleTalebi(CekSenetTuru.AlinanCek, "CK-KURAL-" + Guid.NewGuid().ToString("N")[..6],
            DateTime.UtcNow.AddDays(10), DateTime.UtcNow, 500m, cari.Id, "Keşideci"));

        // Yetkisiz kullanıcı göremez ve işlem yapamaz
        var yetkisiz = new CekSenetServisi(db, new Cari360TestKullanici { AktifYetkiler = new() { CekSenetGoruntule = false, CekSenetIslemYap = false } });
        await Assert.ThrowsAsync<Uygulama.Arayuzler.YetkisizErisimException>(() => yetkisiz.TumunuGetirAsync());
        await Assert.ThrowsAsync<Uygulama.Arayuzler.YetkisizErisimException>(() => yetkisiz.DurumGuncelleAsync(cek.Id, CekSenetDurumu.TahsilEdildi));

        // Kasa/banka seçilmeden tahsil edilemez; alınan çek "ödendi" yapılamaz
        await Assert.ThrowsAsync<ArgumentException>(() => servis.DurumGuncelleAsync(cek.Id, CekSenetDurumu.TahsilEdildi));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.DurumGuncelleAsync(cek.Id, CekSenetDurumu.Odendi));

        // Boş kasadan verilen çek ödenemez
        var verilen = await servis.EkleAsync(new CekSenetEkleTalebi(CekSenetTuru.VerilenCek, "VC-KURAL-" + Guid.NewGuid().ToString("N")[..6],
            DateTime.UtcNow.AddDays(10), DateTime.UtcNow, 999999m, cari.Id, "Biz"));
        var kasa = new Kasa { Ad = "Boş Kasa", Kod = $"KS-{Guid.NewGuid():N}", AktifMi = true };
        db.Kasalar.Add(kasa);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.DurumGuncelleAsync(verilen.Id, CekSenetDurumu.Odendi, kasaId: kasa.Id));
        Assert.Equal(CekSenetDurumu.Portfoyde, (await db.CekSenetler.AsNoTracking().SingleAsync(x => x.Id == verilen.Id)).Durum);
    }

    private static async Task<decimal> CariBakiyeAsync(MuhasebeBaglami db, int cariId) =>
        await db.CariHareketler.Where(h => h.CariKartId == cariId).SumAsync(h => h.Borc - h.Alacak);

    // Muhasebe hesabının (borç - alacak) bakiyesi; yalnız bu çeke (ve verilirse tahsilin kasa hareketine) ait fişler.
    private static async Task<decimal> HesapBakiyeAsync(MuhasebeBaglami db, int cekId, string hesapKodu, int? kasaHareketId = null)
    {
        var cek = await db.CekSenetler.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == cekId);
        return await db.YevmiyeFisleri.IgnoreQueryFilters().Where(f => f.SirketId == cek.SirketId &&
                ((f.BelgeId == cekId && f.BelgeTipi != FinansMuhasebeKaydi.KasaBelgeTipi && f.BelgeTipi != FinansMuhasebeKaydi.BankaBelgeTipi) ||
                 (f.BelgeId == kasaHareketId && f.BelgeTipi == FinansMuhasebeKaydi.KasaBelgeTipi)))
            .SelectMany(f => f.Satirlar).Where(s => s.Hesap!.HesapKodu == hesapKodu).SumAsync(s => s.Borc - s.Alacak);
    }

    [Fact]
    public async Task Alinan_cek_ciro_karsiliksiz_ve_tahsil_cari_ile_muhasebeyi_dogru_isler()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirket, musteri) = await SeedSirketVeCariAsync(db);
        var tedarikci = new CariKart { SirketId = sirket.Id, Kod = $"T-CS-{Guid.NewGuid():N}", Unvan = "Tedarikçi A.Ş.", Tur = CariTuru.Tedarikci, Aktif = true };
        var kasa = new Kasa { SirketId = sirket.Id, Ad = "Kasa", Kod = $"KS-{Guid.NewGuid():N}", AktifMi = true };
        db.AddRange(tedarikci, kasa);
        await db.SaveChangesAsync();
        var servis = new CekSenetServisi(db, StabilizasyonTestOturumu.Olustur());

        // Çek alınınca müşterinin borcu kapanır: 101 borç - 120 alacak
        var cek = await servis.EkleAsync(new CekSenetEkleTalebi(CekSenetTuru.AlinanCek, "CK-CIRO-1", DateTime.UtcNow.AddDays(30), DateTime.UtcNow, 1000m, musteri.Id, "Müşteri"));
        Assert.Equal(-1000m, await CariBakiyeAsync(db, musteri.Id));
        Assert.Equal(1000m, await HesapBakiyeAsync(db, cek.Id, "101"));

        // Ciro: tedarikçiye borcumuz kapanır (320 borç), çek portföyden çıkar (101 alacak); alındığı cariye ve cari seçmeden ciro yok
        await Assert.ThrowsAsync<ArgumentException>(() => servis.DurumGuncelleAsync(cek.Id, CekSenetDurumu.CiroEdildi));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.DurumGuncelleAsync(cek.Id, CekSenetDurumu.CiroEdildi, ciroCariKartId: musteri.Id));
        await servis.DurumGuncelleAsync(cek.Id, CekSenetDurumu.CiroEdildi, ciroCariKartId: tedarikci.Id);
        Assert.Equal(1000m, await CariBakiyeAsync(db, tedarikci.Id));
        Assert.Equal(-1000m, await CariBakiyeAsync(db, musteri.Id));
        Assert.Equal(0m, await HesapBakiyeAsync(db, cek.Id, "101"));
        Assert.Equal(1000m, await HesapBakiyeAsync(db, cek.Id, "320"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.SilAsync(cek.Id, "hata"));

        // Karşılıksız: müşteri yeniden borçlu (120 borç - 101 alacak), sonra nakit tahsil edilince borç kapanır (100 borç - 120 alacak)
        var ikinci = await servis.EkleAsync(new CekSenetEkleTalebi(CekSenetTuru.AlinanCek, "CK-KRS-1", DateTime.UtcNow.AddDays(30), DateTime.UtcNow, 400m, musteri.Id, "Müşteri"));
        Assert.Equal(-1400m, await CariBakiyeAsync(db, musteri.Id));
        await servis.DurumGuncelleAsync(ikinci.Id, CekSenetDurumu.Karsiliksiz);
        Assert.Equal(-1000m, await CariBakiyeAsync(db, musteri.Id));
        Assert.Equal(0m, await HesapBakiyeAsync(db, ikinci.Id, "101"));
        Assert.Equal(0m, await HesapBakiyeAsync(db, ikinci.Id, "120"));
        await servis.DurumGuncelleAsync(ikinci.Id, CekSenetDurumu.TahsilEdildi, kasaId: kasa.Id);
        Assert.Equal(-1400m, await CariBakiyeAsync(db, musteri.Id));
        var kasaH = await db.KasaHareketleri.SingleAsync(h => h.KasaId == kasa.Id);
        Assert.Equal(musteri.Id, kasaH.CariKartId);
        Assert.Equal(400m, await HesapBakiyeAsync(db, ikinci.Id, "100", kasaH.Id));
        Assert.Equal(-400m, await HesapBakiyeAsync(db, ikinci.Id, "120", kasaH.Id));

        // Portföyde normal tahsil: cari ikinci kez işlenmez, karşı hesap 101
        var ucuncu = await servis.EkleAsync(new CekSenetEkleTalebi(CekSenetTuru.AlinanCek, "CK-THS-1", DateTime.UtcNow.AddDays(30), DateTime.UtcNow, 250m, musteri.Id, "Müşteri"));
        await servis.DurumGuncelleAsync(ucuncu.Id, CekSenetDurumu.TahsilEdildi, kasaId: kasa.Id);
        Assert.Equal(-1650m, await CariBakiyeAsync(db, musteri.Id));
        var kasaH2 = await db.KasaHareketleri.Where(h => h.KasaId == kasa.Id).OrderByDescending(h => h.Id).FirstAsync();
        Assert.Equal(0m, await HesapBakiyeAsync(db, ucuncu.Id, "101", kasaH2.Id));
    }

    [Fact]
    public async Task Verilen_cek_iadesi_borcu_geri_acar_ve_portfoydeki_cek_silinince_kayitlar_ters_cevrilir()
    {
        _fixture.RequireDatabase();
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (_, cari) = await SeedSirketVeCariAsync(db);
        var servis = new CekSenetServisi(db, StabilizasyonTestOturumu.Olustur());

        var verilen = await servis.EkleAsync(new CekSenetEkleTalebi(CekSenetTuru.VerilenCek, "VC-IADE-1", DateTime.UtcNow.AddDays(30), DateTime.UtcNow, 700m, cari.Id, "Biz"));
        Assert.Equal(700m, await CariBakiyeAsync(db, cari.Id));
        Assert.Equal(-700m, await HesapBakiyeAsync(db, verilen.Id, "103"));
        await servis.DurumGuncelleAsync(verilen.Id, CekSenetDurumu.IadeEdildi);
        Assert.Equal(0m, await CariBakiyeAsync(db, cari.Id));
        Assert.Equal(0m, await HesapBakiyeAsync(db, verilen.Id, "103"));
        Assert.Equal(0m, await HesapBakiyeAsync(db, verilen.Id, "320"));

        var alinan = await servis.EkleAsync(new CekSenetEkleTalebi(CekSenetTuru.AlinanCek, "CK-SIL-1", DateTime.UtcNow.AddDays(30), DateTime.UtcNow, 300m, cari.Id, "Müşteri"));
        Assert.Equal(-300m, await CariBakiyeAsync(db, cari.Id));
        await servis.SilAsync(alinan.Id, "Yanlış giriş");
        Assert.Equal(0m, await CariBakiyeAsync(db, cari.Id));
        Assert.True((await db.CekSenetler.IgnoreQueryFilters().SingleAsync(x => x.Id == alinan.Id)).SilindiMi);
    }
}
