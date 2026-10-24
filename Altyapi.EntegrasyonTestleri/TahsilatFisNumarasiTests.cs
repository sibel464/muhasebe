using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Kasa ve banka hareket numaraları ayrı ayrı 1'den başlar: kasa hareketi #N ile banka hareketi #N aynı gün tahsilat
// olabilir. İkisinin muhasebe fişi de "TahsilatOdeme" belge tipiyle aynı numarayı taşır; ikinci tahsilat bu yüzden
// reddedilmemeli, iptalleri de birbirinin fişini ters çevirmemeli.
[Collection("PostgreSQL integration")]
public sealed class TahsilatFisNumarasiTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public TahsilatFisNumarasiTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Ayni_numarali_kasa_ve_banka_tahsilati_birlikte_kaydedilir_ve_ayri_ayri_iptal_edilir()
    {
        _fixture.RequireDatabase();
        var sirketId = MuhasebeBaglami.VarsayilanSirketId;
        await using var db = new MuhasebeBaglami(_fixture.Options);
        if (!await db.Sirketler.AnyAsync(x => x.Id == sirketId))
        {
            db.Sirketler.Add(new Sirket { Id = sirketId, Unvan = "Varsayılan Şirket", SirketKodu = "DEF-01" });
            await db.SaveChangesAsync();
        }
        var cari = new CariKart { SirketId = sirketId, Kod = $"TFN-{Guid.NewGuid():N}", Unvan = "Fiş No Cari", Aktif = true, Tur = CariTuru.Musteri };
        var kasa = new Kasa { SirketId = sirketId, Kod = $"TFN-{Guid.NewGuid():N}"[..12], Ad = "Fiş No Kasa", AktifMi = true };
        var banka = new BankaHesabi { SirketId = sirketId, Kod = $"TFN-{Guid.NewGuid():N}"[..12], Ad = "Fiş No Banka", HesapTuru = BankaHesapTuru.VadesizTL, MuhasebeKodu = "102", AktifMi = true };
        db.AddRange(cari, kasa, banka);
        await db.SaveChangesAsync();

        // İki numara sayacını da aynı, ileri bir değere getir (sayaçlar yalnız ileri gider).
        var hedef = Math.Max(await db.KasaHareketleri.IgnoreQueryFilters().MaxAsync(h => (int?)h.Id) ?? 0,
                             await db.BankaHareketleri.IgnoreQueryFilters().MaxAsync(h => (int?)h.Id) ?? 0) + 1000;
        await db.Database.ExecuteSqlRawAsync($"SELECT setval(pg_get_serial_sequence('\"KasaHareketleri\"', 'Id'), {hedef}); SELECT setval(pg_get_serial_sequence('\"BankaHareketleri\"', 'Id'), {hedef});");

        var oturum = StabilizasyonTestOturumu.Olustur();
        var servis = new TahsilatOdemeServisi(db, oturum);
        var tarih = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
        var kasaTahsilati = await servis.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 100m, tarih, "Kasa", KasaId: kasa.Id));
        var bankaTahsilati = await servis.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Havale, cari.Id, 250m, tarih, "Banka", BankaHesabiId: banka.Id));
        Assert.Equal(kasaTahsilati.HareketId, bankaTahsilati.HareketId);

        // Banka tahsilatının iptali banka fişini ters çevirir, kasa fişine dokunmaz; sonra kasa iptali de yapılabilir.
        db.ChangeTracker.Clear();
        await new TahsilatOdemeServisi(db, oturum).IptalEtAsync("Banka", bankaTahsilati.HareketId, "Banka yanlış");
        db.ChangeTracker.Clear();
        await new TahsilatOdemeServisi(db, oturum).IptalEtAsync("Kasa", kasaTahsilati.HareketId, "Kasa yanlış");

        await using var k = new MuhasebeBaglami(_fixture.Options);
        var satirlar = await k.YevmiyeSatirlari.AsNoTracking().Include(s => s.Hesap)
            .Where(s => (s.Fis!.BelgeTipi == "TahsilatOdeme" || s.Fis.BelgeTipi == "TahsilatOdemeIptal") && s.Fis.BelgeId == kasaTahsilati.HareketId && s.CariKartId == null)
            .ToListAsync();
        decimal Net(string kod) => satirlar.Where(s => s.Hesap!.HesapKodu.StartsWith(kod)).Sum(s => s.Borc - s.Alacak);
        Assert.Equal(0m, Net("100"));
        Assert.Equal(0m, Net("102"));
        Assert.Equal(2, satirlar.Count(s => s.Hesap!.HesapKodu.StartsWith("100")));
        Assert.Equal(2, satirlar.Count(s => s.Hesap!.HesapKodu.StartsWith("102")));
        Assert.Equal(0m, (await k.CariKartlar.AsNoTracking().SingleAsync(c => c.Id == cari.Id)).Bakiye);
    }
}
