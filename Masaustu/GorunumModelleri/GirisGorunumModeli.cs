using System.Windows;
using System.Windows.Input;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class GirisGorunumModeli : TemelGorunumModeli
{
    private string _kullaniciAdi = string.Empty;
    private string _hataMessaji = string.Empty;
    private bool _yukleniyor = false;

    public string KullaniciAdi
    {
        get => _kullaniciAdi;
        set => OzellikAyarla(ref _kullaniciAdi, value);
    }

    public string HataMesaji
    {
        get => _hataMessaji;
        set => OzellikAyarla(ref _hataMessaji, value);
    }

    public bool Yukleniyor
    {
        get => _yukleniyor;
        set => OzellikAyarla(ref _yukleniyor, value);
    }

    // Şifreyi view'dan alacağız (PasswordBox binding sorunu)
    public Func<string>? SifreAl { get; set; }
    public Action? GirisBasarili { get; set; }

    public ICommand GirisKomutu { get; }

    public GirisGorunumModeli()
    {
        GirisKomutu = new KomutSarici(GirisYap, GirisYapabilir);
    }

    private bool GirisYapabilir(object? _) =>
        !string.IsNullOrWhiteSpace(KullaniciAdi) && !Yukleniyor;

    private void GirisYap(object? _)
    {
        var sifre = SifreAl?.Invoke() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(sifre))
        {
            HataMesaji = "Şifre boş olamaz.";
            return;
        }

        Yukleniyor = true;
        HataMesaji = string.Empty;

        // TODO: Gerçek kullanıcı doğrulama - şimdilik basit kontrol
        if (KullaniciAdi.Equals("admin", StringComparison.OrdinalIgnoreCase) && sifre == "admin")
        {
            GirisBasarili?.Invoke();
        }
        else
        {
            HataMesaji = "Kullanıcı adı veya şifre hatalı.";
        }

        Yukleniyor = false;
    }
}
