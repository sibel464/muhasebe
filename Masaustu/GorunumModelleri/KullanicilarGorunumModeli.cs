using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Guvenlik;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class KullanicilarGorunumModeli : TemelGorunumModeli
{
    private readonly IKullaniciServisi _kullaniciServisi;
    private readonly IAuditLogServisi? _auditLogServisi;

    private ObservableCollection<Kullanici> _kullanicilar = new();
    private Kullanici? _seciliKullanici;
    private string _aramaMetni = string.Empty;
    private bool _islemYapiliyor;
    private string _bilgiMesaji = string.Empty;
    private bool _duzenlemeModu = false;

    // İşlem / Audit Log Sekmesi Özellikleri
    private ObservableCollection<AuditLog> _auditLoglar = new();
    private AuditLog? _seciliAuditLog;
    private DateTime? _auditBaslangicTarihi = DateTime.Today.AddDays(-30);
    private DateTime? _auditBitisTarihi = DateTime.Today.AddDays(1);
    private string _auditAramaMetni = string.Empty;
    private string _auditSeciliTablo = "Tümü";
    private string _auditSeciliIslemTuruMetni = "Tümü";
    private int? _auditSeciliKullaniciId = null;

    // Yeni veya düzenlenecek kullanıcı için model
    private Kullanici _islemKullanicisi = new() { Yetkiler = new KullaniciYetkileri() };
    private string _islemSifresi = string.Empty;

    public ObservableCollection<Kullanici> Kullanicilar
    {
        get => _kullanicilar;
        set => OzellikAyarla(ref _kullanicilar, value);
    }

    public Kullanici? SeciliKullanici
    {
        get => _seciliKullanici;
        set
        {
            if (OzellikAyarla(ref _seciliKullanici, value))
            {
                (SilKomutu as KomutSarici)?.BildirimGonder();
                (KaydetKomutu as KomutSarici)?.BildirimGonder();
                (SifreDegistirKomutu as KomutSarici)?.BildirimGonder();

                if (value != null)
                {
                    DuzenlemeModu = true;
                    IslemKullanicisi = new Kullanici
                    {
                        Id = value.Id,
                        KullaniciAdi = value.KullaniciAdi,
                        Ad = value.Ad,
                        Soyad = value.Soyad,
                        Eposta = value.Eposta,
                        Aktif = value.Aktif,
                        Rol = value.Rol,
                        SonGirisTarihi = value.SonGirisTarihi,
                        Yetkiler = value.Yetkiler ?? new KullaniciYetkileri()
                    };
                    IslemSifresi = string.Empty;
                }
                else
                {
                    IptalEt();
                }
            }
        }
    }

    public Kullanici IslemKullanicisi
    {
        get => _islemKullanicisi;
        set => OzellikAyarla(ref _islemKullanicisi, value);
    }

    public string IslemSifresi
    {
        get => _islemSifresi;
        set => OzellikAyarla(ref _islemSifresi, value);
    }

    public bool DuzenlemeModu
    {
        get => _duzenlemeModu;
        set => OzellikAyarla(ref _duzenlemeModu, value);
    }

    public string AramaMetni
    {
        get => _aramaMetni;
        set
        {
            if (OzellikAyarla(ref _aramaMetni, value))
            {
                _ = YukleAsync();
            }
        }
    }

    public bool IslemYapiliyor
    {
        get => _islemYapiliyor;
        set => OzellikAyarla(ref _islemYapiliyor, value);
    }

    public string BilgiMesaji
    {
        get => _bilgiMesaji;
        set => OzellikAyarla(ref _bilgiMesaji, value);
    }

    // ─── AUDIT LOG SEKMESİ ───
    public ObservableCollection<AuditLog> AuditLoglar
    {
        get => _auditLoglar;
        set => OzellikAyarla(ref _auditLoglar, value);
    }

    public AuditLog? SeciliAuditLog
    {
        get => _seciliAuditLog;
        set => OzellikAyarla(ref _seciliAuditLog, value);
    }

    public DateTime? AuditBaslangicTarihi
    {
        get => _auditBaslangicTarihi;
        set { if (OzellikAyarla(ref _auditBaslangicTarihi, value)) _ = AuditLoglariYukleAsync(); }
    }

    public DateTime? AuditBitisTarihi
    {
        get => _auditBitisTarihi;
        set { if (OzellikAyarla(ref _auditBitisTarihi, value)) _ = AuditLoglariYukleAsync(); }
    }

    public string AuditAramaMetni
    {
        get => _auditAramaMetni;
        set { if (OzellikAyarla(ref _auditAramaMetni, value)) _ = AuditLoglariYukleAsync(); }
    }

    public string AuditSeciliTablo
    {
        get => _auditSeciliTablo;
        set { if (OzellikAyarla(ref _auditSeciliTablo, value)) _ = AuditLoglariYukleAsync(); }
    }

    public string AuditSeciliIslemTuruMetni
    {
        get => _auditSeciliIslemTuruMetni;
        set { if (OzellikAyarla(ref _auditSeciliIslemTuruMetni, value)) _ = AuditLoglariYukleAsync(); }
    }

    public int? AuditSeciliKullaniciId
    {
        get => _auditSeciliKullaniciId;
        set { if (OzellikAyarla(ref _auditSeciliKullaniciId, value)) _ = AuditLoglariYukleAsync(); }
    }

    public List<string> TabloListesi { get; } = new()
    {
        "Tümü", "CariKart", "Urun", "Fatura", "Siparis", "Irsaliye", "Kasa", "BankaHesabi", "CekSenet", "Kullanici"
    };

    public List<string> IslemTurleriListesi { get; } = new()
    {
        "Tümü", "Ekleme", "Guncelleme", "Silme", "SoftDelete"
    };

    public int ToplamKullaniciSayisi => _kullanicilar.Count;
    public int AktifKullaniciSayisi => _kullanicilar.Count(k => k.Aktif);
    public int YoneticiSayisi => _kullanicilar.Count(k => k.Rol == "Yonetici" || k.Rol == "Yönetici");
    public int AuditLogToplamSayisi => _auditLoglar.Count;

    // Komutlar
    public ICommand YukleKomutu { get; }
    public ICommand YeniKomutu { get; }
    public ICommand KaydetKomutu { get; }
    public ICommand SilKomutu { get; }
    public ICommand SatirSilKomutu { get; }
    public ICommand SifreDegistirKomutu { get; }
    public ICommand IptalKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }
    public ICommand AuditLogYenileKomutu { get; }
    public ICommand AuditLogExcelAktarKomutu { get; }
    public ICommand SablonUygulaKomutu { get; }

    // Rol şablonları: yeni kullanıcı yetkisiz başlar; yönetici uygun şablonu uygulayıp ince ayar yapar.
    private static readonly Dictionary<string, string[]> Sablonlar = new()
    {
        ["Kasiyer / Satış"] = ["SatisYap", "SatisGecmisiGoruntule", "CariGoruntule", "CariEkle", "StokGoruntule", "FaturaGoruntule", "FaturaOlustur", "KasaGoruntule", "TahsilatYap"],
        ["Depo / Stok"] = ["StokGoruntule", "StokEkle", "StokDuzenle", "DepoGoruntule", "DepoTransferYap", "IrsaliyeGoruntule", "IrsaliyeOlustur", "IrsaliyeOnayla", "CariGoruntule"],
        ["Ön Muhasebe / Finans"] = ["CariGoruntule", "CariEkle", "CariDuzenle", "StokGoruntule", "FaturaGoruntule", "FaturaOlustur", "FaturaOnayla", "IrsaliyeGoruntule",
            "KasaGoruntule", "KasaYonet", "GelirEkle", "GiderEkle", "BankaGoruntule", "BankaEkle", "BankaDuzenle", "TahsilatYap", "OdemeYap", "FinansGoruntule",
            "VirmanYap", "Mutabakat", "POSYonet", "CekSenetGoruntule", "CekSenetIslemYap", "RaporGoruntule", "FinansRaporuGoruntule"],
        ["Muhasebe"] = ["MuhasebeFisGoruntule", "MuhasebeFisEkle", "MuhasebeFisOnayla", "MuhasebeFisIptal", "HesapPlaniYonet", "MizanGoruntule", "MaliyetHesaplariGoruntule",
            "CariGoruntule", "FaturaGoruntule", "IrsaliyeGoruntule", "KasaGoruntule", "BankaGoruntule", "FinansGoruntule", "CekSenetGoruntule", "RaporGoruntule", "FinansRaporuGoruntule"],
        ["Yetkileri Temizle"] = [],
    };

    public IReadOnlyList<string> YetkiSablonlari { get; } = Sablonlar.Keys.ToList();

    private string? _seciliYetkiSablonu;
    public string? SeciliYetkiSablonu
    {
        get => _seciliYetkiSablonu;
        set => OzellikAyarla(ref _seciliYetkiSablonu, value);
    }

    // Şablon önceki işaretleri temizleyip yalnızca kendi yetkilerini açar; kayıt Kaydet ile yapılır.
    private void SablonUygula()
    {
        if (SeciliYetkiSablonu == null || !Sablonlar.TryGetValue(SeciliYetkiSablonu, out var acilacaklar))
            return;

        var mevcut = IslemKullanicisi.Yetkiler;
        var yeni = new KullaniciYetkileri { Id = mevcut?.Id ?? 0, KullaniciId = mevcut?.KullaniciId ?? IslemKullanicisi.Id };
        foreach (var ad in acilacaklar)
            typeof(KullaniciYetkileri).GetProperty(ad)!.SetValue(yeni, true);

        IslemKullanicisi = new Kullanici
        {
            Id = IslemKullanicisi.Id,
            KullaniciAdi = IslemKullanicisi.KullaniciAdi,
            Ad = IslemKullanicisi.Ad,
            Soyad = IslemKullanicisi.Soyad,
            Eposta = IslemKullanicisi.Eposta,
            Aktif = IslemKullanicisi.Aktif,
            Rol = IslemKullanicisi.Rol,
            Yetkiler = yeni
        };
    }

    public KullanicilarGorunumModeli(IKullaniciServisi kullaniciServisi, IAuditLogServisi? auditLogServisi = null)
    {
        _kullaniciServisi = kullaniciServisi;
        _auditLogServisi = auditLogServisi;

        YukleKomutu = new KomutSarici(async _ => await YukleAsync());
        YeniKomutu = new KomutSarici(_ => IptalEt());
        KaydetKomutu = new KomutSarici(async _ => await KaydetAsync(), _ => KaydedebilirMi());
        SilKomutu = new KomutSarici(async _ => await SilAsync(), _ => DuzenlemeModu && IslemKullanicisi.Id > 0 && IslemKullanicisi.Id != _kullaniciServisi.AktifKullanici?.Id);
        SatirSilKomutu = new KomutSarici(async param => await SilAsync(param as Kullanici));
        SifreDegistirKomutu = new KomutSarici(async param => await SifreDegistirAsync(param as Kullanici));
        IptalKomutu = new KomutSarici(_ => IptalEt());
        ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.Aktar(Kullanicilar, "Kullanici_Listesi"));
        
        AuditLogYenileKomutu = new KomutSarici(async _ => await AuditLoglariYukleAsync());
        AuditLogExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.Aktar(AuditLoglar, "Islem_Gecmisi_Audit_Logs"));
        SablonUygulaKomutu = new KomutSarici(_ => SablonUygula(), _ => SeciliYetkiSablonu != null);

        _ = IlkYuklemeAsync();
    }

    private async Task IlkYuklemeAsync()
    {
        await YukleAsync();
        await AuditLoglariYukleAsync();
    }

    private async Task YukleAsync()
    {
        IslemYapiliyor = true;
        try
        {
            var tumu = await _kullaniciServisi.TumunuGetirAsync();
            if (!string.IsNullOrWhiteSpace(AramaMetni))
            {
                var kucukArama = AramaMetni.ToLower();
                tumu = tumu.Where(k => 
                    k.KullaniciAdi.ToLower().Contains(kucukArama) || 
                    k.Ad.ToLower().Contains(kucukArama) || 
                    k.Soyad.ToLower().Contains(kucukArama)).ToList();
            }
            Kullanicilar = new ObservableCollection<Kullanici>(tumu);
            BildirimGonder(nameof(ToplamKullaniciSayisi));
            BildirimGonder(nameof(AktifKullaniciSayisi));
            BildirimGonder(nameof(YoneticiSayisi));
        }
        catch (Exception ex)
        {
            BilgiMesaji = "Kullanıcılar yüklenirken hata oluştu: " + ex.Message;
        }
        finally
        {
            IslemYapiliyor = false;
        }
    }

    public async Task AuditLoglariYukleAsync()
    {
        if (_auditLogServisi == null) return;

        try
        {
            string? tablo = AuditSeciliTablo == "Tümü" ? null : AuditSeciliTablo;
            AuditIslemTuru? islemTuru = null;

            if (AuditSeciliIslemTuruMetni == "Ekleme") islemTuru = AuditIslemTuru.Ekleme;
            else if (AuditSeciliIslemTuruMetni == "Guncelleme") islemTuru = AuditIslemTuru.Guncelleme;
            else if (AuditSeciliIslemTuruMetni == "Silme") islemTuru = AuditIslemTuru.Silme;
            else if (AuditSeciliIslemTuruMetni == "SoftDelete") islemTuru = AuditIslemTuru.SoftDelete;

            var loglar = await _auditLogServisi.LoglariGetirAsync(
                baslangic: AuditBaslangicTarihi,
                bitis: AuditBitisTarihi,
                tabloAdi: tablo,
                islemTuru: islemTuru,
                kullaniciId: AuditSeciliKullaniciId,
                limit: 1000);

            if (!string.IsNullOrWhiteSpace(AuditAramaMetni))
            {
                var arama = AuditAramaMetni.ToLower();
                loglar = loglar.Where(l =>
                    (l.KullaniciAdi != null && l.KullaniciAdi.ToLower().Contains(arama)) ||
                    (l.TabloAdi != null && l.TabloAdi.ToLower().Contains(arama)) ||
                    (l.IslemDetayi != null && l.IslemDetayi.ToLower().Contains(arama)) ||
                    (l.KayitId != null && l.KayitId.ToLower().Contains(arama))
                ).ToList();
            }

            AuditLoglar = new ObservableCollection<AuditLog>(loglar);
            BildirimGonder(nameof(AuditLogToplamSayisi));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Audit Log yükleme hatası: " + ex.Message);
        }
    }

    private void IptalEt()
    {
        SeciliKullanici = null;
        DuzenlemeModu = false;
        IslemKullanicisi = new Kullanici { Yetkiler = new KullaniciYetkileri() };
        IslemSifresi = string.Empty;
        BilgiMesaji = string.Empty;
        (SilKomutu as KomutSarici)?.BildirimGonder();
        (KaydetKomutu as KomutSarici)?.BildirimGonder();
        (SifreDegistirKomutu as KomutSarici)?.BildirimGonder();
    }

    private bool KaydedebilirMi()
    {
        if (IslemYapiliyor) return false;
        if (string.IsNullOrWhiteSpace(IslemKullanicisi.KullaniciAdi)) return false;
        if (string.IsNullOrWhiteSpace(IslemKullanicisi.Ad)) return false;
        
        if (!DuzenlemeModu && string.IsNullOrWhiteSpace(IslemSifresi)) return false;

        return true;
    }

    private async Task KaydetAsync()
    {
        IslemYapiliyor = true;
        BilgiMesaji = string.Empty;
        try
        {
            if (DuzenlemeModu)
            {
                await _kullaniciServisi.GuncelleAsync(IslemKullanicisi);
                if (!string.IsNullOrWhiteSpace(IslemSifresi))
                {
                    await _kullaniciServisi.SifreDegistirAsync(IslemKullanicisi.Id, IslemSifresi);
                }
                BilgiMesaji = "Kullanıcı ve yetki matrisi başarıyla güncellendi.";
            }
            else
            {
                await _kullaniciServisi.EkleAsync(IslemKullanicisi, IslemSifresi);
                BilgiMesaji = "Yeni kullanıcı ve yetkileri başarıyla eklendi.";
            }
            
            await YukleAsync();
            IptalEt();
        }
        catch (Exception ex)
        {
            BilgiMesaji = "Hata: " + ex.Message;
        }
        finally
        {
            IslemYapiliyor = false;
        }
    }

    private async Task SifreDegistirAsync(Kullanici? hedefKullanici = null)
    {
        var kullanici = hedefKullanici ?? (IslemKullanicisi.Id > 0 ? IslemKullanicisi : null);
        if (kullanici == null || kullanici.Id <= 0)
        {
            System.Windows.MessageBox.Show("Lütfen önce şifresini değiştirmek istediğiniz kullanıcıyı seçiniz.", "Uyarı", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        string yeniSifre = IslemSifresi;

        if (string.IsNullOrWhiteSpace(yeniSifre))
        {
            var pencere = new Masaustu.Gorunumler.Ortak.MetinGirisPenceresi(
                "Şifre Belirleme / Değiştirme",
                $"'{kullanici.KullaniciAdi}' ({kullanici.Ad} {kullanici.Soyad}) kullanıcısı için yeni şifreyi yazınız:");

            if (pencere.ShowDialog() != true) return;
            yeniSifre = pencere.GirilenMetin;
        }

        if (string.IsNullOrWhiteSpace(yeniSifre))
        {
            System.Windows.MessageBox.Show("Yeni şifre boş olamaz.", "Hata", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }

        IslemYapiliyor = true;
        BilgiMesaji = string.Empty;
        try
        {
            await _kullaniciServisi.SifreDegistirAsync(kullanici.Id, yeniSifre);
            BilgiMesaji = $"'{kullanici.KullaniciAdi}' kullanıcısının şifresi başarıyla yenilendi.";
            IslemSifresi = string.Empty;
            await YukleAsync();
        }
        catch (Exception ex)
        {
            BilgiMesaji = "Şifre değiştirme hatası: " + ex.Message;
        }
        finally
        {
            IslemYapiliyor = false;
        }
    }

    private async Task SilAsync(Kullanici? hedefKullanici = null)
    {
        var silinecek = hedefKullanici ?? (IslemKullanicisi.Id > 0 ? IslemKullanicisi : null);
        if (silinecek == null || silinecek.Id <= 0) return;

        if (_kullaniciServisi.AktifKullanici != null && silinecek.Id == _kullaniciServisi.AktifKullanici.Id)
        {
            System.Windows.MessageBox.Show("Kendi aktif oturum açmış kullanıcı hesabınızı silemezsiniz.", "Uyarı", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var onay1 = System.Windows.MessageBox.Show(
            $"'{silinecek.KullaniciAdi}' ({silinecek.Ad} {silinecek.Soyad}) kullanıcısını silmek istediğinizden emin misiniz?",
            "Kullanıcı Silme Onayı (1/2)",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (onay1 != System.Windows.MessageBoxResult.Yes) return;

        var onay2 = System.Windows.MessageBox.Show(
            "Bu işlem kullanıcının sisteme erişimini tamamen kaldıracaktır. Kesinlikle onaylıyor musunuz?",
            "Kullanıcı Silme Onayı (2/2)",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Stop);

        if (onay2 != System.Windows.MessageBoxResult.Yes) return;

        IslemYapiliyor = true;
        BilgiMesaji = string.Empty;
        try
        {
            await _kullaniciServisi.SilAsync(silinecek.Id);
            BilgiMesaji = $"'{silinecek.KullaniciAdi}' kullanıcısı başarıyla silindi.";
            await YukleAsync();
            IptalEt();
        }
        catch (Exception ex)
        {
            BilgiMesaji = "Hata: " + ex.Message;
        }
        finally
        {
            IslemYapiliyor = false;
        }
    }
}
