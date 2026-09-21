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

        var servis = new CekSenetServisi(db);

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

        // 3. Silme Testi
        await servis.SilAsync(cek.Id, "Hatalı kayıt");
        var silinmis = await db.CekSenetler.FindAsync(cek.Id);
        Assert.NotNull(silinmis);
        Assert.True(silinmis.SilindiMi);
        Assert.Equal("Hatalı kayıt", silinmis.SilmeNedeni);
    }
}
