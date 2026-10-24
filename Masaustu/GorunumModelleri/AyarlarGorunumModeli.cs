using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Ayar;
using EtkiAlani.Modeller.Guvenlik;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.GorunumModelleri;

public class AyarlarGorunumModeli : TemelGorunumModeli
{
    private readonly ISistemAyarlariServisi? _ayarlarServisi;
    private readonly IAuditLogServisi? _auditLogServisi;

    private string _firmaUnvani = string.Empty;
    private string _vergiDairesi = string.Empty;
    private string _vergiNo = string.Empty;
    private string _mersisNo = string.Empty;
    private string _auditTabloArama = string.Empty;
    private string _auditKullaniciArama = string.Empty;
    private string _telefon = string.Empty;
    private string _eposta = string.Empty;
    private string _webSitesi = string.Empty;
    private string _adres = string.Empty;
    private string _fisHeader = string.Empty;
    private string _fisFooter = "Bizi tercih ettiğiniz için teşekkür ederiz!";
    private decimal _varsayilanKdv = 20;
    private string _paraBirimiSembolu = "₺";
    private string _paraBirimiKodu = "TRY";
    private int _varsayilanVadeGunu = 30;
    private string _faturaNotu = string.Empty;
    private string _irsaliyeNotu = string.Empty;

    private string _hataMesaji = string.Empty;
    private string _basariMesaji = string.Empty;
    private bool _yukleniyor;

