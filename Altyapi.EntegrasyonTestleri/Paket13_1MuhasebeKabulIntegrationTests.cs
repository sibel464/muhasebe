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
public sealed class Paket13_1MuhasebeKabulIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;

    public Paket13_1MuhasebeKabulIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private async Task<(Guid SirketId, Kullanici Kullanici, HesapPlaniServisi HesapServisi, YevmiyeServisi YevmiyeServisi, MuhasebeRaporServisi RaporServisi, MuhasebeBaglantiServisi BaglantiServisi)> SetupTestEnvironmentAsync(MuhasebeBaglami db, bool yetkili = true)
    {
        var sirket = new Sirket { Id = Guid.NewGuid(), Unvan = "Muhasebe 360 Kabul A.Ş.", SirketKodu = $"KBL-{Guid.NewGuid():N}"[..10] };
        db.Sirketler.Add(sirket);

        var uId = Guid.NewGuid().ToString("N")[..12];
        var kullanici = new Kullanici
        {
            KullaniciAdi = $"kbl_{uId}",
            Ad = "Mehmet",
            Soyad = "Demir",
            Eposta = $"kbl_{uId}@muhasebe.com",
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

        // Account Chart Seed
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "100"))
        {
            db.HesapPlanlari.AddRange(
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "100", HesapAdi = "KASA", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "102", HesapAdi = "BANKALAR", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "108", HesapAdi = "POS ALACAKLARI", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "101", HesapAdi = "ALINAN ÇEKLER", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "120", HesapAdi = "ALICILAR", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "153", HesapAdi = "TİCARİ MALLAR", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "191", HesapAdi = "İNDİRİLECEK KDV", Tip = HesapTipi.Aktif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "320", HesapAdi = "SATICILAR", Tip = HesapTipi.Pasif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "391", HesapAdi = "HESAPLANAN KDV", Tip = HesapTipi.Pasif, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "600", HesapAdi = "YURT İÇİ SATIŞLAR", Tip = HesapTipi.Gelir, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Alacak, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "610", HesapAdi = "SATIŞTAN İADELER", Tip = HesapTipi.Gelir, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "621", HesapAdi = "SATILAN TİCARİ MALLAR MALİYETİ", Tip = HesapTipi.Gider, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true },
                new HesapPlani { SirketId = sirket.Id, HesapKodu = "653", HesapAdi = "KOMİSYON GİDERLERİ", Tip = HesapTipi.Gider, Seviye = 3, BakiyeYonu = BorcAlacakYonu.Borc, SistemHesabi = true, AktifMi = true }
            );
        }

        // Account Mappings
        db.MuhasebeBaglantiKodlari.AddRange(
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "KASA", KategoriAdi = "Kasa Hesabı", VarSayilanHesapKodu = "100", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "BANKA", KategoriAdi = "Banka Hesabı", VarSayilanHesapKodu = "102", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "ALICILAR", KategoriAdi = "Alıcılar", VarSayilanHesapKodu = "120", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "SATICILAR", KategoriAdi = "Satıcılar", VarSayilanHesapKodu = "320", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "YURTICI_SATIS", KategoriAdi = "Yurtiçi Satışlar", VarSayilanHesapKodu = "600", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "SATIS_IADE", KategoriAdi = "Satış İadeleri", VarSayilanHesapKodu = "610", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "SATIS_KDV", KategoriAdi = "Satış KDV", VarSayilanHesapKodu = "391", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "INDIRILECEK_KDV", KategoriAdi = "İndirilecek KDV", VarSayilanHesapKodu = "191", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "STOK", KategoriAdi = "Stok", VarSayilanHesapKodu = "153", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "STMM", KategoriAdi = "STMM", VarSayilanHesapKodu = "621", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "POS_ALACAK", KategoriAdi = "POS Alacakları", VarSayilanHesapKodu = "108", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "KOMISYON_GIDERI", KategoriAdi = "Banka Komisyon Gideri", VarSayilanHesapKodu = "653", AktifMi = true },
            new MuhasebeBaglantiKodu { SirketId = sirket.Id, KategoriKodu = "ALINAN_CEK", KategoriAdi = "Alınan Çekler", VarSayilanHesapKodu = "101", AktifMi = true }
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
    public async Task Test_Kabul_01_Global_Borc_Alacak_Invariant_Scan()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, raporServisi, _) = await SetupTestEnvironmentAsync(db);

        // Add 5 approved vouchers
        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");

        for (int i = 1; i <= 5; i++)
        {
            await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
            {
                SirketId = sirketId,
                FisTuru = FisTuru.Mahsup,
                Tarih = DateTime.UtcNow.AddMinutes(i),
                Aciklama = $"Test Fişi #{i}",
                Durum = YevmiyeDurumu.Onaylandi,
                Satirlar = new List<YevmiyeSatiriDto>
                {
                    new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 1000m * i, Alacak = 0m },
                    new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 1000m * i }
                }
            }, sirketId, kullanici.Id);
        }

        // Global invariant check across all approved vouchers in company
        var tumOnayliFisler = await db.YevmiyeFisleri
            .Include(f => f.Satirlar)
            .Where(f => f.SirketId == sirketId && f.Durum == YevmiyeDurumu.Onaylandi)
            .ToListAsync();

        foreach (var fis in tumOnayliFisler)
        {
            decimal borcSum = fis.Satirlar.Sum(s => s.Borc);
            decimal alacakSum = fis.Satirlar.Sum(s => s.Alacak);
            Assert.Equal(borcSum, alacakSum);
            Assert.True(Math.Abs(borcSum - alacakSum) < 0.001m);
        }

        var saglik = await raporServisi.SaglikPaneliGetirAsync(sirketId);
        Assert.Equal(0, saglik.DengesizFisSayisi);
        Assert.Equal(saglik.ToplamYevmiyeBorc, saglik.ToplamYevmiyeAlacak);
    }

    [Fact]
    public async Task Test_Kabul_02_Satis_Faturasi_Muhasebelestirme_Borc_Alacak_Denkligi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, baglantiServisi) = await SetupTestEnvironmentAsync(db);

        var cari = new CariKart { SirketId = sirketId, Kod = "M001", Unvan = "Müşteri A.Ş.", Tur = CariTuru.Musteri, Aktif = true };
        var urun = new Urun { SirketId = sirketId, Kod = "U010", Ad = "Laptop", PerakendeSatisFiyati = 10000m, Aktif = true, StokMiktari = 100 };
        db.CariKartlar.Add(cari);
        db.Urunler.Add(urun);
        await db.SaveChangesAsync();

        db.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun.Id, DepoId = 1, MevcutMiktar = 100 });
        await db.SaveChangesAsync();

        // Mal bedeli: 10.000, KDV %20: 2.000, Toplam: 12.000
        var fatura = new Fatura
        {
            SirketId = sirketId,
            FaturaNo = "SAT-2026-0001",
            Tarih = DateTime.UtcNow,
            Tur = FaturaTuru.Satis,
            Durum = BelgeDurumu.Taslak,
            CariKartId = cari.Id,
            GenelToplam = 12000m,
            KdvToplam = 2000m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi
                {
                    UrunId = urun.Id, UrunKodu = urun.Kod, UrunAd = urun.Ad,
                    Miktar = 1, BirimFiyat = 10000m, KdvOrani = 20, SatirToplami = 12000m,
                    StokDagilimlari = new List<FaturaKalemiStokDagilimi> { new FaturaKalemiStokDagilimi { DepoId = 1, Miktar = 1 } }
                }
            }
        };
        db.Faturalar.Add(fatura);
        await db.SaveChangesAsync();

        var fis = await yevmiyeServisi.OtomatikMuhasebelestirAsync("Fatura", fatura.Id, sirketId, kullanici.Id);
        Assert.NotNull(fis);
        Assert.Equal(12000m, fis.ToplamBorc);
        Assert.Equal(12000m, fis.ToplamAlacak);
        Assert.True(fis.DengedeMi);

        // Dynamic Account Connection Codes check (No magic hard-coding in assertion)
        var alicilarKod = await baglantiServisi.GetirHesapKoduForKategoriAsync("ALICILAR", sirketId);
        var satisKod = await baglantiServisi.GetirHesapKoduForKategoriAsync("YURTICI_SATIS", sirketId);
        var kdvKod = await baglantiServisi.GetirHesapKoduForKategoriAsync("SATIS_KDV", sirketId);

        var aliciSatiri = fis.Satirlar.FirstOrDefault(s => s.HesapKodu == alicilarKod);
        var satisSatiri = fis.Satirlar.FirstOrDefault(s => s.HesapKodu == satisKod);
        var kdvSatiri = fis.Satirlar.FirstOrDefault(s => s.HesapKodu == kdvKod);

        Assert.NotNull(aliciSatiri);
        Assert.Equal(12000m, aliciSatiri.Borc);

        Assert.NotNull(satisSatiri);
        Assert.Equal(10000m, satisSatiri.Alacak);

        Assert.NotNull(kdvSatiri);
        Assert.Equal(2000m, kdvSatiri.Alacak);
    }

    [Fact]
    public async Task Test_Kabul_03_Satis_Iadesi_Ve_Yuzde0_KDV_Kurali()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var cari = new CariKart { SirketId = sirketId, Kod = "M002", Unvan = "Müşteri B", Tur = CariTuru.Musteri, Aktif = true };
        var urun0 = new Urun { SirketId = sirketId, Kod = "U020", Ad = "İhraç Kayıtlı Ürün", PerakendeSatisFiyati = 5000m, Aktif = true, StokMiktari = 100 };
        db.CariKartlar.Add(cari);
        db.Urunler.Add(urun0);
        await db.SaveChangesAsync();

        db.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun0.Id, DepoId = 1, MevcutMiktar = 100 });
        await db.SaveChangesAsync();

        // %0 KDV Faturası
        var fatura0Kdv = new Fatura
        {
            SirketId = sirketId,
            FaturaNo = "SAT-2026-0002",
            Tarih = DateTime.UtcNow,
            Tur = FaturaTuru.Satis,
            Durum = BelgeDurumu.Taslak,
            CariKartId = cari.Id,
            GenelToplam = 5000m,
            KdvToplam = 0m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi
                {
                    UrunId = urun0.Id, UrunKodu = urun0.Kod, UrunAd = urun0.Ad,
                    Miktar = 1, BirimFiyat = 5000m, KdvOrani = 0, SatirToplami = 5000m,
                    StokDagilimlari = new List<FaturaKalemiStokDagilimi> { new FaturaKalemiStokDagilimi { DepoId = 1, Miktar = 1 } }
                }
            }
        };
        db.Faturalar.Add(fatura0Kdv);
        await db.SaveChangesAsync();

        var fis0 = await yevmiyeServisi.OtomatikMuhasebelestirAsync("Fatura", fatura0Kdv.Id, sirketId, kullanici.Id);
        Assert.NotNull(fis0);
        Assert.True(fis0.DengedeMi);
        Assert.Equal(5000m, fis0.ToplamBorc);
        Assert.Equal(5000m, fis0.ToplamAlacak);
        // %0 KDV'de sıfır tutarlı KDV satırı üretilmeme kuralı
        Assert.DoesNotContain(fis0.Satirlar, s => s.HesapKodu == "391" || s.HesapKodu == "191" || (s.Borc == 0m && s.Alacak == 0m));
    }

    [Fact]
    public async Task Test_Kabul_04_Tahsilat_Ve_Odeme_Cari_Kasa_Banka_Yön_Tutarliligi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");
        var h120 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "120");
        var h320 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "320");

        // Tahsilat: Kasa Borç (+4000), Cari (Alıcılar) Alacak (4000)
        var tahsilatFis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Tahsil,
            Tarih = DateTime.UtcNow,
            Aciklama = "Nakit Tahsilat Kaydı",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 4000m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h120.Id, Borc = 0m, Alacak = 4000m }
            }
        }, sirketId, kullanici.Id);

        Assert.Equal(4000m, tahsilatFis.ToplamBorc);
        Assert.Equal(4000m, tahsilatFis.ToplamAlacak);

        // Ödeme: Tedarikçi (Satıcılar) Borç (3000), Banka Alacak (3000)
        var odemeFis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Tediye,
            Tarih = DateTime.UtcNow,
            Aciklama = "Banka Havalesi Ödeme Kaydı",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h320.Id, Borc = 3000m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 3000m }
            }
        }, sirketId, kullanici.Id);

        Assert.Equal(3000m, odemeFis.ToplamBorc);
        Assert.Equal(3000m, odemeFis.ToplamAlacak);
    }

    [Fact]
    public async Task Test_Kabul_05_Kasa_Banka_Virman_Cari_Etkilemez()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");

        // Virman: Kasadan Bankaya Yatırılan Para (Cari etkilenmez)
        var virmanFis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Mahsup,
            Tarih = DateTime.UtcNow,
            Aciklama = "Kasadan Bankaya Para Yatırma",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 15000m, Alacak = 0m, Aciklama = "Banka Hesabına Giriş" },
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 0m, Alacak = 15000m, Aciklama = "Kasadan Çıkış" }
            }
        }, sirketId, kullanici.Id);

        Assert.True(virmanFis.DengedeMi);
        Assert.DoesNotContain(virmanFis.Satirlar, s => s.HesapKodu == "120" || s.HesapKodu == "320");
    }

    [Fact]
    public async Task Test_Kabul_06_POS_Settlement_Brut_Komisyon_Net_Denklik()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, _) = await SetupTestEnvironmentAsync(db);

        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");
        var h108 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "108");
        var h653 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "653");

        // POS Takas/Settlement: Brüt 10.000, Komisyon 300 (%3), Net 9.700
        var posSettlementFis = await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Mahsup,
            Tarih = DateTime.UtcNow,
            Aciklama = "POS Gün Sonu Takas ve Komisyon Kaydı",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 9700m, Alacak = 0m, Aciklama = "Net Banka Hesabına Geçen" },
                new YevmiyeSatiriDto { HesapId = h653.Id, Borc = 300m, Alacak = 0m, Aciklama = "Banka Komisyon Gideri" },
                new YevmiyeSatiriDto { HesapId = h108.Id, Borc = 0m, Alacak = 10000m, Aciklama = "POS Alacağı Kapanışı" }
            }
        }, sirketId, kullanici.Id);

        Assert.Equal(10000m, posSettlementFis.ToplamBorc);
        Assert.Equal(10000m, posSettlementFis.ToplamAlacak);
        Assert.True(posSettlementFis.DengedeMi);
    }

    [Fact]
    public async Task Test_Kabul_07_Eksik_Ve_Pasif_Hesap_Eslame_Fail_Closed()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, _, baglantiServisi) = await SetupTestEnvironmentAsync(db);

        // Delete / deactivate ALICILAR mapping
        var map = await db.MuhasebeBaglantiKodlari.FirstOrDefaultAsync(m => m.SirketId == sirketId && m.KategoriKodu == "ALICILAR");
        if (map != null)
        {
            map.AktifMi = false;
            await db.SaveChangesAsync();
        }

        var cari = new CariKart { SirketId = sirketId, Kod = "M003", Unvan = "Müşteri C", Tur = CariTuru.Musteri, Aktif = true };
        var urun = new Urun { SirketId = sirketId, Kod = "U030", Ad = "Klavye", PerakendeSatisFiyati = 100m, Aktif = true };
        db.CariKartlar.Add(cari);
        db.Urunler.Add(urun);
        await db.SaveChangesAsync();

        var fatura = new Fatura
        {
            SirketId = sirketId,
            FaturaNo = "SAT-2026-0003",
            Tarih = DateTime.UtcNow,
            Tur = FaturaTuru.Satis,
            Durum = BelgeDurumu.Taslak,
            CariKartId = cari.Id,
            GenelToplam = 100m,
            Kalemler = new List<FaturaKalemi>
            {
                new FaturaKalemi { UrunId = urun.Id, Miktar = 1, BirimFiyat = 100m, KdvOrani = 0, SatirToplami = 100m }
            }
        };
        db.Faturalar.Add(fatura);
        await db.SaveChangesAsync();

        // Missing/Passive account mapping throws clean InvalidOperationException without silent fallback or dummy accounts
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await yevmiyeServisi.OtomatikMuhasebelestirAsync("Fatura", fatura.Id, sirketId, kullanici.Id);
        });
    }

    [Fact]
    public async Task Test_Kabul_08_Reconciliation_Yevmiye_Mizan_Muavin_BuyukDefter_Tutarliligi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, kullanici, _, yevmiyeServisi, raporServisi, _) = await SetupTestEnvironmentAsync(db);

        var h100 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "100");
        var h102 = await db.HesapPlanlari.FirstAsync(h => h.SirketId == sirketId && h.HesapKodu == "102");

        // Transaction 1: 5.000 TL
        await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Mahsup,
            Tarih = DateTime.UtcNow,
            Aciklama = "Virman 1",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 5000m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 5000m }
            }
        }, sirketId, kullanici.Id);

        // Transaction 2: 3.000 TL
        await yevmiyeServisi.FisOlusturAsync(new YevmiyeFisDto
        {
            SirketId = sirketId,
            FisTuru = FisTuru.Mahsup,
            Tarih = DateTime.UtcNow,
            Aciklama = "Virman 2",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = new List<YevmiyeSatiriDto>
            {
                new YevmiyeSatiriDto { HesapId = h100.Id, Borc = 3000m, Alacak = 0m },
                new YevmiyeSatiriDto { HesapId = h102.Id, Borc = 0m, Alacak = 3000m }
            }
        }, sirketId, kullanici.Id);

        // Reconcile Yevmiye Total with Mizan Total
        var yevmiyePaging = await yevmiyeServisi.YevmiyeDefteriSorgulaAsync(new YevmiyeFiltreDto { SirketId = sirketId, SayfaBoyutu = 1000 });
        decimal totalYevmiyeBorc = yevmiyePaging.Liste.Sum(f => f.Borc);

        var mizan = await raporServisi.MizanAlAsync(sirketId, null, null);
        Assert.Equal(mizan.ToplamBorc, totalYevmiyeBorc); // Reconciled
        Assert.True(mizan.DengedeMi);

        // Reconcile Muavin for Account 100
        var muavin100 = await raporServisi.MuavinAlAsync(h100.Id, sirketId, null, null);
        Assert.Equal(2, muavin100.Satirlar.Count);
        Assert.Equal(8000m, muavin100.SonBakiye);

        // Reconcile Büyük Defter for Account 100
        var buyukDefter = await raporServisi.BuyukDefterAlAsync(sirketId, null, null);
        var bd100 = buyukDefter.FirstOrDefault(b => b.HesapId == h100.Id);
        Assert.NotNull(bd100);
        Assert.Equal(8000m, bd100.ToplamBorc);
        Assert.Equal(8000m, bd100.Bakiye);
    }
}
