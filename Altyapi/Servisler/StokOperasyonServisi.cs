using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Uygulama.Dto.StokOperasyon;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class StokOperasyonServisi : IStokOperasyonServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public StokOperasyonServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    private void YetkiKontrol(Func<EtkiAlani.Modeller.Kullanici.KullaniciYetkileri, bool> yetki, string islem)
    {
        if (_kullaniciServisi?.AktifKullanici == null)
            throw new UnauthorizedAccessException("Bu işlem için geçerli kullanıcı oturumu gerekiyor.");
        _kullaniciServisi.YetkiKontrolEt(yetki, islem);
    }

    // Depo yalnız kendi firmasının şubesine bağlıysa kullanılabilir; başka firmanın deposuna stok yazılmaz.
    private async Task<EtkiAlani.Modeller.Kurumsal.Depo> FirmaDeposuAsync(int depoId, Guid sirketId, string tanim)
    {
        var depo = await _baglam.Depolar.Include(d => d.Sube).FirstOrDefaultAsync(d => d.Id == depoId && d.AktifMi)
            ?? throw new InvalidOperationException($"{tanim} bulunamadı veya pasif.");
        if (depo.Sube != null && depo.Sube.SirketId != sirketId)
            throw new InvalidOperationException($"{tanim} bu firmaya ait değil.");
        return depo;
    }

    // Kilitler her zaman aynı sırada alınır; karşılıklı iki transfer birbirini kilitlemesin.
    private async Task DepoUrunKilitleAsync(int urunId, params int[] depoIdleri)
    {
        foreach (var depoId in depoIdleri.Distinct().OrderBy(x => x))
            await DepoKurallari.LokasyonKilitleAsync(_baglam, urunId, depoId);
    }

    public async Task<List<StokOperasyonMatrisOzetDto>> GetMatrisOzetAsync(Guid sirketId, string? arama = null, int? depoId = null, bool sadeceKritik = false, CancellationToken cancellationToken = default)
    {
        YetkiKontrol(y => y.StokGoruntule, "Stok Operasyon Merkezi Görüntüleme");
        sirketId = _baglam.SirketIdCoz(sirketId);

        var depolar = await _baglam.Depolar.AsNoTracking()
            .Include(d => d.Sube)
            .Where(d => d.AktifMi && (d.Sube == null || d.Sube.SirketId == sirketId))
            .OrderBy(d => d.Id)
            .ToListAsync(cancellationToken);
        int? magazaId = depolar.FirstOrDefault(d => d.DepoTuru == EtkiAlani.Modeller.Kurumsal.DepoTuru.Magaza)?.Id;
        var normalDepolar = depolar.Where(d => d.DepoTuru == EtkiAlani.Modeller.Kurumsal.DepoTuru.Depo).OrderBy(d => d.Id).ToList();
        int? depo1Id = normalDepolar.ElementAtOrDefault(0)?.Id;
        int? depo2Id = normalDepolar.ElementAtOrDefault(1)?.Id;

        var sorgu = _baglam.Urunler.AsNoTracking()
            .Include(u => u.KategoriTanimi)
            .Include(u => u.MarkaTanimi)
            .Include(u => u.BirimTanimi)
            .Include(u => u.LokasyonStoklari)
            .Where(u => u.SirketId == null || u.SirketId == sirketId);

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var a = arama.Trim();
            sorgu = sorgu.Where(u => EF.Functions.ILike(u.Kod, $"%{a}%") ||
                                     EF.Functions.ILike(u.Ad, $"%{a}%") ||
                                     (u.Barkod != null && EF.Functions.ILike(u.Barkod, $"%{a}%")) ||
                                     (u.Kategori != null && EF.Functions.ILike(u.Kategori, $"%{a}%")) ||
                                     (u.KategoriTanimi != null && EF.Functions.ILike(u.KategoriTanimi.Ad, $"%{a}%")) ||
                                     (u.MarkaTanimi != null && EF.Functions.ILike(u.MarkaTanimi.Ad, $"%{a}%")));
        }

        if (sadeceKritik)
        {
            sorgu = sorgu.Where(u => u.StokTakibi && u.StokMiktari <= u.MinimumStok);
        }

        var urunler = await sorgu.OrderBy(u => u.Ad).ToListAsync(cancellationToken);
        var result = new List<StokOperasyonMatrisOzetDto>();

        foreach (var u in urunler)
        {
            var lokasyonlar = u.LokasyonStoklari.ToList();

            var mStok = lokasyonlar.FirstOrDefault(l => magazaId.HasValue && l.DepoId == magazaId.Value);
            var d1Stok = lokasyonlar.FirstOrDefault(l => depo1Id.HasValue && l.DepoId == depo1Id.Value);
            var d2Stok = lokasyonlar.FirstOrDefault(l => depo2Id.HasValue && l.DepoId == depo2Id.Value);

            decimal mFiziksel = mStok?.MevcutMiktar ?? 0;
            decimal mRezerve = mStok?.RezerveMiktar ?? 0;
            decimal mKullanilabilir = Math.Max(0, mFiziksel - mRezerve);

            decimal d1Fiziksel = d1Stok?.MevcutMiktar ?? 0;
            decimal d1Rezerve = d1Stok?.RezerveMiktar ?? 0;
            decimal d1Kullanilabilir = Math.Max(0, d1Fiziksel - d1Rezerve);

            decimal d2Fiziksel = d2Stok?.MevcutMiktar ?? 0;
            decimal d2Rezerve = d2Stok?.RezerveMiktar ?? 0;
            decimal d2Kullanilabilir = Math.Max(0, d2Fiziksel - d2Rezerve);

            decimal topFiziksel = lokasyonlar.Sum(l => l.MevcutMiktar);
            decimal topRezerve = lokasyonlar.Sum(l => l.RezerveMiktar);
            decimal topKullanilabilir = Math.Max(0, topFiziksel - topRezerve);

            // Hizmet / masraf kartı ürün listesinde düzenlenebilsin diye listelenir; stoğu yoktur.
            string durum = !u.StokTakibi ? "Hizmet / Masraf" : topFiziksel <= 0 ? "Stok Yok" : (topFiziksel <= u.MinimumStok ? "Kritik Stok" : "Normal");

            result.Add(new StokOperasyonMatrisOzetDto(
                u.Id,
                u.Kod,
                u.Ad,
                u.Barkod,
                u.KategoriTanimi?.Ad ?? u.Kategori,
                u.MarkaTanimi?.Ad,
                u.BirimTanimi?.Ad ?? u.Birim,
                mFiziksel,
                mRezerve,
                mKullanilabilir,
                d1Fiziksel,
                d1Rezerve,
                d1Kullanilabilir,
                d2Fiziksel,
                d2Rezerve,
                d2Kullanilabilir,
                topFiziksel,
                topRezerve,
                topKullanilabilir,
                u.MinimumStok,
                u.AlisFiyati,
                u.PerakendeSatisFiyati,
                durum,
                u.Aktif
            ));
        }

        if (depoId.HasValue && depoId.Value > 0)
        {
            result = result.Where(x => (depoId == magazaId && x.MagazaFiziksel > 0) ||
                                       (depoId == depo1Id && x.Depo1Fiziksel > 0) ||
                                       (depoId == depo2Id && x.Depo2Fiziksel > 0)).ToList();
        }

        return result;
    }

    public async Task StokGirisiYapAsync(StokGirisIstegi istek, Guid sirketId, int kullaniciId)
    {
        YetkiKontrol(y => y.StokEkle || y.StokDuzenle, "Manuel Stok Girişi");

        if (istek.Miktar <= 0)
            throw new ArgumentException("Giriş miktarı 0'dan büyük olmalıdır.");

        if (string.IsNullOrWhiteSpace(istek.IslemNedeni))
            throw new ArgumentException("Giriş işlemi için neden belirtilmesi zorunludur.");
        sirketId = _baglam.SirketIdCoz(sirketId);

        await using var tx = await _baglam.Database.BeginTransactionAsync();
        await DepoUrunKilitleAsync(istek.UrunId, istek.DepoId);

        var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == istek.UrunId && (u.SirketId == null || u.SirketId == sirketId))
            ?? throw new InvalidOperationException("Ürün bulunamadı veya yetkisiz erişim.");

        var depo = await FirmaDeposuAsync(istek.DepoId, sirketId, "Lokasyon/Depo");

        var lokasyon = await _baglam.UrunLokasyonStoklari.FirstOrDefaultAsync(l => l.UrunId == istek.UrunId && l.DepoId == istek.DepoId);
        if (lokasyon == null)
        {
            lokasyon = new UrunLokasyonStogu { UrunId = istek.UrunId, DepoId = istek.DepoId, MevcutMiktar = 0, RezerveMiktar = 0 };
            _baglam.UrunLokasyonStoklari.Add(lokasyon);
        }

        lokasyon.MevcutMiktar += istek.Miktar;
        lokasyon.GuncellemeTarihi = DateTime.UtcNow;
        // Ürün kartındaki toplam stok stok hareketinden otomatik güncellenir (MuhasebeBaglami).

        var sh = new StokHareketi
        {
            SirketId = sirketId,
            UrunId = istek.UrunId,
            DepoId = istek.DepoId,
            KullaniciId = kullaniciId,
            Tarih = DateTime.SpecifyKind(istek.Tarih, DateTimeKind.Utc),
            HareketTuru = StokHareketTuru.Giris,
            IslemTuru = StokIslemTuru.DevirGiris,
            FisNo = $"GRS-{DateTime.UtcNow:yyyyMMddHHmmss}",
            BelgeNo = istek.ReferansNo,
            Miktar = istek.Miktar,
            BirimFiyat = istek.BirimFiyat > 0 ? istek.BirimFiyat : urun.AlisFiyati,
            Aciklama = $"Manuel Giriş [{istek.IslemNedeni}] {istek.Aciklama}".Trim()
        };

        _baglam.StokHareketleri.Add(sh);
        await _baglam.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task StokCikisiYapAsync(StokCikisIstegi istek, Guid sirketId, int kullaniciId)
    {
        YetkiKontrol(y => y.StokSil || y.StokDuzenle, "Manuel Stok Çıkışı");

        if (istek.Miktar <= 0)
            throw new ArgumentException("Çıkış miktarı 0'dan büyük olmalıdır.");

        if (string.IsNullOrWhiteSpace(istek.IslemNedeni))
            throw new ArgumentException("Çıkış işlemi için neden belirtilmesi zorunludur.");
        sirketId = _baglam.SirketIdCoz(sirketId);

        await using var tx = await _baglam.Database.BeginTransactionAsync();
        await DepoUrunKilitleAsync(istek.UrunId, istek.DepoId);

        var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == istek.UrunId && (u.SirketId == null || u.SirketId == sirketId))
            ?? throw new InvalidOperationException("Ürün bulunamadı veya yetkisiz erişim.");

        var depo = await FirmaDeposuAsync(istek.DepoId, sirketId, "Lokasyon/Depo");

        var lokasyon = await _baglam.UrunLokasyonStoklari.FirstOrDefaultAsync(l => l.UrunId == istek.UrunId && l.DepoId == istek.DepoId);
        decimal mevcutKullanilabilir = (lokasyon?.MevcutMiktar ?? 0) - (lokasyon?.RezerveMiktar ?? 0);

        if (mevcutKullanilabilir < istek.Miktar)
            throw new InvalidOperationException($"'{urun.Ad}' ürünü için seçilen lokasyonda yeterli stok yok. Mevcut kullanılabilir: {mevcutKullanilabilir:N2}, istenen: {istek.Miktar:N2}.");

        lokasyon!.MevcutMiktar -= istek.Miktar;
        lokasyon.GuncellemeTarihi = DateTime.UtcNow;
        // Ürün kartındaki toplam stok stok hareketinden otomatik güncellenir (MuhasebeBaglami).

        var sh = new StokHareketi
        {
            SirketId = sirketId,
            UrunId = istek.UrunId,
            DepoId = istek.DepoId,
            KullaniciId = kullaniciId,
            Tarih = DateTime.SpecifyKind(istek.Tarih, DateTimeKind.Utc),
            HareketTuru = StokHareketTuru.Cikis,
            IslemTuru = StokIslemTuru.SarfFire,
            FisNo = $"CKS-{DateTime.UtcNow:yyyyMMddHHmmss}",
            BelgeNo = istek.ReferansNo,
            Miktar = istek.Miktar,
            BirimFiyat = istek.BirimFiyat > 0 ? istek.BirimFiyat : urun.AlisFiyati,
            Aciklama = $"Manuel Çıkış [{istek.IslemNedeni}] {istek.Aciklama}".Trim()
        };

        _baglam.StokHareketleri.Add(sh);
        await _baglam.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<DepoTransferFisi> TransferOlusturAsync(StokTransferIstegi istek, Guid sirketId, int kullaniciId)
    {
        YetkiKontrol(y => y.StokDuzenle, "Depolar Arası Transfer");

        if (istek.KaynakDepoId <= 0 || istek.HedefDepoId <= 0)
            throw new ArgumentException("Kaynak ve hedef depo seçilmesi zorunludur.");

        if (istek.KaynakDepoId == istek.HedefDepoId)
            throw new InvalidOperationException("Kaynak ve hedef depo aynı olamaz.");

        if (istek.Miktar <= 0)
            throw new ArgumentException("Transfer miktarı 0'dan büyük olmalıdır.");
        sirketId = _baglam.SirketIdCoz(sirketId);

        await using var tx = await _baglam.Database.BeginTransactionAsync();
        await DepoUrunKilitleAsync(istek.UrunId, istek.KaynakDepoId, istek.HedefDepoId);

        var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == istek.UrunId && (u.SirketId == null || u.SirketId == sirketId))
            ?? throw new InvalidOperationException("Ürün bulunamadı veya yetkisiz erişim.");

        var kDepo = await FirmaDeposuAsync(istek.KaynakDepoId, sirketId, "Kaynak depo");
        var hDepo = await FirmaDeposuAsync(istek.HedefDepoId, sirketId, "Hedef depo");

        var kLokasyon = await _baglam.UrunLokasyonStoklari.FirstOrDefaultAsync(l => l.UrunId == istek.UrunId && l.DepoId == istek.KaynakDepoId);
        decimal kKullanilabilir = (kLokasyon?.MevcutMiktar ?? 0) - (kLokasyon?.RezerveMiktar ?? 0);

        if (kLokasyon == null || kKullanilabilir < istek.Miktar)
            throw new InvalidOperationException($"'{urun.Ad}' ürünü için kaynak depoda yeterli stok yok. Mevcut: {kKullanilabilir:N2}, istenen: {istek.Miktar:N2}.");

        // Atomic double-entry update
        kLokasyon.MevcutMiktar -= istek.Miktar;
        kLokasyon.GuncellemeTarihi = DateTime.UtcNow;

        var hLokasyon = await _baglam.UrunLokasyonStoklari.FirstOrDefaultAsync(l => l.UrunId == istek.UrunId && l.DepoId == istek.HedefDepoId);
        if (hLokasyon == null)
        {
            hLokasyon = new UrunLokasyonStogu { UrunId = istek.UrunId, DepoId = istek.HedefDepoId, MevcutMiktar = 0, RezerveMiktar = 0 };
            _baglam.UrunLokasyonStoklari.Add(hLokasyon);
        }
        hLokasyon.MevcutMiktar += istek.Miktar;
        hLokasyon.GuncellemeTarihi = DateTime.UtcNow;

        // Sequence numbering TRF-YYYY-XXXXX
        string fisNo = istek.TransferNo?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(fisNo))
        {
            await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(44504649)");
            var count = await _baglam.DepoTransferFisleri.IgnoreQueryFilters().CountAsync();
            fisNo = $"TRF-{DateTime.Today.Year}-{(count + 1):D5}";
        }

        var transferFisi = new DepoTransferFisi
        {
            SirketId = sirketId,
            FisNo = fisNo,
            Tarih = DateTime.SpecifyKind(istek.Tarih, DateTimeKind.Utc),
            KaynakDepoId = istek.KaynakDepoId,
            HedefDepoId = istek.HedefDepoId,
            Durum = DepoTransferDurumu.Tamamlandi,
            Aciklama = istek.Aciklama ?? "Depolar Arası Transfer",
            ToplamMiktar = istek.Miktar,
            ToplamTutar = istek.Miktar * (istek.BirimFiyat > 0 ? istek.BirimFiyat : urun.AlisFiyati),
            Kalemler = new List<DepoTransferKalemi>
            {
                new DepoTransferKalemi
                {
                    UrunId = istek.UrunId,
                    Miktar = istek.Miktar,
                    BirimFiyat = istek.BirimFiyat > 0 ? istek.BirimFiyat : urun.AlisFiyati
                }
            }
        };

        _baglam.DepoTransferFisleri.Add(transferFisi);
        await _baglam.SaveChangesAsync();

        // Stok hareketlerini yaz
        var cikisHareketi = new StokHareketi
        {
            SirketId = sirketId,
            UrunId = istek.UrunId,
            DepoId = istek.KaynakDepoId,
            HedefDepoId = istek.HedefDepoId,
            KullaniciId = kullaniciId,
            Tarih = transferFisi.Tarih,
            HareketTuru = StokHareketTuru.Cikis,
            IslemTuru = StokIslemTuru.DepoTransferi,
            FisNo = fisNo,
            Miktar = istek.Miktar,
            BirimFiyat = transferFisi.Kalemler[0].BirimFiyat,
            IlgiliBelgeId = transferFisi.Id,
            Aciklama = $"{kDepo.DepoAdi} ➔ {hDepo.DepoAdi} Transfer Çıkışı"
        };

        var girisHareketi = new StokHareketi
        {
            SirketId = sirketId,
            UrunId = istek.UrunId,
            DepoId = istek.HedefDepoId,
            HedefDepoId = istek.KaynakDepoId,
            KullaniciId = kullaniciId,
            Tarih = transferFisi.Tarih,
            HareketTuru = StokHareketTuru.Giris,
            IslemTuru = StokIslemTuru.DepoTransferi,
            FisNo = fisNo,
            Miktar = istek.Miktar,
            BirimFiyat = transferFisi.Kalemler[0].BirimFiyat,
            IlgiliBelgeId = transferFisi.Id,
            Aciklama = $"{kDepo.DepoAdi} ➔ {hDepo.DepoAdi} Transfer Girişi"
        };

        _baglam.StokHareketleri.AddRange(cikisHareketi, girisHareketi);
        await _baglam.SaveChangesAsync();
        await tx.CommitAsync();

        return transferFisi;
    }

    public async Task TransferIptalEtAsync(int transferId, Guid sirketId, int kullaniciId, string neden)
    {
        YetkiKontrol(y => y.StokDuzenle || y.StokSil, "Transfer İptali");

        if (string.IsNullOrWhiteSpace(neden))
            throw new ArgumentException("Transfer iptali için neden belirtilmesi zorunludur.");
        sirketId = _baglam.SirketIdCoz(sirketId);

        await using var tx = await _baglam.Database.BeginTransactionAsync();
        // Aynı fişin eşzamanlı iki iptali stoğu iki kez geri almasın: ikinci istek bekler, sonra "zaten iptal" görür.
        await _baglam.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"DepoTransferFisleri\" WHERE \"Id\" = {transferId} FOR UPDATE");

        var tf = await _baglam.DepoTransferFisleri.Include(f => f.Kalemler)
            .FirstOrDefaultAsync(f => f.Id == transferId && (f.SirketId == null || f.SirketId == sirketId))
            ?? throw new InvalidOperationException("Transfer fişi bulunamadı veya yetkisiz erişim.");

        if (tf.Durum == DepoTransferDurumu.Iptal)
            throw new InvalidOperationException("Bu transfer fişi zaten iptal edilmiş.");

        foreach (var k in tf.Kalemler)
        {
            await DepoUrunKilitleAsync(k.UrunId, tf.KaynakDepoId, tf.HedefDepoId);
            // Verify target location has enough stock to reverse the transfer
            var hLokasyon = await _baglam.UrunLokasyonStoklari.FirstOrDefaultAsync(l => l.UrunId == k.UrunId && l.DepoId == tf.HedefDepoId);
            decimal hKullanilabilir = (hLokasyon?.MevcutMiktar ?? 0) - (hLokasyon?.RezerveMiktar ?? 0);

            if (hLokasyon == null || hKullanilabilir < k.Miktar)
                throw new InvalidOperationException("Transfer iptal edilemez: Hedef depodaki stok miktarı transfer edilen miktardan daha az hale gelmiştir.");

            // Reverse stock
            hLokasyon.MevcutMiktar -= k.Miktar;
            hLokasyon.GuncellemeTarihi = DateTime.UtcNow;

            var kLokasyon = await _baglam.UrunLokasyonStoklari.FirstOrDefaultAsync(l => l.UrunId == k.UrunId && l.DepoId == tf.KaynakDepoId);
            if (kLokasyon == null)
            {
                kLokasyon = new UrunLokasyonStogu { UrunId = k.UrunId, DepoId = tf.KaynakDepoId, MevcutMiktar = 0, RezerveMiktar = 0 };
                _baglam.UrunLokasyonStoklari.Add(kLokasyon);
            }
            kLokasyon.MevcutMiktar += k.Miktar;
            kLokasyon.GuncellemeTarihi = DateTime.UtcNow;

            // Flag old movements as cancelled
            var eskiHareketler = await _baglam.StokHareketleri.Where(sh => sh.IlgiliBelgeId == tf.Id && sh.UrunId == k.UrunId && sh.IslemTuru == StokIslemTuru.DepoTransferi).ToListAsync();
            foreach (var sh in eskiHareketler)
                sh.IptalEdildi = true;
        }

        tf.Durum = DepoTransferDurumu.Iptal;
        tf.Aciklama = $"{tf.Aciklama} (İPTAL EDİLDİ: {neden})";
        tf.GuncellemeTarihi = DateTime.UtcNow;

        await _baglam.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<StokSayimi> SayimOlusturAsync(StokSayimIstegi istek, Guid sirketId, int kullaniciId)
    {
        YetkiKontrol(y => y.StokEkle || y.StokDuzenle, "Stok Sayımı Oluşturma");

        if (istek.DepoId <= 0)
            throw new ArgumentException("Sayım yapılacak depo seçilmesi zorunludur.");

        if (istek.Kalemler == null || istek.Kalemler.Count == 0)
            throw new ArgumentException("Sayım fişinde en az 1 ürün kalemi bulunmalıdır.");

        if (istek.Kalemler.Any(k => k.SayilanMiktar < 0))
            throw new ArgumentException("Sayılan miktar eksi olamaz.");

        // Aynı ürün iki satırda olursa onayda depo stoğu iki kez düzeltilir ve ürün kartı toplamı bozulur.
        if (istek.Kalemler.GroupBy(k => k.UrunId).Any(g => g.Count() > 1))
            throw new ArgumentException("Sayım fişinde aynı ürün birden fazla satırda olamaz.");
        sirketId = _baglam.SirketIdCoz(sirketId);

        await using var tx = await _baglam.Database.BeginTransactionAsync();

        var depo = await FirmaDeposuAsync(istek.DepoId, sirketId, "Depo");

        await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(5459405)");
        var count = await _baglam.StokSayimlari.IgnoreQueryFilters().CountAsync();
        string sayimNo = $"SYM-{DateTime.Today.Year}-{(count + 1):D5}";

        var sayim = new StokSayimi
        {
            SirketId = sirketId,
            SayimNo = sayimNo,
            Tarih = DateTime.SpecifyKind(istek.Tarih, DateTimeKind.Utc),
            DepoId = istek.DepoId,
            Durum = StokSayimDurumu.Sayimda,
            Aciklama = istek.Aciklama ?? $"{depo.DepoAdi} Stok Sayımı",
            OlusturanKullaniciId = kullaniciId
        };

        var urunIdleri = istek.Kalemler.Select(k => k.UrunId).ToList();
        var urunler = await _baglam.Urunler.Where(u => urunIdleri.Contains(u.Id) && (u.SirketId == null || u.SirketId == sirketId)).ToDictionaryAsync(u => u.Id);
        var lokasyonlar = await _baglam.UrunLokasyonStoklari.Where(l => l.DepoId == istek.DepoId && urunIdleri.Contains(l.UrunId)).ToDictionaryAsync(l => l.UrunId);

        foreach (var k in istek.Kalemler)
        {
            if (!urunler.TryGetValue(k.UrunId, out var urun)) continue;
            decimal sistemMiktari = lokasyonlar.TryGetValue(k.UrunId, out var l) ? l.MevcutMiktar : 0;

            sayim.Kalemler.Add(new StokSayimKalemi
            {
                UrunId = k.UrunId,
                SistemMiktari = sistemMiktari,
                SayilanMiktar = k.SayilanMiktar,
                BirimFiyat = urun.AlisFiyati
            });
        }

        _baglam.StokSayimlari.Add(sayim);
        await _baglam.SaveChangesAsync();
        await tx.CommitAsync();

        return sayim;
    }

    public async Task SayimOnaylaAsync(int sayimId, Guid sirketId, int kullaniciId)
    {
        YetkiKontrol(y => y.StokDuzenle, "Stok Sayımı Onaylama");
        sirketId = _baglam.SirketIdCoz(sirketId);

        await using var tx = await _baglam.Database.BeginTransactionAsync();
        // Eşzamanlı iki onay (ya da onay + iptal) aynı sayımı iki kez işlemesin.
        await _baglam.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"StokSayimlari\" WHERE \"Id\" = {sayimId} FOR UPDATE");

        var sayim = await _baglam.StokSayimlari.Include(s => s.Kalemler)
            .FirstOrDefaultAsync(s => s.Id == sayimId && s.SirketId == sirketId)
            ?? throw new InvalidOperationException("Sayım fişi bulunamadı veya yetkisiz erişim.");

        if (sayim.Durum == StokSayimDurumu.Onaylandi)
            throw new InvalidOperationException("Bu sayım fişi zaten onaylanmış.");

        if (sayim.Durum == StokSayimDurumu.Iptal)
            throw new InvalidOperationException("İptal edilmiş sayım fişi onaylanamaz.");

        foreach (var k in sayim.Kalemler)
        {
            var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == k.UrunId);
            if (urun == null) continue;

            await DepoUrunKilitleAsync(k.UrunId, sayim.DepoId);
            var lokasyon = await _baglam.UrunLokasyonStoklari.FirstOrDefaultAsync(l => l.UrunId == k.UrunId && l.DepoId == sayim.DepoId);
            if (lokasyon == null)
            {
                lokasyon = new UrunLokasyonStogu { UrunId = k.UrunId, DepoId = sayim.DepoId, MevcutMiktar = 0, RezerveMiktar = 0 };
                _baglam.UrunLokasyonStoklari.Add(lokasyon);
            }

            // Fark onay anındaki depo stoğuna göre alınır: sayım açıldıktan sonra satış/alış olduysa
            // açılıştaki sistem miktarıyla düzeltmek ürün kartı toplamını depo stoğundan ayırırdı.
            k.SistemMiktari = lokasyon.MevcutMiktar;
            decimal fark = k.FarkMiktari;
            if (fark == 0) continue;

            lokasyon.MevcutMiktar = k.SayilanMiktar;
            lokasyon.GuncellemeTarihi = DateTime.UtcNow;
            // Ürün kartındaki toplam stok sayım farkı hareketinden otomatik güncellenir (MuhasebeBaglami).

            var sh = new StokHareketi
            {
                SirketId = sirketId,
                UrunId = k.UrunId,
                DepoId = sayim.DepoId,
                KullaniciId = kullaniciId,
                Tarih = sayim.Tarih,
                HareketTuru = fark > 0 ? StokHareketTuru.Giris : StokHareketTuru.Cikis,
                IslemTuru = StokIslemTuru.SayimFarki,
                FisNo = sayim.SayimNo,
                Miktar = Math.Abs(fark),
                BirimFiyat = k.BirimFiyat,
                IlgiliBelgeId = sayim.Id,
                Aciklama = $"Sayım Düzeltmesi (Sayılan: {k.SayilanMiktar:N0}, Fark: {(fark > 0 ? "+" : "")}{fark:N0})"
            };

            _baglam.StokHareketleri.Add(sh);
        }

        sayim.Durum = StokSayimDurumu.Onaylandi;
        sayim.OnaylayanKullaniciId = kullaniciId;
        sayim.OnayTarihi = DateTime.UtcNow;
        sayim.GuncellemeTarihi = DateTime.UtcNow;

        await _baglam.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task SayimIptalEtAsync(int sayimId, Guid sirketId, int kullaniciId)
    {
        YetkiKontrol(y => y.StokDuzenle || y.StokSil, "Stok Sayımı İptali");
        sirketId = _baglam.SirketIdCoz(sirketId);

        await using var tx = await _baglam.Database.BeginTransactionAsync();
        await _baglam.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"StokSayimlari\" WHERE \"Id\" = {sayimId} FOR UPDATE");
        var sayim = await _baglam.StokSayimlari.FirstOrDefaultAsync(s => s.Id == sayimId && s.SirketId == sirketId)
            ?? throw new InvalidOperationException("Sayım fişi bulunamadı veya yetkisiz erişim.");

        if (sayim.Durum == StokSayimDurumu.Onaylandi)
            throw new InvalidOperationException("Onaylanmış sayım fişi sonradan iptal edilemez.");

        sayim.Durum = StokSayimDurumu.Iptal;
        sayim.GuncellemeTarihi = DateTime.UtcNow;

        await _baglam.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
