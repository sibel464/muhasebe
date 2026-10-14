using EtkiAlani.Modeller.Kullanici;

namespace Uygulama.Arayuzler;

public interface IKullaniciServisi
{
    // Oturum Yönetimi
    Kullanici? AktifKullanici { get; }
    KullaniciYetkileri? AktifYetkiler { get; }
    Task<bool> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken cancellationToken = default);

    // Ayrıntılı giriş sonucu (kilit, kalan deneme, zorunlu şifre değişimi). Varsayılan uygulama eski davranışı korur.
    async Task<GirisSonucu> GirisDeneAsync(string kullaniciAdi, string sifre, CancellationToken cancellationToken = default) =>
        await GirisYapAsync(kullaniciAdi, sifre, cancellationToken)
            ? new GirisSonucu(GirisDurumu.Basarili)
            : new GirisSonucu(GirisDurumu.HataliBilgi);

    // Oturum sahibinin kendi şifresini değiştirmesi (yönetici yetkisi gerekmez).
    Task KendiSifreniDegistirAsync(string mevcutSifre, string yeniSifre) =>
        throw new NotSupportedException();
    void CikisYap();
    bool YetkiliMi(Func<KullaniciYetkileri, bool> yetkiSorgusu);
    void YetkiKontrolEt(Func<KullaniciYetkileri, bool> yetkiSorgusu, string islemAdi);

    // Kullanıcı CRUD İşlemleri (Süper Admin İçin)
    Task<List<Kullanici>> TumunuGetirAsync();
    Task<Kullanici?> GetirAsync(int id);
    Task<Kullanici> EkleAsync(Kullanici kullanici, string acikSifre);
    Task GuncelleAsync(Kullanici kullanici);
    Task SifreDegistirAsync(int kullaniciId, string yeniAcikSifre);
    Task SilAsync(int id);
}

public enum GirisDurumu { Basarili, HataliBilgi, HesapKilitli }

public sealed record GirisSonucu(GirisDurumu Durum, int KalanDeneme = 0, DateTime? KilitBitisTarihi = null, bool SifreDegistirmeli = false)
{
    public bool Basarili => Durum == GirisDurumu.Basarili;
}

public class YetkisizErisimException : UnauthorizedAccessException
{
    public string IslemAdi { get; }

    public YetkisizErisimException(string islemAdi) 
        : base($"Bu işlemi ('{islemAdi}') gerçekleştirmek için gerekli kullanıcı yetkiniz bulunmamaktadır.")
    {
        IslemAdi = islemAdi;
    }
}
