using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Yardimcilar;
using Uygulama.Arayuzler;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class BankaServisi : IBankaServisi
{
    private const decimal EnBuyukTutar = 99999999999999.9999m;
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public BankaServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<List<BankaHareketi>> TumunuGetirAsync()
    {
        return await _baglam.BankaHareketleri
            .Include(b => b.CariKart)
            .Include(b => b.BankaHesabi)
            .OrderByDescending(b => b.Tarih)
            .ThenByDescending(b => b.Id)
            .ToListAsync();
    }

    public async Task<List<BankaHareketi>> TarihAraligindaGetirAsync(DateTime baslangic, DateTime bitis)
    {
        var basUtc = DateTime.SpecifyKind(baslangic.Date, DateTimeKind.Utc);
        var bitUtc = DateTime.SpecifyKind(bitis.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        return await _baglam.BankaHareketleri
            .Include(b => b.CariKart)
            .Include(b => b.BankaHesabi)
            .Where(b => b.Tarih >= basUtc && b.Tarih <= bitUtc)
            .OrderByDescending(b => b.Tarih)
            .ThenByDescending(b => b.Id)
            .ToListAsync();
    }

    public async Task<List<BankaHareketi>> HesapHareketleriGetirAsync(int bankaHesabiId)
    {
        return await _baglam.BankaHareketleri
            .Include(b => b.CariKart)
            .Include(b => b.BankaHesabi)
            .Where(b => b.BankaHesabiId == bankaHesabiId)
            .OrderByDescending(b => b.Tarih)
            .ThenByDescending(b => b.Id)
            .ToListAsync();
    }

    public async Task<List<BankaHareketi>> CariHareketleriGetirAsync(int cariKartId)
    {
        return await _baglam.BankaHareketleri
            .Include(b => b.CariKart)
            .Include(b => b.BankaHesabi)
            .Where(b => b.CariKartId == cariKartId)
            .OrderByDescending(b => b.Tarih)
            .ThenByDescending(b => b.Id)
            .ToListAsync();
    }

    public async Task<BankaHareketi?> GetirByIdAsync(int id)
    {
        return await _baglam.BankaHareketleri
            .Include(b => b.CariKart)
            .Include(b => b.BankaHesabi)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<BankaHareketi> EkleAsync(BankaHareketi hareket)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.BankaEkle || y.BankaDuzenle, "Banka Hareketi Ekleme");

        if (hareket.Tutar <= 0 || hareket.Tutar > EnBuyukTutar)
            throw new ArgumentException("Banka hareket tutarı sıfırdan büyük olmalıdır.");

        if (string.IsNullOrWhiteSpace(hareket.Aciklama))
            throw new ArgumentException("Açıklama boş bırakılamaz.");

        if (!hareket.BankaHesabiId.HasValue || hareket.BankaHesabiId <= 0)
        {
            var varsayilan = await VarsayilanBankaHesabiGetirVeyaOlusturAsync();
            hareket.BankaHesabiId = varsayilan.Id;
            hareket.BankaAdi ??= varsayilan.Ad;
            hareket.HesapNo ??= varsayilan.Iban;
        }

        hareket.Tarih = DateTime.SpecifyKind(hareket.Tarih, DateTimeKind.Utc);
        hareket.OlusturmaTarihi = DateTime.UtcNow;
        hareket.Aciklama = hareket.Aciklama.Trim();
        if (string.IsNullOrWhiteSpace(hareket.FisNo))
        {
            hareket.FisNo = $"BNK-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        if (hareket.BankaAdi != null) hareket.BankaAdi = hareket.BankaAdi.Trim();
        if (hareket.HesapNo != null)
        {
            hareket.HesapNo = hareket.HesapNo.Trim();
            if (hareket.HesapNo.StartsWith("TR", StringComparison.OrdinalIgnoreCase))
            {
                if (!IbanDogrulayici.Gecerlidir(hareket.HesapNo, out var iban))
                    throw new ArgumentException("Geçerli bir IBAN giriniz.");
                hareket.HesapNo = iban;
            }
        }

        _baglam.BankaHareketleri.Add(hareket);
        await _baglam.SaveChangesAsync();

        return hareket;
    }

    public async Task<BankaHareketi> TahsilatEkleAsync(int cariKartId, string bankaAdi, string hesapNo, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.TahsilatYap || y.BankaEkle, "Banka Tahsilat Girişi");

        if (tutar <= 0 || tutar > EnBuyukTutar)
            throw new ArgumentException("Tahsilat tutarı sıfırdan büyük olmalıdır.");

        if (cariKartId <= 0)
            throw new ArgumentException("Geçerli bir cari kart seçilmelidir.");

        hesapNo = HesapNoNormalizeEt(hesapNo);
        var islemTarihi = DateTime.SpecifyKind(tarih ?? DateTime.UtcNow, DateTimeKind.Utc);

        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == cariKartId);
            if (cari == null)
                throw new KeyNotFoundException("Cari kart bulunamadı.");

            if (!cari.Aktif)
                throw new InvalidOperationException("Pasif durumdaki cari karta tahsilat girilemez.");

            var varsayilanBanka = await VarsayilanBankaHesabiGetirVeyaOlusturAsync();

            // 1. Banka Gelen Havale/EFT Hareketi (Gelir)
            var bankaHareketi = new BankaHareketi
            {
                BankaHesabiId = varsayilanBanka.Id,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                FisTuru = BankaFisTuru.GelenHavale,
                FisNo = $"BNK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                DekontNo = belgeNo,
                Tutar = tutar,
                BankaAdi = string.IsNullOrWhiteSpace(bankaAdi) ? varsayilanBanka.Ad : bankaAdi.Trim(),
                HesapNo = string.IsNullOrWhiteSpace(hesapNo) ? varsayilanBanka.Iban : hesapNo.Trim(),
                Aciklama = aciklama.Trim(),
                CariKartId = cariKartId,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(bankaHareketi);
            await _baglam.SaveChangesAsync();

            // 2. Cari Tahsilat Hareketi (Müşterinin Alacağı artar -> Borcu kapanır)
            var cariHareket = new CariHareket
            {
                CariKartId = cariKartId,
                Tarih = islemTarihi,
                HareketTuru = CariHareketTuru.Tahsilat,
                Aciklama = aciklama.Trim(),
                BelgeNo = belgeNo?.Trim(),
                Borc = 0,
                Alacak = tutar,
                IlgiliBelgeId = bankaHareketi.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.CariHareketler.Add(cariHareket);
            await _baglam.SaveChangesAsync();

            bankaHareketi.IlgiliBelgeId = cariHareket.Id;
            await _baglam.SaveChangesAsync();

            await transaction.CommitAsync();
            return bankaHareketi;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<BankaHareketi> OdemeEkleAsync(int cariKartId, string bankaAdi, string hesapNo, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.OdemeYap || y.BankaEkle, "Banka Ödeme Çıkışı");

        if (tutar <= 0 || tutar > EnBuyukTutar)
            throw new ArgumentException("Ödeme tutarı sıfırdan büyük olmalıdır.");

        if (cariKartId <= 0)
            throw new ArgumentException("Geçerli bir cari kart seçilmelidir.");

        hesapNo = HesapNoNormalizeEt(hesapNo);
        var islemTarihi = DateTime.SpecifyKind(tarih ?? DateTime.UtcNow, DateTimeKind.Utc);

        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == cariKartId);
            if (cari == null)
                throw new KeyNotFoundException("Cari kart bulunamadı.");

            if (!cari.Aktif)
                throw new InvalidOperationException("Pasif durumdaki cari karta banka ödemesi girilemez.");

            var varsayilanBanka = await VarsayilanBankaHesabiGetirVeyaOlusturAsync();

            // 1. Banka Giden Havale/EFT Hareketi (Gider)
            var bankaHareketi = new BankaHareketi
            {
                BankaHesabiId = varsayilanBanka.Id,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gider,
                FisTuru = BankaFisTuru.GidenHavale,
                FisNo = $"BNK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                DekontNo = belgeNo,
                Tutar = tutar,
                BankaAdi = string.IsNullOrWhiteSpace(bankaAdi) ? varsayilanBanka.Ad : bankaAdi.Trim(),
                HesapNo = string.IsNullOrWhiteSpace(hesapNo) ? varsayilanBanka.Iban : hesapNo.Trim(),
                Aciklama = aciklama.Trim(),
                CariKartId = cariKartId,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(bankaHareketi);
            await _baglam.SaveChangesAsync();

            // 2. Cari Ödeme Hareketi (Tedarikçiye Borcumuz artar -> Borç kapanır)
            var cariHareket = new CariHareket
            {
                CariKartId = cariKartId,
                Tarih = islemTarihi,
                HareketTuru = CariHareketTuru.Odeme,
                Aciklama = aciklama.Trim(),
                BelgeNo = belgeNo?.Trim(),
                Borc = tutar,
                Alacak = 0,
                IlgiliBelgeId = bankaHareketi.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.CariHareketler.Add(cariHareket);
            await _baglam.SaveChangesAsync();

            bankaHareketi.IlgiliBelgeId = cariHareket.Id;
            await _baglam.SaveChangesAsync();

            await transaction.CommitAsync();
            return bankaHareketi;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> BankaVirmanYapAsync(int kaynakBankaHesabiId, int hedefBankaHesabiId, decimal tutar, string aciklama, DateTime? tarih = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.BankaEkle || y.BankaDuzenle, "Bankalar Arası Virman");

        if (kaynakBankaHesabiId == hedefBankaHesabiId)
            throw new ArgumentException("Kaynak banka hesabı ile hedef banka hesabı aynı olamaz.");

        if (tutar <= 0)
            throw new ArgumentException("Virman tutarı sıfırdan büyük olmalıdır.");

        var islemTarihi = DateTime.SpecifyKind(tarih ?? DateTime.UtcNow, DateTimeKind.Utc);
        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var kaynak = await _baglam.BankaHesaplari.FirstOrDefaultAsync(b => b.Id == kaynakBankaHesabiId);
            var hedef = await _baglam.BankaHesaplari.FirstOrDefaultAsync(b => b.Id == hedefBankaHesabiId);

            if (kaynak == null || hedef == null)
                throw new KeyNotFoundException("Banka hesapları bulunamadı.");

            var fisNo = $"BNK-VRM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

            // 1. Kaynak Banka Çıkışı (Gider)
            var cikis = new BankaHareketi
            {
                BankaHesabiId = kaynakBankaHesabiId,
                BankaAdi = kaynak.Ad,
                HesapNo = kaynak.Iban,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gider,
                FisTuru = BankaFisTuru.BankaVirman,
                FisNo = fisNo,
                Tutar = tutar,
                Aciklama = $"Virman Çıkış -> {hedef.Ad}: {aciklama}",
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(cikis);
            await _baglam.SaveChangesAsync();

            // 2. Hedef Banka Girişi (Gelir)
            var giris = new BankaHareketi
            {
                BankaHesabiId = hedefBankaHesabiId,
                BankaAdi = hedef.Ad,
                HesapNo = hedef.Iban,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                FisTuru = BankaFisTuru.BankaVirman,
                FisNo = fisNo,
                Tutar = tutar,
                Aciklama = $"Virman Giriş <- {kaynak.Ad}: {aciklama}",
                TersKayitId = cikis.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(giris);
            await _baglam.SaveChangesAsync();

            cikis.TersKayitId = giris.Id;
            await _baglam.SaveChangesAsync();

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> BankadanKasaCekAsync(int bankaHesabiId, int kasaId, decimal tutar, string aciklama, DateTime? tarih = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.BankaEkle || y.KasaYonet, "Bankadan Kasaya Para Çekme");

        if (tutar <= 0)
            throw new ArgumentException("Tutar sıfırdan büyük olmalıdır.");

        var islemTarihi = DateTime.SpecifyKind(tarih ?? DateTime.UtcNow, DateTimeKind.Utc);
        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var banka = await _baglam.BankaHesaplari.FirstOrDefaultAsync(b => b.Id == bankaHesabiId);
            var kasa = await _baglam.Kasalar.FirstOrDefaultAsync(k => k.Id == kasaId);

            if (banka == null || kasa == null)
                throw new KeyNotFoundException("Banka Hesabı veya Kasa bulunamadı.");

            var fisNo = $"BNK-CEK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

            // 1. Banka Çıkışı (Gider)
            var bankaCikis = new BankaHareketi
            {
                BankaHesabiId = bankaHesabiId,
                BankaAdi = banka.Ad,
                HesapNo = banka.Iban,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gider,
                FisTuru = BankaFisTuru.KasadanCekme,
                FisNo = fisNo,
                Tutar = tutar,
                Aciklama = $"Kasaya Para Çekme -> {kasa.Ad}: {aciklama}",
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(bankaCikis);
            await _baglam.SaveChangesAsync();

            // 2. Kasa Girişi (Gelir)
            var kasaGiris = new KasaHareketi
            {
                KasaId = kasaId,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                FisTuru = KasaFisTuru.BankadanCekme,
                FisNo = fisNo,
                Tutar = tutar,
                Aciklama = $"Bankadan Para Çekme <- {banka.Ad}: {aciklama}",
                TersKayitId = bankaCikis.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.KasaHareketleri.Add(kasaGiris);
            await _baglam.SaveChangesAsync();

            bankaCikis.TersKayitId = kasaGiris.Id;
            await _baglam.SaveChangesAsync();

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> PosBlokeHesabaGecirAsync(int posHesapId, int vadesizHesapId, decimal tutar, decimal komisyonTutari, string aciklama, DateTime? tarih = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.BankaEkle || y.BankaDuzenle, "POS Hesaba Geçiş İşlemi");

        if (tutar <= 0)
            throw new ArgumentException("Tutar sıfırdan büyük olmalıdır.");

        var netTutar = tutar - komisyonTutari;
        if (netTutar < 0)
            throw new ArgumentException("Komisyon tutarı toplam tutardan büyük olamaz.");

        var islemTarihi = DateTime.SpecifyKind(tarih ?? DateTime.UtcNow, DateTimeKind.Utc);
        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var posHesap = await _baglam.BankaHesaplari.FirstOrDefaultAsync(b => b.Id == posHesapId);
            var vadesizHesap = await _baglam.BankaHesaplari.FirstOrDefaultAsync(b => b.Id == vadesizHesapId);

            if (posHesap == null || vadesizHesap == null)
                throw new KeyNotFoundException("POS veya Vadesiz Banka Hesabı bulunamadı.");

            var fisNo = $"POS-GEC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

            // 1. POS Hesabından Çıkış (Gider)
            var posCikis = new BankaHareketi
            {
                BankaHesabiId = posHesapId,
                BankaAdi = posHesap.Ad,
                HesapNo = posHesap.Iban,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gider,
                FisTuru = BankaFisTuru.PosHesabaGecis,
                FisNo = fisNo,
                Tutar = tutar,
                KomisyonTutari = komisyonTutari,
                Aciklama = $"POS Bloke Çözümü Vadesiz Hesaba Transfer -> {vadesizHesap.Ad}: {aciklama}",
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(posCikis);
            await _baglam.SaveChangesAsync();

            // 2. Vadesiz Hesaba Net Giriş (Gelir)
            var vadesizGiris = new BankaHareketi
            {
                BankaHesabiId = vadesizHesapId,
                BankaAdi = vadesizHesap.Ad,
                HesapNo = vadesizHesap.Iban,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                FisTuru = BankaFisTuru.PosHesabaGecis,
                FisNo = fisNo,
                Tutar = netTutar,
                KomisyonTutari = komisyonTutari,
                Aciklama = $"POS Bloke Hesabından Geçiş <- {posHesap.Ad} (Net: {netTutar:N2} ₺, Komisyon: {komisyonTutari:N2} ₺): {aciklama}",
                TersKayitId = posCikis.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(vadesizGiris);
            await _baglam.SaveChangesAsync();

            posCikis.TersKayitId = vadesizGiris.Id;
            await _baglam.SaveChangesAsync();

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> SilAsync(int id)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.BankaSil, "Banka Hareketi Silme");
        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var hareket = await _baglam.BankaHareketleri.FirstOrDefaultAsync(b => b.Id == id);
            if (hareket == null) return false;

            hareket.Silindi = true;
            hareket.GuncellemeTarihi = DateTime.UtcNow;

            if (hareket.IlgiliBelgeId.HasValue)
            {
                var cariHareket = await _baglam.CariHareketler.FirstOrDefaultAsync(c => c.Id == hareket.IlgiliBelgeId.Value);
                if (cariHareket != null)
                {
                    cariHareket.Silindi = true;
                    cariHareket.GuncellemeTarihi = DateTime.UtcNow;
                }
            }

            if (hareket.TersKayitId.HasValue)
            {
                var ters = await _baglam.BankaHareketleri.FirstOrDefaultAsync(b => b.Id == hareket.TersKayitId.Value);
                if (ters != null)
                {
                    ters.Silindi = true;
                    ters.GuncellemeTarihi = DateTime.UtcNow;
                }
            }

            await _baglam.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<decimal> BakiyeHesaplaAsync()
    {
        var gelir = await _baglam.BankaHareketleri
            .Where(b => b.Tur == HareketTuru.Gelir)
            .SumAsync(b => (decimal?)b.Tutar) ?? 0;

        var gider = await _baglam.BankaHareketleri
            .Where(b => b.Tur == HareketTuru.Gider)
            .SumAsync(b => (decimal?)b.Tutar) ?? 0;

        return gelir - gider;
    }

    public async Task<decimal> HesapBakiyeHesaplaAsync(string bankaAdi, string? hesapNo = null)
    {
        var query = _baglam.BankaHareketleri.Where(b => b.BankaAdi == bankaAdi);
        if (!string.IsNullOrWhiteSpace(hesapNo))
        {
            query = query.Where(k => k.HesapNo == hesapNo);
        }

        var gelir = await query.Where(k => k.Tur == HareketTuru.Gelir).SumAsync(k => (decimal?)k.Tutar) ?? 0;
        var gider = await query.Where(k => k.Tur == HareketTuru.Gider).SumAsync(k => (decimal?)k.Tutar) ?? 0;

        return gelir - gider;
    }

    public async Task<decimal> HesapBakiyeHesaplaAsync(int bankaHesabiId)
    {
        var gelir = await _baglam.BankaHareketleri
            .Where(b => b.BankaHesabiId == bankaHesabiId && b.Tur == HareketTuru.Gelir)
            .SumAsync(b => (decimal?)b.Tutar) ?? 0;

        var gider = await _baglam.BankaHareketleri
            .Where(b => b.BankaHesabiId == bankaHesabiId && b.Tur == HareketTuru.Gider)
            .SumAsync(b => (decimal?)b.Tutar) ?? 0;

        return gelir - gider;
    }

    public async Task<BankaHesabi> BankaHesabiEkleAsync(BankaHesabi hesap)
    {
        _baglam.BankaHesaplari.Add(hesap);
        await _baglam.SaveChangesAsync();
        return hesap;
    }

    public async Task<BankaHesabi> BankaHesabiGuncelleAsync(BankaHesabi hesap)
    {
        _baglam.BankaHesaplari.Update(hesap);
        await _baglam.SaveChangesAsync();
        return hesap;
    }

    public async Task<BankaHesabi?> BankaHesabiGetirIdAsync(int id)
    {
        return await _baglam.BankaHesaplari.FirstOrDefaultAsync(k => k.Id == id);
    }

    public async Task<IReadOnlyList<BankaHesabi>> BankaHesaplariGetirAsync(bool sadeceAktifler = true)
    {
        var query = _baglam.BankaHesaplari.AsQueryable();
        if (sadeceAktifler)
            query = query.Where(k => k.AktifMi);

        var list = await query.ToListAsync();
        if (list.Count == 0)
        {
            var varsayilan = await VarsayilanBankaHesabiGetirVeyaOlusturAsync();
            list = new List<BankaHesabi> { varsayilan };
        }
        return list;
    }

    private async Task<BankaHesabi> VarsayilanBankaHesabiGetirVeyaOlusturAsync()
    {
        var ilk = await _baglam.BankaHesaplari.FirstOrDefaultAsync();
        if (ilk != null)
        {
            if (!IbanDogrulayici.Gecerlidir(ilk.Iban, out var gecerliIban))
            {
                ilk.Iban = "TR2600061000123456789005001";
                await _baglam.SaveChangesAsync();
            }
            return ilk;
        }

        var yeni = new BankaHesabi
        {
            Kod = "BNK-01",
            Ad = "Ziraat Bankası Vadesiz TL",
            HesapTuru = BankaHesapTuru.VadesizTL,
            Sube = "Kadıköy Şubesi",
            HesapNo = "12345678-5001",
            Iban = "TR2600061000123456789005001",
            MuhasebeKodu = "102.01.001",
            ParaBirimi = "TRY",
            AktifMi = true,
            OlusturmaTarihi = DateTime.UtcNow
        };
        _baglam.BankaHesaplari.Add(yeni);
        await _baglam.SaveChangesAsync();
        return yeni;
    }

    private static string HesapNoNormalizeEt(string hesapNo)
    {
        var normalizeEdilmis = hesapNo?.Trim() ?? string.Empty;
        if (!normalizeEdilmis.StartsWith("TR", StringComparison.OrdinalIgnoreCase))
            return normalizeEdilmis;

        if (!IbanDogrulayici.Gecerlidir(normalizeEdilmis, out var iban))
            throw new ArgumentException("Geçerli bir IBAN giriniz.");

        return iban;
    }
}

