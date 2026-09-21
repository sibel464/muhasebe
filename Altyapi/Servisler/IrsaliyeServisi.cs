using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Irsaliye;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Siparis;
using EtkiAlani.Modeller.Kurumsal;
using Uygulama.Arayuzler;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class IrsaliyeServisi : IIrsaliyeServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public IrsaliyeServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<List<Irsaliye>> TumunuGetirAsync(IrsaliyeTuru? tur = null, IrsaliyeDurumu? durum = null)
    {
        var sorgu = _baglam.Irsaliyeler
            .Include(i => i.CariKart)
            .Include(i => i.Kalemler)
                .ThenInclude(k => k.StokDagilimlari).ThenInclude(d => d.Depo)
            .Include(i => i.Siparis)
            .Include(i => i.OlusturanKullanici)
            .Include(i => i.DonusturulenFatura)
            .AsQueryable();

        if (tur.HasValue)
        {
            sorgu = sorgu.Where(i => i.Tur == tur.Value);
        }

        if (durum.HasValue)
        {
            sorgu = sorgu.Where(i => i.Durum == durum.Value);
        }

        return await sorgu
            .OrderByDescending(i => i.Tarih)
            .ThenByDescending(i => i.OlusturmaTarihi)
            .ToListAsync();
    }

    public async Task<Irsaliye?> IdIleGetirAsync(int id)
    {
        return await _baglam.Irsaliyeler
            .Include(i => i.CariKart)
            .Include(i => i.Kalemler)
                .ThenInclude(k => k.StokDagilimlari).ThenInclude(d => d.Depo)
            .Include(i => i.Siparis)
            .Include(i => i.OlusturanKullanici)
            .Include(i => i.GuncelleyenKullanici)
            .Include(i => i.DonusturulenFatura)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<Irsaliye?> IrsaliyeNoIleGetirAsync(string irsaliyeNo)
    {
        return await _baglam.Irsaliyeler
            .Include(i => i.CariKart)
            .Include(i => i.Kalemler)
            .Include(i => i.DonusturulenFatura)
            .FirstOrDefaultAsync(i => i.IrsaliyeNo == irsaliyeNo);
    }

    public async Task<string> SiraliIrsaliyeNoUretAsync(IrsaliyeTuru tur, DateTime tarih)
    {
        var onEk = tur == IrsaliyeTuru.Satis ? "IRS-SAT" : "IRS-ALS";
        var yil = tarih.Year;
        var onEkTam = $"{onEk}-{yil}-";

        var sonIrsaliye = await _baglam.Irsaliyeler
            .IgnoreQueryFilters()
            .Where(i => i.IrsaliyeNo.StartsWith(onEkTam))
            .OrderByDescending(i => i.IrsaliyeNo)
            .FirstOrDefaultAsync();

        var siraNo = 1;
        if (sonIrsaliye != null)
        {
            var noParca = sonIrsaliye.IrsaliyeNo.Replace(onEkTam, string.Empty);
            if (int.TryParse(noParca, out int mevcutSira))
            {
                siraNo = mevcutSira + 1;
            }
        }

        return $"{onEkTam}{siraNo:D5}";
    }

    public async Task<bool> IrsaliyeNoMevcutMuAsync(string irsaliyeNo, int? haricId = null)
    {
        var sorgu = _baglam.Irsaliyeler.IgnoreQueryFilters().Where(i => i.IrsaliyeNo == irsaliyeNo);
        if (haricId.HasValue)
        {
            sorgu = sorgu.Where(i => i.Id != haricId.Value);
        }
        return await sorgu.AnyAsync();
    }

    public async Task<Irsaliye> EkleAsync(Irsaliye irsaliye)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.IrsaliyeOlustur, "İrsaliye Oluşturma");

        if (irsaliye.CariKartId <= 0)
            throw new ArgumentException("Geçerli bir Cari Kart seçilmelidir.");

        if (irsaliye.Kalemler == null || !irsaliye.Kalemler.Any())
            throw new ArgumentException("İrsaliyede en az bir kalem bulunmalıdır.");

        var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == irsaliye.CariKartId);
        if (cari == null)
            throw new KeyNotFoundException("İlgili Cari Kart sistemde bulunamadı.");

        if (!cari.Aktif)
            throw new InvalidOperationException("Pasif durumdaki Cari Karta irsaliye düzenlenemez.");

        irsaliye.Durum = IrsaliyeDurumu.Taslak;
        irsaliye.IrsaliyeNo = irsaliye.IrsaliyeNo?.Trim() ?? string.Empty;
        irsaliye.Tarih = DateTime.SpecifyKind(irsaliye.Tarih, DateTimeKind.Utc);
        if (irsaliye.SevkTarihi == default)
            throw new ArgumentException("Sevk tarihi zorunludur.");
        irsaliye.SevkTarihi = DateTime.SpecifyKind(irsaliye.SevkTarihi, DateTimeKind.Utc);
        if (irsaliye.SevkTarihi < irsaliye.Tarih.AddDays(-1))
            throw new ArgumentException("Sevk tarihi belge tarihinden önce olamaz.");

        if (string.IsNullOrWhiteSpace(irsaliye.IrsaliyeNo))
        {
            irsaliye.IrsaliyeNo = await SiraliIrsaliyeNoUretAsync(irsaliye.Tur, irsaliye.Tarih);
        }
        else
        {
            var noVarMi = await IrsaliyeNoMevcutMuAsync(irsaliye.IrsaliyeNo);
            if (noVarMi)
                throw new InvalidOperationException($"'{irsaliye.IrsaliyeNo}' numaralı irsaliye zaten mevcut.");
        }

        foreach (var kalem in irsaliye.Kalemler)
        {
            var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == kalem.UrunId);
            if (urun == null)
                throw new KeyNotFoundException($"'{kalem.UrunId}' ID'li ürün sistemde bulunamadı.");

            if (!urun.Aktif)
                throw new InvalidOperationException($"Pasif durumdaki '{urun.Ad}' ürünü irsaliyeye eklenemez.");

            if (kalem.Miktar <= 0)
                throw new ArgumentException($"Ürün miktarı sıfır veya negatif olamaz ({urun.Ad}).");

            if (kalem.BirimFiyat < 0)
                throw new ArgumentException($"Birim fiyatı negatif olamaz ({urun.Ad}).");

            kalem.UrunKodu = urun.Kod;
            kalem.UrunAd = urun.Ad;
            kalem.Birim = urun.Birim;
            kalem.KdvTutari = decimal.Round(kalem.Miktar * kalem.BirimFiyat * kalem.KdvOrani / 100, 4, MidpointRounding.AwayFromZero);
            kalem.SatirToplami = decimal.Round((kalem.Miktar * kalem.BirimFiyat) + kalem.KdvTutari, 4, MidpointRounding.AwayFromZero);
        }

        irsaliye.AraToplam = irsaliye.Kalemler.Sum(k => k.Miktar * k.BirimFiyat);
        irsaliye.KdvToplam = irsaliye.Kalemler.Sum(k => k.KdvTutari);
        irsaliye.GenelToplam = irsaliye.Kalemler.Sum(k => k.SatirToplami);
        irsaliye.OlusturmaTarihi = DateTime.UtcNow;

        _baglam.Irsaliyeler.Add(irsaliye);
        await _baglam.SaveChangesAsync();

        return irsaliye;
    }

    public async Task<Irsaliye> GuncelleAsync(Irsaliye irsaliye)
    {
        var mevcut = await _baglam.Irsaliyeler
            .Include(i => i.Kalemler)
            .FirstOrDefaultAsync(i => i.Id == irsaliye.Id);

        if (mevcut == null)
            throw new KeyNotFoundException("Güncellenecek irsaliye bulunamadı.");

        if (mevcut.Durum != IrsaliyeDurumu.Taslak)
            throw new InvalidOperationException("Yalnızca 'Taslak' durumundaki irsaliyeler düzenlenebilir.");

        if (irsaliye.CariKartId <= 0)
            throw new ArgumentException("Geçerli bir Cari Kart seçilmelidir.");

        if (irsaliye.Kalemler == null || !irsaliye.Kalemler.Any())
            throw new ArgumentException("İrsaliyede en az bir kalem bulunmalıdır.");

        if (!string.IsNullOrWhiteSpace(irsaliye.IrsaliyeNo) && irsaliye.IrsaliyeNo != mevcut.IrsaliyeNo)
        {
            var noVarMi = await IrsaliyeNoMevcutMuAsync(irsaliye.IrsaliyeNo, irsaliye.Id);
            if (noVarMi)
                throw new InvalidOperationException($"'{irsaliye.IrsaliyeNo}' numaralı irsaliye zaten mevcut.");
            mevcut.IrsaliyeNo = irsaliye.IrsaliyeNo;
        }

        mevcut.CariKartId = irsaliye.CariKartId;
        mevcut.Tur = irsaliye.Tur;
        mevcut.Tarih = DateTime.SpecifyKind(irsaliye.Tarih, DateTimeKind.Utc);
        mevcut.Aciklama = irsaliye.Aciklama ?? string.Empty;

        // Eski kalemleri sil ve yenilerini ekle
        _baglam.IrsaliyeKalemleri.RemoveRange(mevcut.Kalemler);
        mevcut.Kalemler.Clear();

        foreach (var kalem in irsaliye.Kalemler)
        {
            var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == kalem.UrunId);
            if (urun == null)
                throw new KeyNotFoundException($"'{kalem.UrunId}' ID'li ürün sistemde bulunamadı.");

            if (!urun.Aktif)
                throw new InvalidOperationException($"Pasif durumdaki '{urun.Ad}' ürünü irsaliyeye eklenemez.");

            var yeniKalem = new IrsaliyeKalemi
            {
                IrsaliyeId = mevcut.Id,
                UrunId = urun.Id,
                UrunKodu = urun.Kod,
                UrunAd = urun.Ad,
                Birim = urun.Birim,
                Miktar = kalem.Miktar,
                BirimFiyat = kalem.BirimFiyat,
                KdvOrani = kalem.KdvOrani,
                KdvTutari = decimal.Round(kalem.Miktar * kalem.BirimFiyat * kalem.KdvOrani / 100, 4, MidpointRounding.AwayFromZero),
                SatirToplami = decimal.Round((kalem.Miktar * kalem.BirimFiyat) + (kalem.Miktar * kalem.BirimFiyat * kalem.KdvOrani / 100), 4, MidpointRounding.AwayFromZero),
                OlusturmaTarihi = DateTime.UtcNow
            };
            mevcut.Kalemler.Add(yeniKalem);
        }

        mevcut.AraToplam = mevcut.Kalemler.Sum(k => k.Miktar * k.BirimFiyat);
        mevcut.KdvToplam = mevcut.Kalemler.Sum(k => k.KdvTutari);
        mevcut.GenelToplam = mevcut.Kalemler.Sum(k => k.SatirToplami);
        mevcut.GuncellemeTarihi = DateTime.UtcNow;

        await _baglam.SaveChangesAsync();
        return mevcut;
    }

    public async Task<bool> OnaylaAsync(int irsaliyeId)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.IrsaliyeOnayla, "İrsaliye Onaylama");

        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var irsaliye = await _baglam.Irsaliyeler
                .Include(i => i.Kalemler)
                .FirstOrDefaultAsync(i => i.Id == irsaliyeId);

            if (irsaliye == null)
                throw new KeyNotFoundException("İrsaliye bulunamadı.");

            if (irsaliye.Durum != IrsaliyeDurumu.Taslak)
                throw new InvalidOperationException("Yalnızca 'Taslak' durumundaki irsaliyeler onaylanabilir.");

            // Stok Hareketleri Oluştur
            foreach (var kalem in irsaliye.Kalemler)
            {
                var stokHareketi = new StokHareketi
                {
                    UrunId = kalem.UrunId,
                    Tarih = irsaliye.Tarih,
                    BelgeNo = irsaliye.IrsaliyeNo,
                    IlgiliBelgeId = irsaliye.Id,
                    Aciklama = $"{irsaliye.IrsaliyeNo} Nolu İrsaliye Kalemi ({kalem.UrunAd})",
                    HareketTuru = irsaliye.Tur == IrsaliyeTuru.Satis ? StokHareketTuru.Cikis : StokHareketTuru.Giris,
                    Miktar = kalem.Miktar,
                    BirimFiyat = kalem.BirimFiyat,
                    Silindi = false,
                    OlusturmaTarihi = DateTime.UtcNow
                };
                _baglam.StokHareketleri.Add(stokHareketi);
            }

            irsaliye.Durum = IrsaliyeDurumu.Onaylandi;
            irsaliye.GuncellemeTarihi = DateTime.UtcNow;

            await _baglam.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> IptalEtAsync(int irsaliyeId)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.IrsaliyeIptal, "İrsaliye İptal Etme");

        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var irsaliye = await _baglam.Irsaliyeler
                .Include(i => i.Kalemler).ThenInclude(k => k.StokDagilimlari)
                .FirstOrDefaultAsync(i => i.Id == irsaliyeId);
            if (irsaliye == null)
                throw new KeyNotFoundException("İrsaliye bulunamadı.");

            if (irsaliye.Durum == IrsaliyeDurumu.Faturalandi)
                throw new InvalidOperationException("Faturalanmış irsaliye doğrudan iptal edilemez. Önce ilişkili fatura iptal edilmelidir.");

            if (irsaliye.Durum == IrsaliyeDurumu.Iptal)
                throw new InvalidOperationException("İrsaliye zaten iptal edilmiş.");

            if (irsaliye.Durum == IrsaliyeDurumu.Onaylandi)
            {
                if (irsaliye.SiparisId.HasValue)
                {
                    var siparis = await _baglam.Siparisler
                        .FromSqlInterpolated($@"SELECT * FROM ""Siparisler"" WHERE ""Id"" = {irsaliye.SiparisId.Value} FOR UPDATE")
                        .Include(x => x.Kalemler).SingleAsync();
                    var dagilimlar = irsaliye.Kalemler.SelectMany(k => k.StokDagilimlari.Select(d => new { Kalem = k, Dagilim = d })).ToList();
                    foreach (var grup in dagilimlar.GroupBy(x => new { x.Kalem.UrunId, x.Dagilim.DepoId }))
                    {
                        var stok = await _baglam.UrunLokasyonStoklari
                            .FromSqlInterpolated($@"SELECT * FROM ""UrunLokasyonStoklari"" WHERE ""UrunId"" = {grup.Key.UrunId} AND ""DepoId"" = {grup.Key.DepoId} FOR UPDATE")
                            .SingleAsync();
                        stok.MevcutMiktar += grup.Sum(x => x.Dagilim.Miktar);

                        var urun = await _baglam.Urunler.FindAsync(grup.Key.UrunId);
                        if (urun != null)
                        {
                            urun.StokMiktari += grup.Sum(x => x.Dagilim.Miktar);
                            urun.GuncellemeTarihi = DateTime.UtcNow;
                        }
                    }
                    foreach (var kalem in irsaliye.Kalemler)
                    {
                        var siparisKalemi = siparis.Kalemler.Single(x => x.Id == kalem.SiparisKalemiId);
                        siparisKalemi.SevkEdilenMiktar -= kalem.Miktar;
                    }
                    SiparisDurumunuHesapla(siparis);
                }
                // İrsaliyeye ait stok hareketlerini soft-delete yap (MuhasebeBaglami stokları otomatik geri yükler)
                var stokHareketleri = await _baglam.StokHareketleri
                    .Where(s => s.BelgeNo == irsaliye.IrsaliyeNo && s.IlgiliBelgeId == irsaliye.Id)
                    .ToListAsync();

                foreach (var sh in stokHareketleri)
                {
                    sh.Silindi = true;
                    sh.GuncellemeTarihi = DateTime.UtcNow;
                }
            }

            irsaliye.Durum = IrsaliyeDurumu.Iptal;
            irsaliye.GuncellemeTarihi = DateTime.UtcNow;

            await _baglam.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public Task<List<Irsaliye>> SiparisSevkiyatlariniGetirAsync(int siparisId) => _baglam.Irsaliyeler
        .AsNoTracking().Include(x => x.Kalemler).ThenInclude(x => x.StokDagilimlari).ThenInclude(x => x.Depo)
        .Where(x => x.SiparisId == siparisId).OrderByDescending(x => x.SevkTarihi).ToListAsync();

    public Task<List<UrunLokasyonStogu>> LokasyonStoklariniGetirAsync(IEnumerable<int> urunIds)
    {
        var ids = urunIds.Distinct().ToList();
        return _baglam.UrunLokasyonStoklari.AsNoTracking().Include(x => x.Depo)
            .Where(x => ids.Contains(x.UrunId) && x.DepoId >= 1 && x.DepoId <= 3)
            .OrderBy(x => x.DepoId).ToListAsync();
    }

    public async Task<Irsaliye> SiparistenSevkEtAsync(Irsaliye irsaliye)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.IrsaliyeOlustur, "İrsaliye Oluşturma");
        if (!irsaliye.SiparisId.HasValue) throw new ArgumentException("Sevk edilecek sipariş seçilmelidir.");
        if (irsaliye.Kalemler.Count == 0) throw new ArgumentException("En az bir sevk kalemi olmalıdır.");

        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var siparis = await _baglam.Siparisler
                .FromSqlInterpolated($@"SELECT * FROM ""Siparisler"" WHERE ""Id"" = {irsaliye.SiparisId.Value} FOR UPDATE")
                .Include(x => x.Kalemler).Include(x => x.CariKart).SingleOrDefaultAsync()
                ?? throw new KeyNotFoundException("Sipariş bulunamadı.");
            if (siparis.Durum is SiparisDurumu.Iptal or SiparisDurumu.Tamamlandi)
                throw new InvalidOperationException("İptal veya tamamlanmış sipariş sevk edilemez.");
            if (siparis.Durum is not (SiparisDurumu.Onaylandi or SiparisDurumu.Beklemede or SiparisDurumu.KismenKarsilandi))
                throw new InvalidOperationException("Sipariş sevke hazır değil.");

            foreach (var kalem in irsaliye.Kalemler)
            {
                var siparisKalemi = siparis.Kalemler.SingleOrDefault(x => x.Id == kalem.SiparisKalemiId)
                    ?? throw new ArgumentException("İrsaliye kalemi sipariş kalemiyle eşleşmiyor.");
                var kalan = siparisKalemi.Miktar - siparisKalemi.SevkEdilenMiktar;
                if (kalem.Miktar <= 0) throw new ArgumentException("Bu sevk miktarı sıfırdan büyük olmalıdır.");
                if (kalem.Miktar > kalan) throw new InvalidOperationException($"'{siparisKalemi.UrunAdi}' için kalan miktardan fazla sevk yapılamaz. Kalan: {kalan:N2}");
                if (kalem.StokDagilimlari.Count == 0 || kalem.StokDagilimlari.Sum(x => x.Miktar) != kalem.Miktar)
                    throw new InvalidOperationException($"'{siparisKalemi.UrunAdi}' stok dağılımı sevk miktarına eşit olmalıdır.");
                if (kalem.StokDagilimlari.Any(x => x.DepoId is < 1 or > 3 || x.Miktar <= 0) || kalem.StokDagilimlari.GroupBy(x => x.DepoId).Any(x => x.Count() > 1))
                    throw new InvalidOperationException("Yalnız Mağaza, Depo 1 ve Depo 2 için pozitif ve tekil dağılım girilebilir.");

                kalem.UrunId = siparisKalemi.UrunId;
                kalem.UrunKodu = siparisKalemi.UrunKodu;
                kalem.Barkod = siparisKalemi.Barkod;
                kalem.UrunAd = siparisKalemi.UrunAdi;
                kalem.Birim = siparisKalemi.Birim;
                kalem.BirimFiyat = siparisKalemi.BirimFiyat;
                kalem.KdvOrani = siparisKalemi.KdvOrani;
                kalem.KdvTutari = decimal.Round(kalem.Miktar * kalem.BirimFiyat * kalem.KdvOrani / 100m, 4);
                kalem.SatirToplami = kalem.Miktar * kalem.BirimFiyat + kalem.KdvTutari;

                foreach (var dagilim in kalem.StokDagilimlari)
                {
                    var stok = await _baglam.UrunLokasyonStoklari
                        .FromSqlInterpolated($@"SELECT * FROM ""UrunLokasyonStoklari"" WHERE ""UrunId"" = {kalem.UrunId} AND ""DepoId"" = {dagilim.DepoId} FOR UPDATE")
                        .SingleOrDefaultAsync();
                    if (stok == null || stok.MevcutMiktar < dagilim.Miktar)
                    {
                        var depoAdi = await _baglam.Depolar.Where(x => x.Id == dagilim.DepoId).Select(x => x.DepoAdi).FirstOrDefaultAsync() ?? $"Depo {dagilim.DepoId}";
                        throw new InvalidOperationException($"{depoAdi} için yeterli stok bulunmuyor. Mevcut: {stok?.MevcutMiktar ?? 0:N2}, İstenen: {dagilim.Miktar:N2}");
                    }
                    stok.MevcutMiktar -= dagilim.Miktar;
                    var urun = await _baglam.Urunler.FindAsync(kalem.UrunId);
                    if (urun != null)
                    {
                        urun.StokMiktari -= dagilim.Miktar;
                        urun.GuncellemeTarihi = DateTime.UtcNow;
                    }
                }
                siparisKalemi.SevkEdilenMiktar += kalem.Miktar;
            }

            irsaliye.IrsaliyeNo = await SiraliSiparisIrsaliyeNoUretAsync(irsaliye.SevkTarihi);
            irsaliye.CariKartId = siparis.CariKartId;
            irsaliye.Tur = IrsaliyeTuru.Satis;
            irsaliye.Durum = IrsaliyeDurumu.Onaylandi;
            irsaliye.Tarih = DateTime.UtcNow;
            irsaliye.SevkTarihi = irsaliye.SevkTarihi.Kind == DateTimeKind.Utc ? irsaliye.SevkTarihi : irsaliye.SevkTarihi.ToUniversalTime();
            irsaliye.TeslimatAdSoyad = siparis.TeslimatAdSoyad; irsaliye.TeslimatTelefon = siparis.TeslimatTelefon;
            irsaliye.TeslimatAdres = siparis.TeslimatAdres; irsaliye.TeslimatIl = siparis.TeslimatIl; irsaliye.TeslimatIlce = siparis.TeslimatIlce;
            irsaliye.ReferansNo = string.IsNullOrWhiteSpace(irsaliye.ReferansNo) ? siparis.ReferansNo : irsaliye.ReferansNo;
            irsaliye.OlusturanKullaniciId = _kullaniciServisi?.AktifKullanici?.Id;
            irsaliye.AraToplam = irsaliye.Kalemler.Sum(x => x.Miktar * x.BirimFiyat);
            irsaliye.KdvToplam = irsaliye.Kalemler.Sum(x => x.KdvTutari);
            irsaliye.GenelToplam = irsaliye.Kalemler.Sum(x => x.SatirToplami);
            _baglam.Irsaliyeler.Add(irsaliye);
            await _baglam.SaveChangesAsync();

            foreach (var kalem in irsaliye.Kalemler)
                foreach (var dagilim in kalem.StokDagilimlari)
                    _baglam.StokHareketleri.Add(new StokHareketi { UrunId = kalem.UrunId, DepoId = dagilim.DepoId, Tarih = irsaliye.SevkTarihi, HareketTuru = StokHareketTuru.Cikis, Miktar = dagilim.Miktar, BirimFiyat = kalem.BirimFiyat, BelgeNo = irsaliye.IrsaliyeNo, IlgiliBelgeId = irsaliye.Id, Aciklama = $"İrsaliye sevkiyatı ({irsaliye.IrsaliyeNo})" });
            SiparisDurumunuHesapla(siparis);
            await _baglam.SaveChangesAsync();
            await tx.CommitAsync();
            return irsaliye;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await tx.RollbackAsync();
            throw new DbUpdateConcurrencyException("Stok başka bir işlem tarafından değiştirildi. Lütfen stokları yenileyip tekrar deneyin.", ex);
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    private async Task<string> SiraliSiparisIrsaliyeNoUretAsync(DateTime tarih)
    {
        const string tip = "Irsaliye"; const string seriKodu = "IRS";
        var seri = await _baglam.BelgeSerileri.FromSqlInterpolated($@"SELECT * FROM ""BelgeSerileri"" WHERE ""SirketId"" = {MuhasebeBaglami.VarsayilanSirketId} AND ""BelgeTipi"" = {tip} AND ""Seri"" = {seriKodu} FOR UPDATE").SingleOrDefaultAsync();
        if (seri == null) { seri = new BelgeSerisi { SirketId = MuhasebeBaglami.VarsayilanSirketId, BelgeTipi = tip, Seri = seriKodu, Prefix = seriKodu }; _baglam.BelgeSerileri.Add(seri); }
        seri.SonNumara++;
        return $"IRS-{tarih.Year}-{seri.SonNumara:D6}";
    }

    private static void SiparisDurumunuHesapla(Siparis siparis)
    {
        var sevk = siparis.Kalemler.Sum(x => x.SevkEdilenMiktar);
        var toplam = siparis.Kalemler.Sum(x => x.Miktar);
        siparis.Durum = sevk <= 0 ? SiparisDurumu.Onaylandi : sevk >= toplam ? SiparisDurumu.Tamamlandi : SiparisDurumu.KismenKarsilandi;
        siparis.GuncellemeTarihi = DateTime.UtcNow;
    }

    public async Task<bool> SilAsync(int irsaliyeId)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.IrsaliyeIptal || y.IrsaliyeOlustur, "İrsaliye Silme");

        var irsaliye = await _baglam.Irsaliyeler
            .Include(i => i.Kalemler)
            .FirstOrDefaultAsync(i => i.Id == irsaliyeId);

        if (irsaliye == null)
            throw new KeyNotFoundException("İrsaliye bulunamadı.");

        if (irsaliye.Durum != IrsaliyeDurumu.Taslak)
            throw new InvalidOperationException("Yalnızca 'Taslak' durumundaki irsaliyeler silinebilir. Onaylı irsaliyeler için İptal işlemini kullanın.");

        irsaliye.Silindi = true;
        irsaliye.GuncellemeTarihi = DateTime.UtcNow;
        foreach (var k in irsaliye.Kalemler)
        {
            k.Silindi = true;
            k.GuncellemeTarihi = DateTime.UtcNow;
        }

        await _baglam.SaveChangesAsync();
        return true;
    }

    public async Task<Fatura> FaturayaDonusturAsync(int irsaliyeId)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.FaturaOlustur, "İrsaliyeyi Faturaya Dönüştürme");

        var kaynak = await IdIleGetirAsync(irsaliyeId) ?? throw new KeyNotFoundException("İrsaliye bulunamadı.");
        if (kaynak.Durum is IrsaliyeDurumu.Iptal or IrsaliyeDurumu.Taslak)
            throw new InvalidOperationException("Yalnız aktif irsaliyeler faturalanabilir.");
        var kalemler = new List<FaturaKalemi>();
        foreach (var ik in kaynak.Kalemler)
        {
            var onceki = await _baglam.FaturaKalemleri
                .Where(x => x.IrsaliyeKalemiId == ik.Id && x.Fatura!.Durum != BelgeDurumu.Iptal)
                .SumAsync(x => x.Miktar);
            var kalan = ik.Miktar - onceki;
            if (kalan > 0) kalemler.Add(new FaturaKalemi { IrsaliyeKalemiId = ik.Id, Miktar = kalan });
        }
        if (kalemler.Count == 0) throw new InvalidOperationException("İrsaliye tamamen faturalanmış.");
        return await new FaturaServisi(_baglam, _kullaniciServisi).KaynaktanFaturaOlusturAsync(new Fatura
        {
            KaynakTuru = FaturaKaynakTuru.Irsaliyeden, IrsaliyeId = irsaliyeId,
            Tarih = DateTime.UtcNow, OdemeSekli = FaturaOdemeSekli.AcikHesap, Kalemler = kalemler
        });

