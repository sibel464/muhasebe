using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Ortak;

namespace Uygulama.Arayuzler;

public interface IBelgeYazdirmaServisi
{
    FirmaBilgisi FirmaBilgisiGetir();
    void FirmaBilgisiGuncelle(FirmaBilgisi bilgi);

    // HTML / Vektör Çıktı Üretimi
    string FaturaHtmlUret(Fatura fatura, FirmaBilgisi? firma = null);
    string CariEkstreHtmlUret(CariKart cari, List<CariHareket> hareketler, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null);
    string TahsilatMakbuzuHtmlUret(CariKart cari, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null, string? kasaBankaAdi = null, FirmaBilgisi? firma = null);
    string OdemeMakbuzuHtmlUret(CariKart cari, decimal tutar, string aciklama, string? belgeNo = null, DateTime? tarih = null, string? kasaBankaAdi = null, FirmaBilgisi? firma = null);
    string KasaHareketDokumuHtmlUret(List<KasaHareketi> hareketler, decimal toplamBakiye, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null);
    string BankaHareketDokumuHtmlUret(List<BankaHareketi> hareketler, decimal toplamBakiye, string? bankaAdi = null, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null);

    // Dosyaya Kaydetme & Görüntüleme
    Task<string> FaturaYazdirAsync(Fatura fatura, string? dosyaYolu = null, FirmaBilgisi? firma = null);
    Task<string> CariEkstreYazdirAsync(CariKart cari, List<CariHareket> hareketler, string? dosyaYolu = null, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null);
    Task<string> MakbuzYazdirAsync(CariKart cari, decimal tutar, bool isTahsilat, string aciklama, string? dosyaYolu = null, string? belgeNo = null, DateTime? tarih = null, string? kasaBankaAdi = null, FirmaBilgisi? firma = null);
    Task<string> KasaDokumuYazdirAsync(List<KasaHareketi> hareketler, decimal toplamBakiye, string? dosyaYolu = null, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null);
    Task<string> BankaDokumuYazdirAsync(List<BankaHareketi> hareketler, decimal toplamBakiye, string? dosyaYolu = null, string? bankaAdi = null, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null);

    // Doğrudan Görüntüleme & Yazdırma Diyaloğu Açma
    Task BelgeyiGoruntuleVeYazdirAsync(string htmlIcerik, string geciciBelgeAdi);
}
