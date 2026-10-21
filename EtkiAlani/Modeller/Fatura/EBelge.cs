using EtkiAlani.Modeller.Ortak;

namespace EtkiAlani.Modeller.Fatura;

// Faturanın elektronik belge türü. Alıcı GİB e-Fatura sistemine kayıtlıysa e-Fatura, değilse e-Arşiv kesilir.
public enum EBelgeTuru
{
    Yok = 0,
    EFatura = 1,
    EArsiv = 2
}

// e-Fatura senaryosu: Temel faturada alıcı kabul/red yanıtı veremez; ticari faturada 8 gün içinde verebilir.
public enum EFaturaSenaryosu
{
    Temel = 0,
    Ticari = 1
}

public enum EBelgeDurumu
{
    Yok = 0,
    Hazirlandi = 1,    // UBL-TR XML üretildi, henüz gönderilmedi (dosya olarak dışa aktarıldıysa portala yüklenmeyi bekler)
    Gonderildi = 2,    // Entegratöre/GİB'e iletildi
    KabulEdildi = 3,   // Alıcı kabul etti (ticari fatura) ya da e-Arşiv raporlandı
    Reddedildi = 4,    // Alıcı reddetti (ticari fatura)
    Hata = 5,          // Gönderim hatası; düzeltilip yeniden hazırlanabilir
    IptalEdildi = 6    // Portal/entegratör üzerinden iptal edildi; fatura programda da iptal edilebilir
}

// Her hazırlama/gönderim denemesinin kaydı: gönderilen XML'in birebir kopyası saklanır.
public class EBelgeKaydi : TemelVarlik
{
    public int FaturaId { get; set; }
    public Fatura? Fatura { get; set; }
    public Guid Ettn { get; set; }
    public EBelgeTuru Tur { get; set; }
    public EBelgeDurumu Durum { get; set; }
    public string Entegrator { get; set; } = string.Empty;
    public string Xml { get; set; } = string.Empty;
    public string? DosyaYolu { get; set; }
    public string? Mesaj { get; set; }
    public int? KullaniciId { get; set; }
}
