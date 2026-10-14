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
}
