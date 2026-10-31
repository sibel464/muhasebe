using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Ortak;

namespace Uygulama.Arayuzler;

// Kasa/banka ekstresi ve gün sonu raporu için yazdırılacak hazır satırlar (bakiye ekranda hesaplanmış olarak gelir).
public sealed record HesapEkstresiSatiri(DateTime Tarih, string FisNo, string Tur, string Cari, string Aciklama, decimal Giris, decimal Cikis, decimal Bakiye);
public sealed record HesapEkstresiBelgesi(string Baslik, string HesapAdi, DateTime? Baslangic, DateTime? Bitis, decimal Devir,
    IReadOnlyList<HesapEkstresiSatiri> Satirlar, decimal ToplamGiris, decimal ToplamCikis, decimal Kapanis);

// Raf/ürün etiketi: fiyat KDV dahil basılır (Fiyat Etiketi Yönetmeliği).
public sealed record UrunEtiketi(string Ad, string? Barkod, decimal KdvDahilFiyat, string Birim);
public enum EtiketDuzeni { A4_21, A4_40, Rulo50x30 }

public interface IBelgeYazdirmaServisi
{
    string EtiketHtmlUret(IReadOnlyList<UrunEtiketi> etiketler, EtiketDuzeni duzen, FirmaBilgisi? firma = null);
    Task<string> EtiketYazdirAsync(IReadOnlyList<UrunEtiketi> etiketler, EtiketDuzeni duzen);

    FirmaBilgisi FirmaBilgisiGetir();
    // Belgenin şirketine ait güncel künye (boşsa aktif şirket).
    Task<FirmaBilgisi> FirmaBilgisiGetirAsync(Guid? sirketId = null);
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
    string MutabakatMektubuHtmlUret(CariKart cari, DateTime tarih, decimal bakiye, IReadOnlyList<Uygulama.Dto.Rapor.BaBsKalemiDto>? baBs = null, FirmaBilgisi? firma = null);
    Task<string> MutabakatMektubuYazdirAsync(CariKart cari, DateTime tarih, decimal bakiye, IReadOnlyList<Uygulama.Dto.Rapor.BaBsKalemiDto>? baBs = null, FirmaBilgisi? firma = null);
    Task<string> MakbuzYazdirAsync(CariKart cari, decimal tutar, bool isTahsilat, string aciklama, string? dosyaYolu = null, string? belgeNo = null, DateTime? tarih = null, string? kasaBankaAdi = null, FirmaBilgisi? firma = null);
    Task<string> KasaDokumuYazdirAsync(List<KasaHareketi> hareketler, decimal toplamBakiye, string? dosyaYolu = null, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null);
    Task<string> BankaDokumuYazdirAsync(List<BankaHareketi> hareketler, decimal toplamBakiye, string? dosyaYolu = null, string? bankaAdi = null, DateTime? baslangic = null, DateTime? bitis = null, FirmaBilgisi? firma = null);

    // Satış siparişi onaylanmadıysa fiyat teklifi, onaylıysa sipariş formu; alışta satın alma siparişi.
    string SiparisHtmlUret(EtkiAlani.Modeller.Siparis.Siparis siparis, FirmaBilgisi? firma = null, int teklifGecerlilikGunu = 15);
    Task<string> SiparisYazdirAsync(EtkiAlani.Modeller.Siparis.Siparis siparis);

    string HesapEkstresiHtmlUret(HesapEkstresiBelgesi belge, FirmaBilgisi? firma = null);
    Task<string> HesapEkstresiYazdirAsync(HesapEkstresiBelgesi belge, FirmaBilgisi? firma = null);

    // Tahakkuk edilmiş maaşların ücret pusulası; her personel ayrı sayfada.
    string MaasPusulasiHtmlUret(int yil, int ay, IReadOnlyList<Uygulama.Dto.Muhasebe.MaasTahakkukSatiriDto> satirlar, FirmaBilgisi? firma = null);
    Task<string> MaasPusulasiYazdirAsync(int yil, int ay, IReadOnlyList<Uygulama.Dto.Muhasebe.MaasTahakkukSatiriDto> satirlar);

    // Doğrudan Görüntüleme & Yazdırma Diyaloğu Açma
    Task BelgeyiGoruntuleVeYazdirAsync(string htmlIcerik, string geciciBelgeAdi);
}
