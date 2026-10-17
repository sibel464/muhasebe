namespace Uygulama.Arayuzler;

public sealed record YedekDosyasi(string DosyaAdi, string TamYol, DateTime OlusturmaTarihi, long Boyut)
{
    public string BoyutMetni => Boyut >= 1024 * 1024 ? $"{Boyut / 1024d / 1024d:N1} MB" : $"{Boyut / 1024d:N0} KB";
}

public interface IVeritabaniYedekServisi
{
    string YedekKlasoru { get; }
    int SaklanacakYedekSayisi { get; }

    // Kullanıcının elle başlattığı yedek; sistem ayarlarını yönetme yetkisi gerekir.
    Task<YedekDosyasi> ElleYedekAlAsync(CancellationToken cancellationToken = default);

    // Son yedek 24 saatten eskiyse yedek alır; alındıysa dosyayı, gerek yoksa null döner.
    Task<YedekDosyasi?> GerekirseGunlukYedekAlAsync(CancellationToken cancellationToken = default);

    // Program yeni sürüme güncellenip veritabanı yapısı değişmeden hemen önce sistemin aldığı yedek (oturum gerekmez).
    Task<YedekDosyasi> GuncellemeOncesiYedekAlAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<YedekDosyasi> YedekleriListele();

    // Seçilen yedeği geri yükler: önce güncel durumun yedeği alınır, yedek ayrı bir veritabanına açılıp doğrulanır,
    // sonra kullanılan veritabanıyla yer değiştirir. Eski veritabanı silinmez, dönen adla saklanır.
    // Sonrasında program yeniden başlatılmalıdır. Sistem ayarlarını yönetme yetkisi gerekir.
    Task<string> GeriYukleAsync(string yedekYolu, CancellationToken cancellationToken = default);
}
