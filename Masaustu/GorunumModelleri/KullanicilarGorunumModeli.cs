using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Guvenlik;
using Masaustu.Yardimcilar;
using Uygulama.Servisler;

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

    // "Kim Ne Yaptı?" sekmesi
    private DenetimSatiri? _seciliAuditSatiri;
    private DateTime? _auditBaslangicTarihi = DateTime.Today.AddDays(-30);
    private DateTime? _auditBitisTarihi = DateTime.Today;
    private string _auditAramaMetni = string.Empty;
    private SecenekOgesi _auditSeciliModul = SecenekOgesi.Tumu;
    private string _auditSeciliIslemTuruMetni = "Tümü";
    private SecenekOgesi _auditSeciliKullanici = SecenekOgesi.Tumu;
    private bool _ayrintiSatirlariniGoster;
    private string _auditMesaji = string.Empty;
    private int _seciliSekmeIndeksi;
    private int _auditYuklemeNo;

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

    // ─── KİM NE YAPTI? SEKMESİ ───
    public TopluKoleksiyon<DenetimSatiri> AuditSatirlari { get; } = new();

    public DenetimSatiri? SeciliAuditSatiri
    {
        get => _seciliAuditSatiri;
        set
        {
            if (OzellikAyarla(ref _seciliAuditSatiri, value))
                BildirimGonder(nameof(SeciliDegisiklikler));
        }
    }

    // Seçili işlemin alan alan "eski → yeni" dökümü.
    public IReadOnlyList<DenetimMetinleri.Degisiklik> SeciliDegisiklikler =>
        _seciliAuditSatiri == null ? [] : DenetimMetinleri.Degisiklikler(_seciliAuditSatiri.Kayit);

    public int SeciliSekmeIndeksi
    {
        get => _seciliSekmeIndeksi;
        set => OzellikAyarla(ref _seciliSekmeIndeksi, value);
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
        set { if (OzellikAyarla(ref _auditAramaMetni, value)) _ = AuditLoglariYukleAsync(gecikmeli: true); }
    }

    public SecenekOgesi AuditSeciliModul
    {
        get => _auditSeciliModul;
        set { if (OzellikAyarla(ref _auditSeciliModul, value ?? SecenekOgesi.Tumu)) _ = AuditLoglariYukleAsync(); }
    }

    public string AuditSeciliIslemTuruMetni
    {
        get => _auditSeciliIslemTuruMetni;
        set { if (OzellikAyarla(ref _auditSeciliIslemTuruMetni, value)) _ = AuditLoglariYukleAsync(); }
    }

    public SecenekOgesi AuditSeciliKullanici
    {
        get => _auditSeciliKullanici;
        set { if (OzellikAyarla(ref _auditSeciliKullanici, value ?? SecenekOgesi.Tumu)) _ = AuditLoglariYukleAsync(); }
    }

    public bool AyrintiSatirlariniGoster
    {
        get => _ayrintiSatirlariniGoster;
        set { if (OzellikAyarla(ref _ayrintiSatirlariniGoster, value)) _ = AuditLoglariYukleAsync(); }
    }

    public string AuditMesaji
    {
        get => _auditMesaji;
        set => OzellikAyarla(ref _auditMesaji, value);
    }

    private List<SecenekOgesi> _kullaniciSecenekleri = [SecenekOgesi.Tumu];
    public List<SecenekOgesi> KullaniciSecenekleri
    {
        get => _kullaniciSecenekleri;
        private set => OzellikAyarla(ref _kullaniciSecenekleri, value);
    }

    public List<SecenekOgesi> ModulSecenekleri { get; } =
        [SecenekOgesi.Tumu, .. DenetimMetinleri.Moduller
            .OrderBy(m => m.Value, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true))
            .Select(m => new SecenekOgesi(m.Key, m.Value))];

    public List<string> IslemTurleriListesi { get; } = new() { "Tümü", "Ekledi", "Değiştirdi", "Sildi", "Sildi (kalıcı)" };

    public int ToplamKullaniciSayisi => _kullanicilar.Count;
    public int AktifKullaniciSayisi => _kullanicilar.Count(k => k.Aktif);
    public int YoneticiSayisi => _kullanicilar.Count(k => k.Rol == "Yonetici" || k.Rol == "Yönetici");
    public int AuditLogToplamSayisi => AuditSatirlari.Count;

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
    public ICommand KullaniciIslemleriKomutu { get; }
    public ICommand AuditFiltreTemizleKomutu { get; }

    // Rol şablonları: yeni kullanıcı yetkisiz başlar; yönetici uygun şablonu uygulayıp ince ayar yapar.
    private static readonly Dictionary<string, string[]> Sablonlar = new()
    {
        ["Kasiyer / Satış"] = ["SatisYap", "SatisGecmisiGoruntule", "CariGoruntule", "CariEkle", "StokGoruntule", "FaturaGoruntule", "FaturaOlustur", "KasaGoruntule", "TahsilatYap"],
        ["Depo / Stok"] = ["StokGoruntule", "StokEkle", "StokDuzenle", "DepoGoruntule", "DepoTransferYap", "IrsaliyeGoruntule", "IrsaliyeOlustur", "IrsaliyeOnayla", "CariGoruntule"],
        ["Personel / Maaş"] = ["PersonelGoruntule", "PersonelEkle", "PersonelDuzenle", "KasaGoruntule", "BankaGoruntule", "OdemeYap"],
        ["Ön Muhasebe / Finans"] = ["CariGoruntule", "CariEkle", "CariDuzenle", "StokGoruntule", "StokFiyatDegistir", "FaturaGoruntule", "FaturaOlustur", "FaturaOnayla", "IrsaliyeGoruntule",
            "KasaGoruntule", "KasaYonet", "GelirEkle", "GiderEkle", "BankaGoruntule", "BankaEkle", "BankaDuzenle", "TahsilatYap", "OdemeYap", "FinansGoruntule",
            "VirmanYap", "Mutabakat", "POSYonet", "CekSenetGoruntule", "CekSenetIslemYap", "RaporGoruntule", "FinansRaporuGoruntule"],
        ["Muhasebe"] = ["PersonelGoruntule", "MuhasebeFisGoruntule", "MuhasebeFisEkle", "MuhasebeFisOnayla", "MuhasebeFisIptal", "HesapPlaniYonet", "MizanGoruntule", "MaliyetHesaplariGoruntule",
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
        AuditLogExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.Aktar(AuditSatirlari, "Kim_Ne_Yapti"));
        KullaniciIslemleriKomutu = new KomutSarici(param => KullaniciIslemleriniGoster(param as Kullanici));
        AuditFiltreTemizleKomutu = new KomutSarici(_ => AuditFiltreleriniTemizle());
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
            var secili = AuditSeciliKullanici;
            KullaniciSecenekleri = [SecenekOgesi.Tumu, .. tumu.OrderBy(k => k.KullaniciAdi)
                .Select(k => new SecenekOgesi(k.Id, $"{k.KullaniciAdi} ({k.Ad} {k.Soyad})".Trim()))];
            _auditSeciliKullanici = KullaniciSecenekleri.FirstOrDefault(k => Equals(k.Deger, secili.Deger)) ?? SecenekOgesi.Tumu;
            BildirimGonder(nameof(AuditSeciliKullanici));
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

    // Kullanıcı listesindeki "İşlemleri" düğmesi: o kişinin yaptıklarını "Kim Ne Yaptı?" sekmesinde açar.
    private void KullaniciIslemleriniGoster(Kullanici? kullanici)
    {
        if (kullanici == null) return;
        AuditSeciliKullanici = KullaniciSecenekleri.FirstOrDefault(k => Equals(k.Deger, kullanici.Id)) ?? SecenekOgesi.Tumu;
        SeciliSekmeIndeksi = 1;
    }

    private void AuditFiltreleriniTemizle()
    {
        _auditAramaMetni = string.Empty; BildirimGonder(nameof(AuditAramaMetni));
        _auditSeciliModul = SecenekOgesi.Tumu; BildirimGonder(nameof(AuditSeciliModul));
        _auditSeciliKullanici = SecenekOgesi.Tumu; BildirimGonder(nameof(AuditSeciliKullanici));
        _auditSeciliIslemTuruMetni = "Tümü"; BildirimGonder(nameof(AuditSeciliIslemTuruMetni));
        _auditBaslangicTarihi = DateTime.Today.AddDays(-30); BildirimGonder(nameof(AuditBaslangicTarihi));
        _auditBitisTarihi = DateTime.Today; BildirimGonder(nameof(AuditBitisTarihi));
        _ = AuditLoglariYukleAsync();
    }

    public async Task AuditLoglariYukleAsync(bool gecikmeli = false)
    {
        if (_auditLogServisi == null) return;
        var no = ++_auditYuklemeNo;
        if (gecikmeli)
        {
            await Task.Delay(350); // yazarken her harfte sorgu atılmasın
            if (no != _auditYuklemeNo) return;
        }

        try
        {
            AuditIslemTuru? islemTuru = AuditSeciliIslemTuruMetni switch
            {
                "Ekledi" => AuditIslemTuru.Ekleme,
                "Değiştirdi" => AuditIslemTuru.Guncelleme,
                "Sildi" => AuditIslemTuru.SoftDelete,
                "Sildi (kalıcı)" => AuditIslemTuru.Silme,
                _ => null
            };

            // Ekrandaki tarihler yerel gündür; kayıt saati UTC tutulur. Bitiş günü dahildir.
            DateTime? baslangic = AuditBaslangicTarihi?.Date.ToUniversalTime();
            DateTime? bitis = AuditBitisTarihi?.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

            var loglar = await _auditLogServisi.LoglariGetirAsync(
                baslangic: baslangic,
                bitis: bitis,
                tabloAdi: AuditSeciliModul.Deger as string,
                islemTuru: islemTuru,
                kullaniciId: AuditSeciliKullanici.Deger as int?,
                limit: 0, // sınırsız: seçilen tarih aralığının tamamı
                ayrintiSatirlari: AyrintiSatirlariniGoster || AuditSeciliModul.Deger != null,
                arama: string.IsNullOrWhiteSpace(AuditAramaMetni) ? null : AuditAramaMetni);
            if (no != _auditYuklemeNo) return; // arada yeni bir süzme istendi

            var satirlar = await Task.Run(() => loglar.Select(l => new DenetimSatiri(l)).ToList());
            if (no != _auditYuklemeNo) return;
            AuditSatirlari.HepsiniDegistir(satirlar);
            AuditMesaji = satirlar.Count == 0 ? "Seçilen ölçütlere uyan işlem yok." : string.Empty;
            BildirimGonder(nameof(AuditLogToplamSayisi));
        }
        catch (YetkisizErisimException)
        {
            AuditSatirlari.HepsiniDegistir([]);
            AuditMesaji = "İşlem geçmişini görme yetkiniz yok (Güvenlik Kayıtlarını Gör).";
        }
        catch (Exception ex)
        {
            AuditMesaji = "İşlem geçmişi yüklenemedi: " + ex.Message;
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

// Açılır liste seçeneği ("Tümü" için Deger null).
public sealed record SecenekOgesi(object? Deger, string Ad)
{
    public static readonly SecenekOgesi Tumu = new(null, "Tümü");
    public override string ToString() => Ad;
}

// "Kim Ne Yaptı?" listesinin bir satırı: denetim kaydının Türkçe, okunur hali.
public sealed class DenetimSatiri
{
    public DenetimSatiri(AuditLog kayit)
    {
        Kayit = kayit;
        Zaman = DenetimMetinleri.YerelZaman(kayit.Tarih);
        Kullanici = string.IsNullOrWhiteSpace(kayit.KullaniciAdi) ? "Sistem" : kayit.KullaniciAdi!;
        Modul = DenetimMetinleri.ModulAdi(kayit.TabloAdi);
        Islem = DenetimMetinleri.IslemAdi(kayit.IslemTuru, kayit.TabloAdi);
        KayitNo = kayit.KayitId;
        Ozet = DenetimMetinleri.Ozet(kayit);
        Bilgisayar = kayit.IstemciBilgisi ?? "";
    }

    internal AuditLog Kayit { get; }
    public DateTime Zaman { get; }
    public string Kullanici { get; }
    public string Modul { get; }
    public string Islem { get; }
    public string KayitNo { get; }
    public string Ozet { get; }
    public string Bilgisayar { get; }
}
