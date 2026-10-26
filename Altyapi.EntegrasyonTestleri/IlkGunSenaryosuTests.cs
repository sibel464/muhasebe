using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Tanimlar;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Yeni satın alan müşterinin ilk günü: hiçbir tanım yapmadan, boş veritabanında ekranların çağırdığı servislerle
// cari/ürün açar, mal alır, satar. Paylaşılan test veritabanı başka testlerin kasalarını içerdiği için kendi boş DB'sini kurar.
public sealed class IlkGunSenaryosuTests : IAsyncLifetime
{
    private readonly IsolatedPostgreSqlFixture _db = new();
    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private static Cari360TestKullanici TamYetkili()
    {
        var y = new KullaniciYetkileri();
        foreach (var p in typeof(KullaniciYetkileri).GetProperties().Where(p => p.PropertyType == typeof(bool) && p.CanWrite))
            p.SetValue(y, true);
        return new Cari360TestKullanici { AktifYetkiler = y };
    }

    [Fact]
    public async Task Bos_kurulumda_ilk_gun_alis_ve_satis_tanim_yapmadan_calisir()
    {
        _db.RequireDatabase();
        var kullanici = TamYetkili();

        await using (var c = new MuhasebeBaglami(_db.Options))
            Assert.All(await new BaslangicKontrolServisi(c).AdimlariGetirAsync(), x => Assert.False(x.Tamam, x.Baslik));

        int musteriId, tedarikciId, urunId;
        await using (var c = new MuhasebeBaglami(_db.Options))
        {
            var cariler = new CariServisi(c, kullanici);
            musteriId = (await cariler.OtomatikKodlaEkleAsync(new CariKart { Unvan = "İlk Müşteri", Tur = CariTuru.Musteri, Aktif = true })).Id;
            tedarikciId = (await cariler.OtomatikKodlaEkleAsync(new CariKart { Unvan = "İlk Tedarikçi", Tur = CariTuru.Tedarikci, Aktif = true })).Id;

            var adet = await c.MerkeziTanimlar.SingleAsync(x => x.Grup == TanimGruplari.Birim && x.Kod == "ADET");
            urunId = (await new UrunServisi(c, kullanici).GuvenliEkleAsync(new Urun
            {
                Ad = "İlk Ürün", Birim = adet.KisaAd ?? adet.Ad, BirimTanimiId = adet.Id, KdvOrani = 20,
                AlisFiyati = 50, PerakendeSatisFiyati = 100, Aktif = true
            }, otomatikKod: true, otomatikBarkod: true)).Id;
        }

        // Tedarikçiden 10 adet alış: stok girer.
        await using (var c = new MuhasebeBaglami(_db.Options))
        {
            var faturalar = new FaturaServisi(c, kullanici);
            var alis = await faturalar.EkleAsync(new Fatura
            {
                FaturaNo = "ALIS-0001", Tur = FaturaTuru.Alis, CariKartId = tedarikciId, Tarih = DateTime.UtcNow,
                Kalemler = [new FaturaKalemi { UrunId = urunId, Miktar = 10, BirimFiyat = 50, KdvOrani = 20 }]
            });
            await faturalar.OnaylaAsync(alis.Id);
        }
        await using (var c = new MuhasebeBaglami(_db.Options))
            Assert.Equal(10, (await c.Urunler.SingleAsync(u => u.Id == urunId)).StokMiktari);

        // İlk nakit satış: kasa tanımlanmamış olsa da para bir kasaya girer.
        await using (var c = new MuhasebeBaglami(_db.Options))
            await new FaturaServisi(c, kullanici).PosSatisiniTamamlaAsync(Satis("SAT-0001", musteriId, urunId, 2), "Nakit");
        await using (var c = new MuhasebeBaglami(_db.Options))
        {
            var kasa = await c.Kasalar.SingleAsync();
            Assert.Equal(240m, await new KasaServisi(c, kullanici).KasaBakiyeHesaplaAsync(kasa.Id));
            Assert.Equal(8, (await c.Urunler.SingleAsync(u => u.Id == urunId)).StokMiktari);
        }

        // POS hesabı yokken kartlı satış anlaşılır bir uyarıyla reddedilir, hiçbir iz bırakmaz.
        await using (var c = new MuhasebeBaglami(_db.Options))
        {
            var hata = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new FaturaServisi(c, kullanici).PosSatisiniTamamlaAsync(Satis("SAT-0002", musteriId, urunId, 1), "Kredi Kartı"));
            Assert.Contains("POS", hata.Message);
        }
        await using (var c = new MuhasebeBaglami(_db.Options))
        {
            Assert.False(await c.Faturalar.AnyAsync(f => f.FaturaNo == "SAT-0002"));
            Assert.Equal(8, (await c.Urunler.SingleAsync(u => u.Id == urunId)).StokMiktari);

            var rapor = new MuhasebeRaporServisi(c, kullanici);
            var mizan = await rapor.MizanAlAsync(MuhasebeBaglami.VarsayilanSirketId, null, null);
            Assert.True(mizan.DengedeMi);
            Assert.Empty(await rapor.GetirMuhasebelesmemisBelgelerAsync(MuhasebeBaglami.VarsayilanSirketId));
        }

