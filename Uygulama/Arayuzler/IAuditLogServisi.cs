using EtkiAlani.Modeller.Guvenlik;

namespace Uygulama.Arayuzler;

public interface IAuditLogServisi
{
    Task<List<AuditLog>> LoglariGetirAsync(
        DateTime? baslangic = null, 
        DateTime? bitis = null, 
        string? tabloAdi = null, 
        AuditIslemTuru? islemTuru = null,
        int? kullaniciId = null,
        int limit = 100);

    Task LogEkleAsync(AuditLog log);
}
