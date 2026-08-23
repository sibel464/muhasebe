using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Altyapi.Veri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class GirisGorunumModeli : TemelGorunumModeli
{
    private readonly MuhasebeBaglami _baglam;
    
    private string _kullaniciAdi = string.Empty;
    private string _hataMesaji = string.Empty;
    private bool _yukleniyor = false;

    public string KullaniciAdi
    {
        get => _kullaniciAdi;
        set => OzellikAyarla(ref _kullaniciAdi, value);
    }

    public string HataMesaji
    {
        get => _hataMesaji;
        set => OzellikAyarla(ref _hataMesaji, value);
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

    public GirisGorunumModeli(MuhasebeBaglami baglam)
    {
        _baglam = baglam;
        GirisKomutu = new KomutSarici(async _ => await GirisYapAsync(), _ => GirisYapabilir());
    }

    private bool GirisYapabilir() =>
        !string.IsNullOrWhiteSpace(KullaniciAdi) && !Yukleniyor;

    private async Task GirisYapAsync()
    {
        var sifre = SifreAl?.Invoke() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(sifre))
        {
            HataMesaji = "Şifre boş olamaz.";
            return;
        }

        Yukleniyor = true;
        HataMesaji = string.Empty;

        try
        {
            // Veritabanından kullanıcıyı sorgula
            var kullanici = await _baglam.Kullanicilar
                .FirstOrDefaultAsync(k => k.KullaniciAdi == KullaniciAdi && k.Aktif);

            if (kullanici != null && BCrypt.Net.BCrypt.Verify(sifre, kullanici.SifreHash))
            {
                // Giriş başarılı
                kullanici.SonGirisTarihi = DateTime.UtcNow;
                await _baglam.SaveChangesAsync();
                
                GirisBasarili?.Invoke();
            }
            else
            {
                HataMesaji = "Kullanıcı adı veya şifre hatalı.";
            }
        }
        catch (Exception ex)
        {
            HataMesaji = "Veritabanına bağlanılamadı. Lütfen servis durumunu kontrol ediniz.";
            // Gerçek loglama veya console çıktı
            System.Diagnostics.Debug.WriteLine($"Giriş Hatası: {ex.Message}");
        }
        finally
        {
            Yukleniyor = false;
        }
    }
}