        // Uyarıdaki gibi POS hesabı tanımlanınca kartlı satış POS hesabına yazılır.
        int posId;
        await using (var c = new MuhasebeBaglami(_db.Options))
        {
            posId = (await new BankaServisi(c, kullanici).BankaHesabiEkleAsync(new BankaHesabi
            {
                Kod = "POS-01", Ad = "İş Bankası POS", HesapTuru = BankaHesapTuru.POSHesabi, AktifMi = true
            })).Id;
            await new FaturaServisi(c, kullanici).PosSatisiniTamamlaAsync(Satis("SAT-0002", musteriId, urunId, 1), "Kredi Kartı");
        }
        await using (var c = new MuhasebeBaglami(_db.Options))
            Assert.Equal(120m, await new BankaServisi(c, kullanici).HesapBakiyeHesaplaAsync(posId));

        // Veresiye satış ve ertesi gün kasaya tahsilat: müşterinin borcu kapanır.
        await using (var c = new MuhasebeBaglami(_db.Options))
            await new FaturaServisi(c, kullanici).PosSatisiniTamamlaAsync(Satis("SAT-0003", musteriId, urunId, 3), "Açık Hesap");
        await using (var c = new MuhasebeBaglami(_db.Options))
        {
            var kasa = await c.Kasalar.SingleAsync();
            await new KasaServisi(c, kullanici).TahsilatEkleAsync(kasa.Id, musteriId, 360m, "Veresiye tahsilatı");
        }
        await using (var c = new MuhasebeBaglami(_db.Options))
        {
            var kasa = await c.Kasalar.SingleAsync();
            Assert.Equal(600m, await new KasaServisi(c, kullanici).KasaBakiyeHesaplaAsync(kasa.Id));
            var musteri = await c.CariKartlar.SingleAsync(x => x.Id == musteriId);
            Assert.Equal(0m, musteri.Bakiye);
            Assert.Equal(4, (await c.Urunler.SingleAsync(u => u.Id == urunId)).StokMiktari);

            var rapor = new MuhasebeRaporServisi(c, kullanici);
            Assert.True((await rapor.MizanAlAsync(MuhasebeBaglami.VarsayilanSirketId, null, null)).DengedeMi);
            Assert.True((await rapor.SaglikPaneliGetirAsync(MuhasebeBaglami.VarsayilanSirketId)).SaglikliMi);
            Assert.Empty(await rapor.GetirMuhasebelesmemisBelgelerAsync(MuhasebeBaglami.VarsayilanSirketId));
        }

        await using (var c = new MuhasebeBaglami(_db.Options))
        {
            var adimlar = await new BaslangicKontrolServisi(c).AdimlariGetirAsync();
            Assert.Equal(["Ayarlar", "Banka"], adimlar.Where(x => !x.Tamam).Select(x => x.Sayfa)); // firma künyesi ve vadesiz hesap kaldı
        }

        // Rapor merkezindeki her rapor ilk günün verisiyle açılır.
        await using (var c = new MuhasebeBaglami(_db.Options))
        {
            var r = StabilizasyonTestOturumu.Rapor(c, MuhasebeBaglami.VarsayilanSirketId);
            var f = new Uygulama.Dto.Rapor.SatisRaporFiltreDto { BaslangicTarihi = DateTime.Today.AddDays(-1), BitisTarihi = DateTime.Today.AddDays(1) };
            var ozet = await r.GetYoneticiOzetiAsync(f);
            await r.GetDonemKarsilastirmaAsync(f);
            Assert.Equal(3, (await r.GetSatisRaporuAsync(f)).TotalCount);
            await r.GetEnCokSatanUrunlerAsync(f); await r.GetEnKarliUrunlerAsync(f); await r.GetAlisRaporuAsync(f);
            await r.GetCariRaporuAsync(f); await r.GetCariYaslandirmaAsync(f); await r.GetTahsilatPerformansiAsync(f);
            await r.GetStokRaporuAsync(f); await r.GetHareketsizUrunlerAsync(f); await r.GetStokDegerRaporuAsync(f);
            await r.GetFinansRaporuAsync(f); await r.GetNakitAkisRaporuAsync(f); await r.GetKasaBankaRaporuAsync(f);
            await r.GetPosRaporuAsync(f); await r.GetCekSenetRaporuAsync(f); await r.GetKdvRaporuAsync(f);
            Assert.NotNull(ozet);
        }
    }

    private static Fatura Satis(string no, int cariId, int urunId, decimal miktar) => new()
    {
        FaturaNo = no, Tur = FaturaTuru.Satis, CariKartId = cariId, Tarih = DateTime.UtcNow,
        Kalemler = [new FaturaKalemi { UrunId = urunId, Miktar = miktar, BirimFiyat = 100, KdvOrani = 20 }]
    };
}
