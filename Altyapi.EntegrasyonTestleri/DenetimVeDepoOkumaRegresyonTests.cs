using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Tanimlar;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class DenetimVeDepoOkumaRegresyonTests(IsolatedPostgreSqlFixture fixture)
{
    [Fact]
    public async Task Audit_gercek_oturum_kullanicisini_yazar_ve_cikis_sonrasi_sisteme_doner()
    {
        fixture.RequireDatabase();
        var oturum = new AktifFirmaBaglami();
        await using var sorgu = new MuhasebeBaglami(fixture.Options);
        var mevcutKullanici = await sorgu.Kullanicilar.AsNoTracking().FirstAsync();
        oturum.Degistir(oturum.SirketId, oturum.SubeId, oturum.MaliDonemId,
            mevcutKullanici.Id, mevcutKullanici.KullaniciAdi);

        var tanim = YeniTanim();
        await using (var baglam = new MuhasebeBaglami(fixture.Options, oturum))
        {
            baglam.MerkeziTanimlar.Add(tanim);
            await baglam.SaveChangesAsync();
        }

        var kullaniciKaydi = await sorgu.AuditLoglar.AsNoTracking()
            .SingleAsync(x => x.TabloAdi == nameof(MerkeziTanim) && x.KayitId == tanim.Id.ToString());
        Assert.Equal(mevcutKullanici.Id, kullaniciKaydi.KullaniciId);
        Assert.Equal(mevcutKullanici.KullaniciAdi, kullaniciKaydi.KullaniciAdi);

        oturum.Degistir(oturum.SirketId, oturum.SubeId, oturum.MaliDonemId, null);
        var sistemTanimi = YeniTanim();
        await using (var baglam = new MuhasebeBaglami(fixture.Options, oturum))
        {
            baglam.MerkeziTanimlar.Add(sistemTanimi);
            await baglam.SaveChangesAsync();
        }

        var sistemKaydi = await sorgu.AuditLoglar.AsNoTracking()
            .SingleAsync(x => x.TabloAdi == nameof(MerkeziTanim) && x.KayitId == sistemTanimi.Id.ToString());
        Assert.Null(sistemKaydi.KullaniciId);
        Assert.Equal("Sistem", sistemKaydi.KullaniciAdi);
    }

    [Fact]
    public async Task Depo_listeleme_bos_sonucta_ornek_kayit_olusturmaz()
    {
        fixture.RequireDatabase();
        await using var baglam = new MuhasebeBaglami(fixture.Options);
        var aktifIdler = await baglam.Depolar.AsNoTracking().Where(x => x.AktifMi).Select(x => x.Id).ToListAsync();
        var oncekiSayi = await baglam.Depolar.CountAsync();
        try
        {
            await baglam.Depolar.Where(x => x.AktifMi).ExecuteUpdateAsync(x => x.SetProperty(d => d.AktifMi, false));
            var servis = new DepoServisi(new TestFactory(fixture.Options),
                new Cari360TestKullanici { AktifKullanici = null });

            Assert.Empty(await servis.DepolariGetirAsync());
            Assert.Equal(oncekiSayi, await baglam.Depolar.CountAsync());
        }
        finally
        {
            await baglam.Depolar.Where(x => aktifIdler.Contains(x.Id))
                .ExecuteUpdateAsync(x => x.SetProperty(d => d.AktifMi, true));
        }
    }

    private static MerkeziTanim YeniTanim() => new()
    {
        Grup = TanimGruplari.Marka,
        Kod = "AUDIT_" + Guid.NewGuid().ToString("N"),
        Ad = "Audit regresyon"
    };

    private sealed class TestFactory(DbContextOptions<MuhasebeBaglami> options) : IDbContextFactory<MuhasebeBaglami>
    {
        public MuhasebeBaglami CreateDbContext() => new(options);
    }
}
