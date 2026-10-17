using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Tarih seçici bitiş gününü 00:00 olarak verir; raporlar o günün tüm kayıtlarını içermelidir.
[Collection("PostgreSQL integration")]
public sealed class RaporTarihAraligiTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public RaporTarihAraligiTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public void Aralik_gun_bazinda_ve_utc_etiketli()
    {
        var a = RaporTarihAraligi.Olustur(new DateTime(2026, 10, 1, 15, 30, 0), new DateTime(2026, 10, 18));
        Assert.Equal(new DateTime(2026, 10, 1), a.Baslangic);
        Assert.Equal(new DateTime(2026, 10, 19), a.BitisSonrasi);
        Assert.Equal(new DateTime(2026, 10, 18), a.BitisGunu);
        Assert.Equal(DateTimeKind.Utc, a.Baslangic.Kind);
        Assert.Equal(DateTimeKind.Utc, a.BitisSonrasi.Kind);
    }

    [Fact]
    public async Task Bitis_gununun_ogleden_sonraki_fisi_mizan_ve_muavinde_gorunur()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Tarih Test", SirketKodu = $"TT-{Guid.NewGuid():N}"[..10] };
        db.Sirketler.Add(sirket);
        await db.SaveChangesAsync();
        var kasa = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(db, sirket.Id, "100");
        var sermaye = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(db, sirket.Id, "500");
        db.YevmiyeFisleri.Add(new YevmiyeFis
        {
            SirketId = sirket.Id, FisNo = $"TT-{Guid.NewGuid():N}"[..14], Tarih = new DateTime(2026, 10, 18, 14, 0, 0, DateTimeKind.Utc),
            BelgeTipi = "Test", Durum = YevmiyeDurumu.Onaylandi, Aciklama = "Öğleden sonra",
            Satirlar = [new YevmiyeSatiri { HesapId = kasa, Borc = 250 }, new YevmiyeSatiri { HesapId = sermaye, Alacak = 250 }]
        });
        await db.SaveChangesAsync();

        var servis = new MuhasebeRaporServisi(db, new Cari360TestKullanici { AktifYetkiler = new() { MizanGoruntule = true } });
        // DatePicker'ın verdiği gibi: saat bilgisi olmayan (Unspecified) günler
        var gun = new DateTime(2026, 10, 18);
        var mizan = await servis.MizanAlAsync(sirket.Id, gun, gun);
        Assert.Equal(250, mizan.Satirlar.Single(s => s.HesapKodu == "100").DonemBorc);
        var muavin = await servis.MuavinAlAsync(kasa, sirket.Id, gun, gun);
        Assert.Equal(250, muavin.DonemBorc);
        Assert.Equal(gun, muavin.BitisTarihi.Date);
        var ertesiGun = await servis.MuavinAlAsync(kasa, sirket.Id, gun.AddDays(1), gun.AddDays(1));
        Assert.Equal(0, ertesiGun.DonemBorc);
        Assert.Equal(250, ertesiGun.DevirBakiye);
    }
}
