using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Kullanici;

// Yeni kullanıcı hiçbir yetkiyle başlamaz (en az yetki ilkesi); yetkileri yönetici tek tek açar.
public class KullaniciYetkileri : TemelVarlik
{
    public int KullaniciId { get; set; }
    
    // Cari / Müşteri Yetkileri
    public bool CariGoruntule { get; set; }
    public bool CariEkle { get; set; }
    public bool CariDuzenle { get; set; }
    public bool CariSil { get; set; }

    // Personel Yetkileri
    public bool PersonelGoruntule { get; set; }
    public bool PersonelEkle { get; set; }
    public bool PersonelDuzenle { get; set; }
    public bool PersonelSil { get; set; }

    // Banka Yetkileri
    public bool BankaGoruntule { get; set; }
    public bool BankaEkle { get; set; }
    public bool BankaDuzenle { get; set; }
    public bool BankaSil { get; set; }

    // Stok Yetkileri
    public bool StokGoruntule { get; set; }
    public bool StokEkle { get; set; }
    public bool StokDuzenle { get; set; }
    public bool StokSil { get; set; }
    public bool StokFiyatDegistir { get; set; }

    // Satış Yetkileri
    public bool SatisYap { get; set; }
    public bool SatisIptalEt { get; set; }
    public bool SatisBeklet { get; set; }
    public bool FisYazdir { get; set; }
    public bool SatisGecmisiGoruntule { get; set; }

    // Finans Yetkileri
    public bool FinansGoruntule { get; set; }
    public bool KasaGoruntule { get; set; }
    public bool KasaIslem { get; set; }
    public bool BankaIslem { get; set; }
    public bool VirmanYap { get; set; }
    public bool FinansIptal { get; set; }
    public bool Mutabakat { get; set; }
    public bool POSYonet { get; set; }
    public bool FinansRaporlari { get; set; }
    public bool KasaYonet { get; set; }
    public bool TahsilatYap { get; set; }
    public bool OdemeYap { get; set; }
    public bool GelirEkle { get; set; }
    public bool GiderEkle { get; set; }

    // Rapor Yetkileri
    public bool RaporGoruntule { get; set; }
    public bool FinansRaporuGoruntule { get; set; }
    public bool StokRaporuGoruntule { get; set; }
    public bool SatisRaporuGoruntule { get; set; }
    
    // Fatura Yetkileri
    public bool FaturaGoruntule { get; set; }
    public bool FaturaOlustur { get; set; }
    public bool FaturaOnayla { get; set; }
    public bool FaturaIptal { get; set; }

    // İrsaliye Yetkileri
    public bool IrsaliyeGoruntule { get; set; }
    public bool IrsaliyeOlustur { get; set; }
    public bool IrsaliyeOnayla { get; set; }
    public bool IrsaliyeIptal { get; set; }

    // Depo Yetkileri
    public bool DepoGoruntule { get; set; }
    public bool DepoTransferYap { get; set; }

    // Çek & Senet Yetkileri
    public bool CekSenetGoruntule { get; set; }
    public bool CekSenetIslemYap { get; set; }

    // Muhasebe Yetkileri
    public bool MuhasebeFisGoruntule { get; set; }
    public bool MuhasebeFisEkle { get; set; }
    public bool MuhasebeFisDuzenle { get; set; }
    public bool MuhasebeFisOnayla { get; set; }
    public bool MuhasebeFisIptal { get; set; }
    public bool HesapPlaniYonet { get; set; }
    public bool MizanGoruntule { get; set; }
    public bool MaliyetHesaplariGoruntule { get; set; }

    // Sistem Ayarları ve Kullanıcı Yönetimi
    public bool AyarlariYonet { get; set; }
    public bool KullaniciYonet { get; set; }
    public bool AuditLogGoruntule { get; set; }
}
