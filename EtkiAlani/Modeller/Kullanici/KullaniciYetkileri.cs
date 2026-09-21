using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Kullanici;

public class KullaniciYetkileri : TemelVarlik
{
    public int KullaniciId { get; set; }
    
    // Cari / Müşteri Yetkileri
    public bool CariGoruntule { get; set; } = true;
    public bool CariEkle { get; set; } = true;
    public bool CariDuzenle { get; set; } = true;
    public bool CariSil { get; set; } = false;

    // Personel Yetkileri
    public bool PersonelGoruntule { get; set; } = false;
    public bool PersonelEkle { get; set; } = false;
    public bool PersonelDuzenle { get; set; } = false;
    public bool PersonelSil { get; set; } = false;

    // Banka Yetkileri
    public bool BankaGoruntule { get; set; } = true;
    public bool BankaEkle { get; set; } = false;
    public bool BankaDuzenle { get; set; } = false;
    public bool BankaSil { get; set; } = false;

    // Stok Yetkileri
    public bool StokGoruntule { get; set; } = true;
    public bool StokEkle { get; set; } = true;
    public bool StokDuzenle { get; set; } = true;
    public bool StokSil { get; set; } = false;
    public bool StokFiyatDegistir { get; set; } = false;

    // Satış Yetkileri
    public bool SatisYap { get; set; } = true;
    public bool SatisIptalEt { get; set; } = false;
    public bool SatisBeklet { get; set; } = true;
    public bool FisYazdir { get; set; } = true;
    public bool SatisGecmisiGoruntule { get; set; } = true;

    // Finans Yetkileri
    public bool FinansGoruntule { get; set; } = true;
    public bool KasaGoruntule { get; set; } = true;
    public bool KasaIslem { get; set; } = true;
    public bool BankaIslem { get; set; } = true;
    public bool VirmanYap { get; set; } = true;
    public bool FinansIptal { get; set; } = true;
    public bool Mutabakat { get; set; } = true;
    public bool POSYonet { get; set; } = true;
    public bool FinansRaporlari { get; set; } = true;
    public bool KasaYonet { get; set; } = false;
    public bool TahsilatYap { get; set; } = true;
    public bool OdemeYap { get; set; } = true;
    public bool GelirEkle { get; set; } = false;
    public bool GiderEkle { get; set; } = false;

    // Rapor Yetkileri
    public bool RaporGoruntule { get; set; } = false;
    public bool FinansRaporuGoruntule { get; set; } = false;
    public bool StokRaporuGoruntule { get; set; } = false;
    public bool SatisRaporuGoruntule { get; set; } = false;
    
    // Fatura Yetkileri
    public bool FaturaGoruntule { get; set; } = true;
    public bool FaturaOlustur { get; set; } = true;
    public bool FaturaOnayla { get; set; } = true;
    public bool FaturaIptal { get; set; } = false;

    // İrsaliye Yetkileri
    public bool IrsaliyeGoruntule { get; set; } = true;
    public bool IrsaliyeOlustur { get; set; } = true;
    public bool IrsaliyeOnayla { get; set; } = true;
    public bool IrsaliyeIptal { get; set; } = false;

    // Depo Yetkileri
    public bool DepoGoruntule { get; set; } = true;
    public bool DepoTransferYap { get; set; } = true;

    // Çek & Senet Yetkileri
    public bool CekSenetGoruntule { get; set; } = true;
    public bool CekSenetIslemYap { get; set; } = true;

    // Muhasebe Yetkileri
    public bool MuhasebeFisGoruntule { get; set; } = true;
    public bool MuhasebeFisEkle { get; set; } = true;
    public bool MuhasebeFisDuzenle { get; set; } = true;
    public bool MuhasebeFisOnayla { get; set; } = true;
    public bool MuhasebeFisIptal { get; set; } = true;
    public bool HesapPlaniYonet { get; set; } = true;
    public bool MizanGoruntule { get; set; } = true;
    public bool MaliyetHesaplariGoruntule { get; set; } = true;

    // Sistem Ayarları ve Kullanıcı Yönetimi
    public bool AyarlariYonet { get; set; } = false;
    public bool KullaniciYonet { get; set; } = false;
    public bool AuditLogGoruntule { get; set; } = false;
}
