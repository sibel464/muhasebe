using EtkiAlani.Modeller.Guvenlik;
using EtkiAlani.Modeller.Kullanici;
using Uygulama.Arayuzler;
using Microsoft.EntityFrameworkCore;
using Altyapi.Veri;

namespace Altyapi.Servisler;

// IDbContextFactory kullanılarak Singleton olarak kayıt edilebilir.
// Bu sayede AktifKullanici bilgisi tüm uygulama boyunca tek instance'da tutulur.
public class KullaniciServisi : IKullaniciServisi
{
    public const int AzamiHataliDeneme = 5;
    public static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(15);
    public const int EnKisaSifreUzunlugu = 8;

    // Kullanıcı bulunamadığında da aynı süre harcanır; yanıt süresinden kullanıcı adı tahmin edilemez.
    private static readonly Lazy<string> SahteHash = new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")));

    private readonly IDbContextFactory<MuhasebeBaglami> _baglamFactory;

    public Kullanici? AktifKullanici { get; private set; }
    public KullaniciYetkileri? AktifYetkiler => AktifKullanici?.Yetkiler;

    public KullaniciServisi(IDbContextFactory<MuhasebeBaglami> baglamFactory)
    {
        _baglamFactory = baglamFactory;
    }

    public async Task<bool> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken cancellationToken = default)
        => (await GirisDeneAsync(kullaniciAdi, sifre, cancellationToken)).Basarili;

    public async Task<GirisSonucu> GirisDeneAsync(string kullaniciAdi, string sifre, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(kullaniciAdi) || string.IsNullOrWhiteSpace(sifre))
            return new GirisSonucu(GirisDurumu.HataliBilgi);

        var arananKullanici = kullaniciAdi.Trim().ToLowerInvariant();
        await GirisAsamasiniLoglaAsync("DB context oluşturuluyor");

        await using var baglam = await _baglamFactory.CreateDbContextAsync(cancellationToken);
        await GirisAsamasiniLoglaAsync("Kullanıcı sorgusu başladı");

        var kullanici = await baglam.Kullanicilar
            .AsNoTracking()
            .Include(k => k.Yetkiler)
            .FirstOrDefaultAsync(k => k.KullaniciAdi == arananKullanici && k.Aktif, cancellationToken);
        await GirisAsamasiniLoglaAsync($"Kullanıcı sorgusu tamamlandı: {kullanici != null}");

        // BCrypt doğrulaması CPU yoğun olduğundan UI thread'i dışında çalıştırılır.
        if (kullanici == null)
        {
            await Task.Run(() => BCrypt.Net.BCrypt.Verify(sifre, SahteHash.Value), cancellationToken);
            await OturumOlayiKaydetAsync(baglam, null, arananKullanici, "Hatalı giriş: kullanıcı bulunamadı veya pasif");
            return new GirisSonucu(GirisDurumu.HataliBilgi);
        }

        var simdi = DateTime.UtcNow;
        if (kullanici.KilitBitisTarihi > simdi)
        {
            await OturumOlayiKaydetAsync(baglam, kullanici.Id, kullanici.KullaniciAdi, "Kilitli hesaba giriş denemesi");
            return new GirisSonucu(GirisDurumu.HesapKilitli, KilitBitisTarihi: kullanici.KilitBitisTarihi);
        }

        var sifreDogru = await Task.Run(() => BCrypt.Net.BCrypt.Verify(sifre, kullanici.SifreHash), cancellationToken);
        await GirisAsamasiniLoglaAsync("Şifre doğrulaması tamamlandı");

        if (!sifreDogru)
        {
            // Sayaç veritabanında atomik artırılır; eşzamanlı denemeler sayımı kaçıramaz.
            await baglam.Kullanicilar
                .Where(k => k.Id == kullanici.Id)
                .ExecuteUpdateAsync(g => g.SetProperty(k => k.HataliGirisSayisi, k => k.HataliGirisSayisi + 1), cancellationToken);
            var hataSayisi = await baglam.Kullanicilar.Where(k => k.Id == kullanici.Id)
                .Select(k => k.HataliGirisSayisi).SingleAsync(cancellationToken);

            if (hataSayisi >= AzamiHataliDeneme)
            {
                var kilitBitis = simdi.Add(KilitSuresi);
                await baglam.Kullanicilar
                    .Where(k => k.Id == kullanici.Id)
                    .ExecuteUpdateAsync(g => g
                        .SetProperty(k => k.HataliGirisSayisi, 0)
                        .SetProperty(k => k.KilitBitisTarihi, kilitBitis), cancellationToken);
                await OturumOlayiKaydetAsync(baglam, kullanici.Id, kullanici.KullaniciAdi, $"Hesap {AzamiHataliDeneme} hatalı deneme nedeniyle kilitlendi");
                return new GirisSonucu(GirisDurumu.HesapKilitli, KilitBitisTarihi: kilitBitis);
            }

            await OturumOlayiKaydetAsync(baglam, kullanici.Id, kullanici.KullaniciAdi, $"Hatalı şifre ({hataSayisi}/{AzamiHataliDeneme})");
            return new GirisSonucu(GirisDurumu.HataliBilgi, KalanDeneme: AzamiHataliDeneme - hataSayisi);
        }

        await GirisAsamasiniLoglaAsync("Oturum kaydı başlıyor");
        await baglam.Kullanicilar
            .Where(k => k.Id == kullanici.Id)
            .ExecuteUpdateAsync(g => g
                .SetProperty(k => k.SonGirisTarihi, simdi)
                .SetProperty(k => k.HataliGirisSayisi, 0)
                .SetProperty(k => k.KilitBitisTarihi, (DateTime?)null), cancellationToken);
        kullanici.SonGirisTarihi = simdi;
        kullanici.HataliGirisSayisi = 0;
        kullanici.KilitBitisTarihi = null;
        await OturumOlayiKaydetAsync(baglam, kullanici.Id, kullanici.KullaniciAdi, "Başarılı giriş");
        await GirisAsamasiniLoglaAsync("Oturum kaydı tamamlandı");

        AktifKullanici = kullanici;
        return new GirisSonucu(GirisDurumu.Basarili, SifreDegistirmeli: kullanici.SifreDegistirmeli);
    }

    // Oturum olayları denetim kaydına yazılır; kayıt hatası girişi engellemez.
    private static async Task OturumOlayiKaydetAsync(MuhasebeBaglami baglam, int? kullaniciId, string kullaniciAdi, string olay)
    {
        try
        {
            baglam.AuditLoglar.Add(new AuditLog
            {
                KullaniciId = kullaniciId,
                KullaniciAdi = kullaniciAdi.Length > 100 ? kullaniciAdi[..100] : kullaniciAdi,
                IslemTuru = AuditIslemTuru.Guncelleme,
                TabloAdi = "Oturum",
                KayitId = kullaniciId?.ToString() ?? "-",
                Tarih = DateTime.UtcNow,
                IstemciBilgisi = Environment.MachineName,
                IslemDetayi = olay
            });
            await baglam.SaveChangesAsync();
        }
        catch
        {
            baglam.ChangeTracker.Clear();
        }
    }

    private static readonly SemaphoreSlim LogKilidi = new(1, 1);

    private static async Task GirisAsamasiniLoglaAsync(string asama)
    {
        await LogKilidi.WaitAsync();
        try
        {
            var yol = Altyapi.Veri.UygulamaKlasorleri.LogYolu("login-akis.log");
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

    public static bool YoneticiRolu(string? rol) => rol is "Yonetici" or "Yönetici";

    private bool AktifYoneticiMi => AktifKullanici != null && YoneticiRolu(AktifKullanici.Rol);

    public bool YetkiliMi(Func<KullaniciYetkileri, bool> yetkiSorgusu)
    {
        if (AktifYoneticiMi)
            return true;

        return AktifYetkiler != null && yetkiSorgusu(AktifYetkiler);
    }

    public void YetkiKontrolEt(Func<KullaniciYetkileri, bool> yetkiSorgusu, string islemAdi)
    {
        if (AktifYoneticiMi)
            return;

        if (AktifYetkiler == null || !yetkiSorgusu(AktifYetkiler))
        {
            throw new YetkisizErisimException(islemAdi);
        }
    }

    // Kurallar: en az 8 karakter, harf ve rakam içermeli, kullanıcı adını içermemeli.
    public static void SifreKuraliDogrula(string? sifre, string? kullaniciAdi = null)
    {
        if (string.IsNullOrWhiteSpace(sifre) || sifre.Length < EnKisaSifreUzunlugu)
            throw new ArgumentException($"Şifre en az {EnKisaSifreUzunlugu} karakter olmalıdır.");
        if (sifre.Length > 72)
            throw new ArgumentException("Şifre en fazla 72 karakter olabilir.");
        if (!sifre.Any(char.IsLetter) || !sifre.Any(char.IsDigit))
            throw new ArgumentException("Şifre en az bir harf ve bir rakam içermelidir.");
        if (!string.IsNullOrWhiteSpace(kullaniciAdi) && kullaniciAdi.Trim().Length >= 3 &&
            sifre.Contains(kullaniciAdi.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Şifre kullanıcı adını içeremez.");
    }

    // Yönetici olmayan bir kullanıcı yöneticisi, kendisinde olmayan bir yetkiyi başkasına (veya kendine) veremez.
    private void YetkiVermeSiniriniDenetle(KullaniciYetkileri? eski, KullaniciYetkileri? yeni)
    {
        if (AktifYoneticiMi || yeni == null)
            return;

        foreach (var ozellik in YetkiOzellikleri)
        {
            var yeniDeger = (bool)ozellik.GetValue(yeni)!;
            var eskiDeger = eski != null && (bool)ozellik.GetValue(eski)!;
            var bende = AktifYetkiler != null && (bool)ozellik.GetValue(AktifYetkiler)!;
            if (yeniDeger && !eskiDeger && !bende)
                throw new YetkisizErisimException($"'{ozellik.Name}' yetkisini verme");
        }
    }

    private static readonly System.Reflection.PropertyInfo[] YetkiOzellikleri = typeof(KullaniciYetkileri).GetProperties()
        .Where(p => p.DeclaringType == typeof(KullaniciYetkileri) && p.PropertyType == typeof(bool))
        .ToArray();

    private static async Task BaskaAktifYoneticiOlmaliAsync(MuhasebeBaglami baglam, int haricKullaniciId)
    {
        if (!await baglam.Kullanicilar.AnyAsync(k => k.Id != haricKullaniciId && k.Aktif && (k.Rol == "Yonetici" || k.Rol == "Yönetici")))
            throw new InvalidOperationException("Sistemde en az bir aktif yönetici kalmalıdır.");
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
        if (!AktifYoneticiMi && YoneticiRolu(kullanici.Rol))
            throw new YetkisizErisimException("Yönetici rolünde kullanıcı oluşturma");
        YetkiVermeSiniriniDenetle(null, kullanici.Yetkiler);

        kullanici.KullaniciAdi = kullanici.KullaniciAdi?.Trim().ToLowerInvariant() ?? string.Empty;
        if (kullanici.KullaniciAdi.Length < 3 || kullanici.KullaniciAdi.Length > 100 || kullanici.KullaniciAdi.Any(char.IsWhiteSpace))
            throw new ArgumentException("Kullanıcı adı 3-100 karakter olmalı ve boşluk içermemelidir.");
        SifreKuraliDogrula(acikSifre, kullanici.KullaniciAdi);

        await using var baglam = await _baglamFactory.CreateDbContextAsync();
        if (await baglam.Kullanicilar.IgnoreQueryFilters().AnyAsync(k => k.KullaniciAdi == kullanici.KullaniciAdi))
            throw new InvalidOperationException("Bu kullanıcı adı zaten kullanılıyor.");

        kullanici.SifreHash = BCrypt.Net.BCrypt.HashPassword(acikSifre);
        // Yöneticinin belirlediği ilk şifre geçicidir; kullanıcı ilk girişte kendi şifresini belirler.
        kullanici.SifreDegistirmeli = true;
        kullanici.HataliGirisSayisi = 0;
        kullanici.KilitBitisTarihi = null;

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

        // Yönetici olmayan bir kullanıcı yöneticisi, yönetici rolü atayamaz ve yöneticileri düzenleyemez.
        if (!AktifYoneticiMi && (YoneticiRolu(kullanici.Rol) || YoneticiRolu(mevcut.Rol)))
            throw new YetkisizErisimException("Yönetici rolünü atama / yönetici hesabını düzenleme");
        YetkiVermeSiniriniDenetle(mevcut.Yetkiler, kullanici.Yetkiler);

        if (AktifKullanici?.Id == mevcut.Id && !kullanici.Aktif)
            throw new InvalidOperationException("Kendi hesabınızı pasife alamazsınız.");
        if (YoneticiRolu(mevcut.Rol) && mevcut.Aktif && !(YoneticiRolu(kullanici.Rol) && kullanici.Aktif))
            await BaskaAktifYoneticiOlmaliAsync(baglam, mevcut.Id);

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
            foreach (var ozellik in YetkiOzellikleri)
                ozellik.SetValue(mevcut.Yetkiler, ozellik.GetValue(kullanici.Yetkiler));
        }

        await baglam.SaveChangesAsync();

        if (AktifKullanici?.Id == mevcut.Id)
            AktifKullanici = mevcut.Aktif ? mevcut : null;
    }

    // Yönetici tarafından şifre sıfırlama: yeni şifre geçicidir ve hesap kilidi kaldırılır.
    public async Task SifreDegistirAsync(int kullaniciId, string yeniAcikSifre)
    {
        YetkiKontrolEt(y => y.KullaniciYonet, "Kullanıcı Şifresi Değiştirme");
        await using var baglam = await _baglamFactory.CreateDbContextAsync();
        var kullanici = await baglam.Kullanicilar.FindAsync(kullaniciId);
        if (kullanici == null) throw new InvalidOperationException("Kullanıcı bulunamadı.");
        if (!AktifYoneticiMi && YoneticiRolu(kullanici.Rol))
            throw new YetkisizErisimException("Yönetici şifresini sıfırlama");
        SifreKuraliDogrula(yeniAcikSifre, kullanici.KullaniciAdi);

        kullanici.SifreHash = BCrypt.Net.BCrypt.HashPassword(yeniAcikSifre);
        kullanici.SifreDegistirmeli = AktifKullanici?.Id != kullaniciId;
        kullanici.SifreDegistirmeTarihi = DateTime.UtcNow;
        kullanici.HataliGirisSayisi = 0;
        kullanici.KilitBitisTarihi = null;
        await baglam.SaveChangesAsync();
    }

    public async Task KendiSifreniDegistirAsync(string mevcutSifre, string yeniSifre)
    {
        var aktif = AktifKullanici ?? throw new YetkisizErisimException("Oturum doğrulama");
        if (string.IsNullOrEmpty(mevcutSifre))
            throw new ArgumentException("Mevcut şifrenizi giriniz.");
        SifreKuraliDogrula(yeniSifre, aktif.KullaniciAdi);

        await using var baglam = await _baglamFactory.CreateDbContextAsync();
        var kullanici = await baglam.Kullanicilar.FirstOrDefaultAsync(k => k.Id == aktif.Id && k.Aktif)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı veya pasif.");

        if (!await Task.Run(() => BCrypt.Net.BCrypt.Verify(mevcutSifre, kullanici.SifreHash)))
            throw new InvalidOperationException("Mevcut şifre hatalı.");
        if (await Task.Run(() => BCrypt.Net.BCrypt.Verify(yeniSifre, kullanici.SifreHash)))
            throw new InvalidOperationException("Yeni şifre mevcut şifre ile aynı olamaz.");

        var simdi = DateTime.UtcNow;
        kullanici.SifreHash = BCrypt.Net.BCrypt.HashPassword(yeniSifre);
        kullanici.SifreDegistirmeli = false;
        kullanici.SifreDegistirmeTarihi = simdi;
        kullanici.GuncellemeTarihi = simdi;
        await baglam.SaveChangesAsync();
        await OturumOlayiKaydetAsync(baglam, kullanici.Id, kullanici.KullaniciAdi, "Kullanıcı kendi şifresini değiştirdi");

        aktif.SifreHash = kullanici.SifreHash;
        aktif.SifreDegistirmeli = false;
        aktif.SifreDegistirmeTarihi = simdi;
    }

    public async Task SilAsync(int id)
    {
        YetkiKontrolEt(y => y.KullaniciYonet, "Kullanıcı Silme");
        if (AktifKullanici != null && AktifKullanici.Id == id)
            throw new InvalidOperationException("Aktif oturum açmış kullanıcı silinemez.");

        await using var baglam = await _baglamFactory.CreateDbContextAsync();
        var kullanici = await baglam.Kullanicilar.FindAsync(id);
        if (kullanici == null) return;
        if (YoneticiRolu(kullanici.Rol))
        {
            if (!AktifYoneticiMi)
                throw new YetkisizErisimException("Yönetici hesabını silme");
            if (kullanici.Aktif)
                await BaskaAktifYoneticiOlmaliAsync(baglam, kullanici.Id);
        }

        kullanici.Silindi = true;
        kullanici.Aktif = false;
        await baglam.SaveChangesAsync();
    }
}
