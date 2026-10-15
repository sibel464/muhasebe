using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
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
            .FirstOrDefaultAsync(u => u.Id == id);
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
        _baglam.Urunler.Add(urun);
        try 
        { 
            await _baglam.SaveChangesAsync(); 

            // Yeni ürün oluşturulurken varsayılan lokasyon stoklarını ilklendir
            var mevcutLokasyonSayisi = await _baglam.UrunLokasyonStoklari.CountAsync(x => x.UrunId == urun.Id);
            if (mevcutLokasyonSayisi == 0)
            {
                _baglam.UrunLokasyonStoklari.AddRange(
                    new UrunLokasyonStogu { UrunId = urun.Id, DepoId = 1, MevcutMiktar = urun.StokMiktari, RezerveMiktar = 0 },
                    new UrunLokasyonStogu { UrunId = urun.Id, DepoId = 2, MevcutMiktar = 0, RezerveMiktar = 0 },
                    new UrunLokasyonStogu { UrunId = urun.Id, DepoId = 3, MevcutMiktar = 0, RezerveMiktar = 0 }
                );
                await _baglam.SaveChangesAsync();
            }
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

        var kodVarMi = await KodMevcutMuAsync(urun.Kod, urun.Id);
        if (kodVarMi)
            throw new InvalidOperationException($"'{urun.Kod}' kodlu ürün zaten sistemde mevcut.");

        urun.Kod = urun.Kod.Trim().ToUpperInvariant();
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
        var stokFarki = urun.StokMiktari - mevcutUrun.StokMiktari;
        mevcutUrun.StokMiktari = urun.StokMiktari;
        if (stokFarki != 0)
        {
            var magazaStok = await _baglam.UrunLokasyonStoklari.FirstOrDefaultAsync(s => s.UrunId == urun.Id && s.DepoId == 1);
            if (magazaStok == null)
            {
                magazaStok = new UrunLokasyonStogu { UrunId = urun.Id, DepoId = 1, MevcutMiktar = urun.StokMiktari, RezerveMiktar = 0 };
                _baglam.UrunLokasyonStoklari.Add(magazaStok);
            }
            else
            {
                magazaStok.MevcutMiktar += stokFarki;
                magazaStok.GuncellemeTarihi = DateTime.UtcNow;
            }
        }

        mevcutUrun.MinimumStok = urun.MinimumStok;
        mevcutUrun.Aktif = urun.Aktif;
        mevcutUrun.GuncellemeTarihi = DateTime.UtcNow;

        await _baglam.SaveChangesAsync();
        return mevcutUrun;
    }

    public async Task<bool> SilAsync(int id, string? silmeNedeni = null)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.StokSil, "Stok Kartı Silme");

        var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == id);
        if (urun == null) return false;

        // Soft-delete uygulanıyor
        urun.Silindi = true;
        urun.GuncellemeTarihi = DateTime.UtcNow;
        urun.SilinmeTarihi = DateTime.UtcNow;
        urun.SilinmeNedeni = silmeNedeni;
        
        await _baglam.SaveChangesAsync();
        return true;
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

    public async Task LokasyonStoklariniKaydetAsync(int urunId, List<UrunLokasyonStogu> stoklar)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.StokDuzenle, "Ürün lokasyon stoklarını güncelleme");

        var mevcutStoklar = await _baglam.UrunLokasyonStoklari
            .Where(s => s.UrunId == urunId)
            .ToListAsync();

        _baglam.UrunLokasyonStoklari.RemoveRange(mevcutStoklar);
        foreach (var stok in stoklar)
        {
            stok.Id = 0;
            stok.UrunId = urunId;
            stok.Urun = null;
            stok.Depo = null;
        }

        await _baglam.UrunLokasyonStoklari.AddRangeAsync(stoklar);

        var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == urunId);
        if (urun != null)
        {
            urun.StokMiktari = stoklar.Sum(s => s.MevcutMiktar);
            urun.GuncellemeTarihi = DateTime.UtcNow;
        }

        await _baglam.SaveChangesAsync();
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
