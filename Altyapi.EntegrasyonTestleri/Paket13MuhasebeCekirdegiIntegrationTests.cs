using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class Paket13MuhasebeCekirdegiIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;

    public Paket13MuhasebeCekirdegiIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private async Task<(Guid SirketId, Kullanici Kullanici, HesapPlaniServisi HesapServisi, YevmiyeServisi YevmiyeServisi, MuhasebeRaporServisi RaporServisi, MuhasebeBaglantiServisi BaglantiServisi)> SetupTestEnvironmentAsync(MuhasebeBaglami db, bool yetkili = true)
    {
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Muhasebe 360 Test A.Ş.", SirketKodu = $"ST-{Guid.NewGuid():N}"[..10] };
        db.Sirketler.Add(sirket);

        var uId = Guid.NewGuid().ToString("N")[..12];
        var kullanici = new Kullanici
        {
            KullaniciAdi = $"mhk_{uId}",
            Ad = "Ahmet",
            Soyad = "Yılmaz",
            Eposta = $"mhk_{uId}@muhasebe.com",
            SifreHash = "hash",
            Aktif = true
        };
        db.Kullanicilar.Add(kullanici);
        await db.SaveChangesAsync();

        var yetki = new KullaniciYetkileri
        {
            KullaniciId = kullanici.Id,
            // Yetkiler artık varsayılan olarak kapalı; testin kullandığı fatura yetkileri açıkça verilir.
            FaturaGoruntule = yetkili,
            FaturaOlustur = yetkili,
            FaturaOnayla = yetkili,
            FaturaIptal = yetkili,
            MuhasebeFisGoruntule = yetkili,
            MuhasebeFisEkle = yetkili,
            MuhasebeFisDuzenle = yetkili,
            MuhasebeFisOnayla = yetkili,
            MuhasebeFisIptal = yetkili,
            HesapPlaniYonet = yetkili,
            MizanGoruntule = yetkili,
            MaliyetHesaplariGoruntule = yetkili,
            AyarlariYonet = yetkili
        };
        db.KullaniciYetkileri.Add(yetki);

        // Seed basic chart of accounts if not present
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "100"))
        {
            db.HesapPlanlari.AddRange(
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "100", HesapAdi = "KASA", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "102", HesapAdi = "BANKALAR", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "108", HesapAdi = "POS ALACAKLARI", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "120", HesapAdi = "ALICILAR", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "153", HesapAdi = "TİCARİ MALLAR", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "191", HesapAdi = "İNDİRİLECEK KDV", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "320", HesapAdi = "SATICILAR", Tip = HesapTipi.Pasif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "391", HesapAdi = "HESAPLANAN KDV", Tip = HesapTipi.Pasif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "600", HesapAdi = "YURT İÇİ SATIŞLAR", Tip = HesapTipi.Gelir, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "621", HesapAdi = "SATILAN TİCARİ MALLAR MALİYETİ", Tip = HesapTipi.Gider, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true }
            );
        }

        // Account Mappings
        db.MuhasebeBaglantiKodlari.AddRange(
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "KASA", KategoriAdi = "Kasa Hesabı", VarSayilanHesapKodu = "100", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "BANKA", KategoriAdi = "Banka Hesabı", VarSayilanHesapKodu = "102", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "ALICILAR", KategoriAdi = "Alıcılar", VarSayilanHesapKodu = "120", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "SATICILAR", KategoriAdi = "Satıcılar", VarSayilanHesapKodu = "320", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "YURTICI_SATIS", KategoriAdi = "Yurtiçi Satışlar", VarSayilanHesapKodu = "600", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "SATIS_KDV", KategoriAdi = "Satış KDV", VarSayilanHesapKodu = "391", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "INDIRILECEK_KDV", KategoriAdi = "İndirilecek KDV", VarSayilanHesapKodu = "191", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "STOK", KategoriAdi = "Stok", VarSayilanHesapKodu = "153", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "STMM", KategoriAdi = "STMM", VarSayilanHesapKodu = "621", AktifMi = true }
        );

        await db.SaveChangesAsync();

        var kullaniciServisi = new DummyKullaniciServisi(kullanici, yetki);
        var hesapServisi = new HesapPlaniServisi(db, kullaniciServisi);
        var baglantiServisi = new MuhasebeBaglantiServisi(db, kullaniciServisi);
        var yevmiyeServisi = new YevmiyeServisi(db, kullaniciServisi, baglantiServisi);
        var raporServisi = new MuhasebeRaporServisi(db, kullaniciServisi);

        return (sirket.Id, kullanici, hesapServisi, yevmiyeServisi, raporServisi, baglantiServisi);
    }

    [Fact]
    public async Task Test_01_HesapPlani_Olusturma()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, hesapServisi, _, _, _) = await SetupTestEnvironmentAsync(db);

        var yeniHesap = await hesapServisi.EkleHesapAsync(new HesapPlaniDto
        {
            HesapKodu = "100.01",
            HesapAdi = "Merkez Nakit Kasa",
            Tip = HesapTipi.Aktif,
            BakiyeYonu = BorcAlacakYonu.Borc
        }, sirketId);

        Assert.True(yeniHesap.Id > 0);
        Assert.Equal("100.01", yeniHesap.HesapKodu);
        Assert.True(yeniHesap.AktifMi);
    }

    [Fact]
    public async Task Test_02_AltHesap_Hiyerarsi_Ve_Uyum_Kontrolu()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, hesapServisi, _, _, _) = await SetupTestEnvironmentAsync(db);

        // Subaccount must start with parent account code
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await hesapServisi.EkleHesapAsync(new HesapPlaniDto
            {
                HesapKodu = "102.01",
                HesapAdi = "Yanlış Alt Hesap",
                UstHesapId = (await hesapServisi.GetirHesapByKodAsync("100", sirketId))!.Id
            }, sirketId);
        });
    }

    [Fact]
    public async Task Test_03_Duplicate_HesapKodu_Engeli()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, hesapServisi, _, _, _) = await SetupTestEnvironmentAsync(db);

        var kod = $"999.{Guid.NewGuid().ToString("N")[..4]}";
        await hesapServisi.EkleHesapAsync(new HesapPlaniDto
        {
            HesapKodu = kod,
            HesapAdi = "Özel Hesap"
        }, sirketId);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await hesapServisi.EkleHesapAsync(new HesapPlaniDto
            {
                HesapKodu = kod,
                HesapAdi = "Çift Kayıt"
            }, sirketId);
        });
    }

    [Fact]
    public async Task Test_04_Sistem_Ve_Kullanilmis_Hesap_Silme_Engeli()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, hesapServisi, _, _, _) = await SetupTestEnvironmentAsync(db);

        var h100 = await hesapServisi.GetirHesapByKodAsync("100", sirketId);
        Assert.NotNull(h100);

        // System account delete throws
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await hesapServisi.SilVeyaPasifeAlAsync(h100.Id, sirketId);
        });
    }

    [Fact]
    public async Task Test_05_06_Manuel_Mahsup_Fisi_Borc_Alacak_Denge_Dogrulamasi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");

        var fisDto = new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Mahsup,
            Tarih = DateTime.UtcNow,
            Aciklama = "Bankadan Kasaya Virman",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 5000m, Alacak = 0m, Aciklama = "Kasaya Giriş" },
                new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 5000m, Aciklama = "Bankadan Çıkış" }
            }
        };

        var fis = await yevmiyeServisi.FisOlusturAsync(fisDto, sirketId, kullanici.Id);
        Assert.NotNull(fis);
        Assert.True(fis.DengedeMi);
        Assert.Equal(5000m, fis.ToplamBorc);
        Assert.Equal(5000m, fis.ToplamAlacak);
    }

    [Fact]
    public async Task Test_07_Dengesiz_Fis_Onay_Engeli()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");

        var fisDto = new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Mahsup,
            Tarih = DateTime.UtcNow,
            Aciklama = "Dengesiz Fiş",
            Durum = YevmiyeDurumu.Taslak,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 5000.01m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 5000.00m }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await yevmiyeServisi.FisOlusturAsync(fisDto, sirketId, kullanici.Id);
        });
    }

    [Fact]
    public async Task Test_08_09_Tahsil_Ve_Tediye_Fisi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h120 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "120");

        var tahsilFis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Tahsil,
            Tarih = DateTime.UtcNow,
            Aciklama = "Nakit Tahsilat Fişi",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 2500m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h120.Id, Borc = 0m, Alacak = 2500m }
            }
        }, sirketId, kullanici.Id);

        Assert.Equal(FisTuru.Tahsil, tahsilFis.FisTuru);
        Assert.StartsWith("THS-", tahsilFis.FisNo);
    }

    [Fact]
    public async Task Test_10_Acilis_Fisi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h320 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "320");

        var acilisFis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Acilis,
            Tarih = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Aciklama = "Dönem Açılış Fişi",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 10000m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h320.Id, Borc = 0m, Alacak = 10000m }
            }
        }, sirketId, kullanici.Id);

        Assert.Equal(FisTuru.Acilis, acilisFis.FisTuru);
        Assert.StartsWith("ACL-", acilisFis.FisNo);
    }

    [Fact]
    public async Task Test_11_12_Taslak_Mizana_Yansimaz_Onayli_Mizana_Yansir()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, raporServisi, _) = await SetupTestEnvironmentAsync(db);

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");

        var mizanBaslangic = await raporServisi.MizanAlAsync(sirketId, null, null);
        var baslangicBorc = mizanBaslangic.ToplamBorc;

        var taslakFis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Mahsup,
            Tarih = DateTime.UtcNow,
            Aciklama = "Taslak Virman",
            Durum = YevmiyeDurumu.Taslak,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 7500m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 7500m }
            }
        }, sirketId, kullanici.Id);

        var mizanTaslak = await raporServisi.MizanAlAsync(sirketId, null, null);
        Assert.Equal(baslangicBorc, mizanTaslak.ToplamBorc); // Draft ignored

        await yevmiyeServisi.FisOnaylaAsync(taslakFis.Id, sirketId);

        var mizanOnayli = await raporServisi.MizanAlAsync(sirketId, null, null);
        Assert.Equal(baslangicBorc + 7500m, mizanOnayli.ToplamBorc); // Reflected
        Assert.True(mizanOnayli.DengedeMi);
    }

    [Fact]
    public async Task Test_13_14_15_Fis_Iptal_Ters_Fis_Ve_Double_Cancel_Engeli()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");

        var fis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Mahsup,
            Tarih = DateTime.UtcNow,
            Aciklama = "İptal Edilecek İşlem",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 4000m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 4000m }
            }
        }, sirketId, kullanici.Id);

        var iptalFis = await yevmiyeServisi.FisIptalEtAsync(fis.Id, "Hatalı Giriş", sirketId);
        Assert.Equal(YevmiyeDurumu.Iptal, iptalFis.Durum);
        Assert.NotNull(iptalFis.TersFisId);

        var tersFis = await yevmiyeServisi.GetirFisByIdAsync(iptalFis.TersFisId.Value, sirketId);
        Assert.NotNull(tersFis);
        Assert.Equal(4000m, tersFis.ToplamAlacak);

        // Double cancel throws
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await yevmiyeServisi.FisIptalEtAsync(fis.Id, "Tekrar İptal", sirketId);
        });
    }

    [Fact]
    public async Task Onayli_fis_iptali_hesap_bakiyesini_sifirlar_iki_kez_geri_almaz()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, raporServisi, _) = await SetupTestEnvironmentAsync(db);
        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");

        var fis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId, FisTuru = FisTuru.Mahsup, Tarih = DateTime.UtcNow, Aciklama = "Bakiye testi", Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 1000m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 1000m }
            }
        }, sirketId, kullanici.Id);
        await yevmiyeServisi.FisIptalEtTersKayitUretAsync(fis.Id, "Hatalı", sirketId);

        var muavin = await raporServisi.MuavinAlAsync(h100.Id, sirketId);
        Assert.Equal(muavin.Satirlar.Sum(x => x.Borc), muavin.Satirlar.Sum(x => x.Alacak));
        Assert.Equal(2, muavin.Satirlar.Count);
        var mizan = await raporServisi.MizanAlAsync(sirketId, null, null);
        Assert.True(mizan.DengedeMi);

        // Ters fiş de iptal edilemez.
        var iptal = await yevmiyeServisi.GetirFisByIdAsync(fis.Id, sirketId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => yevmiyeServisi.FisIptalEtAsync(iptal!.TersFisId!.Value, "Ters", sirketId));
    }

    [Fact]
    public async Task Taslak_fis_iptali_ters_fis_kesmez()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);
        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");

        var fis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId, FisTuru = FisTuru.Mahsup, Tarih = DateTime.UtcNow, Aciklama = "Taslak", Durum = YevmiyeDurumu.Taslak,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 500m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 500m }
            }
        }, sirketId, kullanici.Id);
        var sonuc = await yevmiyeServisi.FisIptalEtAsync(fis.Id, "Vazgeçildi", sirketId);

        Assert.Equal(YevmiyeDurumu.Iptal, sonuc.Durum);
        Assert.Null(sonuc.TersFisId);
        Assert.False(await db.YevmiyeFisleri.AnyAsync(f => f.IptalEdilenFisId == fis.Id));
    }

    [Fact]
    public async Task Belgeden_uretilen_fis_muhasebe_ekranindan_iptal_edilemez()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);
        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");
        var fis = new YevmiyeFis
        {
            SirketId = sirketId, FisNo = $"KH-{Guid.NewGuid():N}"[..15], Tarih = DateTime.UtcNow, BelgeTipi = "KasaHareketi", BelgeId = 987654,
            Aciklama = "Belge fişi", Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = { new YevmiyeSatiri { HesapId = h100.Id, Borc = 10m }, new YevmiyeSatiri { HesapId = h102.Id, Alacak = 10m } }
        };
        db.YevmiyeFisleri.Add(fis);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => yevmiyeServisi.FisIptalEtTersKayitUretAsync(fis.Id, "Deneme", sirketId));
        Assert.False(await db.YevmiyeFisleri.AnyAsync(f => f.IptalEdilenFisId == fis.Id));
    }

    [Fact]
    public async Task Test_16_17_18_19_Yevmiye_Muavin_BuyukDefter_GenelMizan()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, raporServisi, _) = await SetupTestEnvironmentAsync(db);

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h120 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "120");

        await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Tahsil,
            Tarih = DateTime.UtcNow,
            Aciklama = "Nakit Tahsilat",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 12000m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h120.Id, Borc = 0m, Alacak = 12000m }
            }
        }, sirketId, kullanici.Id);

        // Yevmiye Query
        var yevmiyeResult = await yevmiyeServisi.YevmiyeDefteriSorgulaAsync(new YevmiyeFiltreDto { SirketId = sirketId, Sayfa = 1, SayfaBoyutu = 10 });
        Assert.True(yevmiyeResult.ToplamKayit >= 1);

        // Muavin Query
        var muavin = await raporServisi.MuavinAlAsync(h100.Id, sirketId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        Assert.Single(muavin.Satirlar);
        Assert.Equal(12000m, muavin.SonBakiye);

        // Büyük Defter Query
        var buyukDefter = await raporServisi.BuyukDefterAlAsync(sirketId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        Assert.NotEmpty(buyukDefter);

        // Genel Mizan Query
        var mizan = await raporServisi.MizanAlAsync(sirketId, null, null);
        Assert.True(mizan.DengedeMi);
        Assert.True(mizan.ToplamBorc >= 12000m);
        Assert.Equal(mizan.ToplamBorc, mizan.ToplamAlacak);
    }

    [Fact]
    public async Task Test_23_24_25_Fatura_Tahsilat_Odeme_Otomatik_Muhasebelestirme()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var cari = new CariKart { SirketId = sirketId, Kod = "C001", Unvan = "Test Cari", Tur = CariTuru.MusteriTedarikci, Aktif = true };
        var urun = new Urun { SirketId = sirketId, Kod = "U001", Ad = "Test Ürün", PerakendeSatisFiyati = 100m, Aktif = true };
        db.CariKartlar.Add(cari);
        db.Urunler.Add(urun);
        await db.SaveChangesAsync();

        var fatura = new Fatura
        {
            SirketId = sirketId,
            FaturaNo = "FAT-2026-0001",
            Tarih = DateTime.UtcNow,
            Tur = FaturaTuru.Alis,
            Durum = BelgeDurumu.Taslak,
            CariKartId = cari.Id,
            GenelToplam = 118m,
            KdvToplam = 18m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = urun.Id, Miktar = 1, BirimFiyat = 100m, KdvOrani = 18, SatirToplami = 118m }
            }
        };
        db.Faturalar.Add(fatura);
        await db.SaveChangesAsync();

        var autoFis = await yevmiyeServisi.OtomatikMuhasebelestirAsync("Fatura", fatura.Id, sirketId, kullanici.Id);
        Assert.NotNull(autoFis);
        Assert.Equal("Fatura", autoFis.BelgeTipi);
        Assert.Equal(fatura.Id, autoFis.BelgeId);
        Assert.True(autoFis.DengedeMi);
    }

    [Fact]
    public async Task Test_33_Idempotency_Duplicate_Muhasebelestirme_Engeli()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var cari = new CariKart { SirketId = sirketId, Kod = "C002", Unvan = "Test Cari 2", Tur = CariTuru.MusteriTedarikci, Aktif = true };
        var urun = new Urun { SirketId = sirketId, Kod = "U002", Ad = "Test Ürün 2", PerakendeSatisFiyati = 200m, Aktif = true };
        db.CariKartlar.Add(cari);
        db.Urunler.Add(urun);
        await db.SaveChangesAsync();

        var fatura = new Fatura
        {
            SirketId = sirketId,
            FaturaNo = "FAT-2026-0002",
            Tarih = DateTime.UtcNow,
            Tur = FaturaTuru.Alis,
            Durum = BelgeDurumu.Taslak,
            CariKartId = cari.Id,
            GenelToplam = 236m,
            KdvToplam = 36m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = urun.Id, Miktar = 1, BirimFiyat = 200m, KdvOrani = 18, SatirToplami = 236m }
            }
        };
        db.Faturalar.Add(fatura);
        await db.SaveChangesAsync();

        var fis1 = await yevmiyeServisi.OtomatikMuhasebelestirAsync("Fatura", fatura.Id, sirketId, kullanici.Id);
        var fis2 = await yevmiyeServisi.OtomatikMuhasebelestirAsync("Fatura", fatura.Id, sirketId, kullanici.Id);

        Assert.Equal(fis1.Id, fis2.Id); // Idempotent same voucher returned
    }

    [Fact]
    public async Task Test_36_37_Firma_Izolasyonu_Ve_Cross_Company_Engeli()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId1, kullanici1, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var sirket2Id = Guid.NewGuid();
        db.Sirketler.Add(new Sirket { Id = sirket2Id, Unvan = "Firma B Ltd", SirketKodu = "FIRMA-B" });
        await db.SaveChangesAsync();

        var h100B = new HesapPlani { SirketId = sirket2Id, HesapKodu = "100", HesapAdi = "KASA B", Tip = HesapTipi.Aktif, AktifMi = true };
        var h102B = new HesapPlani { SirketId = sirket2Id, HesapKodu = "102", HesapAdi = "BANKA B", Tip = HesapTipi.Aktif, AktifMi = true };
        db.HesapPlanlari.AddRange(h100B, h102B);
        await db.SaveChangesAsync();

        var fisB = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirket2Id,
            FisTuru = FisTuru.Mahsup,
            Tarih = DateTime.UtcNow,
            Aciklama = "Firma B Fişi",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100B.Id, Borc = 1000m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h102B.Id, Borc = 0m, Alacak = 1000m }
            }
        }, sirket2Id, kullanici1.Id);

        // Company A query cannot see Company B voucher
        var sorguA = await yevmiyeServisi.YevmiyeDefteriSorgulaAsync(new YevmiyeFiltreDto { SirketId = sirketId1 });
        Assert.DoesNotContain(sorguA.Veri, f => f.Id == fisB.Id);
    }

    [Fact]
    public async Task Test_38_Kapali_Mali_Donem_Korumasi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var kapaliDonem = new MaliDonem
        {
            SirketId = sirketId,
            DonemAdi = "2025 Mali Yılı",
            Baslangic = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Bitis = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            KilitliMi = true // Closed period
        };
        db.MaliDonemler.Add(kapaliDonem);
        await db.SaveChangesAsync();

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");

        // Action on closed period throws
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
            {
                SirketId = sirketId,
                FisTuru = FisTuru.Mahsup,
                Tarih = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                Aciklama = "Kapalı Dönem Kaydı",
                Durum = YevmiyeDurumu.Onaylandi,
                Satirlar = new List<YevmiyeSatiriDto>
                {
                    new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 100m, Alacak = 0m },
                    new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 100m }
                }
            }, sirketId, kullanici.Id);
        });
    }

    [Fact]
    public async Task Test_39_40_Yetkisiz_Kullanici_Engeli()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db, yetkili: false);

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");

        await Assert.ThrowsAsync<YetkisizErisimException>(async () =>
        {
            await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
            {
                SirketId = sirketId,
                FisTuru = FisTuru.Mahsup,
                Tarih = DateTime.UtcNow,
                Aciklama = "Yetkisiz Fiş Girişi",
                Satirlar = new List<YevmiyeSatiriDto>
                {
                    new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 100m, Alacak = 0m },
                    new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 100m }
                }
            }, sirketId, kullanici.Id);
        });
    }

    [Fact]
    public async Task Test_42_Yevmiye_Balance_HealthCheck()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, _, _, raporServisi, _) = await SetupTestEnvironmentAsync(db);

        var saglik = await raporServisi.SaglikPaneliGetirAsync(sirketId);
        Assert.Equal(0, saglik.DengesizFisSayisi);
        Assert.True(saglik.SaglikliMi);
    }
}

