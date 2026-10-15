using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Siparis;
using Uygulama.Arayuzler;
using Altyapi.Veri;
using Altyapi.Yardimcilar;
using Npgsql;

namespace Altyapi.Servisler;

public class UrunServisi : IUrunServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public UrunServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<List<Urun>> TumunuGetirAsync()
    {
        // Global query filter (!Silindi) DbContext üzerinde zaten tanımlı
        return await _baglam.Urunler
            .Include(u => u.KategoriTanimi).Include(u => u.MarkaTanimi).Include(u => u.BirimTanimi).Include(u => u.Depo)
            .OrderBy(u => u.Ad)
            .ToListAsync();
    }

    public async Task<Urun?> IdIleGetirAsync(int id)
    {
        return await _baglam.Urunler
            .Include(u => u.KategoriTanimi).Include(u => u.MarkaTanimi).Include(u => u.BirimTanimi).Include(u => u.Depo)
            .Include(u => u.SatisFiyatlari).Include(u => u.LokasyonStoklari).ThenInclude(l => l.Depo)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    // Ürün kartı ve fiyat listesi tek işlemde kaydedilir; biri başarısız olursa hiçbiri kaydedilmez.
    public async Task<Urun> KartGuncelleAsync(Urun urun, List<UrunSatisFiyati> fiyatlar)
    {
        await using var tx = await _baglam.Database.BeginTransactionAsync();
        var sonuc = await GuncelleAsync(urun);
        await FiyatlariKaydetAsync(urun.Id, fiyatlar);
        await tx.CommitAsync();
        return sonuc;
    }

    public async Task<Urun?> KodIleGetirAsync(string kod)
    {
        return await _baglam.Urunler
            .Include(u => u.KategoriTanimi).Include(u => u.MarkaTanimi).Include(u => u.BirimTanimi).Include(u => u.Depo)
            .FirstOrDefaultAsync(u => u.Kod == kod);
    }

    public async Task<Urun> EkleAsync(Urun urun)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.StokEkle, "Stok Kartı Ekleme");

        // Ticari İş Kuralları ve Doğrulamalar
        if (string.IsNullOrWhiteSpace(urun.Kod))
            throw new ArgumentException("Ürün kodu boş olamaz.");
        
        if (string.IsNullOrWhiteSpace(urun.Ad))
            throw new ArgumentException("Ürün adı boş olamaz.");

        if (urun.AlisFiyati < 0)
            throw new ArgumentException("Alış fiyatı negatif olamaz.");

        if (urun.PerakendeSatisFiyati < 0 || urun.ToptanSatisFiyati < 0)
            throw new ArgumentException("Satış fiyatları negatif olamaz.");

        if (urun.KdvOrani < 0 || urun.KdvOrani > 100)
            throw new ArgumentException("Geçersiz KDV oranı.");

        urun.Kod = urun.Kod.Trim().ToUpperInvariant();
        var kodVarMi = await KodMevcutMuAsync(urun.Kod);
        if (kodVarMi)
            throw new InvalidOperationException($"'{urun.Kod}' kodlu ürün zaten sistemde mevcut.");

        // Peşin (tek çekim) fiyat girildiyse fatura/sipariş/irsaliyenin kullandığı satış fiyatı da odur.
        var tekCekim = TekCekimTlFiyati(urun.SatisFiyatlari);
        if (tekCekim != null)
            urun.PerakendeSatisFiyati = tekCekim.Fiyat;

        urun.OlusturmaTarihi = DateTime.UtcNow;
        urun.Silindi = false;

        urun.Kod = urun.Kod.Trim().ToUpperInvariant();
        if (urun.Barkod != null) urun.Barkod = urun.Barkod.Trim();
        if (urun.Barkod != null && await BarkodMevcutMuAsync(urun.Barkod))
            throw new InvalidOperationException("Bu barkod zaten kayıtlı: " + (await BarkodIleGetirAsync(urun.Barkod))?.Ad);

        await AcilisStogunuHazirlaAsync(urun);
        _baglam.Urunler.Add(urun);
        try
        {
            await _baglam.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new InvalidOperationException("Bu ürün kodu veya barkod zaten kullanılıyor.", ex); }
        return urun;
    }

    public async Task<Urun> GuvenliEkleAsync(Urun urun, bool otomatikKod, bool otomatikBarkod)
    {
        await using var tx = await _baglam.Database.BeginTransactionAsync();
        try
        {
            await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(5552554)");
            var uretici = new UrunKodBarkodUreteci(_baglam);
            if (otomatikKod) urun.Kod = await uretici.SonrakiKodUretAsync();
            if (otomatikBarkod) urun.Barkod = await uretici.SonrakiBarkodUretAsync();
            var sonuc = await EkleAsync(urun); await tx.CommitAsync(); return sonuc;
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    // Açılış stoğu yalnızca ürün ilk kaydedilirken girilir; sonraki her değişiklik stok fişiyle yapılır.
    // Her depo için bir "açılış stoğu" giriş hareketi yazılır, böylece stok miktarı hareketlerle hep tutar.
    private async Task AcilisStogunuHazirlaAsync(Urun urun)
    {
        var lokasyonlar = urun.LokasyonStoklari.Where(l => l.MevcutMiktar != 0).ToList();
        if (lokasyonlar.Any(l => l.MevcutMiktar < 0))
            throw new ArgumentException("Açılış stoğu negatif olamaz.");
        if (lokasyonlar.GroupBy(l => l.DepoId).Any(g => g.Count() > 1))
            throw new ArgumentException("Aynı depoya iki kez açılış stoğu girilemez.");

        var depoIdleri = lokasyonlar.Select(l => l.DepoId).ToList();
        var aktifDepolar = await _baglam.Depolar.Where(d => depoIdleri.Contains(d.Id) && d.AktifMi).Select(d => d.Id).ToListAsync();
        if (aktifDepolar.Count != depoIdleri.Count)
            throw new InvalidOperationException("Açılış stoğu girilen depolardan biri bulunamadı veya pasif.");

        urun.LokasyonStoklari = lokasyonlar;
        urun.StokMiktari = lokasyonlar.Sum(l => l.MevcutMiktar);
        // Hareket tarihi uygulama kuralına göre yerel saatle (UTC etiketiyle) yazılır; listeler dönüştürmeden gösterir.
        var simdi = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
        foreach (var lokasyon in lokasyonlar)
        {
            lokasyon.RezerveMiktar = 0;
            _baglam.StokHareketleri.Add(new StokHareketi
            {
                Urun = urun,
                DepoId = lokasyon.DepoId,
                SirketId = urun.SirketId,
                KullaniciId = _kullaniciServisi?.AktifKullanici?.Id,
                Tarih = simdi,
                HareketTuru = StokHareketTuru.Giris,
                IslemTuru = StokIslemTuru.DevirGiris,
                FisNo = $"ACL-{simdi:yyyyMMddHHmmss}",
                Miktar = lokasyon.MevcutMiktar,
                BirimFiyat = urun.AlisFiyati,
                Aciklama = "Açılış stoğu (ürün kartı)"
            });
        }
    }

    public Task<string> OtomatikKodOnizleAsync() => new UrunKodBarkodUreteci(_baglam).SonrakiKodUretAsync();
    public Task<string> OtomatikBarkodOnizleAsync() => new UrunKodBarkodUreteci(_baglam).SonrakiBarkodUretAsync();
    public Task<bool> BarkodMevcutMuAsync(string barkod, int? haricId = null) => _baglam.Urunler.AnyAsync(x => x.Barkod == barkod.Trim() && (!haricId.HasValue || x.Id != haricId));
    public async Task<Urun?> BarkodIleGetirAsync(string barkod) => await _baglam.Urunler
        .Include(x => x.KategoriTanimi).Include(x => x.MarkaTanimi).Include(x => x.BirimTanimi)
        .Include(x => x.SatisFiyatlari).Include(x => x.LokasyonStoklari).ThenInclude(x => x.Depo)
        .FirstOrDefaultAsync(x => x.Barkod == barkod.Trim());
    public Task<List<string>> KullanilanKodlariGetirAsync() => _baglam.Urunler.Select(x => x.Kod).OrderBy(x => x).ToListAsync();
    public Task<List<string>> KullanilanBarkodlariGetirAsync() => _baglam.Urunler.Where(x => x.Barkod != null).Select(x => x.Barkod!).OrderBy(x => x).ToListAsync();

    public async Task<Urun> GuncelleAsync(Urun urun)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.StokDuzenle, "Stok Kartı Güncelleme");

        // Ticari İş Kuralları ve Doğrulamalar
        if (string.IsNullOrWhiteSpace(urun.Kod))
            throw new ArgumentException("Ürün kodu boş olamaz.");
        
        if (string.IsNullOrWhiteSpace(urun.Ad))
            throw new ArgumentException("Ürün adı boş olamaz.");

        if (urun.AlisFiyati < 0)
            throw new ArgumentException("Alış fiyatı negatif olamaz.");

        if (urun.PerakendeSatisFiyati < 0 || urun.ToptanSatisFiyati < 0)
            throw new ArgumentException("Satış fiyatları negatif olamaz.");

        if (urun.KdvOrani < 0 || urun.KdvOrani > 100)
            throw new ArgumentException("Geçersiz KDV oranı.");

        urun.Kod = urun.Kod.Trim().ToUpperInvariant();
        var kodVarMi = await KodMevcutMuAsync(urun.Kod, urun.Id);
        if (kodVarMi)
            throw new InvalidOperationException($"'{urun.Kod}' kodlu ürün zaten sistemde mevcut.");

        urun.Barkod = string.IsNullOrWhiteSpace(urun.Barkod) ? null : urun.Barkod.Trim();
        if (urun.Barkod != null && await BarkodMevcutMuAsync(urun.Barkod, urun.Id))
            throw new InvalidOperationException("Bu barkod başka bir üründe kullanılıyor.");

        var mevcutUrun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == urun.Id);
        if (mevcutUrun == null)
            throw new KeyNotFoundException("Güncellenecek ürün bulunamadı.");

        // Snapshot ve Değer Güncellemeleri
        mevcutUrun.Kod = urun.Kod;
        mevcutUrun.Ad = urun.Ad;
        mevcutUrun.Barkod = urun.Barkod;
        mevcutUrun.Aciklama = urun.Aciklama;
        mevcutUrun.Kategori = urun.Kategori;
        mevcutUrun.Birim = urun.Birim;
        mevcutUrun.KategoriTanimiId = urun.KategoriTanimiId;
        mevcutUrun.MarkaTanimiId = urun.MarkaTanimiId;
        mevcutUrun.BirimTanimiId = urun.BirimTanimiId;
        mevcutUrun.DepoId = urun.DepoId;
        mevcutUrun.AlisFiyati = urun.AlisFiyati;
        mevcutUrun.PerakendeSatisFiyati = urun.PerakendeSatisFiyati;
        mevcutUrun.ToptanSatisFiyati = urun.ToptanSatisFiyati;
        // POS önce tek çekim fiyatına bakar; satış fiyatı değişince o da aynı kalmalı.
        var tekCekimFiyati = TekCekimTlFiyati(await _baglam.UrunSatisFiyatlari.Where(f => f.UrunId == urun.Id).ToListAsync());
        if (tekCekimFiyati != null)
            tekCekimFiyati.Fiyat = urun.PerakendeSatisFiyati;
        mevcutUrun.KdvOrani = urun.KdvOrani;
        // Stok miktarı bilerek güncellenmez: kart düzenlemesi stoğu değiştiremez, stok yalnızca
        // fatura/irsaliye/stok giriş-çıkış/sayım fişleriyle (hareket kaydı bırakarak) değişir.
        mevcutUrun.MinimumStok = urun.MinimumStok;
        mevcutUrun.Aktif = urun.Aktif;
        mevcutUrun.GuncellemeTarihi = DateTime.UtcNow;

        await _baglam.SaveChangesAsync();
        return mevcutUrun;
    }

    public async Task<bool> SilAsync(int id, string? silmeNedeni = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.StokSil, "Stok Kartı Silme");

        if (string.IsNullOrWhiteSpace(silmeNedeni) || silmeNedeni.Trim().Length < 3)
            throw new ArgumentException("Silme nedeni yazılmalıdır (en az 3 karakter).");

        var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == id);
        if (urun == null) return false;

        // Stoğu olan ürün silinirse elde olan mal kayıtlardan kaybolur; önce stok çıkışı/sayım yapılmalı.
        var lokasyonStogu = await _baglam.UrunLokasyonStoklari.Where(l => l.UrunId == id)
            .Select(l => new { l.MevcutMiktar, l.RezerveMiktar }).ToListAsync();
        var stok = lokasyonStogu.Count > 0 ? lokasyonStogu.Sum(l => l.MevcutMiktar) : urun.StokMiktari;
        if (stok != 0)
            throw new InvalidOperationException(
                $"'{urun.Ad}' ürününün {stok:N2} {urun.Birim} stoğu var, silinemez. " +
                "Önce Ürünler ekranından Stok Çıkışı veya Stok Sayımı ile stoğu sıfırlayın ya da ürünü kartından pasif yapın.");
        if (lokasyonStogu.Sum(l => l.RezerveMiktar) != 0)
            throw new InvalidOperationException($"'{urun.Ad}' ürünü için rezerve edilmiş stok var, silinemez.");

        var acikSiparisVar = await _baglam.SiparisKalemleri.AnyAsync(k => k.UrunId == id && k.Siparis != null &&
            (k.Siparis.Durum == SiparisDurumu.Taslak || k.Siparis.Durum == SiparisDurumu.Beklemede ||
             k.Siparis.Durum == SiparisDurumu.Onaylandi || k.Siparis.Durum == SiparisDurumu.KismenKarsilandi));
        if (acikSiparisVar)
            throw new InvalidOperationException($"'{urun.Ad}' ürünü açık bir siparişte geçiyor, silinemez. Önce siparişi tamamlayın veya iptal edin.");

        // Kayıt fiziksel olarak silinmez; geçmiş faturalar, hareketler ve raporlar ürünü görmeye devam eder.
        urun.Silindi = true;
        urun.Aktif = false;
        urun.GuncellemeTarihi = DateTime.UtcNow;
        urun.SilinmeTarihi = DateTime.UtcNow;
        urun.SilinmeNedeni = $"{silmeNedeni.Trim()} (Silen: {_kullaniciServisi?.AktifKullanici?.KullaniciAdi ?? "?"})";

        await _baglam.SaveChangesAsync();
        return true;
    }

    public async Task<List<Urun>> SilinenleriGetirAsync()
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.StokGoruntule, "Silinen ürünleri görüntüleme");
        return await _baglam.Urunler.IgnoreQueryFilters([MuhasebeBaglami.SilinmisUrunFiltresi]).AsNoTracking()
            .Where(u => u.Silindi)
            .OrderByDescending(u => u.SilinmeTarihi)
            .ToListAsync();
    }

    // Silinen ürünü geri getirir. Kodu veya barkodu bu arada yeni bir ürüne verildiyse geri alınamaz.
    public async Task GeriAlAsync(int id)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.StokSil, "Silinen ürünü geri alma");

        var urun = await _baglam.Urunler.IgnoreQueryFilters([MuhasebeBaglami.SilinmisUrunFiltresi])
            .FirstOrDefaultAsync(u => u.Id == id && u.Silindi)
            ?? throw new KeyNotFoundException("Geri alınacak ürün bulunamadı.");

        if (await KodMevcutMuAsync(urun.Kod))
            throw new InvalidOperationException($"'{urun.Kod}' kodu artık başka bir üründe kullanılıyor; bu ürün geri alınamaz.");
        if (urun.Barkod != null && await BarkodMevcutMuAsync(urun.Barkod))
            throw new InvalidOperationException($"'{urun.Barkod}' barkodu artık başka bir üründe kullanılıyor; bu ürün geri alınamaz.");

        urun.Silindi = false;
        urun.Aktif = true;
        urun.SilinmeTarihi = null;
        urun.SilinmeNedeni = null;
        urun.GuncellemeTarihi = DateTime.UtcNow;
        await _baglam.SaveChangesAsync();
    }

    public async Task<List<Urun>> AraAsync(string aramaMetni)
    {
        if (string.IsNullOrWhiteSpace(aramaMetni))
            return await TumunuGetirAsync();

        return await _baglam.Urunler
            .Where(u => EF.Functions.ILike(u.Kod, $"%{aramaMetni}%") || 
                        EF.Functions.ILike(u.Ad, $"%{aramaMetni}%") || 
                        (u.Barkod != null && EF.Functions.ILike(u.Barkod, $"%{aramaMetni}%")) ||
                        (u.Kategori != null && EF.Functions.ILike(u.Kategori, $"%{aramaMetni}%")) ||
                        (u.MarkaTanimi != null && EF.Functions.ILike(u.MarkaTanimi.Ad, $"%{aramaMetni}%")))
            .OrderBy(u => u.Ad)
            .ToListAsync();
    }

    public async Task<List<Urun>> FiltreliAraAsync(UrunFiltre filtre)
    {
        var sorgu = _baglam.Urunler.AsQueryable();

        if (filtre.SadeceAktifler)
            sorgu = sorgu.Where(u => u.Aktif);

        if (!string.IsNullOrWhiteSpace(filtre.Kategori))
            sorgu = sorgu.Where(u => u.Kategori != null && EF.Functions.ILike(u.Kategori, $"%{filtre.Kategori}%"));

        if (!string.IsNullOrWhiteSpace(filtre.AramaMetni))
        {
            sorgu = sorgu.Where(u => EF.Functions.ILike(u.Kod, $"%{filtre.AramaMetni}%") ||
                                     EF.Functions.ILike(u.Ad, $"%{filtre.AramaMetni}%") ||
                                     (u.Barkod != null && EF.Functions.ILike(u.Barkod, $"%{filtre.AramaMetni}%")));
        }

        if (filtre.SadeceKritikStok)
        {
            sorgu = sorgu.Where(u => u.StokMiktari <= u.MinimumStok);
        }

        return await sorgu
            .OrderBy(u => u.Ad)
            .ToListAsync();
    }

    public async Task<bool> KodMevcutMuAsync(string kod, int? haricId = null)
    {
        return await _baglam.Urunler
            .AnyAsync(u => u.Kod == kod && (!haricId.HasValue || u.Id != haricId.Value));
    }

    public async Task<List<UrunSatisFiyati>> FiyatlariGetirAsync(int urunId)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.StokGoruntule, "Ürün fiyatlarını görüntüleme");

        return await _baglam.UrunSatisFiyatlari
            .Where(f => f.UrunId == urunId)
            .OrderBy(f => f.TaksitSayisi)
            .ThenBy(f => f.ParaBirimiId)
            .ToListAsync();
    }

    public async Task FiyatlariKaydetAsync(int urunId, List<UrunSatisFiyati> fiyatlar)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.StokDuzenle, "Ürün fiyatlarını güncelleme");

        var mevcutFiyatlar = await _baglam.UrunSatisFiyatlari
            .Where(f => f.UrunId == urunId)
            .ToListAsync();

        _baglam.UrunSatisFiyatlari.RemoveRange(mevcutFiyatlar);
        foreach (var fiyat in fiyatlar)
        {
            fiyat.Id = 0;
            fiyat.UrunId = urunId;
            fiyat.Urun = null;
        }

        await _baglam.UrunSatisFiyatlari.AddRangeAsync(fiyatlar);

        var tekCekim = TekCekimTlFiyati(fiyatlar);
        if (tekCekim != null)
        {
            var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == urunId);
            if (urun != null)
            {
                urun.PerakendeSatisFiyati = tekCekim.Fiyat;
                urun.GuncellemeTarihi = DateTime.UtcNow;
            }
        }

        await _baglam.SaveChangesAsync();
    }

    // Para birimi boş kayıtlar eski TL fiyatlarıdır (ParaBirimiId 1 = TRY).
    private static UrunSatisFiyati? TekCekimTlFiyati(IEnumerable<UrunSatisFiyati> fiyatlar) =>
        fiyatlar.FirstOrDefault(f => f.TaksitSayisi == 1 && f.AktifMi && (f.ParaBirimiId == null || f.ParaBirimiId == 1));

    public async Task<List<UrunLokasyonStogu>> LokasyonStoklariniGetirAsync(int urunId)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.StokGoruntule, "Ürün lokasyon stoklarını görüntüleme");

        return await _baglam.UrunLokasyonStoklari
            .Include(s => s.Depo)
            .Where(s => s.UrunId == urunId)
            .OrderBy(s => s.Depo!.DepoAdi)
            .ToListAsync();
    }

    public async Task<Dictionary<int, decimal>> DepoStoklariniGetirAsync(int depoId)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.StokGoruntule, "Depo stoklarını görüntüleme");

        return await _baglam.UrunLokasyonStoklari.AsNoTracking()
            .Where(s => s.DepoId == depoId)
            .GroupBy(s => s.UrunId)
            .Select(g => new { UrunId = g.Key, Miktar = g.Sum(x => x.MevcutMiktar) })
            .ToDictionaryAsync(x => x.UrunId, x => x.Miktar);
    }

    public async Task<List<Depo>> DepolariGetirAsync()
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.StokGoruntule, "Depoları görüntüleme");

        return await _baglam.Depolar
            .Include(d => d.Sube)
            .Where(d => d.AktifMi)
            .OrderBy(d => d.DepoAdi)
            .ToListAsync();
    }
}
