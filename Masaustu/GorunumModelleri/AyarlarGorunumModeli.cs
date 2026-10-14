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

    private string _firmaUnvani = "Örnek Ticaret Ltd. Şti.";
    private string _vergiDairesi = "Kadıköy V.D.";
    private string _vergiNo = "1234567890";
    private string _telefon = "0216 555 00 00";
    private string _eposta = "info@ornekticaret.com";
    private string _webSitesi = "www.ornekticaret.com";
    private string _adres = "Atatürk Cad. No: 15/A Kadıköy / İstanbul";
    private string _fisHeader = "Örnek Ticaret Ltd. Şti.";
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
    public string Telefon { get => _telefon; set => OzellikAyarla(ref _telefon, value); }
    public string Eposta { get => _eposta; set => OzellikAyarla(ref _eposta, value); }
    public string WebSitesi { get => _webSitesi; set => OzellikAyarla(ref _webSitesi, value); }
    public string Adres { get => _adres; set => OzellikAyarla(ref _adres, value); }
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
        : $"Program her açılışta son yedek 24 saatten eskiyse otomatik yedek alır. Son {_yedekServisi.SaklanacakYedekSayisi} yedek saklanır, daha eskiler silinir.";
    public string YedekDurumu { get => _yedekDurumu; set => OzellikAyarla(ref _yedekDurumu, value); }
    public bool YedekAliniyor { get => _yedekAliniyor; set => OzellikAyarla(ref _yedekAliniyor, value); }
    public ICommand YedekAlKomutu { get; }
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
            Telefon = ayar.Telefon ?? string.Empty;
            Eposta = ayar.Eposta ?? string.Empty;
            WebSitesi = ayar.WebSitesi ?? string.Empty;
            Adres = ayar.Adres ?? string.Empty;
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
            var loglar = await _auditLogServisi.LoglariGetirAsync(limit: 50);
            AuditLoglar.Clear();
            foreach (var log in loglar)
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
                Telefon = Telefon,
                Eposta = Eposta,
                WebSitesi = WebSitesi,
                Adres = Adres,
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

    private void YedekKlasorunuAc()
    {
        if (string.IsNullOrEmpty(YedekKlasoru)) return;
        System.IO.Directory.CreateDirectory(YedekKlasoru);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{YedekKlasoru}\"") { UseShellExecute = true });
    }
}
