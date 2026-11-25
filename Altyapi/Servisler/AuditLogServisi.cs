using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Guvenlik;
using Uygulama.Arayuzler;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class AuditLogServisi : IAuditLogServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi _kullaniciServisi;

    public AuditLogServisi(MuhasebeBaglami baglam, IKullaniciServisi kullaniciServisi)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<List<AuditLog>> LoglariGetirAsync(
        DateTime? baslangic = null, 
        DateTime? bitis = null, 
        string? tabloAdi = null, 
        AuditIslemTuru? islemTuru = null,
        int? kullaniciId = null,
        int limit = 100,
        bool ayrintiSatirlari = true,
        string? arama = null)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.AuditLogGoruntule, "Denetim Kayıtlarını Görüntüleme");
        var sorgu = _baglam.AuditLoglar.AsNoTracking().AsQueryable();

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

        // Fatura kalemi, stok hareketi gibi ana belgeyle kendiliğinden yazılan satırlar istenirse gizlenir.
        if (!ayrintiSatirlari)
        {
            var ayrinti = Uygulama.Servisler.DenetimMetinleri.AyrintiTablolari.ToList();
            sorgu = sorgu.Where(l => !ayrinti.Contains(l.TabloAdi));
        }

        // Arama veritabanında yapılır: kullanıcı, bilgisayar, modül (Türkçe adıyla da), kayıt no ve değişen değerler içinde.
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var desen = "%" + arama.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var tablolar = Uygulama.Servisler.DenetimMetinleri.ModulAdiylaEslesenTablolar(arama.Trim()).ToList();
            sorgu = sorgu.Where(l =>
                EF.Functions.ILike(l.KullaniciAdi ?? "", desen) || EF.Functions.ILike(l.TabloAdi, desen) || tablolar.Contains(l.TabloAdi) ||
                EF.Functions.ILike(l.KayitId, desen) || EF.Functions.ILike(l.IslemDetayi ?? "", desen) ||
                EF.Functions.ILike(l.IstemciBilgisi ?? "", desen) ||
                EF.Functions.ILike(l.EskiDegerJson ?? "", desen) || EF.Functions.ILike(l.YeniDegerJson ?? "", desen));
        }

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
