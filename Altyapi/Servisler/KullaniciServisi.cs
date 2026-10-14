using EtkiAlani.Modeller.Kullanici;
using Uygulama.Arayuzler;
using Microsoft.EntityFrameworkCore;
using Altyapi.Veri;

namespace Altyapi.Servisler;

// IDbContextFactory kullanılarak Singleton olarak kayıt edilebilir.
// Bu sayede AktifKullanici bilgisi tüm uygulama boyunca tek instance'da tutulur.
public class KullaniciServisi : IKullaniciServisi
{
    private readonly IDbContextFactory<MuhasebeBaglami> _baglamFactory;

    public Kullanici? AktifKullanici { get; private set; }
    public KullaniciYetkileri? AktifYetkiler => AktifKullanici?.Yetkiler;

    public KullaniciServisi(IDbContextFactory<MuhasebeBaglami> baglamFactory)
    {
        _baglamFactory = baglamFactory;
    }

    public async Task<bool> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(kullaniciAdi) || string.IsNullOrWhiteSpace(sifre))
            return false;

        var arananKullanici = kullaniciAdi.Trim().ToLowerInvariant();
        await GirisAsamasiniLoglaAsync("DB context oluşturuluyor");

        await using var baglam = await _baglamFactory.CreateDbContextAsync(cancellationToken);
        await GirisAsamasiniLoglaAsync("Kullanıcı sorgusu başladı");

        var kullanici = await baglam.Kullanicilar
            .AsNoTracking()
            .Include(k => k.Yetkiler)
            .FirstOrDefaultAsync(k => k.KullaniciAdi == arananKullanici && k.Aktif, cancellationToken);
        await GirisAsamasiniLoglaAsync($"Kullanıcı sorgusu tamamlandı: {kullanici != null}");

        if (kullanici == null)
            return false;

        // BCrypt doğrulaması CPU yoğun olduğundan UI thread'i dışında çalıştırılır.
        var sifreDogru = await Task.Run(() => BCrypt.Net.BCrypt.Verify(sifre, kullanici.SifreHash), cancellationToken);
        await GirisAsamasiniLoglaAsync("Şifre doğrulaması tamamlandı");

        if (sifreDogru)
        {
            await GirisAsamasiniLoglaAsync("Oturum kaydı başlıyor");
            var sonGirisTarihi = DateTime.UtcNow;
            await baglam.Kullanicilar
                .Where(k => k.Id == kullanici.Id)
                .ExecuteUpdateAsync(guncelleme => guncelleme
                    .SetProperty(k => k.SonGirisTarihi, sonGirisTarihi), cancellationToken);
            kullanici.SonGirisTarihi = sonGirisTarihi;
            await GirisAsamasiniLoglaAsync("Oturum kaydı tamamlandı");

            AktifKullanici = kullanici;
            return true;
        }

        return false;
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

    public void CikisYap()
    {
        AktifKullanici = null;
    }

    public bool YetkiliMi(Func<KullaniciYetkileri, bool> yetkiSorgusu)
    {
        if (AktifKullanici != null && (AktifKullanici.Rol == "Yonetici" || AktifKullanici.Rol == "Yönetici"))
            return true;

        return AktifYetkiler != null && yetkiSorgusu(AktifYetkiler);
    }

    public void YetkiKontrolEt(Func<KullaniciYetkileri, bool> yetkiSorgusu, string islemAdi)
    {
        if (AktifKullanici != null && (AktifKullanici.Rol == "Yonetici" || AktifKullanici.Rol == "Yönetici"))
            return;

        if (AktifYetkiler == null || !yetkiSorgusu(AktifYetkiler))
        {
            throw new YetkisizErisimException(islemAdi);
        }
    }

    public async Task<List<Kullanici>> TumunuGetirAsync()
    {
        YetkiKontrolEt(y => y.KullaniciYonet, "Kullanıcıları Görüntüleme");
        await using var baglam = await _baglamFactory.CreateDbContextAsync();
        return await baglam.Kullanicilar.Include(k => k.Yetkiler).ToListAsync();
    }

    public async Task<Kullanici?> GetirAsync(int id)
    {
        YetkiKontrolEt(y => y.KullaniciYonet, "Kullanıcı Görüntüleme");
        await using var baglam = await _baglamFactory.CreateDbContextAsync();
        return await baglam.Kullanicilar.Include(k => k.Yetkiler).FirstOrDefaultAsync(k => k.Id == id);
    }

    public async Task<Kullanici> EkleAsync(Kullanici kullanici, string acikSifre)
    {
        YetkiKontrolEt(y => y.KullaniciYonet, "Kullanıcı Ekleme");
        await using var baglam = await _baglamFactory.CreateDbContextAsync();

        kullanici.KullaniciAdi = kullanici.KullaniciAdi.Trim().ToLowerInvariant();

        if (await baglam.Kullanicilar.AnyAsync(k => k.KullaniciAdi == kullanici.KullaniciAdi))
            throw new InvalidOperationException("Bu kullanıcı adı zaten kullanılıyor.");

        kullanici.SifreHash = BCrypt.Net.BCrypt.HashPassword(acikSifre);

        if (kullanici.Yetkiler == null)
            kullanici.Yetkiler = new KullaniciYetkileri(); // Default false hepsi

        baglam.Kullanicilar.Add(kullanici);
        await baglam.SaveChangesAsync();
        return kullanici;
    }

    public async Task GuncelleAsync(Kullanici kullanici)
    {
        YetkiKontrolEt(y => y.KullaniciYonet, "Kullanıcı Güncelleme");
        await using var baglam = await _baglamFactory.CreateDbContextAsync();

        kullanici.KullaniciAdi = kullanici.KullaniciAdi.Trim().ToLowerInvariant();

        if (await baglam.Kullanicilar.AnyAsync(k => k.KullaniciAdi == kullanici.KullaniciAdi && k.Id != kullanici.Id))
            throw new InvalidOperationException("Bu kullanıcı adı zaten kullanılıyor.");

        var mevcut = await baglam.Kullanicilar.Include(k => k.Yetkiler)
            .FirstOrDefaultAsync(k => k.Id == kullanici.Id)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        mevcut.KullaniciAdi = kullanici.KullaniciAdi;
        mevcut.Ad = kullanici.Ad;
        mevcut.Soyad = kullanici.Soyad;
        mevcut.Eposta = kullanici.Eposta;
        mevcut.Aktif = kullanici.Aktif;
        mevcut.Rol = kullanici.Rol;
        mevcut.GuncellemeTarihi = DateTime.UtcNow;

        if (kullanici.Yetkiler != null)
        {
            mevcut.Yetkiler ??= new KullaniciYetkileri();
            foreach (var ozellik in typeof(KullaniciYetkileri).GetProperties()
                         .Where(p => p.DeclaringType == typeof(KullaniciYetkileri) && p.PropertyType == typeof(bool)))
                ozellik.SetValue(mevcut.Yetkiler, ozellik.GetValue(kullanici.Yetkiler));
        }

        await baglam.SaveChangesAsync();

        if (AktifKullanici?.Id == mevcut.Id)
            AktifKullanici = mevcut.Aktif ? mevcut : null;
    }

    public async Task SifreDegistirAsync(int kullaniciId, string yeniAcikSifre)
    {
        YetkiKontrolEt(y => y.KullaniciYonet, "Kullanıcı Şifresi Değiştirme");
        await using var baglam = await _baglamFactory.CreateDbContextAsync();
        var kullanici = await baglam.Kullanicilar.FindAsync(kullaniciId);
        if (kullanici == null) throw new InvalidOperationException("Kullanıcı bulunamadı.");

        kullanici.SifreHash = BCrypt.Net.BCrypt.HashPassword(yeniAcikSifre);
        await baglam.SaveChangesAsync();
    }

    public async Task SilAsync(int id)
    {
        YetkiKontrolEt(y => y.KullaniciYonet, "Kullanıcı Silme");
        if (AktifKullanici != null && AktifKullanici.Id == id)
            throw new InvalidOperationException("Aktif oturum açmış kullanıcı silinemez.");

        await using var baglam = await _baglamFactory.CreateDbContextAsync();
        var kullanici = await baglam.Kullanicilar.FindAsync(id);
        if (kullanici == null) return;

        kullanici.Silindi = true;
        kullanici.Aktif = false;
        await baglam.SaveChangesAsync();
    }
}
