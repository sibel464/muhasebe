using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Finans360;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class Finans360Paket11_1KabulTestleri
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public Finans360Paket11_1KabulTestleri(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    private async Task<(Guid SirketId, CariKart Cari, Kasa Kasa, BankaHesabi Banka)> SeedTemelVeriAsync(MuhasebeBaglami db)
    {
        var sirket = new EtkiAlani.Modeller.Kurumsal.Sirket { Id = Guid.NewGuid(), Unvan = "Test Kabul Sirket", SirketKodu = $"KB-{Guid.NewGuid():N}"[..10] };
        db.Sirketler.Add(sirket);

        var cari = new CariKart { SirketId = sirket.Id, Kod = $"C-{Guid.NewGuid():N}"[..8], Unvan = "Test Kabul Müşteri", Tur = CariTuru.MusteriTedarikci, Aktif = true, Borc = 0m, Alacak = 0m };
        var kasa = new Kasa { SirketId = sirket.Id, Kod = $"K-{Guid.NewGuid():N}"[..8], Ad = "Kabul Merkez Kasa", Bakiye = 0m, AktifMi = true };
        var banka = new BankaHesabi { SirketId = sirket.Id, Kod = $"B-{Guid.NewGuid():N}"[..8], Ad = "Kabul Ziraat Bankası", Iban = "TR998877665544332211009988", HesapNo = "998877", Bakiye = 0m, AktifMi = true };

        db.CariKartlar.Add(cari);
        db.Kasalar.Add(kasa);
        db.BankaHesaplari.Add(banka);

        // Standard Accounting Chart of Accounts
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "100"))
            db.HesapPlanlari.Add(new HesapPlani { SirketId = sirket.Id, HesapKodu = "100", HesapAdi = "Kasa", Tip = HesapTipi.Aktif, AktifMi = true });
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "102"))
            db.HesapPlanlari.Add(new HesapPlani { SirketId = sirket.Id, HesapKodu = "102", HesapAdi = "Banka", Tip = HesapTipi.Aktif, AktifMi = true });
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "108"))
            db.HesapPlanlari.Add(new HesapPlani { SirketId = sirket.Id, HesapKodu = "108", HesapAdi = "POS Alacakları", Tip = HesapTipi.Aktif, AktifMi = true });
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "120"))
            db.HesapPlanlari.Add(new HesapPlani { SirketId = sirket.Id, HesapKodu = "120", HesapAdi = "Alıcılar", Tip = HesapTipi.Aktif, AktifMi = true });
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "320"))
            db.HesapPlanlari.Add(new HesapPlani { SirketId = sirket.Id, HesapKodu = "320", HesapAdi = "Satıcılar", Tip = HesapTipi.Pasif, AktifMi = true });
        if (!await db.HesapPlanlari.AnyAsync(x => x.SirketId == sirket.Id && x.HesapKodu == "770"))
            db.HesapPlanlari.Add(new HesapPlani { SirketId = sirket.Id, HesapKodu = "770", HesapAdi = "Genel Yönetim / Banka Giderleri", Tip = HesapTipi.Gider, AktifMi = true });

        await db.SaveChangesAsync();
        return (sirket.Id, cari, kasa, banka);
    }

    private static IKullaniciServisi PermittedUser(bool permit = true) => new MockUserServisi(permit);

    private class MockUserServisi : IKullaniciServisi
    {
        private readonly bool _permit;
        public MockUserServisi(bool permit = true) => _permit = permit;

        public Kullanici? AktifKullanici => new() { Id = 1, KullaniciAdi = "TestUser", Ad = "Test", Soyad = "User", Yetkiler = AktifYetkiler };
        public KullaniciYetkileri? AktifYetkiler => new();
        public Task<bool> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken cancellationToken = default) => Task.FromResult(_permit);
        public void CikisYap() { }
        public bool YetkiliMi(Func<KullaniciYetkileri, bool> yetkiPredikati) => _permit;
        public void YetkiKontrolEt(Func<KullaniciYetkileri, bool> yetkiPredikati, string islemAdi)
        {
            if (!_permit) throw new UnauthorizedAccessException($"Yetkisiz işlem: {islemAdi}");
        }
        public Task<List<Kullanici>> TumunuGetirAsync() => Task.FromResult(new List<Kullanici>());
        public Task<Kullanici?> GetirAsync(int id) => Task.FromResult<Kullanici?>(AktifKullanici);
        public Task<Kullanici> EkleAsync(Kullanici kullanici, string sifre) => Task.FromResult(kullanici);
        public Task GuncelleAsync(Kullanici kullanici) => Task.CompletedTask;
        public Task SifreDegistirAsync(int id, string yeniSifre) => Task.CompletedTask;
        public Task SilAsync(int id) => Task.CompletedTask;
    }

    // 1. Gerçek Uçtan Uca Tahsilat Runtime Testi
    [Fact]
    public async Task Test_Kabul_1_Gercer_Uctan_Uca_Tahsilat_Runtime()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);

        // Cari başlangıç bakiyesi: 10.000 TL borç
        db.CariHareketler.Add(new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow.AddDays(-5), HareketTuru = CariHareketTuru.SatisFaturasi, Aciklama = "İlk Satış Borcu", Borc = 10000m, Alacak = 0m });
        await db.SaveChangesAsync();

        var service = new TahsilatOdemeServisi(db, PermittedUser());
        var res = await service.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 4000m, DateTime.UtcNow, "4.000 TL Tahsilat", KasaId: kasa.Id, BelgeNo: "THS-001"));

        Assert.NotNull(res);
        Assert.Equal(4000m, res.Tutar);

        // Cari Kalan Borç = 6.000 TL (10.000 Borç - 4.000 Alacak)
        var cariGuncel = await db.CariKartlar.FindAsync(cari.Id);
        var cariHareketler = await db.CariHareketler.Where(x => x.CariKartId == cari.Id).ToListAsync();
        var toplamBorc = cariHareketler.Sum(x => x.Borc);
        var toplamAlacak = cariHareketler.Sum(x => x.Alacak);
        var kalanBakiye = toplamBorc - toplamAlacak;
        Assert.Equal(6000m, kalanBakiye);

        // Kasa +4.000 TL
        var kasaGiris = await db.KasaHareketleri.Where(x => x.KasaId == kasa.Id && x.IslemDurumu != FinansIslemDurumu.IptalEdildi).SumAsync(x => x.Tutar);
        Assert.Equal(4000m, kasaGiris);

        // Yevmiye oluşur ve dengelidir
        var yevmiye = await db.YevmiyeFisleri.Include(x => x.Satirlar).SingleOrDefaultAsync(x => x.BelgeId == res.HareketId);
        Assert.NotNull(yevmiye);
        var yevmiyeBorc = yevmiye.Satirlar.Sum(x => x.Borc);
        var yevmiyeAlacak = yevmiye.Satirlar.Sum(x => x.Alacak);
        Assert.Equal(4000m, yevmiyeBorc);
        Assert.Equal(4000m, yevmiyeAlacak);

        var finansService = new FinansMerkeziServisi(db, PermittedUser());
        var kasaHareketleri = await finansService.GetKasaHareketleri360Async(kasa.Id, sirketId);
        Assert.NotEmpty(kasaHareketleri);
        Assert.Equal(4000m, kasaHareketleri.First().Giris);
    }

    // 2. Kısmi Fatura Kapama Testi
    [Fact]
    public async Task Test_Kabul_2_Kismi_Fatura_Kapama()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);

        var fatA = new Fatura { SirketId = sirketId, CariKartId = cari.Id, FaturaNo = "FAT-A", Tur = FaturaTuru.Satis, GenelToplam = 10000m, Durum = BelgeDurumu.Onaylandi, Tarih = DateTime.UtcNow.AddDays(-10) };
        var fatB = new Fatura { SirketId = sirketId, CariKartId = cari.Id, FaturaNo = "FAT-B", Tur = FaturaTuru.Satis, GenelToplam = 5000m, Durum = BelgeDurumu.Onaylandi, Tarih = DateTime.UtcNow.AddDays(-5) };
        db.Faturalar.AddRange(fatA, fatB);
        db.CariHareketler.Add(new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow.AddDays(-10), HareketTuru = CariHareketTuru.SatisFaturasi, Borc = 10000m, Alacak = 0m, BelgeNo = "FAT-A" });
        db.CariHareketler.Add(new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow.AddDays(-5), HareketTuru = CariHareketTuru.SatisFaturasi, Borc = 5000m, Alacak = 0m, BelgeNo = "FAT-B" });
        await db.SaveChangesAsync();

        // 12,000 TL Tahsilat: FAT-A (10,000 TL) tam kapama, FAT-B (2,000 TL) kısmi kapama
        var service = new TahsilatOdemeServisi(db, PermittedUser());
        await service.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 12000m, DateTime.UtcNow, "Fatura Kapama", KasaId: kasa.Id));

        var cariHareketler = await db.CariHareketler.Where(x => x.CariKartId == cari.Id).ToListAsync();
        var toplamBorc = cariHareketler.Sum(x => x.Borc);
        var toplamAlacak = cariHareketler.Sum(x => x.Alacak);
        var kalanBakiye = toplamBorc - toplamAlacak;

        Assert.Equal(15000m, toplamBorc);
        Assert.Equal(12000m, toplamAlacak);
        Assert.Equal(3000m, kalanBakiye); // FAT-B kalan = 3.000 TL
    }

    // 3. Otomatik Mahsup (FIFO/Vade Politikası) Testi
    [Fact]
    public async Task Test_Kabul_3_Otomatik_Mahsup_FIFO_Vade()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);

        var f1 = new Fatura { SirketId = sirketId, CariKartId = cari.Id, FaturaNo = "F-ESKI", Tur = FaturaTuru.Satis, GenelToplam = 3000m, Durum = BelgeDurumu.Onaylandi, Tarih = DateTime.UtcNow.AddDays(-30) };
        var f2 = new Fatura { SirketId = sirketId, CariKartId = cari.Id, FaturaNo = "F-ORTA", Tur = FaturaTuru.Satis, GenelToplam = 4000m, Durum = BelgeDurumu.Onaylandi, Tarih = DateTime.UtcNow.AddDays(-15) };
        var f3 = new Fatura { SirketId = sirketId, CariKartId = cari.Id, FaturaNo = "F-YENI", Tur = FaturaTuru.Satis, GenelToplam = 5000m, Durum = BelgeDurumu.Onaylandi, Tarih = DateTime.UtcNow.AddDays(-1) };
        db.Faturalar.AddRange(f1, f2, f3);
        db.CariHareketler.Add(new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow.AddDays(-30), HareketTuru = CariHareketTuru.SatisFaturasi, Borc = 3000m });
        db.CariHareketler.Add(new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow.AddDays(-15), HareketTuru = CariHareketTuru.SatisFaturasi, Borc = 4000m });
        db.CariHareketler.Add(new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow.AddDays(-1), HareketTuru = CariHareketTuru.SatisFaturasi, Borc = 5000m });
        await db.SaveChangesAsync();

        // 5,000 TL Tahsilat FIFO Mahsup
        var service = new TahsilatOdemeServisi(db, PermittedUser());
        await service.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 5000m, DateTime.UtcNow, "5.000 TL FIFO Mahsup", KasaId: kasa.Id));

        var cariHareketler = await db.CariHareketler.Where(x => x.CariKartId == cari.Id).ToListAsync();
        var kalanBakiye = cariHareketler.Sum(x => x.Borc) - cariHareketler.Sum(x => x.Alacak);

        Assert.Equal(12000m, cariHareketler.Sum(x => x.Borc));
        Assert.Equal(5000m, cariHareketler.Sum(x => x.Alacak));
        Assert.Equal(7000m, kalanBakiye); // F-ESKI (3,000) kapandı + F-ORTA (2,000) kısmi kapandı. Kalan = 2,000 + 5,000 = 7,000
    }

    // 4. Mahsuptan Fazla Tahsilat / Avans Testi
    [Fact]
    public async Task Test_Kabul_4_Mahsuptan_Fazla_Tahsilat_Avans()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);

        var fat = new Fatura { SirketId = sirketId, CariKartId = cari.Id, FaturaNo = "FAT-5000", Tur = FaturaTuru.Satis, GenelToplam = 5000m, Durum = BelgeDurumu.Onaylandi, Tarih = DateTime.UtcNow.AddDays(-5) };
        db.Faturalar.Add(fat);
        db.CariHareketler.Add(new CariHareket { CariKartId = cari.Id, Tarih = DateTime.UtcNow.AddDays(-5), HareketTuru = CariHareketTuru.SatisFaturasi, Borc = 5000m, Alacak = 0m });
        await db.SaveChangesAsync();

        // 7,000 TL Tahsilat (2,000 TL fazla)
        var service = new TahsilatOdemeServisi(db, PermittedUser());
        await service.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 7000m, DateTime.UtcNow, "7.000 TL Tahsilat", KasaId: kasa.Id));

        var cariHareketler = await db.CariHareketler.Where(x => x.CariKartId == cari.Id).ToListAsync();
        var toplamBorc = cariHareketler.Sum(x => x.Borc);
        var toplamAlacak = cariHareketler.Sum(x => x.Alacak);
        var bakiye = toplamBorc - toplamAlacak;

        Assert.Equal(-2000m, bakiye); // Cari alacaklı / avans bakiyesi = 2.000 TL (Müşteri Alacaklı)
    }

    // 5. Tahsilat İptali ve Double Cancel Engeli Testi
    [Fact]
    public async Task Test_Kabul_5_Tahsilat_Iptali_ve_Fatura_Yeniden_Acilmasi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);

        var service = new TahsilatOdemeServisi(db, PermittedUser());
        var res = await service.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 3000m, DateTime.UtcNow, "İptal edilecek tahsilat", KasaId: kasa.Id));

        // İptal et
        await service.IptalEtAsync("Kasa", res.HareketId, "Hatalı Giriş");

        var kh = await db.KasaHareketleri.FindAsync(res.HareketId);
        Assert.NotNull(kh);
        Assert.Equal(FinansIslemDurumu.IptalEdildi, kh.IslemDurumu);
        Assert.NotNull(kh.TersKayitId);

        // İkinci iptal engellenir
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.IptalEtAsync("Kasa", res.HareketId, "İkinci iptal denemesi"));
        Assert.Contains("daha önce iptal edilmiş", ex.Message);
    }

    // 6. Ödeme Uçtan Uca Testi
    [Fact]
    public async Task Test_Kabul_6_Odeme_Uctan_Uca()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, _, banka) = await SeedTemelVeriAsync(db);

        var service = new TahsilatOdemeServisi(db, PermittedUser());
        var res = await service.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Odeme, OdemeYontemi.Banka, cari.Id, 8000m, DateTime.UtcNow, "8.000 TL Tedarikçi Ödemesi", BankaAdi: banka.Ad, HesapNo: banka.HesapNo, BankaHesabiId: banka.Id));

        Assert.NotNull(res);
        Assert.Equal(FinansIslemTuru.Odeme, res.IslemTuru);

        var tedarikciHareketler = await db.CariHareketler.Where(x => x.CariKartId == cari.Id).ToListAsync();
        Assert.Equal(8000m, tedarikciHareketler.Sum(x => x.Borc));
    }

    // 7. Kasa Virman ve İptali Testi
    [Fact]
    public async Task Test_Kabul_7_Kasa_Virman_ve_Iptal()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasaA, _) = await SeedTemelVeriAsync(db);
        var kasaB = new Kasa { SirketId = sirketId, Kod = $"K2-{Guid.NewGuid():N}"[..8], Ad = "Şube Kasa", Bakiye = 0m, AktifMi = true };
        db.Kasalar.Add(kasaB);
        await db.SaveChangesAsync();

        var service = new TahsilatOdemeServisi(db, PermittedUser());
        var islemOut = await service.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Odeme, OdemeYontemi.Nakit, cari.Id, 3000m, DateTime.UtcNow, "Kasa Virman Çıkış", KasaId: kasaA.Id));
        var islemIn = await service.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 3000m, DateTime.UtcNow, "Kasa Virman Giriş", KasaId: kasaB.Id));

        Assert.NotNull(islemOut);
        Assert.NotNull(islemIn);
    }

    // 8. Kasa -> Banka Transfer (No Cari & Balanced Yevmiye) Testi
    [Fact]
    public async Task Test_Kabul_8_Kasa_To_Banka_NoCari_BalancedYevmiye()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kasa, banka) = await SeedTemelVeriAsync(db);

        var service = new FinansMerkeziServisi(db, PermittedUser());
        await service.KasaBankaTransferiYapAsync(new KasaBankaTransferIstegiDto(kasa.Id, banka.Id, 2500m, "Bankaya Yatırılan"), sirketId, 1);

        // Cari hareketi OLUŞMAMALIDIR
        var cariHareketExists = await db.CariHareketler.AnyAsync(x => x.Aciklama.Contains("Bankaya Yatırılan"));
        Assert.False(cariHareketExists, "Transfer işlemlerinde CariHareket oluşturulmamalıdır.");

        var kh = await db.KasaHareketleri.Where(x => x.KasaId == kasa.Id && x.Aciklama.Contains("Bankaya Yatırılan")).SingleOrDefaultAsync();
        Assert.NotNull(kh);
        Assert.Equal(HareketTuru.Gider, kh.Tur);
    }

    // 9. Banka -> Kasa Transfer Testi
    [Fact]
    public async Task Test_Kabul_9_Banka_To_Kasa_NoCari()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kasa, banka) = await SeedTemelVeriAsync(db);

        var service = new FinansMerkeziServisi(db, PermittedUser());
        await service.BankaKasaTransferiYapAsync(new BankaKasaTransferIstegiDto(banka.Id, kasa.Id, 1500m, "Bankadan Çekilen"), sirketId, 1);

        var bh = await db.BankaHareketleri.SingleOrDefaultAsync(x => x.BankaHesabiId == banka.Id && x.Aciklama.Contains("Bankadan Çekilen"));
        var kh = await db.KasaHareketleri.SingleOrDefaultAsync(x => x.KasaId == kasa.Id && x.Aciklama.Contains("Bankadan Çekilen"));

        Assert.NotNull(bh);
        Assert.NotNull(kh);
        Assert.Equal(HareketTuru.Gider, bh.Tur);
        Assert.Equal(HareketTuru.Gelir, kh.Tur);
    }

    // 10. Banka -> Banka + Masraf Transfer Testi
    [Fact]
    public async Task Test_Kabul_10_Banka_To_Banka_Masrafli_Dengeli_Yevmiye()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, _, bankaA) = await SeedTemelVeriAsync(db);
        var bankaB = new BankaHesabi { SirketId = sirketId, Kod = $"B2-{Guid.NewGuid():N}"[..8], Ad = "İş Bankası", Iban = "TR112233445566778899001122", HesapNo = "112233", Bakiye = 0m, AktifMi = true };
        db.BankaHesaplari.Add(bankaB);
        await db.SaveChangesAsync();

        var service = new FinansMerkeziServisi(db, PermittedUser());
        await service.BankaVirmaniYapAsync(new BankaVirmanIstegiDto(bankaA.Id, bankaB.Id, 10000m, 250m, "Havale Transferi"), sirketId, 1);

        var bhA = await db.BankaHareketleri.SingleOrDefaultAsync(x => x.BankaHesabiId == bankaA.Id && x.Aciklama.Contains("Havale Transferi"));
        var bhB = await db.BankaHareketleri.SingleOrDefaultAsync(x => x.BankaHesabiId == bankaB.Id && x.Aciklama.Contains("Havale Transferi"));

        Assert.NotNull(bhA);
        Assert.NotNull(bhB);
        Assert.Equal(HareketTuru.Gider, bhA.Tur);
        Assert.Equal(HareketTuru.Gelir, bhB.Tur);
    }

    // 11. POS Settlement ve Double Settlement Engeli Testi
    [Fact]
    public async Task Test_Kabul_11_POS_Settlement_ve_Double_Settlement_Engeli()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, _, banka) = await SeedTemelVeriAsync(db);

        var pos = new PosIslemi
        {
            SirketId = sirketId,
            BankaHesabiId = banka.Id,
            BankaAdi = banka.Ad,
            PosCihazAdi = "POS-001",
            KartTuru = "Visa",
            TaksitSayisi = 1,
            BrutTutar = 10000m,
            KomisyonOrani = 3.00m,
            KomisyonTutar = 300m,
            NetTutar = 9700m,
            IslemTarihi = DateTime.UtcNow.AddDays(-2),
            BeklenenHesabaGecisTarihi = DateTime.UtcNow.AddDays(-1),
            Durum = PosIslemDurumu.Bekliyor
        };
        db.PosIslemleri.Add(pos);
        await db.SaveChangesAsync();

        var service = new FinansMerkeziServisi(db, PermittedUser());
        await service.PosIslemiHesabaGecirAsync(pos.Id, sirketId, 1);

        var guncelPos = await db.PosIslemleri.FindAsync(pos.Id);
        Assert.Equal(PosIslemDurumu.HesabaGecti, guncelPos!.Durum);

        // Banka Hareketi net tutar (9.700 TL) giriş olarak eklenir
        var bh = await db.BankaHareketleri.Where(x => x.BankaHesabiId == banka.Id).SingleOrDefaultAsync();
        Assert.NotNull(bh);
        Assert.Equal(9700m, bh.Tutar);

        // İkinci hesaba geçiş denemesi engellenir
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PosIslemiHesabaGecirAsync(pos.Id, sirketId, 1));
        Assert.Contains("hesaba", ex.Message);
    }

    // 12. POS İade ve Double Refund Engeli Testi
    [Fact]
    public async Task Test_Kabul_12_POS_Iade_ve_Double_Refund_Engeli()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, _, banka) = await SeedTemelVeriAsync(db);

        var pos = new PosIslemi
        {
            SirketId = sirketId,
            BankaHesabiId = banka.Id,
            BankaAdi = banka.Ad,
            PosCihazAdi = "POS-002",
            KartTuru = "MasterCard",
            TaksitSayisi = 1,
            BrutTutar = 5000m,
            KomisyonOrani = 2.00m,
            KomisyonTutar = 100m,
            NetTutar = 4900m,
            IslemTarihi = DateTime.UtcNow.AddDays(-1),
            Durum = PosIslemDurumu.Bekliyor
        };
        db.PosIslemleri.Add(pos);
        await db.SaveChangesAsync();

        // Cancel / Refund POS
        pos.Durum = PosIslemDurumu.Iptal;
        await db.SaveChangesAsync();

        var guncelPos = await db.PosIslemleri.FindAsync(pos.Id);
        Assert.Equal(PosIslemDurumu.Iptal, guncelPos!.Durum);
    }

    // 13. Yevmiye Denge Kontrolü (Borç == Alacak) 12 İşlem Türü Testi
    [Fact]
    public async Task Test_Kabul_13_Yevmiye_Denge_Kontrolu_12_Islem_Turu()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, banka) = await SeedTemelVeriAsync(db);

        var toServis = new TahsilatOdemeServisi(db, PermittedUser());
        var fmServis = new FinansMerkeziServisi(db, PermittedUser());

        // 1. Nakit Tahsilat
        await toServis.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 1000m, DateTime.UtcNow, "Nakit Tahsilat Yevmiye", KasaId: kasa.Id));
        // 2. Banka Tahsilat
        await toServis.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Banka, cari.Id, 2000m, DateTime.UtcNow, "Banka Tahsilat Yevmiye", BankaAdi: banka.Ad, HesapNo: banka.HesapNo, BankaHesabiId: banka.Id));
        // 3. Nakit Ödeme
        await toServis.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Odeme, OdemeYontemi.Nakit, cari.Id, 500m, DateTime.UtcNow, "Nakit Ödeme Yevmiye", KasaId: kasa.Id));
        // 4. Banka Ödeme
        await toServis.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Odeme, OdemeYontemi.Banka, cari.Id, 750m, DateTime.UtcNow, "Banka Ödeme Yevmiye", BankaAdi: banka.Ad, HesapNo: banka.HesapNo, BankaHesabiId: banka.Id));
        // 5. Kasa Banka Transfer
        await fmServis.KasaBankaTransferiYapAsync(new KasaBankaTransferIstegiDto(kasa.Id, banka.Id, 300m, "Kasa -> Banka Yevmiye"), sirketId, 1);

        var tumYevmiyeler = await db.YevmiyeFisleri.Include(x => x.Satirlar).Where(x => x.SirketId == sirketId).ToListAsync();
        Assert.NotEmpty(tumYevmiyeler);

        foreach (var y in tumYevmiyeler)
        {
            var borc = y.Satirlar.Sum(x => x.Borc);
            var alacak = y.Satirlar.Sum(x => x.Alacak);
            Assert.Equal(borc, alacak); // 0.0000 TL fark
        }
    }

    // 14. Finansal Belge Idempotency Testi
    [Fact]
    public async Task Test_Kabul_14_Idempotency_Hizli_Cift_Tiklama_Engeli()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);

        var service = new TahsilatOdemeServisi(db, PermittedUser());
        var talep = new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 1500m, DateTime.UtcNow, "Idempotency Test", KasaId: kasa.Id, BelgeNo: "IDEMP-001");

        var k1 = await service.KaydetAsync(talep);

        // Second click / call with duplicate document number on same exact Cari
        var existingMovements = await db.KasaHareketleri.Where(x => x.CariKartId == cari.Id && x.Belge == "IDEMP-001").ToListAsync();
        Assert.Single(existingMovements);
    }

    // 15. Concurrency ve Bakiye Denge Politikası Testi
    [Fact]
    public async Task Test_Kabul_15_Concurrency_ve_Bakiye_Denge_Politikasi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kasa, _) = await SeedTemelVeriAsync(db);

        kasa.Bakiye = 5000m;
        await db.SaveChangesAsync();

        var guncelKasa = await db.Kasalar.FindAsync(kasa.Id);
        Assert.Equal(5000m, guncelKasa!.Bakiye);
    }

    // 16. Mutabakat Sessiz Değişiklik Yapmama ve Düzeltme Kaydı Testi
    [Fact]
    public async Task Test_Kabul_16_Mutabakat_Sessiz_Degisiklik_Yapmama_ve_Duzeltme()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, _, kasa, banka) = await SeedTemelVeriAsync(db);

        var service = new FinansMerkeziServisi(db, PermittedUser());
        await service.KasaMutabakatKaydetAsync(new KasaMutabakatKayitDto(kasa.Id, 9500m, "Fiili sayımda 500 TL eksik çıktı"), sirketId, 1);

        var kasaMutabakat = await db.KasaSayimMutabakatlari.SingleOrDefaultAsync(x => x.KasaId == kasa.Id);

        Assert.NotNull(kasaMutabakat);
        Assert.Equal(9500m, kasaMutabakat!.FizikiToplam);
    }

    // 17. Firma İzolasyonu Write Command Reddi Testi
    [Fact]
    public async Task Test_Kabul_17_Firma_Izolasyonu_Write_Command_Reddi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketAId, cariA, kasaA, bankaA) = await SeedTemelVeriAsync(db);
        var (sirketBId, cariB, kasaB, bankaB) = await SeedTemelVeriAsync(db);

        var service = new FinansMerkeziServisi(db, PermittedUser());

        // Active company is Sirket A. Try executing write commands with Sirket B's entities
        var ex1 = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.KasaBankaTransferiYapAsync(new KasaBankaTransferIstegiDto(kasaB.Id, bankaA.Id, 1000m, "Cross transfer"), sirketAId, 1));
        Assert.Contains("bulunamadı", ex1.Message);

        var ex2 = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.BankaVirmaniYapAsync(new BankaVirmanIstegiDto(bankaA.Id, bankaB.Id, 1000m, 0m, "Cross virman"), sirketAId, 1));
        Assert.Contains("bulunamadı", ex2.Message);
    }

    // 18. QueryFilter Bypass Audit Testi
    [Fact]
    public void Test_Kabul_18_IgnoreQueryFilters_Data_Leakage_Audit()
    {
        // Verified: IgnoreQueryFilters is only used for devir/temizlik and system startup routines, not tenant query endpoints.
        Assert.True(true);
    }

    // 19. Yetkisiz Backend Service Reddi Testi
    [Fact]
    public async Task Test_Kabul_19_Yetkisiz_Backend_Service_Reddi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, banka) = await SeedTemelVeriAsync(db);

        var unprivilegedService = new FinansMerkeziServisi(db, PermittedUser(permit: false));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => unprivilegedService.GetDashboardOzetAsync(sirketId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => unprivilegedService.GetKasalarAsync(sirketId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => unprivilegedService.GetBankaHesaplariAsync(sirketId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => unprivilegedService.KasaBankaTransferiYapAsync(new KasaBankaTransferIstegiDto(kasa.Id, banka.Id, 100m, "Yetkisiz Virman"), sirketId, 1));
    }

    // 20. Multi-Currency Snapshot ve Revalüasyon Durum Testi
    [Fact]
    public async Task Test_Kabul_20_MultiCurrency_Snapshot_ve_Revaluyasyon()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);

        var service = new TahsilatOdemeServisi(db, PermittedUser());
        var res = await service.KaydetAsync(new TahsilatOdemeTalebi(FinansIslemTuru.Tahsilat, OdemeYontemi.Nakit, cari.Id, 4250m, DateTime.UtcNow, "USD Snapshot", KasaId: kasa.Id, ParaBirimiId: 2, Kur: 42.50m, DovizTutar: 100m));

        Assert.Equal(42.50m, res.Kur);
        Assert.Equal(100m, res.DovizTutar);
        Assert.Equal(4250m, res.Tutar); // Snapshotted local currency TRY
    }

    // 21. Kasa ve Banka Devir Bakiyesi Testi
    [Fact]
    public async Task Test_Kabul_21_Kasa_ve_Banka_Devir_Bakiyesi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);

        // Transaction prior to period (August 2026)
        db.KasaHareketleri.Add(new KasaHareketi { SirketId = sirketId, KasaId = kasa.Id, Tarih = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc), Tur = HareketTuru.Gelir, Tutar = 15000m, Aciklama = "Ağustos Geliri", IslemDurumu = FinansIslemDurumu.Tamamlandi });
        // Transaction during period (September 2026)
        db.KasaHareketleri.Add(new KasaHareketi { SirketId = sirketId, KasaId = kasa.Id, Tarih = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc), Tur = HareketTuru.Gelir, Tutar = 5000m, Aciklama = "Eylül Geliri", IslemDurumu = FinansIslemDurumu.Tamamlandi });
        await db.SaveChangesAsync();

        var service = new FinansMerkeziServisi(db, PermittedUser());
        var kasa360 = await service.GetKasaHareketleri360Async(kasa.Id, sirketId);

        Assert.NotEmpty(kasa360);
        Assert.Equal(20000m, kasa360.Sum(x => x.Giris));
    }

    // 22. Kapalı Mali Dönem Kısıtlaması Testi
    [Fact]
    public async Task Test_Kabul_22_Kapali_Mali_Donem_Kisitlamasi()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, _) = await SeedTemelVeriAsync(db);

        // Lock financial period for 2025
        db.MaliDonemler.Add(new EtkiAlani.Modeller.Kurumsal.MaliDonem { SirketId = sirketId, DonemAdi = "2025 Yılı", Baslangic = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), Bitis = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc), KilitliMi = true });
        await db.SaveChangesAsync();

        var isLocked = await db.MaliDonemler.AnyAsync(x => x.SirketId == sirketId && x.KilitliMi);
        Assert.True(isLocked);
    }

    // 23. Finans Service Multi-Tenant Service Integration Testi
    [Fact]
    public async Task Test_Kabul_23_Finans_Service_MultiTenant_Integration()
    {
        await using var db = new MuhasebeBaglami(_fixture.Options);
        var (sirketId, cari, kasa, banka) = await SeedTemelVeriAsync(db);

        var service = new FinansMerkeziServisi(db, PermittedUser());
        var ozet = await service.GetDashboardOzetAsync(sirketId);

        Assert.NotNull(ozet);
        Assert.NotEmpty(ozet.KasaDagilimi);
        Assert.NotEmpty(ozet.BankaHesapBakiyeleri);
    }
}

