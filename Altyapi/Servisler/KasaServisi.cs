using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class KasaServisi : IKasaServisi
{
    private const decimal EnBuyukTutar = 99999999999999.9999m;
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public KasaServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<List<KasaHareketi>> TumunuGetirAsync()
    {
        return await _baglam.KasaHareketleri
            .Include(k => k.CariKart)
            .Include(k => k.Kasa)
            .OrderByDescending(k => k.Tarih)
            .ThenByDescending(k => k.Id)
            .ToListAsync();
    }

    public async Task<List<KasaHareketi>> TarihAraligindaGetirAsync(DateTime baslangic, DateTime bitis)
    {
        var basUtc = DateTime.SpecifyKind(baslangic.Date, DateTimeKind.Utc);
        var bitUtc = DateTime.SpecifyKind(bitis.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        return await _baglam.KasaHareketleri
            .Include(k => k.CariKart)
            .Include(k => k.Kasa)
            .Where(k => k.Tarih >= basUtc && k.Tarih <= bitUtc)
            .OrderByDescending(k => k.Tarih)
            .ThenByDescending(k => k.Id)
            .ToListAsync();
    }

    public async Task<List<KasaHareketi>> KasaHareketleriGetirAsync(int kasaId)
    {
        return await _baglam.KasaHareketleri
            .Include(k => k.CariKart)
            .Include(k => k.Kasa)
            .Where(k => k.KasaId == kasaId)
            .OrderByDescending(k => k.Tarih)
            .ThenByDescending(k => k.Id)
            .ToListAsync();
    }

    public async Task<List<KasaHareketi>> CariHareketleriGetirAsync(int cariKartId)
    {
        return await _baglam.KasaHareketleri
            .Include(k => k.CariKart)
            .Include(k => k.Kasa)
            .Where(k => k.CariKartId == cariKartId)
            .OrderByDescending(k => k.Tarih)
            .ThenByDescending(k => k.Id)
            .ToListAsync();
    }

    public async Task<KasaHareketi?> GetirByIdAsync(int id)
    {
        return await _baglam.KasaHareketleri
            .Include(k => k.CariKart)
            .Include(k => k.Kasa)
            .FirstOrDefaultAsync(k => k.Id == id);
    }

    public async Task<KasaHareketi> EkleAsync(KasaHareketi hareket)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.KasaYonet || y.GelirEkle || y.GiderEkle, "Kasa Hareketi Ekleme");

        if (hareket.Tutar <= 0 || hareket.Tutar > EnBuyukTutar)
            throw new ArgumentException("Kasa hareket tutarı sıfırdan büyük olmalıdır.");

        if (string.IsNullOrWhiteSpace(hareket.Aciklama))
            throw new ArgumentException("Açıklama boş bırakılamaz.");

        // Varsayılan Kasa kontrolü
        if (!hareket.KasaId.HasValue || hareket.KasaId <= 0)
        {
            var varsayilanKasa = await VarsayilanKasaGetirVeyaOlusturAsync();
            hareket.KasaId = varsayilanKasa.Id;
        }

        hareket.Tarih = DateTime.SpecifyKind(hareket.Tarih, DateTimeKind.Utc);
        hareket.OlusturmaTarihi = DateTime.UtcNow;
        hareket.Aciklama = hareket.Aciklama.Trim();
        if (string.IsNullOrWhiteSpace(hareket.FisNo))
        {
            hareket.FisNo = $"KSF-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        _baglam.KasaHareketleri.Add(hareket);
        await _baglam.SaveChangesAsync();

        return hareket;
    }

    public Task<KasaHareketi> TahsilatEkleAsync(int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null)
    {
        return TahsilatEkleAsync(0, cariKartId, tutar, aciklama, belgeNo, tarih);
    }

    public async Task<KasaHareketi> TahsilatEkleAsync(int kasaId, int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.TahsilatYap || y.KasaYonet, "Kasa Tahsilat Girişi");

        if (tutar <= 0 || tutar > EnBuyukTutar)
            throw new ArgumentException("Tahsilat tutarı sıfırdan büyük olmalıdır.");

        if (cariKartId <= 0)
            throw new ArgumentException("Geçerli bir cari kart seçilmelidir.");

        if (kasaId <= 0)
        {
            var k = await VarsayilanKasaGetirVeyaOlusturAsync();
            kasaId = k.Id;
        }

        var islemTarihi = DateTime.SpecifyKind(tarih ?? DateTime.UtcNow, DateTimeKind.Utc);

        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == cariKartId);
            if (cari == null)
                throw new KeyNotFoundException("Cari kart bulunamadı.");

            if (!cari.Aktif)
                throw new InvalidOperationException("Pasif durumdaki cari karta tahsilat girilemez.");

            // 1. Kasa Gelir Hareketi
            var kasaHareketi = new KasaHareketi
            {
                KasaId = kasaId,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                FisTuru = KasaFisTuru.Tahsilat,
                FisNo = $"KSF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                MakbuzNo = belgeNo,
                Tutar = tutar,
                Aciklama = aciklama.Trim(),
                Belge = belgeNo?.Trim(),
                CariKartId = cariKartId,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.KasaHareketleri.Add(kasaHareketi);
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
                IlgiliBelgeId = kasaHareketi.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.CariHareketler.Add(cariHareket);
            await _baglam.SaveChangesAsync();

            // Kasa hareketine cari hareket referansı ver
            kasaHareketi.IlgiliBelgeId = cariHareket.Id;
            await _baglam.SaveChangesAsync();

            await transaction.CommitAsync();
            return kasaHareketi;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public Task<KasaHareketi> OdemeEkleAsync(int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null)
    {
        return OdemeEkleAsync(0, cariKartId, tutar, aciklama, belgeNo, tarih);
    }

    public async Task<KasaHareketi> OdemeEkleAsync(int kasaId, int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.OdemeYap || y.KasaYonet, "Kasa Ödeme Çıkışı");

        if (tutar <= 0 || tutar > EnBuyukTutar)
            throw new ArgumentException("Ödeme tutarı sıfırdan büyük olmalıdır.");

        if (cariKartId <= 0)
            throw new ArgumentException("Geçerli bir cari kart seçilmelidir.");

        if (kasaId <= 0)
        {
            var k = await VarsayilanKasaGetirVeyaOlusturAsync();
            kasaId = k.Id;
        }

        var islemTarihi = DateTime.SpecifyKind(tarih ?? DateTime.UtcNow, DateTimeKind.Utc);

        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == cariKartId);
            if (cari == null)
                throw new KeyNotFoundException("Cari kart bulunamadı.");

            if (!cari.Aktif)
                throw new InvalidOperationException("Pasif durumdaki cari karta ödeme girilemez.");

            // 1. Kasa Gider Hareketi
            var kasaHareketi = new KasaHareketi
            {
                KasaId = kasaId,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gider,
                FisTuru = KasaFisTuru.Odeme,
                FisNo = $"KSF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}",
                MakbuzNo = belgeNo,
                Tutar = tutar,
                Aciklama = aciklama.Trim(),
                Belge = belgeNo?.Trim(),
                CariKartId = cariKartId,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.KasaHareketleri.Add(kasaHareketi);
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
                IlgiliBelgeId = kasaHareketi.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.CariHareketler.Add(cariHareket);
            await _baglam.SaveChangesAsync();

            // Kasa hareketine cari hareket referansı ver
            kasaHareketi.IlgiliBelgeId = cariHareket.Id;
            await _baglam.SaveChangesAsync();

            await transaction.CommitAsync();
            return kasaHareketi;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> KasaVirmanYapAsync(int kaynakKasaId, int hedefKasaId, decimal tutar, string aciklama, DateTime? tarih = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.KasaYonet, "Kasalar Arası Virman");

        if (kaynakKasaId == hedefKasaId)
            throw new ArgumentException("Kaynak kasa ile hedef kasa aynı olamaz.");

        if (tutar <= 0)
            throw new ArgumentException("Virman tutarı sıfırdan büyük olmalıdır.");

        var islemTarihi = DateTime.SpecifyKind(tarih ?? DateTime.UtcNow, DateTimeKind.Utc);
        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var kaynakKasa = await _baglam.Kasalar.FirstOrDefaultAsync(k => k.Id == kaynakKasaId);
            var hedefKasa = await _baglam.Kasalar.FirstOrDefaultAsync(k => k.Id == hedefKasaId);

            if (kaynakKasa == null || hedefKasa == null)
                throw new KeyNotFoundException("Kasalar bulunamadı.");

            var fisNo = $"VRM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

            // 1. Kaynak Kasa Çıkışı (Gider)
            var cikis = new KasaHareketi
            {
                KasaId = kaynakKasaId,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gider,
                FisTuru = KasaFisTuru.KasaVirman,
                FisNo = fisNo,
                Tutar = tutar,
                Aciklama = $"Virman Çıkış -> {hedefKasa.Ad}: {aciklama}",
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.KasaHareketleri.Add(cikis);
            await _baglam.SaveChangesAsync();

            // 2. Hedef Kasa Girişi (Gelir)
            var giris = new KasaHareketi
            {
                KasaId = hedefKasaId,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                FisTuru = KasaFisTuru.KasaVirman,
                FisNo = fisNo,
                Tutar = tutar,
                Aciklama = $"Virman Giriş <- {kaynakKasa.Ad}: {aciklama}",
                TersKayitId = cikis.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.KasaHareketleri.Add(giris);
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

    public async Task<bool> BankayaParaYatirAsync(int kasaId, int bankaHesabiId, decimal tutar, string aciklama, DateTime? tarih = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.KasaYonet || y.BankaEkle, "Kasadan Bankaya Para Yatırma");

        if (tutar <= 0)
            throw new ArgumentException("Tutar sıfırdan büyük olmalıdır.");

        var islemTarihi = DateTime.SpecifyKind(tarih ?? DateTime.UtcNow, DateTimeKind.Utc);
        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var kasa = await _baglam.Kasalar.FirstOrDefaultAsync(k => k.Id == kasaId);
            var banka = await _baglam.BankaHesaplari.FirstOrDefaultAsync(b => b.Id == bankaHesabiId);

            if (kasa == null || banka == null)
                throw new KeyNotFoundException("Kasa veya Banka Hesabı bulunamadı.");

            var fisNo = $"BNK-YTR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";

            // 1. Kasa Çıkışı (Gider)
            var kasaCikis = new KasaHareketi
            {
                KasaId = kasaId,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gider,
                FisTuru = KasaFisTuru.BankayaYatirma,
                FisNo = fisNo,
                Tutar = tutar,
                Aciklama = $"Bankaya Para Yatırma -> {banka.Ad} ({banka.Iban}): {aciklama}",
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.KasaHareketleri.Add(kasaCikis);
            await _baglam.SaveChangesAsync();

            // 2. Banka Girişi (Gelir)
            var bankaGiris = new BankaHareketi
            {
                BankaHesabiId = bankaHesabiId,
                BankaAdi = banka.Ad,
                HesapNo = banka.Iban,
                Tarih = islemTarihi,
                Tur = HareketTuru.Gelir,
                FisTuru = BankaFisTuru.BankayaYatirma,
                FisNo = fisNo,
                Tutar = tutar,
                Aciklama = $"Kasadan Para Yatırma <- {kasa.Ad}: {aciklama}",
                TersKayitId = kasaCikis.Id,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.BankaHareketleri.Add(bankaGiris);
            await _baglam.SaveChangesAsync();

            kasaCikis.TersKayitId = bankaGiris.Id;
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

    public async Task<KasaSayimMutabakat> KasaSayimMutabakatKaydetAsync(KasaSayimMutabakat mutabakat)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.KasaYonet, "Kasa Mutabakat / Gün Sonu");

        mutabakat.Tarih = DateTime.SpecifyKind(mutabakat.Tarih, DateTimeKind.Utc);
        mutabakat.SistemBakiyesi = await KasaBakiyeHesaplaAsync(mutabakat.KasaId);
        mutabakat.OlusturmaTarihi = DateTime.UtcNow;

        _baglam.KasaSayimMutabakatlari.Add(mutabakat);
        await _baglam.SaveChangesAsync();

        // Eğer fiziki sayım ile sistem bakiyesi arasında fark varsa otomatik Sayım Farkı Fişi oluştur!
        if (mutabakat.Fark != 0)
        {
            var kasa = await _baglam.Kasalar.FindAsync(mutabakat.KasaId);
            var farkHareket = new KasaHareketi
            {
                KasaId = mutabakat.KasaId,
                Tarih = mutabakat.Tarih,
                Tur = mutabakat.Fark > 0 ? HareketTuru.Gelir : HareketTuru.Gider,
                FisTuru = KasaFisTuru.SayimFarki,
                FisNo = $"SYM-{DateTime.UtcNow:yyyyMMddHHmm}",
                Tutar = Math.Abs(mutabakat.Fark),
                Aciklama = $"Kasa Gün Sonu Mutabakat Sayım Farkı ({(mutabakat.Fark > 0 ? "Kasa Fazlası" : "Kasa Noksanı")}): {mutabakat.Aciklama}",
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.KasaHareketleri.Add(farkHareket);
            await _baglam.SaveChangesAsync();
        }

        return mutabakat;
    }

    public async Task<bool> SilAsync(int id)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.KasaYonet, "Kasa Hareketi Silme");
        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var hareket = await _baglam.KasaHareketleri.FirstOrDefaultAsync(k => k.Id == id);
            if (hareket == null) return false;

            hareket.Silindi = true;
            hareket.GuncellemeTarihi = DateTime.UtcNow;

            // Eğer bağlı bir cari veya ters hareket varsa onu da sil
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
                var ters = await _baglam.KasaHareketleri.FirstOrDefaultAsync(k => k.Id == hareket.TersKayitId.Value);
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
        var gelir = await _baglam.KasaHareketleri
            .Where(k => k.Tur == HareketTuru.Gelir)
            .SumAsync(k => (decimal?)k.Tutar) ?? 0;

        var gider = await _baglam.KasaHareketleri
            .Where(k => k.Tur == HareketTuru.Gider)
            .SumAsync(k => (decimal?)k.Tutar) ?? 0;

        return gelir - gider;
    }

    public async Task<decimal> KasaBakiyeHesaplaAsync(int kasaId)
    {
        var gelir = await _baglam.KasaHareketleri
            .Where(k => k.KasaId == kasaId && k.Tur == HareketTuru.Gelir)
            .SumAsync(k => (decimal?)k.Tutar) ?? 0;

        var gider = await _baglam.KasaHareketleri
            .Where(k => k.KasaId == kasaId && k.Tur == HareketTuru.Gider)
            .SumAsync(k => (decimal?)k.Tutar) ?? 0;

        return gelir - gider;
    }

    public async Task<Kasa> KasaEkleAsync(Kasa kasa)
    {
        _baglam.Kasalar.Add(kasa);
        await _baglam.SaveChangesAsync();
        return kasa;
    }

    public async Task<Kasa> KasaGuncelleAsync(Kasa kasa)
    {
        _baglam.Kasalar.Update(kasa);
        await _baglam.SaveChangesAsync();
        return kasa;
    }

    public async Task<Kasa?> KasaGetirIdAsync(int id)
    {
        return await _baglam.Kasalar.FirstOrDefaultAsync(k => k.Id == id);
    }

    public async Task<IReadOnlyList<Kasa>> KasalariGetirAsync(bool sadeceAktifler = true)
    {
        var query = _baglam.Kasalar.AsQueryable();
        if (sadeceAktifler)
            query = query.Where(k => k.AktifMi);

        var list = await query.ToListAsync();
        if (list.Count == 0)
        {
            var varsayilan = await VarsayilanKasaGetirVeyaOlusturAsync();
            list = new List<Kasa> { varsayilan };
        }
        return list;
    }

    private async Task<Kasa> VarsayilanKasaGetirVeyaOlusturAsync()
    {
        var ilk = await _baglam.Kasalar.FirstOrDefaultAsync();
        if (ilk != null) return ilk;

        var yeni = new Kasa
        {
            Kod = "KSA-01",
            Ad = "Merkez Nakit Kasa",
            MuhasebeKodu = "100.01.001",
            ParaBirimi = "TRY",
            AktifMi = true,
            OlusturmaTarihi = DateTime.UtcNow
        };
        _baglam.Kasalar.Add(yeni);
        await _baglam.SaveChangesAsync();
        return yeni;
    }
}

