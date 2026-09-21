using EtkiAlani.Modeller.Kullanici;

namespace Uygulama.Arayuzler;

public interface IKullaniciServisi
{
    // Oturum Yönetimi
    Kullanici? AktifKullanici { get; }
    KullaniciYetkileri? AktifYetkiler { get; }
    Task<bool> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken cancellationToken = default);
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

public class YetkisizErisimException : UnauthorizedAccessException
{
    public string IslemAdi { get; }

    public YetkisizErisimException(string islemAdi) 
        : base($"Bu işlemi ('{islemAdi}') gerçekleştirmek için gerekli kullanıcı yetkiniz bulunmamaktadır.")
    {
        IslemAdi = islemAdi;
    }
}
