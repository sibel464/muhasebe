using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Yardimcilar;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class FaturaHesapVeNumaraIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public FaturaHesapVeNumaraIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public void Genel_iskonto_kdv_matrahini_dusurur_ve_satirlara_dagitilir()
    {
        // 1000 TL %20 KDV + 500 TL %10 KDV, fatura altı %10 iskonto.
        var sonuc = FaturaHesaplayici.Hesapla([new(1, 1000, 0, 20), new(1, 500, 0, 10)], 10);

        Assert.Equal(150m, sonuc.GenelIskontoTutari);
        Assert.Equal(100m, sonuc.Satirlar[0].GenelIskontoPayi);
        Assert.Equal(50m, sonuc.Satirlar[1].GenelIskontoPayi);
        Assert.Equal(225m, sonuc.KdvToplam);            // 900*%20 + 450*%10 (eskiden 250 çıkıyordu)
        Assert.Equal(1575m, sonuc.GenelToplam);
        Assert.Equal(sonuc.GenelToplam, sonuc.Satirlar.Sum(s => s.Toplam));
    }

    [Fact]
    public void Kurus_payi_dagitimda_kaybolmaz()
    {
        var sonuc = FaturaHesaplayici.Hesapla([new(1, 33.33m, 0, 20), new(1, 33.33m, 0, 20), new(1, 33.34m, 0, 20)], 7);
        Assert.Equal(sonuc.GenelIskontoTutari, sonuc.Satirlar.Sum(s => s.GenelIskontoPayi));
        Assert.Equal(sonuc.Matrah + sonuc.KdvToplam, sonuc.GenelToplam);
    }

    [Fact]
    public async Task Numarasiz_es_zamanli_faturalar_farkli_muteselsil_numara_alir()
    {
        int cariId, urunId;
        await using (var db = new MuhasebeBaglami(_fixture.Options))
            (cariId, urunId) = await CariVeUrunAsync(db, CariTuru.Musteri);

        async Task<string> EkleAsync()
        {
            await using var db = new MuhasebeBaglami(_fixture.Options);
            var f = await new FaturaServisi(db, Oturum()).EkleAsync(Fatura(cariId, urunId, FaturaTuru.Satis));
            return f.FaturaNo;
        }

        var numaralar = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => EkleAsync()));
        Assert.Equal(5, numaralar.Distinct().Count());
        Assert.All(numaralar, n => Assert.Matches(@"^SAT\d{4}\d{9}$", n));
    }

    [Fact]
    public async Task Pesin_alis_faturasi_onayda_odemeyi_ve_kasa_cikisini_kaydeder()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (cariId, urunId) = await CariVeUrunAsync(db, CariTuru.Tedarikci);
        var servis = new FaturaServisi(db, Oturum());
        var fatura = Fatura(cariId, urunId, FaturaTuru.Alis);
        fatura.OdemeSekli = FaturaOdemeSekli.Nakit;
        fatura = await servis.EkleAsync(fatura);

        await servis.OnaylaVeOdemeyiKaydetAsync(fatura.Id);

        await using var kontrol = new MuhasebeBaglami(_fixture.Options);
        var kasa = await kontrol.KasaHareketleri.SingleAsync(h => h.FaturaId == fatura.Id);
        Assert.Equal(HareketTuru.Gider, kasa.Tur);
        Assert.Equal(fatura.GenelToplam, kasa.Tutar);
        var cari = await kontrol.CariKartlar.SingleAsync(c => c.Id == cariId);
        Assert.Equal(0m, cari.Bakiye); // alış alacağı ödemeyle kapandı
    }

    private static Fatura Fatura(int cariId, int urunId, FaturaTuru tur) => new()
    {
        CariKartId = cariId, Tarih = DateTime.UtcNow, Tur = tur, FaturaNo = string.Empty,
        Kalemler = [new FaturaKalemi { UrunId = urunId, Miktar = 1, BirimFiyat = 100, KdvOrani = 20 }]
    };

    private static async Task<(int CariId, int UrunId)> CariVeUrunAsync(MuhasebeBaglami db, CariTuru tur)
    {
        var sirketId = MuhasebeBaglami.VarsayilanSirketId;
        if (!await db.Sirketler.AnyAsync(x => x.Id == sirketId))
        {
            db.Sirketler.Add(new Sirket { Id = sirketId, Unvan = "Varsayılan Şirket", SirketKodu = "DEF-01" });
            await db.SaveChangesAsync();
        }
        var cari = new CariKart { SirketId = sirketId, Kod = $"FN-{Guid.NewGuid():N}", Unvan = "Numara Test Cari", Aktif = true, Tur = tur };
        var urun = new Urun { SirketId = sirketId, Kod = $"FN-{Guid.NewGuid():N}", Ad = "Numara Test Ürün", Birim = "Adet", Aktif = true, StokMiktari = 50 };
        db.AddRange(cari, urun);
        await db.SaveChangesAsync();
        var depo = await DepoKurallari.VarsayilanDepoAsync(db);
        db.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun.Id, DepoId = depo.Id, MevcutMiktar = 50 });
        await db.SaveChangesAsync();
        return (cari.Id, urun.Id);
    }

    private static Uygulama.Arayuzler.IKullaniciServisi Oturum()
    {
        var oturum = (Cari360TestKullanici)StabilizasyonTestOturumu.Olustur();
        oturum.AktifYetkiler!.FaturaOlustur = true;
        oturum.AktifYetkiler.FaturaOnayla = true;
        return oturum;
    }
}