    public string FirmaUnvani { get => _firmaUnvani; set => OzellikAyarla(ref _firmaUnvani, value); }
    public string VergiDairesi { get => _vergiDairesi; set => OzellikAyarla(ref _vergiDairesi, value); }
    public string VergiNo { get => _vergiNo; set => OzellikAyarla(ref _vergiNo, value); }
    public string MersisNo { get => _mersisNo; set => OzellikAyarla(ref _mersisNo, value); }
    public string AuditTabloArama { get => _auditTabloArama; set => OzellikAyarla(ref _auditTabloArama, value); }
    public string AuditKullaniciArama { get => _auditKullaniciArama; set => OzellikAyarla(ref _auditKullaniciArama, value); }
    public string Telefon { get => _telefon; set => OzellikAyarla(ref _telefon, value); }
    public string Eposta { get => _eposta; set => OzellikAyarla(ref _eposta, value); }
    public string WebSitesi { get => _webSitesi; set => OzellikAyarla(ref _webSitesi, value); }
    public string Adres { get => _adres; set => OzellikAyarla(ref _adres, value); }
    private string _il = string.Empty, _ilce = string.Empty, _postaKodu = string.Empty, _eBelgeKlasoru = string.Empty;
    private int _eFaturaSenaryoIndex;
    public string Il { get => _il; set => OzellikAyarla(ref _il, value); }
    public string Ilce { get => _ilce; set => OzellikAyarla(ref _ilce, value); }
    public string PostaKodu { get => _postaKodu; set => OzellikAyarla(ref _postaKodu, value); }
    public string EBelgeKlasoru { get => _eBelgeKlasoru; set => OzellikAyarla(ref _eBelgeKlasoru, value); }
    public string EBelgeVarsayilanKlasor => Altyapi.EBelge.DosyaEBelgeEntegratoru.VarsayilanKlasor;
    // 0: Temel fatura, 1: Ticari fatura
    public int EFaturaSenaryoIndex { get => _eFaturaSenaryoIndex; set => OzellikAyarla(ref _eFaturaSenaryoIndex, value); }
    public string FisHeader { get => _fisHeader; set => OzellikAyarla(ref _fisHeader, value); }
    public string FisFooter { get => _fisFooter; set => OzellikAyarla(ref _fisFooter, value); }
    public decimal VarsayilanKdv { get => _varsayilanKdv; set => OzellikAyarla(ref _varsayilanKdv, value); }
    public string ParaBirimiSembolu { get => _paraBirimiSembolu; set => OzellikAyarla(ref _paraBirimiSembolu, value); }
    public string ParaBirimiKodu { get => _paraBirimiKodu; set => OzellikAyarla(ref _paraBirimiKodu, value); }
    public int VarsayilanVadeGunu { get => _varsayilanVadeGunu; set => OzellikAyarla(ref _varsayilanVadeGunu, value); }
    public string FaturaNotu { get => _faturaNotu; set => OzellikAyarla(ref _faturaNotu, value); }
    public string IrsaliyeNotu { get => _irsaliyeNotu; set => OzellikAyarla(ref _irsaliyeNotu, value); }

    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }
    public string BasariMesaji { get => _basariMesaji; set => OzellikAyarla(ref _basariMesaji, value); }
    public bool Yukleniyor { get => _yukleniyor; set => OzellikAyarla(ref _yukleniyor, value); }

    public ObservableCollection<AuditLog> AuditLoglar { get; } = new();

    // Yedekleme
    private readonly IVeritabaniYedekServisi? _yedekServisi;
    private string _yedekDurumu = string.Empty;
    private bool _yedekAliniyor;
    public ObservableCollection<YedekDosyasi> Yedekler { get; } = new();
    public string YedekKlasoru => _yedekServisi?.YedekKlasoru ?? string.Empty;
    public string YedekBilgisi => _yedekServisi == null
        ? string.Empty
        : $"Son yedek 24 saatten eskiyse program otomatik yedek alır (açılışta ve açık kaldığı sürece saatte bir bakılır). Her yedek alındıktan sonra açılarak denetlenir. Son {_yedekServisi.SaklanacakYedekSayisi} yedek saklanır, daha eskiler silinir.";
    private string _ikinciYedekKlasoru = string.Empty;
    public string IkinciYedekKlasoru { get => _ikinciYedekKlasoru; set => OzellikAyarla(ref _ikinciYedekKlasoru, value); }
    private string _ikinciKopyaDurumu = string.Empty;
    public string IkinciKopyaDurumu { get => _ikinciKopyaDurumu; set => OzellikAyarla(ref _ikinciKopyaDurumu, value); }
    public ICommand IkinciKonumSecKomutu { get; }
    public ICommand IkinciKonumKaydetKomutu { get; }
    public string YedekDurumu { get => _yedekDurumu; set => OzellikAyarla(ref _yedekDurumu, value); }
    public bool YedekAliniyor { get => _yedekAliniyor; set => OzellikAyarla(ref _yedekAliniyor, value); }
    public ICommand YedekAlKomutu { get; }
    public ICommand SeciliYedegiGeriYukleKomutu { get; }
    public ICommand DosyadanGeriYukleKomutu { get; }
    private YedekDosyasi? _seciliYedek;
    public YedekDosyasi? SeciliYedek { get => _seciliYedek; set { if (OzellikAyarla(ref _seciliYedek, value)) CommandManager.InvalidateRequerySuggested(); } }
    public ICommand YedekKlasoruAcKomutu { get; }

    public ICommand KaydetKomutu { get; }
    public ICommand YenileKomutu { get; }
    public ICommand AuditLoglariYenileKomutu { get; }

    public AyarlarGorunumModeli(ISistemAyarlariServisi? ayarlarServisi = null, IAuditLogServisi? auditLogServisi = null, IVeritabaniYedekServisi? yedekServisi = null)
    {
        _ayarlarServisi = ayarlarServisi;
        _auditLogServisi = auditLogServisi;
        _yedekServisi = yedekServisi;
        YedekAlKomutu = new KomutSarici(async _ => await YedekAlAsync(), _ => !YedekAliniyor && _yedekServisi != null);
        YedekKlasoruAcKomutu = new KomutSarici(() => YedekKlasorunuAc());
        SeciliYedegiGeriYukleKomutu = new KomutSarici(async _ => { if (SeciliYedek != null) await GeriYukleAsync(SeciliYedek.TamYol, SeciliYedek.OlusturmaTarihi); },
            _ => !YedekAliniyor && _yedekServisi != null && SeciliYedek != null && App.AgModu != Altyapi.Veri.AgModu.Istemci);
        // Ağ istemcisinde geri yükleme yapılmaz: sunucudaki ortak veritabanı bu bilgisayardan değiştirilmesin.
        DosyadanGeriYukleKomutu = new KomutSarici(async _ => await DosyadanGeriYukleAsync(), _ => !YedekAliniyor && _yedekServisi != null && App.AgModu != Altyapi.Veri.AgModu.Istemci);
        IkinciKonumSecKomutu = new KomutSarici(() => IkinciKonumSec(), () => _yedekServisi != null);
        IkinciKonumKaydetKomutu = new KomutSarici(() => IkinciKonumKaydet(), () => _yedekServisi != null);
        IkinciYedekKlasoru = _yedekServisi?.IkinciYedekKlasoru ?? string.Empty;
        YedekleriYenile();

        KaydetKomutu = new KomutSarici(async () => await KaydetAsync());
        YenileKomutu = new KomutSarici(async () => await AyarlariYukleAsync());
        AuditLoglariYenileKomutu = new KomutSarici(async () => await AuditLoglariYukleAsync());

        if (_ayarlarServisi != null)
        {
            _ = AyarlariYukleAsync();
        }
        if (_auditLogServisi != null)
        {
            _ = AuditLoglariYukleAsync();
        }
    }

    public async Task AyarlariYukleAsync()
    {
        if (_ayarlarServisi == null) return;

        try
        {
            Yukleniyor = true;
            HataMesaji = string.Empty;

            var ayar = await _ayarlarServisi.AyarlariGetirAsync();
            FirmaUnvani = ayar.FirmaUnvani;
            VergiDairesi = ayar.VergiDairesi ?? string.Empty;
            VergiNo = ayar.VergiNo ?? string.Empty;
            MersisNo = ayar.MersisNo ?? string.Empty;
            Telefon = ayar.Telefon ?? string.Empty;
            Eposta = ayar.Eposta ?? string.Empty;
            WebSitesi = ayar.WebSitesi ?? string.Empty;
            Adres = ayar.Adres ?? string.Empty;
            Il = ayar.Il ?? string.Empty;
            Ilce = ayar.Ilce ?? string.Empty;
            PostaKodu = ayar.PostaKodu ?? string.Empty;
            EBelgeKlasoru = ayar.EBelgeKlasoru ?? string.Empty;
            EFaturaSenaryoIndex = (int)ayar.EFaturaVarsayilanSenaryo;
            FisHeader = ayar.FisHeader ?? string.Empty;
            FisFooter = ayar.FisFooter ?? string.Empty;
            VarsayilanKdv = ayar.VarsayilanKdv;
            ParaBirimiSembolu = ayar.ParaBirimiSembolu;
            ParaBirimiKodu = ayar.ParaBirimiKodu;
            VarsayilanVadeGunu = ayar.VarsayilanVadeGunu;
            FaturaNotu = ayar.FaturaNotu ?? string.Empty;
            IrsaliyeNotu = ayar.IrsaliyeNotu ?? string.Empty;
        }
        catch (Exception ex)
        {
            HataMesaji = $"Ayarlar yüklenirken hata oluştu: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    public async Task AuditLoglariYukleAsync()
    {
        if (_auditLogServisi == null) return;

        try
        {
            var tablo = string.IsNullOrWhiteSpace(AuditTabloArama) ? null : AuditTabloArama.Trim();
            var kullanici = AuditKullaniciArama.Trim();
            // Kullanıcı süzgeci ada göre yapılır; süzgeç varken daha geniş bir pencereden aranır.
            var loglar = await _auditLogServisi.LoglariGetirAsync(tabloAdi: tablo, limit: kullanici.Length > 0 ? 1000 : 50);
            AuditLoglar.Clear();
            foreach (var log in loglar.Where(l => kullanici.Length == 0 || (l.KullaniciAdi ?? string.Empty).Contains(kullanici, StringComparison.CurrentCultureIgnoreCase)).Take(50))
            {
                AuditLoglar.Add(log);
            }
        }
        catch
        {
        }
    }

    public async Task KaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (string.IsNullOrWhiteSpace(FirmaUnvani))
        {
            HataMesaji = "Firma Ünvanı zorunludur.";
            return;
        }

        if (_ayarlarServisi == null)
        {
            BasariMesaji = "✅ Ayarlar başarıyla güncellendi (Demo Modu).";
            return;
        }

        try
        {
            Yukleniyor = true;
            var ayar = new SistemAyarlari
            {
                Id = 1,
                FirmaUnvani = FirmaUnvani,
                VergiDairesi = VergiDairesi,
                VergiNo = VergiNo,
                MersisNo = MersisNo,
                Telefon = Telefon,
                Eposta = Eposta,
                WebSitesi = WebSitesi,
                Adres = Adres,
                Il = Il,
                Ilce = Ilce,
                PostaKodu = PostaKodu,
                EBelgeKlasoru = EBelgeKlasoru,
                EFaturaVarsayilanSenaryo = (EtkiAlani.Modeller.Fatura.EFaturaSenaryosu)EFaturaSenaryoIndex,
                FisHeader = FisHeader,
                FisFooter = FisFooter,
                VarsayilanKdv = VarsayilanKdv,
                ParaBirimiSembolu = ParaBirimiSembolu,
                ParaBirimiKodu = ParaBirimiKodu,
                VarsayilanVadeGunu = VarsayilanVadeGunu,
                FaturaNotu = FaturaNotu,
                IrsaliyeNotu = IrsaliyeNotu
            };

            await _ayarlarServisi.AyarlariKaydetAsync(ayar);
            BasariMesaji = "✅ Firma ve sistem ayarları PostgreSQL veritabanına başarıyla kaydedildi!";
            await AuditLoglariYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Ayarlar kaydedilirken hata: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private void YedekleriYenile()
    {
        Yedekler.Clear();
        if (_yedekServisi == null) return;
        foreach (var yedek in _yedekServisi.YedekleriListele())
            Yedekler.Add(yedek);
        YedekDurumu = Yedekler.Count == 0
            ? "Henüz yedek alınmamış."
            : $"Son yedek: {Yedekler[0].OlusturmaTarihi:dd.MM.yyyy HH:mm} ({Yedekler[0].BoyutMetni})";
        IkinciKopyaDurumu = _yedekServisi.IkinciYedekKlasoru == null
            ? "⚠ İkinci konum tanımlı değil: bu bilgisayarın diski bozulursa veriler ve yedekler birlikte kaybolur. Bir harici disk, USB bellek veya ağ klasörü seçmeniz önerilir."
            : _yedekServisi.IkinciKopyaDurumu;
    }

    private void IkinciKonumSec()
    {
        var secim = new Microsoft.Win32.OpenFolderDialog { Title = "İkinci yedek konumunu seçin" };
        if (System.IO.Directory.Exists(IkinciYedekKlasoru)) secim.InitialDirectory = IkinciYedekKlasoru;
        if (secim.ShowDialog() == true)
        {
            IkinciYedekKlasoru = secim.FolderName;
            IkinciKonumKaydet();
        }
    }

    private void IkinciKonumKaydet()
    {
        if (_yedekServisi == null) return;
        try
        {
            _yedekServisi.IkinciYedekKlasoruAyarla(IkinciYedekKlasoru);
            YedekleriYenile();
            if (_yedekServisi.IkinciYedekKlasoru != null && _yedekServisi.IkinciKopyaDurumu.Length == 0)
                IkinciKopyaDurumu = "✅ Kaydedildi. Bundan sonraki her yedeğin kopyası bu konuma da yazılacak; hemen kopyalamak için \"Şimdi Yedek Al\".";
        }
        catch (Exception ex)
        {
            IkinciKopyaDurumu = $"❌ {ex.Message}";
        }
    }

    private async Task YedekAlAsync()
    {
        if (_yedekServisi == null) return;
        YedekAliniyor = true;
        YedekDurumu = "Yedek alınıyor, lütfen bekleyin...";
        try
        {
            var yedek = await _yedekServisi.ElleYedekAlAsync();
            YedekleriYenile();
            YedekDurumu = $"✅ Yedek alındı: {yedek.DosyaAdi} ({yedek.BoyutMetni})";
        }
        catch (Exception ex)
        {
            YedekDurumu = $"❌ {ex.Message}";
        }
        finally
        {
            YedekAliniyor = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task DosyadanGeriYukleAsync()
    {
        var secim = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Geri yüklenecek yedek dosyasını seçin",
            Filter = "Veritabanı yedeği (*.dump)|*.dump",
            InitialDirectory = System.IO.Directory.Exists(YedekKlasoru) ? YedekKlasoru : null
        };
        if (secim.ShowDialog() != true) return;
        await GeriYukleAsync(secim.FileName, System.IO.File.GetLastWriteTime(secim.FileName));
    }

    // Geri yükleme tüm verinin yerine yedeği koyar: kullanıcı "GERİ YÜKLE" yazarak onaylar. Önce güncel durumun
    // yedeği alınır ve eski veritabanı silinmeden saklanır; işlem sonunda program yeniden başlar.
    private async Task GeriYukleAsync(string yol, DateTime yedekTarihi)
    {
        if (_yedekServisi == null) return;
        var onay = new Masaustu.Gorunumler.Ortak.MetinGirisPenceresi(
            "Yedekten Geri Yükle",
            $"{System.IO.Path.GetFileName(yol)}  ({yedekTarihi:dd.MM.yyyy HH:mm})\n\n" +
            "Programdaki TÜM veriler bu yedekteki haline döner; yedekten sonra girilen kayıtlar görünmez olur. " +
            "Güvenlik için önce şu anki verinin yedeği alınır ve eski veritabanı silinmeden saklanır. " +
            "İşlem bitince program yeniden başlar; başka bilgisayarda açık olan program varsa kapatın.\n\n" +
            "Devam etmek için GERİ YÜKLE yazın:")
        { Owner = System.Windows.Application.Current.MainWindow };
        if (onay.ShowDialog() != true) return;
        if (!string.Equals(onay.GirilenMetin.Trim(), "GERİ YÜKLE", StringComparison.CurrentCultureIgnoreCase))
        {
            YedekDurumu = "Geri yükleme iptal edildi (onay metni eşleşmedi).";
            return;
        }

        YedekAliniyor = true;
        CommandManager.InvalidateRequerySuggested();
        YedekDurumu = "Geri yükleniyor: önce güncel verinin yedeği alınıyor, sonra yedek açılıyor. Lütfen bekleyin...";
        try
        {
            var eski = await _yedekServisi.GeriYukleAsync(yol);
            System.Windows.MessageBox.Show(
                $"Yedek geri yüklendi.\n\nÖnceki veriler silinmedi: \"{eski}\" adlı veritabanında ve yedek klasöründe duruyor.\n\nProgram şimdi yeniden başlayacak.",
                "Geri Yükleme Tamamlandı", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            App.YenidenBaslat = true;
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "GeriYukle");
            YedekDurumu = $"❌ Geri yüklenemedi, verileriniz değişmedi: {ex.Message}";
        }
        finally
        {
            YedekAliniyor = false;
            YedekleriYenile();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void YedekKlasorunuAc()
    {
        if (string.IsNullOrEmpty(YedekKlasoru)) return;
        System.IO.Directory.CreateDirectory(YedekKlasoru);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{YedekKlasoru}\"") { UseShellExecute = true });
    }
}
