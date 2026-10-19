using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Uygulama.Dto.Muhasebe;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Muhasebe ekranları şirket kimliğini boş (Guid.Empty) gönderir; servisler aktif firmayı kullanmalıdır.
[Collection("PostgreSQL integration")]
public sealed class MuhasebeEkranSirketCozumuTests(IsolatedPostgreSqlFixture fixture)
{
    [Fact]
    public async Task Bos_sirket_kimligiyle_mizan_saglik_ve_hesap_ekleme_aktif_firmada_calisir()
    {
        fixture.RequireDatabase();
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Mizan Test", SirketKodu = $"MZ-{Guid.NewGuid():N}"[..12] };
        await using (var kur = new MuhasebeBaglami(fixture.Options)) { kur.Sirketler.Add(sirket); await kur.SaveChangesAsync(); }

        await using var db = new MuhasebeBaglami(fixture.Options) { AktifSirketId = sirket.Id };
        var bugun = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
        await FinansMuhasebeKaydi.FisEkleAsync(db, sirket.Id, "Test", 1, bugun, "Test fişi", "T-1",
            new FinansMuhasebeKaydi.Satir("100", 500m, 0), new FinansMuhasebeKaydi.Satir("600", 0, 500m));

        var oturum = new Cari360TestKullanici { AktifYetkiler = new EtkiAlani.Modeller.Kullanici.KullaniciYetkileri { MizanGoruntule = true, HesapPlaniYonet = true } };
        var rapor = new MuhasebeRaporServisi(db, oturum);
        var mizan = await rapor.MizanAlAsync(Guid.Empty, null, null);
        Assert.Equal(500m, mizan.Satirlar.Single(s => s.HesapKodu == "100").DonemBorc);
        Assert.Equal(500m, mizan.Satirlar.Single(s => s.HesapKodu == "600").DonemAlacak);
        var defter = await rapor.BuyukDefterAlAsync(Guid.Empty, null, null);
        Assert.Contains(defter, s => s.HesapKodu == "100" && s.DonemBorc == 500m);
        var saglik = await rapor.SaglikPaneliGetirAsync(Guid.Empty);
        Assert.Equal(500m, saglik.ToplamYevmiyeBorc);

        var hesapServisi = new HesapPlaniServisi(db, oturum);
        await hesapServisi.EkleHesapAsync(new HesapPlaniDto { HesapKodu = "100.01", HesapAdi = "Merkez Kasa", Tip = EtkiAlani.Modeller.Muhasebe.HesapTipi.Aktif }, Guid.Empty);
        Assert.True(await db.HesapPlanlari.AnyAsync(h => h.SirketId == sirket.Id && h.HesapKodu == "100.01"));
    }
}
