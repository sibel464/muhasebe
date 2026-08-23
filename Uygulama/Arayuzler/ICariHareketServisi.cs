using EtkiAlani.Modeller.Cari;

namespace Uygulama.Arayuzler;

public interface ICariHareketServisi
{
    Task<List<CariHareket>> CariKartinHareketleriniGetirAsync(int cariKartId);
    Task<CariHareket> EkleAsync(CariHareket hareket);
    Task<bool> SilAsync(int id);
    Task<decimal> BakiyeHesaplaAsync(int cariKartId);
}
