using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class UrunServisi : IUrunServisi
{
    private readonly MuhasebeBaglami _baglam;

    public UrunServisi(MuhasebeBaglami baglam)
    {
        _baglam = baglam;
    }

    public async Task<List<Urun>> TumunuGetirAsync()
    {
        // Global query filter (!Silindi) DbContext üzerinde zaten tanımlı
        return await _baglam.Urunler
            .OrderBy(u => u.Ad)
            .ToListAsync();
    }

    public async Task<Urun?> IdIleGetirAsync(int id)
    {
        return await _baglam.Urunler
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<Urun?> KodIleGetirAsync(string kod)
    {
        return await _baglam.Urunler
            .FirstOrDefaultAsync(u => u.Kod == kod);
    }

    public async Task<Urun> EkleAsync(Urun urun)
    {
        // Ticari İş Kuralları ve Doğrulamalar
        if (string.IsNullOrWhiteSpace(urun.Kod))
            throw new ArgumentException("Ürün kodu boş olamaz.");
        
        if (string.IsNullOrWhiteSpace(urun.Ad))
            throw new ArgumentException("Ürün adı boş olamaz.");

        if (urun.AlisFiyati < 0)
            throw new ArgumentException("Alış fiyatı negatif olamaz.");

        if (urun.SatisFiyati < 0)
            throw new ArgumentException("Satış fiyatı negatif olamaz.");

        if (urun.KdvOrani < 0 || urun.KdvOrani > 100)
            throw new ArgumentException("Geçersiz KDV oranı.");

        var kodVarMi = await KodMevcutMuAsync(urun.Kod);
        if (kodVarMi)
            throw new InvalidOperationException($"'{urun.Kod}' kodlu ürün zaten sistemde mevcut.");

        urun.OlusturmaTarihi = DateTime.UtcNow;
        urun.Silindi = false;

        _baglam.Urunler.Add(urun);
        await _baglam.SaveChangesAsync();
        return urun;
    }

    public async Task<Urun> GuncelleAsync(Urun urun)
    {
        // Ticari İş Kuralları ve Doğrulamalar
        if (string.IsNullOrWhiteSpace(urun.Kod))
            throw new ArgumentException("Ürün kodu boş olamaz.");
        
        if (string.IsNullOrWhiteSpace(urun.Ad))
            throw new ArgumentException("Ürün adı boş olamaz.");

        if (urun.AlisFiyati < 0)
            throw new ArgumentException("Alış fiyatı negatif olamaz.");

        if (urun.SatisFiyati < 0)
            throw new ArgumentException("Satış fiyatı negatif olamaz.");

        var kodVarMi = await KodMevcutMuAsync(urun.Kod, urun.Id);
        if (kodVarMi)
            throw new InvalidOperationException($"'{urun.Kod}' kodlu ürün zaten sistemde mevcut.");

        var mevcutUrun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == urun.Id);
        if (mevcutUrun == null)
            throw new KeyNotFoundException("Güncellenecek ürün bulunamadı.");

        // Değerleri güncelle
        mevcutUrun.Kod = urun.Kod;
        mevcutUrun.Ad = urun.Ad;
        mevcutUrun.Barkod = urun.Barkod;
        mevcutUrun.Kategori = urun.Kategori;
        mevcutUrun.Birim = urun.Birim;
        mevcutUrun.AlisFiyati = urun.AlisFiyati;
        mevcutUrun.SatisFiyati = urun.SatisFiyati;
        mevcutUrun.KdvOrani = urun.KdvOrani;
        mevcutUrun.StokMiktari = urun.StokMiktari;
        mevcutUrun.MinimumStok = urun.MinimumStok;
        mevcutUrun.Aktif = urun.Aktif;
        mevcutUrun.GuncellemeTarihi = DateTime.UtcNow;

        await _baglam.SaveChangesAsync();
        return mevcutUrun;
    }

    public async Task<bool> SilAsync(int id)
    {
        var urun = await _baglam.Urunler.FirstOrDefaultAsync(u => u.Id == id);
        if (urun == null) return false;

        // Soft-delete uygulanıyor
        urun.Silindi = true;
        urun.GuncellemeTarihi = DateTime.UtcNow;
        
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
                        (u.Kategori != null && EF.Functions.ILike(u.Kategori, $"%{aramaMetni}%")))
            .OrderBy(u => u.Ad)
            .ToListAsync();
    }

    public async Task<bool> KodMevcutMuAsync(string kod, int? haricId = null)
    {
        return await _baglam.Urunler
            .AnyAsync(u => u.Kod == kod && (!haricId.HasValue || u.Id != haricId.Value));
    }
}