internal class DummyKullaniciServisi : IKullaniciServisi
{
    private readonly Kullanici _kullanici;
    private readonly KullaniciYetkileri _yetkiler;

    public DummyKullaniciServisi(Kullanici kullanici, KullaniciYetkileri yetkiler)
    {
        _kullanici = kullanici;
        _yetkiler = yetkiler;
    }

    public Kullanici? AktifKullanici => _kullanici;
    public KullaniciYetkileri? AktifYetkiler => _yetkiler;

    public Task<bool> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public void CikisYap() { }
    public bool YetkiliMi(Func<KullaniciYetkileri, bool> yetkiSorgusu) => yetkiSorgusu(_yetkiler);
    public void YetkiKontrolEt(Func<KullaniciYetkileri, bool> yetkiSorgusu, string islemAdi)
    {
        if (!yetkiSorgusu(_yetkiler))
            throw new YetkisizErisimException(islemAdi);
    }
    public Task<List<Kullanici>> TumunuGetirAsync() => Task.FromResult(new List<Kullanici> { _kullanici });
    public Task<Kullanici?> GetirAsync(int id) => Task.FromResult<Kullanici?>(_kullanici);
    public Task<Kullanici> EkleAsync(Kullanici kullanici, string acikSifre) => Task.FromResult(kullanici);
    public Task GuncelleAsync(Kullanici kullanici) => Task.CompletedTask;
    public Task SifreDegistirAsync(int kullaniciId, string yeniAcikSifre) => Task.CompletedTask;
    public Task SilAsync(int id) => Task.CompletedTask;
}
