using System.Windows.Input;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class GirisGorunumModeli : TemelGorunumModeli
{
    private readonly IKullaniciServisi _kullaniciServisi;
    
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
    public Func<Task>? GirisBasarili { get; set; }

    public ICommand GirisKomutu { get; }

    public GirisGorunumModeli(IKullaniciServisi kullaniciServisi)
    {
        _kullaniciServisi = kullaniciServisi;
        GirisKomutu = new KomutSarici(async _ => await GirisYapAsync(), _ => GirisYapabilir());
    }

    private bool GirisYapabilir() =>
        !string.IsNullOrWhiteSpace(KullaniciAdi) && !Yukleniyor;

    private async Task GirisYapAsync()
    {
        if (Yukleniyor)
            return;

        var sifre = SifreAl?.Invoke() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(sifre))
        {
            HataMesaji = "Şifre boş olamaz.";
            return;
        }

        Yukleniyor = true;
        HataMesaji = string.Empty;
        using var girisIptal = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            await GirisAsamasiniLoglaAsync("Authentication başladı");
            var basarili = await _kullaniciServisi
            .GirisYapAsync(KullaniciAdi, sifre, girisIptal.Token);
            await GirisAsamasiniLoglaAsync($"Authentication tamamlandı: {basarili}");

            if (basarili)
            {
                await GirisAsamasiniLoglaAsync("Başarılı giriş callback'i çağrılıyor");
                if (GirisBasarili != null)
                    await GirisBasarili();
            }
            else
            {
                HataMesaji = "Kullanıcı adı veya şifre hatalı.";
            }
        }
        catch (OperationCanceledException) when (girisIptal.IsCancellationRequested)
        {
            HataMesaji = "Giriş işlemi zaman aşımına uğradı. Veritabanı bağlantısını kontrol edin.";
        }
        catch (Exception ex)
        {
            HataMesaji = "Giriş işlemi sırasında bir hata oluştu. Lütfen bağlantınızı kontrol edin.";
            await GirisHatasiLoglaAsync(ex);
            System.Diagnostics.Debug.WriteLine($"Giriş Hatası: {ex.Message}");
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private static readonly SemaphoreSlim LogKilidi = new(1, 1);

    private static async Task GirisAsamasiniLoglaAsync(string asama)
    {
        await LogKilidi.WaitAsync();
        try
        {
            var yol = System.IO.Path.Combine(AppContext.BaseDirectory, "login-akis.log");
            await System.IO.File.AppendAllTextAsync(yol, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {asama}{Environment.NewLine}");
        }
        catch
        {
        }
        finally
        {
            LogKilidi.Release();
        }
    }

    private static async Task GirisHatasiLoglaAsync(Exception exception)
    {
        await LogKilidi.WaitAsync();
        try
        {
            var yol = System.IO.Path.Combine(AppContext.BaseDirectory, "login-hatalari.log");
            await System.IO.File.AppendAllTextAsync(yol, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Giriş hatası: {exception.Message}{Environment.NewLine}");
        }
        catch
        {
        }
        finally
        {
            LogKilidi.Release();
        }
    }
}
