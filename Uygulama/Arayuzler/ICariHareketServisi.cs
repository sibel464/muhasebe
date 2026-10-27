using EtkiAlani.Modeller.Cari;

namespace Uygulama.Arayuzler;

public interface ICariHareketServisi
{
    Task<List<CariHareket>> CariKartinHareketleriniGetirAsync(int cariKartId);
    Task<List<(string Kod, string Ad)>> KarsiHesaplariGetirAsync(Guid sirketId);
    // Elle cari fişi: cari hareketi + muhasebe fişi (karşı hesap seçilen hesap) birlikte yazılır.
    Task<CariHareket> EkleAsync(CariHareket hareket, string karsiHesapKodu);
    Task<bool> SilAsync(int id);
    Task<decimal> BakiyeHesaplaAsync(int cariKartId);
    // Toplu mutabakat için: verilen günün sonundaki (borç - alacak) bakiyeler, hareketi olan her cari için.
    Task<Dictionary<int, decimal>> TarihtekiBakiyelerAsync(DateTime tarih);
}
