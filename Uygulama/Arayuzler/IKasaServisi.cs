using EtkiAlani.Modeller.Finans;

namespace Uygulama.Arayuzler;

public interface IKasaServisi
{
    Task<List<KasaHareketi>> TumunuGetirAsync();
    Task<List<KasaHareketi>> TarihAraligindaGetirAsync(DateTime baslangic, DateTime bitis);
    Task<List<KasaHareketi>> KasaHareketleriGetirAsync(int kasaId);
    Task<List<KasaHareketi>> CariHareketleriGetirAsync(int cariKartId);
    Task<KasaHareketi?> GetirByIdAsync(int id);
    Task<KasaHareketi> EkleAsync(KasaHareketi hareket);
    Task<KasaHareketi> TahsilatEkleAsync(int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null);
    Task<KasaHareketi> TahsilatEkleAsync(int kasaId, int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null, decimal dovizTutar = 0, decimal kur = 0);
    Task<KasaHareketi> OdemeEkleAsync(int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null);
    Task<KasaHareketi> OdemeEkleAsync(int kasaId, int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null, decimal dovizTutar = 0, decimal kur = 0);
    Task<bool> KasaVirmanYapAsync(int kaynakKasaId, int hedefKasaId, decimal tutar, string aciklama, DateTime? tarih = null);
    Task<bool> BankayaParaYatirAsync(int kasaId, int bankaHesabiId, decimal tutar, string aciklama, DateTime? tarih = null);
    Task<KasaSayimMutabakat> KasaSayimMutabakatKaydetAsync(KasaSayimMutabakat mutabakat);
    Task<bool> SilAsync(int id);
    Task<decimal> BakiyeHesaplaAsync();
    Task<decimal> KasaBakiyeHesaplaAsync(int kasaId);
    // Dövizli kasada kasanın para birimindeki bakiye (TL kasada 0).
    Task<decimal> KasaDovizBakiyeHesaplaAsync(int kasaId);

    // Kasa Tanım İşlemleri
    Task<Kasa> KasaEkleAsync(Kasa kasa);
    Task<Kasa> KasaGuncelleAsync(Kasa kasa);
    Task<Kasa?> KasaGetirIdAsync(int id);
    Task<IReadOnlyList<Kasa>> KasalariGetirAsync(bool sadeceAktifler = true);
}

