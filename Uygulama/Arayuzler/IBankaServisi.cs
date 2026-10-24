using EtkiAlani.Modeller.Finans;

namespace Uygulama.Arayuzler;

public interface IBankaServisi
{
    Task<List<BankaHareketi>> TumunuGetirAsync();
    Task<List<BankaHareketi>> TarihAraligindaGetirAsync(DateTime baslangic, DateTime bitis);
    Task<List<BankaHareketi>> HesapHareketleriGetirAsync(int bankaHesabiId);
    Task<List<BankaHareketi>> CariHareketleriGetirAsync(int cariKartId);
    Task<BankaHareketi?> GetirByIdAsync(int id);
    Task<BankaHareketi> EkleAsync(BankaHareketi hareket);
    Task<BankaHareketi> TahsilatEkleAsync(int cariKartId, string bankaAdi, string hesapNo, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null);
    Task<BankaHareketi> OdemeEkleAsync(int cariKartId, string bankaAdi, string hesapNo, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null);
    Task<BankaHareketi> HesabaTahsilatEkleAsync(int bankaHesabiId, int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null, decimal dovizTutar = 0, decimal kur = 0);
    Task<BankaHareketi> HesaptanOdemeEkleAsync(int bankaHesabiId, int cariKartId, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null, decimal dovizTutar = 0, decimal kur = 0);
    Task<bool> BankaVirmanYapAsync(int kaynakBankaHesabiId, int hedefBankaHesabiId, decimal tutar, string aciklama, DateTime? tarih = null);
    Task<bool> BankadanKasaCekAsync(int bankaHesabiId, int kasaId, decimal tutar, string aciklama, DateTime? tarih = null);
    Task<bool> PosBlokeHesabaGecirAsync(int posHesapId, int vadesizHesapId, decimal tutar, decimal komisyonTutari, string aciklama, DateTime? tarih = null);
    Task<bool> SilAsync(int id);
    Task<decimal> BakiyeHesaplaAsync();
    Task<decimal> HesapBakiyeHesaplaAsync(string bankaAdi, string? hesapNo = null);
    Task<decimal> HesapBakiyeHesaplaAsync(int bankaHesabiId);
    // Dövizli hesapta hesabın para birimindeki bakiye (TL hesapta 0).
    Task<decimal> HesapDovizBakiyeHesaplaAsync(int bankaHesabiId);

    // Banka Hesabı Tanım İşlemleri
    Task<BankaHesabi> BankaHesabiEkleAsync(BankaHesabi hesap);
    Task<BankaHesabi> BankaHesabiGuncelleAsync(BankaHesabi hesap);
    Task<BankaHesabi?> BankaHesabiGetirIdAsync(int id);
    Task<IReadOnlyList<BankaHesabi>> BankaHesaplariGetirAsync(bool sadeceAktifler = true);
}