#pragma warning disable CS0162
        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var irsaliye = await _baglam.Irsaliyeler
                .Include(i => i.Kalemler)
                .Include(i => i.CariKart)
                .FirstOrDefaultAsync(i => i.Id == irsaliyeId);

            if (irsaliye == null)
                throw new KeyNotFoundException("İrsaliye bulunamadı.");

            if (irsaliye.Durum != IrsaliyeDurumu.Onaylandi)
                throw new InvalidOperationException("Yalnızca 'Onaylandı' durumundaki irsaliyeler faturaya dönüştürülebilir.");

            if (irsaliye.CariKart == null || !irsaliye.CariKart.Aktif)
                throw new InvalidOperationException("İrsaliyenin cari kartı pasif veya bulunamadı.");

            // Fatura Numarası Üret
            var faturaTuru = irsaliye.Tur == IrsaliyeTuru.Satis ? FaturaTuru.Satis : FaturaTuru.Alis;
            var onEk = faturaTuru == FaturaTuru.Satis ? "FAT-SAT" : "FAT-ALS";
            var yil = DateTime.UtcNow.Year;
            var onEkTam = $"{onEk}-{yil}-";

            var sonFatura = await _baglam.Faturalar
                .IgnoreQueryFilters()
                .Where(f => f.FaturaNo.StartsWith(onEkTam))
                .OrderByDescending(f => f.FaturaNo)
                .FirstOrDefaultAsync();

            var siraNo = 1;
            if (sonFatura != null)
            {
                var noParca = sonFatura.FaturaNo.Replace(onEkTam, string.Empty);
                if (int.TryParse(noParca, out int mevcutSira))
                {
                    siraNo = mevcutSira + 1;
                }
            }
            var yeniFaturaNo = $"{onEkTam}{siraNo:D5}";

            // Fatura Oluştur
            var fatura = new Fatura
            {
                FaturaNo = yeniFaturaNo,
                Tarih = DateTime.UtcNow,
                CariKartId = irsaliye.CariKartId,
                Tur = faturaTuru,
                Durum = BelgeDurumu.Onaylandi,
                AraToplam = irsaliye.AraToplam,
                SatirIskontosuToplam = 0,
                GenelIskontoOrani = 0,
                GenelIskontoTutari = 0,
                KdvToplam = irsaliye.KdvToplam,
                YuvarlamaTutari = 0,
                GenelToplam = irsaliye.GenelToplam,
                Aciklama = $"{irsaliye.IrsaliyeNo} numaralı irsaliyeden dönüştürülmüştür. {irsaliye.Aciklama}".Trim(),
                OlusturmaTarihi = DateTime.UtcNow
            };

            foreach (var ik in irsaliye.Kalemler)
            {
                fatura.Kalemler.Add(new FaturaKalemi
                {
                    UrunId = ik.UrunId,
                    UrunKodu = ik.UrunKodu,
                    UrunAd = ik.UrunAd,
                    Birim = ik.Birim,
                    Miktar = ik.Miktar,
                    BirimFiyat = ik.BirimFiyat,
                    IskontoOrani = 0,
                    IskontoTutari = 0,
                    KdvOrani = ik.KdvOrani,
                    KdvTutari = ik.KdvTutari,
                    SatirToplami = ik.SatirToplami,
                    OlusturmaTarihi = DateTime.UtcNow
                });
            }

            _baglam.Faturalar.Add(fatura);
            await _baglam.SaveChangesAsync(); // Fatura ID'si oluşsun

            // Cari Hareket Kaydı Ekle (Stok hareketi mükerrer olmasın diye yeniden eklenmez, irsaliyenin stok hareketi geçerlidir)
            var cariHareket = new CariHareket
            {
                CariKartId = fatura.CariKartId,
                Tarih = fatura.Tarih,
                HareketTuru = fatura.Tur == FaturaTuru.Satis ? CariHareketTuru.SatisFaturasi : CariHareketTuru.AlisFaturasi,
                Aciklama = $"{(fatura.Tur == FaturaTuru.Satis ? "Satış Faturası" : "Alış Faturası")} ({fatura.FaturaNo}) - İrsaliye: {irsaliye.IrsaliyeNo}",
                BelgeNo = fatura.FaturaNo,
                IlgiliBelgeId = fatura.Id,
                Borc = fatura.Tur == FaturaTuru.Satis ? fatura.GenelToplam : 0,
                Alacak = fatura.Tur == FaturaTuru.Alis ? fatura.GenelToplam : 0,
                OlusturmaTarihi = DateTime.UtcNow
            };
            _baglam.CariHareketler.Add(cariHareket);

            // İrsaliye Durumunu Güncelle
            irsaliye.Durum = IrsaliyeDurumu.Faturalandi;
            irsaliye.DonusturulenFaturaId = fatura.Id;
            irsaliye.GuncellemeTarihi = DateTime.UtcNow;

            await _baglam.SaveChangesAsync();
            await tx.CommitAsync();

            return fatura;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
#pragma warning restore CS0162
    }
}
