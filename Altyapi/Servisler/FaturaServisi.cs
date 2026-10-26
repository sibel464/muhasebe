using Microsoft.EntityFrameworkCore;
using Npgsql;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Siparis;
using EtkiAlani.Modeller.Irsaliye;
using EtkiAlani.Modeller.Kurumsal;
using Uygulama.Arayuzler;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class FaturaServisi : IFaturaServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;
    private readonly IFinancialTransactionService _financialTransactionService;

    public FaturaServisi(
        MuhasebeBaglami baglam,
        IKullaniciServisi? kullaniciServisi = null,
        IFinancialTransactionService? financialTransactionService = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
        _financialTransactionService = financialTransactionService ?? new FinancialTransactionService(baglam, kullaniciServisi);
    }

    public async Task<List<Fatura>> TumunuGetirAsync(FaturaTuru? turFilter = null)
    {
        // Liste salt okunur ve her çağrıda veritabanından taze okunur: e-belge durumu, ödeme gibi başka ekran/bağlamda
        // yapılan değişiklikler, bu bağlamda önceden izlenen eski nesneler yüzünden gizlenmesin.
        var sorgu = _baglam.Faturalar
            .AsNoTrackingWithIdentityResolution()
            .Include(f => f.CariKart)
            .Include(f => f.Kalemler)
            .ThenInclude(k => k.StokDagilimlari)
            .AsSplitQuery()
            .AsQueryable();

        if (turFilter.HasValue)
        {
            sorgu = sorgu.Where(f => f.Tur == turFilter.Value);
        }

        return await sorgu
            .OrderByDescending(f => f.Tarih)
            .ThenByDescending(f => f.OlusturmaTarihi)
            .ToListAsync();
    }

    public async Task<List<Fatura>> ListeGetirAsync(FaturaTuru? turFilter = null, BelgeDurumu? durum = null)
    {
        var sorgu = _baglam.Faturalar.AsNoTracking().Include(f => f.CariKart).AsQueryable();
        if (turFilter.HasValue) sorgu = sorgu.Where(f => f.Tur == turFilter.Value);
        if (durum.HasValue) sorgu = sorgu.Where(f => f.Durum == durum.Value);
        return await sorgu.OrderByDescending(f => f.Tarih).ThenByDescending(f => f.Id).ToListAsync();
    }

    public Task<Fatura?> DetayGetirAsync(int id) =>
        _baglam.Faturalar.AsNoTracking().Include(f => f.CariKart).Include(f => f.Kalemler).ThenInclude(k => k.StokDagilimlari)
            .AsSplitQuery().FirstOrDefaultAsync(f => f.Id == id);

    public Task<int> SayAsync(DateTime baslangic) => _baglam.Faturalar.CountAsync(f => f.Tarih >= baslangic);

    public async Task<Fatura?> IdIleGetirAsync(int id)
    {
        return await _baglam.Faturalar
            .Include(f => f.CariKart)
            .Include(f => f.Kalemler)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<Fatura?> FaturaNoIleGetirAsync(string faturaNo)
    {
        return await _baglam.Faturalar
            .Include(f => f.CariKart)
            .Include(f => f.Kalemler)
            .FirstOrDefaultAsync(f => f.FaturaNo == faturaNo);
    }

    public async Task<Fatura> EkleAsync(Fatura fatura)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.FaturaOlustur, "Fatura Oluşturma");

        // 1. Temel İş Doğrulamaları
        if (fatura.CariKartId <= 0)
            throw new ArgumentException("Geçerli bir Cari Kart seçilmelidir.");

        if (fatura.Kalemler == null || !fatura.Kalemler.Any())
            throw new ArgumentException("Faturada en az bir kalem bulunmalıdır.");

        var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == fatura.CariKartId);
        if (cari == null)
            throw new KeyNotFoundException("İlgili Cari Kart sistemde bulunamadı.");

        if (!cari.Aktif)
            throw new InvalidOperationException("Pasif durumdaki Cari Karta fatura kesilemez.");

        fatura.SirketId ??= _baglam.SirketIdCoz(cari.SirketId);

        // POS'ta bekletilen satış finansal hareket üretmeden Beklemede kalmalıdır.
        if (fatura.Durum != BelgeDurumu.Beklemede)
            fatura.Durum = BelgeDurumu.Taslak;
        fatura.FaturaNo = fatura.FaturaNo?.Trim() ?? string.Empty;
        fatura.Tarih = DateTime.SpecifyKind(fatura.Tarih, DateTimeKind.Utc);

        // Numara verme ile kayıt aynı işlemde ve kilit altında yapılır: eşzamanlı iki fatura aynı sıra numarasını alamaz.
        var transactionSahibi = _baglam.Database.CurrentTransaction == null;
        await using var tx = transactionSahibi ? await _baglam.Database.BeginTransactionAsync() : null;

        // FaturaNo boşsa sıralı (müteselsil) fatura no üret
        if (string.IsNullOrWhiteSpace(fatura.FaturaNo))
        {
            await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(46415455)");
            fatura.FaturaNo = await SiraliFaturaNoUretAsync(fatura.Tur, fatura.Tarih);
        }
        else
        {
            var noVarMi = await FaturaNoMevcutMuAsync(fatura.FaturaNo);
            if (noVarMi)
                throw new InvalidOperationException($"'{fatura.FaturaNo}' numaralı fatura zaten mevcut.");
        }

        // Ürün pasiflik ve mevcudiyet kontrolleri + snapshot kopyalamaları
        foreach (var kalem in fatura.Kalemler)
        {
            var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == kalem.UrunId);
            if (urun == null)
                throw new KeyNotFoundException($"'{kalem.UrunId}' ID'li ürün sistemde bulunamadı.");

            if (!urun.Aktif)
                throw new InvalidOperationException($"Pasif durumdaki '{urun.Ad}' ürünü faturaya eklenemez.");

            if (kalem.Miktar <= 0)
                throw new ArgumentException($"Ürün miktarı sıfır veya negatif olamaz ({urun.Ad}).");

            if (kalem.BirimFiyat < 0)
                throw new ArgumentException($"Birim fiyatı negatif olamaz ({urun.Ad}).");

            if (kalem.IskontoOrani < 0 || kalem.IskontoOrani > 100)
                throw new ArgumentException($"İskonto oranı 0-100 arasında olmalıdır ({urun.Ad}).");

            if (kalem.KdvOrani < 0)
                throw new ArgumentException($"KDV oranı negatif olamaz ({urun.Ad}).");

            // Snapshot alanlarını doldur (Geçmiş verilerin korunması için)
            kalem.UrunKodu = urun.Kod;
            kalem.UrunAd = urun.Ad;
            kalem.Birim = urun.Birim;
        }

        // Tutar ve KDV hesaplamalarını yap
        HesaplaFaturaToplamlari(fatura);

        fatura.OlusturmaTarihi = DateTime.UtcNow;
        fatura.Silindi = false;

        _baglam.Faturalar.Add(fatura);
        await _baglam.SaveChangesAsync();
        if (tx != null)
            await tx.CommitAsync();

        return fatura;
    }

    public async Task<Fatura> GuncelleAsync(Fatura fatura)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.FaturaOlustur, "Fatura Güncelleme");
        // 1. Temel İş Doğrulamaları
        if (fatura.Id <= 0)
            throw new ArgumentException("Güncellenecek fatura bulunamadı.");

        var mevcutFatura = await _baglam.Faturalar
            .Include(f => f.Kalemler)
            .FirstOrDefaultAsync(f => f.Id == fatura.Id);

        if (mevcutFatura == null)
            throw new KeyNotFoundException("Güncellenecek fatura sistemde bulunamadı.");

        if (mevcutFatura.Durum != BelgeDurumu.Taslak)
            throw new InvalidOperationException("Yalnızca taslak durumundaki faturalar güncellenebilir.");

        if (fatura.CariKartId <= 0)
            throw new ArgumentException("Geçerli bir Cari Kart seçilmelidir.");

        if (fatura.Kalemler == null || !fatura.Kalemler.Any())
            throw new ArgumentException("Faturada en az bir kalem bulunmalıdır.");

        var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == fatura.CariKartId);
        if (cari == null)
            throw new KeyNotFoundException("İlgili Cari Kart sistemde bulunamadı.");

        if (!cari.Aktif)
            throw new InvalidOperationException("Pasif durumdaki Cari Karta fatura kesilemez.");

        fatura.FaturaNo = fatura.FaturaNo?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(fatura.FaturaNo) && fatura.FaturaNo != mevcutFatura.FaturaNo)
        {
            var noVarMi = await FaturaNoMevcutMuAsync(fatura.FaturaNo, fatura.Id);
            if (noVarMi)
                throw new InvalidOperationException($"'{fatura.FaturaNo}' numaralı fatura zaten mevcut.");
        }

        // Ürün pasiflik ve mevcudiyet kontrolleri + snapshot kopyalamaları
        foreach (var kalem in fatura.Kalemler)
        {
            var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == kalem.UrunId);
            if (urun == null)
                throw new KeyNotFoundException($"'{kalem.UrunId}' ID'li ürün sistemde bulunamadı.");

            if (!urun.Aktif)
                throw new InvalidOperationException($"Pasif durumdaki '{urun.Ad}' ürünü faturaya eklenemez.");

            if (kalem.Miktar <= 0)
                throw new ArgumentException($"Ürün miktarı sıfır veya negatif olamaz ({urun.Ad}).");

            if (kalem.BirimFiyat < 0)
                throw new ArgumentException($"Birim fiyatı negatif olamaz ({urun.Ad}).");

            if (kalem.IskontoOrani < 0 || kalem.IskontoOrani > 100)
                throw new ArgumentException($"İskonto oranı 0-100 arasında olmalıdır ({urun.Ad}).");

            if (kalem.KdvOrani < 0)
                throw new ArgumentException($"KDV oranı negatif olamaz ({urun.Ad}).");

            // Snapshot alanlarını doldur
            kalem.UrunKodu = urun.Kod;
            kalem.UrunAd = urun.Ad;
            kalem.Birim = urun.Birim;
        }

        // Fatura başlık alanlarını güncelle
        mevcutFatura.CariKartId = fatura.CariKartId;
        mevcutFatura.Tarih = DateTime.SpecifyKind(fatura.Tarih, DateTimeKind.Utc);
        mevcutFatura.FaturaNo = fatura.FaturaNo;
        mevcutFatura.Tur = fatura.Tur;
        mevcutFatura.GenelIskontoOrani = fatura.GenelIskontoOrani;
        mevcutFatura.TevkifatKodu = fatura.TevkifatKodu;
        mevcutFatura.TevkifatOrani = fatura.TevkifatOrani;
        mevcutFatura.Aciklama = fatura.Aciklama ?? string.Empty;
        mevcutFatura.GuncellemeTarihi = DateTime.UtcNow;

        // Kalemleri güncelle (Var olanları sil, yenilerini ekle)
        _baglam.FaturaKalemleri.RemoveRange(mevcutFatura.Kalemler);
        mevcutFatura.Kalemler.Clear();
        foreach (var kalem in fatura.Kalemler)
        {
            mevcutFatura.Kalemler.Add(new FaturaKalemi
            {
                UrunId = kalem.UrunId,
                UrunKodu = kalem.UrunKodu,
                UrunAd = kalem.UrunAd,
                Birim = kalem.Birim,
                Miktar = kalem.Miktar,
                BirimFiyat = kalem.BirimFiyat,
                IskontoOrani = kalem.IskontoOrani,
                KdvOrani = kalem.KdvOrani
            });
        }

        // Toplamları yeniden hesapla
        HesaplaFaturaToplamlari(mevcutFatura);

        await _baglam.SaveChangesAsync();
        return mevcutFatura;
    }

    public async Task<bool> SilAsync(int id)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.FaturaOlustur, "Taslak Fatura Silme");
        var fatura = await _baglam.Faturalar
            .Include(f => f.Kalemler)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (fatura == null) return false;

        if (fatura.Durum is not (BelgeDurumu.Taslak or BelgeDurumu.Beklemede))
            throw new InvalidOperationException("Yalnızca taslak veya bekleyen faturalar silinebilir. Onaylanmış faturaları iptal ediniz.");

        fatura.Silindi = true;
        fatura.GuncellemeTarihi = DateTime.UtcNow;

        foreach (var kalem in fatura.Kalemler)
        {
            kalem.Silindi = true;
            kalem.GuncellemeTarihi = DateTime.UtcNow;
        }

        await _baglam.SaveChangesAsync();
        return true;
    }

    public async Task<Fatura> OnaylaAsync(int id)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.FaturaOnayla, "Fatura Onaylama");
        var fatura = await _baglam.Faturalar
            .Include(f => f.Kalemler)
            .FirstOrDefaultAsync(f => f.Id == id);
        if (fatura == null)
            throw new KeyNotFoundException("Onaylanacak fatura bulunamadı.");

        if (string.IsNullOrWhiteSpace(fatura.FaturaNo))
            fatura.FaturaNo = await SiraliFaturaNoUretAsync(fatura.Tur, fatura.Tarih);
        else if (await FaturaNoMevcutMuAsync(fatura.FaturaNo, fatura.Id))
            throw new InvalidOperationException($"'{fatura.FaturaNo}' numaralı fatura zaten mevcut.");

        HesaplaFaturaToplamlari(fatura);
        fatura.Tarih = DateTime.SpecifyKind(fatura.Tarih, DateTimeKind.Utc);
        fatura.GuncellemeTarihi = DateTime.UtcNow;
        await _financialTransactionService.CreateFromInvoiceAsync(id);
        return fatura;
    }

    // Fatura ekranından onaylanan peşin (nakit/kart/banka) faturanın ödemesi de aynı işlemde kaydedilir.
    // Eskiden yalnız sipariş/irsaliye kaynaklı faturada yapılıyordu; manuel peşin alış faturasında kasa/banka
    // hareketi oluşmuyor, tedarikçi borcu kapanmıyordu.
    public async Task<Fatura> OnaylaVeOdemeyiKaydetAsync(int id)
    {
        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var fatura = await OnaylaAsync(id);
            if (fatura.OdemeSekli != FaturaOdemeSekli.AcikHesap)
            {
                var (kasa, banka, karsiKod) = await OdemeHareketleriniEkleAsync(fatura);
                await _baglam.SaveChangesAsync();
                if (kasa != null)
                {
                    await FinansMuhasebeKaydi.KasaHareketiFisiEkleAsync(_baglam, kasa, karsiKod, kasa.CariKartId);
                    if (kasa.Tur == HareketTuru.Gider && kasa.KasaId is int kasaId && await FinansKurallari.KasaBakiyesiAsync(_baglam, kasaId) < 0)
                        throw new InvalidOperationException("Kasada bu ödeme için yeterli bakiye yok. Kasa bakiyesi eksiye düşemez.");
                }
                if (banka != null)
                    await FinansMuhasebeKaydi.BankaHareketiFisiEkleAsync(_baglam, banka, karsiKod, banka.CariKartId);
            }
            await tx.CommitAsync();
            return fatura;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<Fatura> PosSatisiniTamamlaAsync(Fatura fatura, string odemeYontemi, int? bekleyenFaturaId = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.FaturaOlustur && y.FaturaOnayla, "POS Satış Tamamlama");
        await using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var eklenenFatura = await EkleAsync(fatura);
            await OnaylaAsync(eklenenFatura.Id);

            KasaHareketi? posKasa = null;
            BankaHareketi? posBanka = null;
            if (odemeYontemi != "Açık Hesap")
            {
                _baglam.CariHareketler.Add(new CariHareket
                {
                    CariKartId = eklenenFatura.CariKartId, HareketTuru = CariHareketTuru.Tahsilat,
                    Tarih = eklenenFatura.Tarih, BelgeNo = eklenenFatura.FaturaNo,
                    Alacak = eklenenFatura.OdenecekTutar, Borc = 0,
                    Aciklama = $"POS Tahsilat ({odemeYontemi})", IlgiliBelgeId = eklenenFatura.Id
                });

                if (odemeYontemi == "Nakit")
                {
                    _baglam.KasaHareketleri.Add(posKasa = new KasaHareketi
                    {
                        Tarih = eklenenFatura.Tarih, Tur = HareketTuru.Gelir, Tutar = eklenenFatura.OdenecekTutar,
                        Aciklama = "POS Nakit Satış Tahsilatı", Belge = eklenenFatura.FaturaNo,
                        CariKartId = eklenenFatura.CariKartId, FaturaId = eklenenFatura.Id, IlgiliBelgeId = eklenenFatura.Id
                    });
                }
                else if (odemeYontemi is "Kredi Kartı" or "Banka")
                {
                    var krediKarti = odemeYontemi == "Kredi Kartı";
                    var hesap = await TahsilatHesabiSecAsync(eklenenFatura.SirketId, krediKarti) ?? throw HesapYok(krediKarti);
                    _baglam.BankaHareketleri.Add(posBanka = new BankaHareketi
                    {
                        Tarih = eklenenFatura.Tarih, Tur = HareketTuru.Gelir, Tutar = eklenenFatura.OdenecekTutar,
                        Aciklama = $"POS {odemeYontemi} Tahsilatı", BankaAdi = hesap?.Ad ?? (krediKarti ? "POS" : "Banka"),
                        SirketId = hesap?.SirketId, BankaHesabiId = hesap?.Id, HesapNo = hesap?.Iban,
                        OdemeYontemi = krediKarti ? OdemeYontemi.KrediKarti : OdemeYontemi.Banka,
                        CariKartId = eklenenFatura.CariKartId, FaturaId = eklenenFatura.Id, IlgiliBelgeId = eklenenFatura.Id
                    });
                    if (krediKarti && hesap != null) PosIslemiEkle(eklenenFatura, hesap);
                }
                else
                    throw new ArgumentException("Desteklenmeyen POS ödeme yöntemi.", nameof(odemeYontemi));
            }

            if (bekleyenFaturaId.HasValue)
            {
                var bekleyen = await _baglam.Faturalar.Include(x => x.Kalemler)
                    .SingleOrDefaultAsync(x => x.Id == bekleyenFaturaId.Value && x.Durum == BelgeDurumu.Beklemede);
                if (bekleyen == null)
                    throw new InvalidOperationException("Geri yüklenen bekleyen satış artık mevcut değil.");
                bekleyen.Silindi = true;
                bekleyen.GuncellemeTarihi = DateTime.UtcNow;
                foreach (var kalem in bekleyen.Kalemler)
                {
                    kalem.Silindi = true;
                    kalem.GuncellemeTarihi = DateTime.UtcNow;
                }
            }

            await _baglam.SaveChangesAsync();

            // Tahsilat fişi: 100 Kasa / 102 Bankalar borç - 120 Alıcılar alacak
            if (posKasa != null) await FinansMuhasebeKaydi.KasaHareketiFisiEkleAsync(_baglam, posKasa, "120", posKasa.CariKartId);
            if (posBanka != null) await FinansMuhasebeKaydi.BankaHareketiFisiEkleAsync(_baglam, posBanka, "120", posBanka.CariKartId);

            await transaction.CommitAsync();
            return eklenenFatura;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Fatura> IptalEtAsync(int id, string? neden = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.FaturaIptal, "Fatura İptal Etme");

        // ACID transaction başlatılması
        using var transaction = await _baglam.Database.BeginTransactionAsync();
        try
        {
            var fatura = await _baglam.Faturalar
                .Include(f => f.Kalemler)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (fatura == null)
                throw new KeyNotFoundException("İptal edilecek fatura bulunamadı.");

            if (fatura.Durum != BelgeDurumu.Onaylandi)
                throw new InvalidOperationException("Yalnızca onaylanmış durumdaki faturalar iptal edilebilir.");

            // Alıcıya ulaşmış e-belge programdan tek taraflı iptal edilemez: önce GİB/entegratör portalında iptal
            // (e-Arşiv) ya da alıcının reddi/iade faturası (e-Fatura) gerekir, sonra e-belge durumu işaretlenir.
            // Durum başka bir bağlamda (e-Belge servisi) değişmiş olabilir; izlenen nesne yerine veritabanından okunur.
            var eBelge = await _baglam.Faturalar.AsNoTracking().Where(f => f.Id == id)
                .Select(f => new { f.EBelgeDurumu, f.EBelgeTuru }).SingleAsync();
            if (eBelge.EBelgeDurumu is EBelgeDurumu.Gonderildi or EBelgeDurumu.KabulEdildi)
                throw new InvalidOperationException(
                    $"Bu fatura {(eBelge.EBelgeTuru == EBelgeTuru.EArsiv ? "e-Arşiv" : "e-Fatura")} olarak gönderildi; programdan doğrudan iptal edilemez. " +
                    "Önce entegratör/GİB portalında iptal edin (e-Fatura için alıcı reddetmeli ya da iade faturası kesmelidir), " +
                    "ardından e-Belge menüsünden 'İptal edildi' ya da 'Reddedildi' olarak işaretleyip faturayı iptal edin.");

            // Durumu güncelle
            fatura.Durum = BelgeDurumu.Iptal;
            fatura.GuncellemeTarihi = DateTime.UtcNow;

            if (fatura.SiparisId.HasValue)
            {
                var siparis = await _baglam.Siparisler
                    .FromSqlInterpolated($@"SELECT * FROM ""Siparisler"" WHERE ""Id"" = {fatura.SiparisId.Value} FOR UPDATE")
                    .Include(x => x.Kalemler).SingleAsync();
                foreach (var kalem in fatura.Kalemler.Where(x => x.SiparisKalemiId.HasValue))
                    siparis.Kalemler.Single(x => x.Id == kalem.SiparisKalemiId).FaturalananMiktar -= kalem.Miktar;
            }

            if (fatura.IrsaliyeId.HasValue)
            {
                var irsaliye = await _baglam.Irsaliyeler
                    .FromSqlInterpolated($@"SELECT * FROM ""Irsaliyeler"" WHERE ""Id"" = {fatura.IrsaliyeId.Value} FOR UPDATE")
                    .SingleAsync();
                irsaliye.Durum = IrsaliyeDurumu.Onaylandi;
                irsaliye.DonusturulenFaturaId = null;
            }

            // Faturanın kendi cari/stok hareketleri: IlgiliBelgeId başka belge türlerinde (kasa, çek, irsaliye, sayım)
            // aynı sayıyı taşıyabildiği için belge numarasıyla birlikte eşleştirilir.
            var cariHareketler = await _baglam.CariHareketler
                .Where(h => h.IlgiliBelgeId == fatura.Id && h.BelgeNo == fatura.FaturaNo)
                .ToListAsync();

            var stokHareketleri = await _baglam.StokHareketleri
                .Where(h => h.IlgiliBelgeId == fatura.Id && h.BelgeNo == fatura.FaturaNo)
                .ToListAsync();

            var sirketId = fatura.SirketId ?? throw new InvalidOperationException("Faturanın şirket bağlantısı bulunamadı.");
            var kaynakFis = await _baglam.YevmiyeFisleri
                .Include(x => x.Satirlar)
                .FirstOrDefaultAsync(x => x.SirketId == sirketId && x.BelgeTipi == "Fatura" && x.BelgeId == fatura.Id);
            if (kaynakFis == null)
                throw new InvalidOperationException("İptal edilecek faturanın finansal kaydı bulunamadı.");

            var iptalFisVarMi = await _baglam.YevmiyeFisleri
                .AnyAsync(x => x.SirketId == sirketId && x.BelgeTipi == "FaturaIptal" && x.BelgeId == fatura.Id);
            if (iptalFisVarMi)
                throw new InvalidOperationException("Bu fatura için ters kayıt zaten oluşturulmuş.");

            var iptalBelgeNo = $"{fatura.FaturaNo}-IPTAL";
            neden = neden?.Trim();
            if (neden?.Length > 200) neden = neden[..200];
            var nedenEki = string.IsNullOrEmpty(neden) ? string.Empty : $" (Neden: {neden})";
            // İadenin parası bugün çıkar; belge tarihleri yerel saatle (UTC etiketli) saklanır.
            var simdi = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
            if (nedenEki.Length > 0)
            {
                var ek = $"İptal{nedenEki}";
                fatura.Aciklama = string.IsNullOrWhiteSpace(fatura.Aciklama) ? ek : $"{fatura.Aciklama[..Math.Min(fatura.Aciklama.Length, 500 - ek.Length - 3)]} - {ek}";
            }
            foreach (var hareket in cariHareketler)
            {
                _baglam.CariHareketler.Add(new CariHareket
                {
                    CariKartId = hareket.CariKartId, Tarih = fatura.Tarih, BelgeNo = iptalBelgeNo,
                    IlgiliBelgeId = fatura.Id, Aciklama = $"{fatura.FaturaNo} nolu fatura iptal ters kaydı{nedenEki}",
                    HareketTuru = hareket.HareketTuru switch
                    {
                        CariHareketTuru.SatisFaturasi => CariHareketTuru.SatisIadesi,
                        CariHareketTuru.AlisFaturasi => CariHareketTuru.AlisIadesi,
                        CariHareketTuru.SatisIadesi => CariHareketTuru.SatisFaturasi,
                        CariHareketTuru.AlisIadesi => CariHareketTuru.AlisFaturasi,
                        _ => CariHareketTuru.Diger
                    },
                    Borc = hareket.Alacak, Alacak = hareket.Borc
                });
            }

            foreach (var hareket in stokHareketleri)
            {
                if (hareket.DepoId.HasValue)
                {
                    var yon = hareket.HareketTuru == StokHareketTuru.Cikis ? 1m : -1m;
                    var etkilenen = await _baglam.UrunLokasyonStoklari
                        .Where(s => s.UrunId == hareket.UrunId && s.DepoId == hareket.DepoId.Value &&
                                    (yon > 0 || s.MevcutMiktar >= hareket.Miktar))
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(s => s.MevcutMiktar, s => s.MevcutMiktar + yon * hareket.Miktar)
                            .SetProperty(s => s.GuncellemeTarihi, DateTime.UtcNow));
                    if (etkilenen != 1)
                        throw new InvalidOperationException("İptal işleminde özgün stok alanı güvenli biçimde güncellenemedi.");
                    // Ürün kartındaki toplam stok, aşağıdaki ters stok hareketinden MuhasebeBaglami tarafından güncellenir.
                }

                _baglam.StokHareketleri.Add(new StokHareketi
                {
                    UrunId = hareket.UrunId, DepoId = hareket.DepoId, Tarih = fatura.Tarih, BelgeNo = iptalBelgeNo,
                    IlgiliBelgeId = fatura.Id, Aciklama = $"{fatura.FaturaNo} nolu fatura iptal ters kaydı{nedenEki}",
                    HareketTuru = hareket.HareketTuru == StokHareketTuru.Giris ? StokHareketTuru.Cikis : StokHareketTuru.Giris,
                    IslemTuru = hareket.IslemTuru, Miktar = hareket.Miktar, BirimFiyat = hareket.BirimFiyat
                });
            }

            // Fatura fişi daha önce Muhasebe ekranından elle iptal edildiyse ters kaydı zaten vardır; ikinci kez ters çevrilmez.
            if (kaynakFis.Durum != YevmiyeDurumu.Iptal && kaynakFis.TersFisId == null)
            {
                _baglam.YevmiyeFisleri.Add(new YevmiyeFis
                {
                    SirketId = sirketId, FisNo = $"YF-I-{fatura.Id:D10}", Tarih = fatura.Tarih,
                    BelgeTipi = "FaturaIptal", BelgeId = fatura.Id,
                    Aciklama = $"{fatura.FaturaNo} nolu fatura iptal ters kaydı{nedenEki}", Durum = YevmiyeDurumu.Onaylandi,
                    Satirlar = kaynakFis.Satirlar.Select(s => new YevmiyeSatiri
                    {
                        HesapId = s.HesapId, Borc = s.Alacak, Alacak = s.Borc, CariKartId = s.CariKartId,
                        Aciklama = $"{s.Aciklama} - İptal ters kaydı"
                    }).ToList()
                });
            }

            // SaveChangesAsync (otomatik olarak soft-delete hareketleri algılar, CariKart bakiye ve Urun stok miktarlarını geri çeker)
            await _baglam.SaveChangesAsync();

            // Faturanın peşin tahsilatı/ödemesi de ters kayıtla geri alınır (para iadesi) ve fişlenir.
            foreach (var asil in await _baglam.KasaHareketleri.Where(h => h.FaturaId == fatura.Id && h.IslemDurumu == FinansIslemDurumu.Tamamlandi).ToListAsync())
            {
                var ters = new KasaHareketi
                {
                    SirketId = asil.SirketId, KasaId = asil.KasaId, Tarih = simdi, FisTuru = asil.FisTuru,
                    Tur = asil.Tur == HareketTuru.Gelir ? HareketTuru.Gider : HareketTuru.Gelir, Tutar = asil.Tutar,
                    CariKartId = asil.CariKartId, Aciklama = $"{fatura.FaturaNo} nolu fatura iptali - ödeme iadesi", Belge = iptalBelgeNo,
                    IslemDurumu = FinansIslemDurumu.TersKayit, TersKayitId = asil.Id
                };
                _baglam.KasaHareketleri.Add(ters);
                asil.IslemDurumu = FinansIslemDurumu.IptalEdildi;
                await _baglam.SaveChangesAsync();
                asil.TersKayitId = ters.Id;
                await FinansMuhasebeKaydi.TersFisEkleAsync(_baglam, true, asil.Id, ters, null);
                if (ters.Tur == HareketTuru.Gider && ters.KasaId is int kasaId && await FinansKurallari.KasaBakiyesiAsync(_baglam, kasaId) < 0)
                    throw new InvalidOperationException("Fatura iptali için kasada iade edilecek kadar bakiye yok.");
            }
            // Hesaba geçmemiş kart tahsilatı POS listesinden düşer.
            foreach (var pos in await _baglam.PosIslemleri.Where(p => p.FaturaId == fatura.Id && p.Durum == PosIslemDurumu.Bekliyor).ToListAsync())
                pos.Durum = PosIslemDurumu.Iptal;
            foreach (var asil in await _baglam.BankaHareketleri.Where(h => h.FaturaId == fatura.Id && h.IslemDurumu == FinansIslemDurumu.Tamamlandi).ToListAsync())
            {
                var ters = new BankaHareketi
                {
                    SirketId = asil.SirketId, BankaHesabiId = asil.BankaHesabiId, Tarih = simdi, FisTuru = asil.FisTuru,
                    Tur = asil.Tur == HareketTuru.Gelir ? HareketTuru.Gider : HareketTuru.Gelir, Tutar = asil.Tutar,
                    CariKartId = asil.CariKartId, BankaAdi = asil.BankaAdi, HesapNo = asil.HesapNo, OdemeYontemi = asil.OdemeYontemi,
                    Aciklama = $"{fatura.FaturaNo} nolu fatura iptali - ödeme iadesi",
                    IslemDurumu = FinansIslemDurumu.TersKayit, TersKayitId = asil.Id
                };
                _baglam.BankaHareketleri.Add(ters);
                asil.IslemDurumu = FinansIslemDurumu.IptalEdildi;
                await _baglam.SaveChangesAsync();
                asil.TersKayitId = ters.Id;
                await FinansMuhasebeKaydi.TersFisEkleAsync(_baglam, false, asil.Id, null, ters);
            }
            await _baglam.SaveChangesAsync();
            await transaction.CommitAsync();

            return fatura;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Fatura başka bir işlem tarafından değiştirildi. Lütfen kaydı yenileyin.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Bu fatura için ters finansal kayıt zaten oluşturulmuştur.", ex);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public Task<List<Fatura>> SiparisFaturalariniGetirAsync(int siparisId) => _baglam.Faturalar.AsNoTracking()
        .Include(x => x.Kalemler).Where(x => x.SiparisId == siparisId).OrderByDescending(x => x.Tarih).ToListAsync();

    public Task<List<Fatura>> IrsaliyeFaturalariniGetirAsync(int irsaliyeId) => _baglam.Faturalar.AsNoTracking()
        .Include(x => x.Kalemler).Where(x => x.IrsaliyeId == irsaliyeId).OrderByDescending(x => x.Tarih).ToListAsync();

    public async Task<Fatura> KaynaktanFaturaOlusturAsync(Fatura fatura)
    {
        if (fatura.KaynakTuru is not (FaturaKaynakTuru.Siparisten or FaturaKaynakTuru.Irsaliyeden))
            throw new ArgumentException("Kaynak türü Siparişten veya İrsaliyeden olmalıdır.");
        if ((fatura.KaynakTuru == FaturaKaynakTuru.Siparisten) != fatura.SiparisId.HasValue ||
            (fatura.KaynakTuru == FaturaKaynakTuru.Irsaliyeden) != fatura.IrsaliyeId.HasValue)
            throw new ArgumentException("Fatura kaynak bağlantısı geçersiz.");

        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            Siparis? siparis = null; Irsaliye? irsaliye = null;
            if (fatura.KaynakTuru == FaturaKaynakTuru.Irsaliyeden)
            {
                irsaliye = await _baglam.Irsaliyeler
                    .FromSqlInterpolated($@"SELECT * FROM ""Irsaliyeler"" WHERE ""Id"" = {fatura.IrsaliyeId!.Value} FOR UPDATE")
                    .Include(x => x.Kalemler).ThenInclude(x => x.StokDagilimlari)
                    .Include(x => x.Siparis).ThenInclude(x => x!.Kalemler).SingleOrDefaultAsync()
                    ?? throw new KeyNotFoundException("İrsaliye bulunamadı.");
                if (irsaliye.Durum is IrsaliyeDurumu.Iptal or IrsaliyeDurumu.Taslak)
                    throw new InvalidOperationException("İptal veya taslak irsaliye faturalanamaz.");
                siparis = irsaliye.Siparis;
                fatura.SiparisId = irsaliye.SiparisId;
                fatura.CariKartId = irsaliye.CariKartId;
            }
            else
            {
                siparis = await _baglam.Siparisler
                    .FromSqlInterpolated($@"SELECT * FROM ""Siparisler"" WHERE ""Id"" = {fatura.SiparisId!.Value} FOR UPDATE")
                    .SingleOrDefaultAsync()
                    ?? throw new KeyNotFoundException("Sipariş bulunamadı.");
                if (siparis.Durum is SiparisDurumu.Iptal or SiparisDurumu.Taslak) throw new InvalidOperationException("İptal veya taslak sipariş faturalanamaz.");
                fatura.CariKartId = siparis.CariKartId;
            }
            if (fatura.Kalemler.Count == 0) throw new InvalidOperationException("En az bir fatura kalemi seçilmelidir.");

            foreach (var fk in fatura.Kalemler)
            {
                SiparisKalemi? sk;
                if (irsaliye != null)
                {
                    var ik = irsaliye.Kalemler.SingleOrDefault(x => x.Id == fk.IrsaliyeKalemiId)
                        ?? throw new InvalidOperationException("Fatura kalemi irsaliye kalemiyle eşleşmiyor.");
                    sk = siparis?.Kalemler.SingleOrDefault(x => x.Id == ik.SiparisKalemiId);
                    var onceki = await _baglam.FaturaKalemleri.Where(x => x.IrsaliyeKalemiId == ik.Id && x.Fatura!.Durum != BelgeDurumu.Iptal).SumAsync(x => x.Miktar);
                    if (fk.Miktar <= 0 || fk.Miktar > ik.Miktar - onceki)
                        throw new InvalidOperationException("Faturalanabilir irsaliye miktarı başka bir işlem tarafından değiştirildi. Lütfen kayıtları yenileyin.");
                    fk.SiparisKalemiId = ik.SiparisKalemiId; fk.UrunId = ik.UrunId; fk.UrunKodu = ik.UrunKodu; fk.UrunAd = ik.UrunAd; fk.Birim = ik.Birim;
                    // Siparişsiz (elle girilmiş) irsaliyede fiyat ve KDV irsaliye kaleminden gelir.
                    fk.BirimFiyat = ik.BirimFiyat; fk.IskontoOrani = 0; fk.KdvOrani = ik.KdvOrani;
                }
                else
                {
                    sk = await _baglam.SiparisKalemleri
                        .FromSqlInterpolated($@"SELECT * FROM ""SiparisKalemleri"" WHERE ""Id"" = {fk.SiparisKalemiId!.Value} FOR UPDATE")
                        .SingleOrDefaultAsync(x => x.SiparisId == siparis!.Id)
                        ?? throw new InvalidOperationException("Fatura kalemi sipariş kalemiyle eşleşmiyor.");
                    var kalan = sk.Miktar - sk.FaturalananMiktar;
                    if (fk.Miktar <= 0 || fk.Miktar > kalan)
                        throw new InvalidOperationException("Faturalanabilir miktar başka bir işlem tarafından değiştirildi. Lütfen kayıtları yenileyin.");
                    if (fk.StokDagilimlari.Count == 0 || fk.StokDagilimlari.Sum(x => x.Miktar) != fk.Miktar)
                        throw new InvalidOperationException("Doğrudan sipariş faturasında stok dağılımı zorunludur.");
                    fk.UrunId = sk.UrunId; fk.UrunKodu = sk.UrunKodu; fk.UrunAd = sk.UrunAdi; fk.Birim = sk.Birim;
                }
                if (sk != null)
                {
                    fk.BirimFiyat = sk.BirimFiyat; fk.IskontoOrani = sk.IskontoOrani; fk.KdvOrani = sk.KdvOrani;
                    sk.FaturalananMiktar += fk.Miktar;
                }
            }

            fatura.Tur = irsaliye != null
                ? (irsaliye.Tur == IrsaliyeTuru.Alis ? FaturaTuru.Alis : FaturaTuru.Satis)
                : (siparis?.SiparisTuru == SiparisTuru.Alis ? FaturaTuru.Alis : FaturaTuru.Satis);
            // Belge tarihleri yerel saatle (UTC etiketli) saklanır.
            fatura.Durum = BelgeDurumu.Taslak; fatura.Tarih = fatura.Tarih == default ? DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc) : fatura.Tarih;
            fatura.Aciklama = $"{(irsaliye != null ? irsaliye.IrsaliyeNo : siparis!.SiparisNo)} kaynaklı fatura";
            var eklenen = await EkleAsync(fatura);
            await _financialTransactionService.CreateFromInvoiceAsync(eklenen.Id);

            (KasaHareketi? Kasa, BankaHareketi? Banka, string KarsiKod)? odeme = null;
            if (fatura.OdemeSekli != FaturaOdemeSekli.AcikHesap)
                odeme = await OdemeHareketleriniEkleAsync(fatura);
            if (irsaliye != null)
            {
                var tumu = irsaliye.Kalemler.All(ik => _baglam.FaturaKalemleri.Local.Where(x => x.IrsaliyeKalemiId == ik.Id).Sum(x => x.Miktar) +
                    _baglam.FaturaKalemleri.Where(x => x.IrsaliyeKalemiId == ik.Id && x.Fatura!.Durum != BelgeDurumu.Iptal).Sum(x => x.Miktar) >= ik.Miktar);
                if (tumu) { irsaliye.Durum = IrsaliyeDurumu.Faturalandi; irsaliye.DonusturulenFaturaId = fatura.Id; }
            }
            await _baglam.SaveChangesAsync();
            if (odeme is { } o)
            {
                if (o.Kasa != null)
                {
                    await FinansMuhasebeKaydi.KasaHareketiFisiEkleAsync(_baglam, o.Kasa, o.KarsiKod, o.Kasa.CariKartId);
                    if (o.Kasa.Tur == HareketTuru.Gider && o.Kasa.KasaId is int kasaId && await FinansKurallari.KasaBakiyesiAsync(_baglam, kasaId) < 0)
                        throw new InvalidOperationException("Kasada bu ödeme için yeterli bakiye yok. Kasa bakiyesi eksiye düşemez.");
                }
                if (o.Banka != null)
                    await FinansMuhasebeKaydi.BankaHareketiFisiEkleAsync(_baglam, o.Banka, o.KarsiKod, o.Banka.CariKartId);
            }
            await tx.CommitAsync(); return fatura;
        }
        catch (DbUpdateConcurrencyException ex) { await tx.RollbackAsync(); throw new InvalidOperationException("Faturalanabilir miktar başka bir işlem tarafından değiştirildi. Lütfen kayıtları yenileyin.", ex); }
        catch { await tx.RollbackAsync(); throw; }
    }

    // Peşin faturanın ödemesi: satış ve alış iadesinde para girer (tahsilat), alış ve satış iadesinde para çıkar (ödeme).
    private async Task<(KasaHareketi? Kasa, BankaHareketi? Banka, string KarsiKod)> OdemeHareketleriniEkleAsync(Fatura fatura)
    {
        var giris = fatura.Tur is FaturaTuru.Satis or FaturaTuru.AlisIade;
        var karsiKod = fatura.Tur is FaturaTuru.Satis or FaturaTuru.SatisIade ? "120" : "320";
        var islem = giris ? "tahsilatı" : "ödemesi";
        _baglam.CariHareketler.Add(new CariHareket
        {
            CariKartId = fatura.CariKartId, Tarih = fatura.Tarih, BelgeNo = fatura.FaturaNo, IlgiliBelgeId = fatura.Id,
            HareketTuru = giris ? CariHareketTuru.Tahsilat : CariHareketTuru.Odeme,
            Alacak = giris ? fatura.OdenecekTutar : 0, Borc = giris ? 0 : fatura.OdenecekTutar,
            Aciklama = $"Fatura {islem} ({OdemeAdi(fatura.OdemeSekli)})"
        });
        if (fatura.OdemeSekli == FaturaOdemeSekli.Nakit)
        {
            var kasa = new KasaHareketi
            {
                Tarih = fatura.Tarih, Tur = giris ? HareketTuru.Gelir : HareketTuru.Gider, FisTuru = giris ? KasaFisTuru.Tahsilat : KasaFisTuru.Odeme,
                Tutar = fatura.OdenecekTutar, Aciklama = $"Nakit fatura {islem}", Belge = fatura.FaturaNo,
                CariKartId = fatura.CariKartId, FaturaId = fatura.Id, IlgiliBelgeId = fatura.Id
            };
            _baglam.KasaHareketleri.Add(kasa);
            return (kasa, null, karsiKod);
        }

        // Kartla tahsilat POS hesabına, havale vadesiz hesaba yazılır. Kartla ödemede (şirket kartı) hesap seçilmez.
        var krediKarti = fatura.OdemeSekli == FaturaOdemeSekli.KrediKarti;
        var hesap = giris || !krediKarti ? await TahsilatHesabiSecAsync(fatura.SirketId, krediKarti) ?? throw HesapYok(krediKarti) : null;
        var banka = new BankaHareketi
        {
            Tarih = fatura.Tarih, Tur = giris ? HareketTuru.Gelir : HareketTuru.Gider, FisTuru = giris ? BankaFisTuru.GelenHavale : BankaFisTuru.GidenHavale,
            Tutar = fatura.OdenecekTutar, Aciklama = $"{OdemeAdi(fatura.OdemeSekli)} fatura {islem}",
            BankaAdi = hesap?.Ad ?? (krediKarti ? "POS" : "Banka"),
            SirketId = hesap?.SirketId, BankaHesabiId = hesap?.Id, HesapNo = hesap?.Iban,
            CariKartId = fatura.CariKartId, FaturaId = fatura.Id, IlgiliBelgeId = fatura.Id
        };
        _baglam.BankaHareketleri.Add(banka);
        if (giris && krediKarti && hesap != null) PosIslemiEkle(fatura, hesap);
        return (null, banka, karsiKod);
    }

    // Seçim ekranı olmayan satış/fatura tahsilatının yazılacağı hesap: kart için POS hesabı (108, bloke), havale için
    // vadesiz TL hesap; aynı türden birden çok hesap varsa ilk açılan. Uygun hesap yoksa null döner ve
    // MuhasebeBaglami hareketi yalnız tek aktif hesap varsa ona bağlar.
    // Hesap yokken para, hiçbir banka/POS hesabının bakiyesinde görünmeyen hesapsız bir harekete yazılıyordu.
    private static InvalidOperationException HesapYok(bool krediKarti) => new(krediKarti
        ? "Kartlı işlem için tanımlı aktif bir POS hesabı yok. Banka ekranından \"POS Hesabı\" türünde bir hesap tanımlayıp (komisyon oranıyla) tekrar deneyin."
        : "Havale/EFT işlemi için tanımlı aktif bir vadesiz TL banka hesabı yok. Banka ekranından bir hesap tanımlayıp tekrar deneyin.");

    private async Task<BankaHesabi?> TahsilatHesabiSecAsync(Guid? sirketId, bool krediKarti)
    {
        var tur = krediKarti ? BankaHesapTuru.POSHesabi : BankaHesapTuru.VadesizTL;
        var hesaplar = _baglam.BankaHesaplari.AsNoTracking().Where(b => b.AktifMi && b.HesapTuru == tur);
        if (sirketId is Guid id && id != Guid.Empty)
            hesaplar = hesaplar.Where(b => b.SirketId == id);
        return await hesaplar.OrderBy(b => b.Id).FirstOrDefaultAsync();
    }

    // Kartlı satış Finans Merkezi'ndeki POS listesine "hesaba geçmeyi bekliyor" olarak düşer.
    private void PosIslemiEkle(Fatura fatura, BankaHesabi posHesap)
    {
        var komisyon = decimal.Round(fatura.OdenecekTutar * posHesap.PosKomisyonOrani / 100m, 2);
        _baglam.PosIslemleri.Add(new PosIslemi
        {
            SirketId = posHesap.SirketId, BankaHesabiId = posHesap.Id, BankaAdi = posHesap.Ad, PosCihazAdi = posHesap.Ad,
            BrutTutar = fatura.OdenecekTutar, KomisyonOrani = posHesap.PosKomisyonOrani, KomisyonTutar = komisyon,
            NetTutar = fatura.OdenecekTutar - komisyon, IslemTarihi = fatura.Tarih,
            BeklenenHesabaGecisTarihi = fatura.Tarih.AddDays(Math.Max(0, posHesap.ValorGunu)),
            FaturaId = fatura.Id, CariKartId = fatura.CariKartId, Aciklama = fatura.FaturaNo
        });
    }
    private static string OdemeAdi(FaturaOdemeSekli x) => x switch { FaturaOdemeSekli.AcikHesap => "Açık Hesap", FaturaOdemeSekli.KrediKarti => "Kredi Kartı", _ => x.ToString() };

    public async Task<List<Fatura>> AraAsync(string aramaMetni, FaturaTuru? turFilter = null)
    {
        if (string.IsNullOrWhiteSpace(aramaMetni))
            return await TumunuGetirAsync(turFilter);

        var sorgu = _baglam.Faturalar
            .Include(f => f.CariKart)
            .Include(f => f.Kalemler)
            .AsQueryable();

        if (turFilter.HasValue)
        {
            sorgu = sorgu.Where(f => f.Tur == turFilter.Value);
        }

        return await sorgu
            .Where(f => EF.Functions.ILike(f.FaturaNo, $"%{aramaMetni}%") ||
                        (f.CariKart != null && EF.Functions.ILike(f.CariKart.Unvan, $"%{aramaMetni}%")) ||
                        EF.Functions.ILike(f.Aciklama, $"%{aramaMetni}%"))
            .OrderByDescending(f => f.Tarih)
            .ToListAsync();
    }

    public async Task<bool> FaturaNoMevcutMuAsync(string faturaNo, int? haricId = null)
    {
        if (string.IsNullOrWhiteSpace(faturaNo)) return false;

        return await _baglam.Faturalar
            .IgnoreQueryFilters() // Silinmişler dahil benzersizlik kontrolü
            .AnyAsync(f => f.FaturaNo == faturaNo && (!haricId.HasValue || f.Id != haricId.Value));
    }

    // Fatura Tutar/KDV/Yuvarlama/İskonto Hesaplama Motoru (decimal standardı)
    // Tutar/iskonto/KDV hesabı ortak hesaplayıcıyla yapılır (ekran önizlemesi ile birebir aynı sonuç).
    // Fatura altı iskonto satırlara dağıtılır ve KDV matrahını düşürür; eskiden KDV iskontosuz tutardan
    // hesaplandığı için genel iskontolu faturalarda fazla KDV çıkıyordu.
    private static void HesaplaFaturaToplamlari(Fatura fatura)
    {
        if (fatura.GenelIskontoOrani < 0 || fatura.GenelIskontoOrani > 100)
            throw new ArgumentException("Fatura altı iskonto oranı 0-100 arasında olmalıdır.");

        var sonuc = EtkiAlani.Yardimcilar.FaturaHesaplayici.Hesapla(
            fatura.Kalemler.Select(k => new EtkiAlani.Yardimcilar.FaturaHesaplayici.Girdi(k.Miktar, k.BirimFiyat, k.IskontoOrani, k.KdvOrani)).ToList(),
            fatura.GenelIskontoOrani);

        for (var i = 0; i < fatura.Kalemler.Count; i++)
        {
            var kalem = fatura.Kalemler[i];
            var satir = sonuc.Satirlar[i];
            kalem.IskontoTutari = satir.SatirIskontosu;
            kalem.KdvTutari = satir.Kdv;
            kalem.SatirToplami = satir.Toplam; // genel iskonto payı düşülmüş, KDV dahil satır tutarı
        }

        fatura.AraToplam = sonuc.AraToplam;
        fatura.SatirIskontosuToplam = sonuc.SatirIskontosuToplam;
        fatura.GenelIskontoTutari = sonuc.GenelIskontoTutari;
        fatura.KdvToplam = sonuc.KdvToplam;
        fatura.GenelToplam = sonuc.GenelToplam;
        fatura.YuvarlamaTutari = 0;
        TevkifatiHesapla(fatura);
    }

    // Kısmi KDV tevkifatı (ör. temizlik 9/10, yük taşıma 2/10) yalnız satış ve alış faturasında uygulanır.
    // İadeli tevkifat ve tam tevkifat (8xx kodları) desteklenmez; bunlar açıkça reddedilir.
    private static void TevkifatiHesapla(Fatura fatura)
    {
        fatura.TevkifatKodu = string.IsNullOrWhiteSpace(fatura.TevkifatKodu) ? null : fatura.TevkifatKodu.Trim();
        if (fatura.TevkifatKodu == null)
        {
            fatura.TevkifatOrani = 0;
            fatura.TevkifatTutari = 0;
            return;
        }
        if (fatura.Tur is not (FaturaTuru.Satis or FaturaTuru.Alis))
            throw new InvalidOperationException("KDV tevkifatı yalnız satış ve alış faturasında uygulanabilir; iade faturasında tevkifat desteklenmez.");
        if (EtkiAlani.Yardimcilar.TevkifatKodlari.Bul(fatura.TevkifatKodu) is not { } tanim)
            throw new InvalidOperationException($"'{fatura.TevkifatKodu}' tanınan bir KDV tevkifat kodu değil.");
        if (fatura.TevkifatOrani <= 0)
            fatura.TevkifatOrani = tanim.Oran;
        if (fatura.TevkifatOrani is > 1 || fatura.TevkifatOrani * 10 != decimal.Truncate(fatura.TevkifatOrani * 10))
            throw new InvalidOperationException("Tevkifat oranı 1/10 ile 10/10 arasında, onda bir katlarında olmalıdır.");
        if (fatura.KdvToplam <= 0)
            throw new InvalidOperationException("KDV'si olmayan faturada tevkifat uygulanamaz.");
        fatura.TevkifatTutari = EtkiAlani.Yardimcilar.TevkifatKodlari.Tutar(fatura.KdvToplam, fatura.TevkifatOrani);
    }

    private async Task<string> SiraliFaturaNoUretAsync(FaturaTuru tur, DateTime tarih)
    {
        string prefix = tur switch
        {
            FaturaTuru.Satis => "SAT",
            FaturaTuru.Alis => "ALS",
            FaturaTuru.SatisIade => "STI",
            FaturaTuru.AlisIade => "ALI",
            _ => "FAT"
        };
        string yilStr = tarih.Year.ToString();
        string searchPrefix = $"{prefix}{yilStr}";

        // En son atanan fatura numarasını bulmak için silinmişler dahil tara
        var sonFaturaNo = await _baglam.Faturalar
            .IgnoreQueryFilters()
            .Where(f => f.FaturaNo.StartsWith(searchPrefix))
            .OrderByDescending(f => f.FaturaNo)
            .Select(f => f.FaturaNo)
            .FirstOrDefaultAsync();

        long siraNo = 1;
        if (sonFaturaNo != null && sonFaturaNo.Length == 16)
        {
            var siraPart = sonFaturaNo.Substring(7); // Prefix(3) + Yıl(4) = 7 hane sonrası sıra nosudur
            if (long.TryParse(siraPart, out var parsedSira))
            {
                siraNo = parsedSira + 1;
            }
        }

        return $"{prefix}{yilStr}{siraNo:D9}";
    }
}
