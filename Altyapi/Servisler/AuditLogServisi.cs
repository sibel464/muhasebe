using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Guvenlik;
using Uygulama.Arayuzler;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class AuditLogServisi : IAuditLogServisi
{
    private readonly MuhasebeBaglami _baglam;

    public AuditLogServisi(MuhasebeBaglami baglam)
    {
        _baglam = baglam;
    }

    public async Task<List<AuditLog>> LoglariGetirAsync(
        DateTime? baslangic = null, 
        DateTime? bitis = null, 
        string? tabloAdi = null, 
        AuditIslemTuru? islemTuru = null,
        int? kullaniciId = null,
        int limit = 100)
    {
        var sorgu = _baglam.AuditLoglar.AsQueryable();

        if (baslangic.HasValue)
            sorgu = sorgu.Where(l => l.Tarih >= baslangic.Value);

        if (bitis.HasValue)
            sorgu = sorgu.Where(l => l.Tarih <= bitis.Value);

        if (!string.IsNullOrWhiteSpace(tabloAdi))
            sorgu = sorgu.Where(l => l.TabloAdi == tabloAdi);

        if (islemTuru.HasValue)
            sorgu = sorgu.Where(l => l.IslemTuru == islemTuru.Value);

        if (kullaniciId.HasValue)
            sorgu = sorgu.Where(l => l.KullaniciId == kullaniciId.Value);

        sorgu = sorgu.OrderByDescending(l => l.Tarih);
        if (limit > 0 && limit < int.MaxValue)
            sorgu = sorgu.Take(limit);

        return await sorgu.ToListAsync();
    }

    public async Task LogEkleAsync(AuditLog log)
    {
        _baglam.AuditLoglar.Add(log);
        await _baglam.SaveChangesAsync();
    }
}
