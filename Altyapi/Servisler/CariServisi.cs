using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Cari;
using Uygulama.Arayuzler;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class CariServisi : ICariServisi
{
    private readonly MuhasebeBaglami _baglam;

    public CariServisi(MuhasebeBaglami baglam)
    {
        _baglam = baglam;
    }

    public async Task<List<CariKart>> TumunuGetirAsync(CariTuru? turFilter = null)
    {
        var sorgu = _baglam.CariKartlar.AsQueryQueryFilterSafe(); // Global query filter already applied

        if (turFilter.HasValue)
        {
            sorgu = sorgu.Where(c => c.Tur == turFilter.Value);
        }

        return await sorgu
            .OrderBy(c => c.Unvan)
            .ToListAsync();
    }

    public async Task<CariKart?> IdIleGetirAsync(int id)
    {
        return await _baglam.CariKartlar
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<CariKart?> KodIleGetirAsync(string kod)
    {
        return await _baglam.CariKartlar
            .FirstOrDefaultAsync(c => c.Kod == kod);
    }

    public async Task<CariKart> EkleAsync(CariKart cari)
    {
        // Temel Ticari Validasyonlar
        if (string.IsNullOrWhiteSpace(cari.Kod))
            throw new ArgumentException("Cari kodu boş olamaz.");

        if (string.IsNullOrWhiteSpace(cari.Unvan))
            throw new ArgumentException("Cari ünvan/ad boş olamaz.");

        // Boşlukları temizle
        cari.Kod = cari.Kod.Trim();
        cari.Unvan = cari.Unvan.Trim();
        if (cari.Ad != null) cari.Ad = cari.Ad.Trim();
        if (cari.Soyad != null) cari.Soyad = cari.Soyad.Trim();

        var kodVarMi = await KodMevcutMuAsync(cari.Kod);
        if (kodVarMi)
            throw new InvalidOperationException($"'{cari.Kod}' kodlu cari kart zaten mevcut.");

        cari.OlusturmaTarihi = DateTime.UtcNow;
        cari.Silindi = false;

        _baglam.CariKartlar.Add(cari);
        await _baglam.SaveChangesAsync();
        return cari;
    }

    public async Task<CariKart> GuncelleAsync(CariKart cari)
    {
        if (string.IsNullOrWhiteSpace(cari.Kod))
            throw new ArgumentException("Cari kodu boş olamaz.");

        if (string.IsNullOrWhiteSpace(cari.Unvan))
            throw new ArgumentException("Cari ünvan/ad boş olamaz.");

        cari.Kod = cari.Kod.Trim();
        cari.Unvan = cari.Unvan.Trim();

        var kodVarMi = await KodMevcutMuAsync(cari.Kod, cari.Id);
        if (kodVarMi)
            throw new InvalidOperationException($"'{cari.Kod}' kodlu cari kart zaten mevcut.");

        var mevcutCari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == cari.Id);
        if (mevcutCari == null)
            throw new KeyNotFoundException("Güncellenecek cari kart bulunamadı.");

        // Güncelleme
        mevcutCari.Kod = cari.Kod;
        mevcutCari.Tur = cari.Tur;
        mevcutCari.Tip = cari.Tip;
        mevcutCari.Unvan = cari.Unvan;
        mevcutCari.Ad = cari.Ad;
        mevcutCari.Soyad = cari.Soyad;
        mevcutCari.VergiDairesi = cari.VergiDairesi;
        mevcutCari.VergiNo = cari.VergiNo;
        mevcutCari.TcNo = cari.TcNo;
        mevcutCari.Telefon = cari.Telefon;
        mevcutCari.CepTelefonu = cari.CepTelefonu;
        mevcutCari.Eposta = cari.Eposta;
        mevcutCari.WebSitesi = cari.WebSitesi;
        mevcutCari.Adres = cari.Adres;
        mevcutCari.Il = cari.Il;
        mevcutCari.Ilce = cari.Ilce;
        mevcutCari.PostaKodu = cari.PostaKodu;
        mevcutCari.Aciklama = cari.Aciklama;
        mevcutCari.Aktif = cari.Aktif;
        mevcutCari.GuncellemeTarihi = DateTime.UtcNow;

        await _baglam.SaveChangesAsync();
        return mevcutCari;
    }

    public async Task<bool> SilAsync(int id)
    {
        var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.Id == id);
        if (cari == null) return false;

        // Ticari İlişki Güvenlik Kontrolü
        var islemGormus = await IslemGormusMuAsync(id);
        if (islemGormus)
            throw new InvalidOperationException("Bu cari kart geçmiş işlemler nedeniyle silinemez. Pasif duruma alınabilir.");

        cari.Silindi = true;
        cari.GuncellemeTarihi = DateTime.UtcNow;

        await _baglam.SaveChangesAsync();
        return true;
    }

    public async Task<List<CariKart>> AraAsync(string aramaMetni, CariTuru? turFilter = null)
    {
        if (string.IsNullOrWhiteSpace(aramaMetni))
            return await TumunuGetirAsync(turFilter);

        var sorgu = _baglam.CariKartlar.AsQueryable();

        if (turFilter.HasValue)
        {
            sorgu = sorgu.Where(c => c.Tur == turFilter.Value);
        }

        return await sorgu
            .Where(c => EF.Functions.ILike(c.Kod, $"%{aramaMetni}%") ||
                        EF.Functions.ILike(c.Unvan, $"%{aramaMetni}%") ||
                        (c.Ad != null && EF.Functions.ILike(c.Ad, $"%{aramaMetni}%")) ||
                        (c.Soyad != null && EF.Functions.ILike(c.Soyad, $"%{aramaMetni}%")) ||
                        (c.VergiNo != null && EF.Functions.ILike(c.VergiNo, $"%{aramaMetni}%")) ||
                        (c.Telefon != null && EF.Functions.ILike(c.Telefon, $"%{aramaMetni}%")))
            .OrderBy(c => c.Unvan)
            .ToListAsync();
    }

    public async Task<bool> KodMevcutMuAsync(string kod, int? haricId = null)
    {
        return await _baglam.CariKartlar
            .AnyAsync(c => c.Kod == kod && (!haricId.HasValue || c.Id != haricId.Value));
    }

    public async Task<bool> IslemGormusMuAsync(int id)
    {
        // Müşteriyle/cariyle ilişkili fatura var mı kontrol et
        return await _baglam.Faturalar.AnyAsync(f => f.CariKartId == id);
    }
}

// Global sorgu filtrelerinin düzgün çalışmasını sağlamak için yardımcı uzantı metodu
internal static class QueryExtensions
{
    public static IQueryable<T> AsQueryQueryFilterSafe<T>(this DbSet<T> dbSet) where T : class
    {
        return dbSet.AsQueryable();
    }
}
