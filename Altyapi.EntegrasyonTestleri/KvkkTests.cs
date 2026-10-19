using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class KvkkTests(IsolatedPostgreSqlFixture fixture)
{
    private static KvkkServisi Servis(MuhasebeBaglami db) => new(db, new Cari360TestKullanici
    {
        AktifYetkiler = new EtkiAlani.Modeller.Kullanici.KullaniciYetkileri { CariGoruntule = true, CariSil = true }
    });

    private async Task<(MuhasebeBaglami Db, CariKart Cari)> HazirlaAsync()
    {
        fixture.RequireDatabase();
        var db = new MuhasebeBaglami(fixture.Options);
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "KVKK Test", SirketKodu = $"KV-{Guid.NewGuid():N}"[..10] };
        db.Sirketler.Add(sirket);
        await db.SaveChangesAsync();
        var cari = new CariKart
        {
            SirketId = sirket.Id, Kod = $"KV-{Guid.NewGuid():N}"[..10], Unvan = "Ayşe Yılmaz", Ad = "Ayşe", Soyad = "Yılmaz", TcNo = "12345678901",
            CepTelefonu = "05551112233", Eposta = "ayse@ornek.com", Adres = "Atatürk Cad. 1", Il = "İzmir", Iban = "TR000000000000000000000001", Aktif = true
        };
        db.CariKartlar.Add(cari);
        await db.SaveChangesAsync();
        // Değişiklik günlüğüne eski telefon düşsün
        cari.CepTelefonu = "05559998877";
        await db.SaveChangesAsync();
        return (db, cari);
    }

    [Fact]
    public async Task Islemi_olmayan_cari_dokumu_alinir_ve_anonimlestirilince_gunluk_de_temizlenir()
    {
        var (db, cari) = await HazirlaAsync();
        await using var _ = db;
        var servis = Servis(db);

        var dokum = await servis.KisiselVeriDokumuAsync(cari.Id);
        Assert.Contains("12345678901", dokum);
        Assert.Contains("ayse@ornek.com", dokum);

        await servis.AnonimlestirAsync(cari.Id, "19.10.2026 e-posta başvurusu");

        await using var kontrol = new MuhasebeBaglami(fixture.Options);
        var c = await kontrol.CariKartlar.IgnoreQueryFilters([MuhasebeBaglami.SilinmisFiltresi]).SingleAsync(x => x.Id == cari.Id);
        Assert.StartsWith("ANONİM-", c.Unvan);
        Assert.Null(c.TcNo); Assert.Null(c.CepTelefonu); Assert.Null(c.Eposta); Assert.Null(c.Adres); Assert.Null(c.Iban);
        Assert.True(c.Silindi);
        var gunluk = await kontrol.AuditLoglar.Where(l => l.TabloAdi == nameof(CariKart) && l.KayitId == cari.Id.ToString())
            .Select(l => (l.EskiDegerJson ?? "") + (l.YeniDegerJson ?? "")).ToListAsync();
        Assert.NotEmpty(gunluk);
        Assert.DoesNotContain(gunluk, g => g.Contains("12345678901") || g.Contains("0555") || g.Contains("ayse@") || g.Contains("Yılmaz") || g.Contains("Yılmaz".Replace("ı", "\\u0131")));
    }

    [Fact]
    public async Task Saklama_suresi_dolmamis_cari_anonimlestirilemez()
    {
        var (db, cari) = await HazirlaAsync();
        await using var _ = db;
        db.CariHareketler.Add(new CariHareket { CariKartId = cari.Id, Tarih = DateTime.SpecifyKind(DateTime.Today.AddYears(-3), DateTimeKind.Utc), HareketTuru = CariHareketTuru.SatisFaturasi, Borc = 100m });
        await db.SaveChangesAsync();
        var servis = Servis(db);

        var durum = await servis.SaklamaDurumuAsync(cari.Id);
        Assert.False(durum.AnonimlestirilebilirMi);
        Assert.Equal(new DateTime(DateTime.Today.Year - 3 + KvkkServisi.SaklamaYili, 12, 31), durum.SaklamaBitisi);
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.AnonimlestirAsync(cari.Id, "başvuru"));
        Assert.Equal("12345678901", (await db.CariKartlar.AsNoTracking().SingleAsync(x => x.Id == cari.Id)).TcNo);

        // 11 yıl önceki kayıt: süre dolmuş
        var eski = await db.CariHareketler.SingleAsync(h => h.CariKartId == cari.Id);
        eski.Tarih = DateTime.SpecifyKind(DateTime.Today.AddYears(-11).AddMonths(-1), DateTimeKind.Utc);
        await db.SaveChangesAsync();
        Assert.True((await servis.SaklamaDurumuAsync(cari.Id)).AnonimlestirilebilirMi);
    }
}
