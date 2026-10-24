namespace Uygulama.Arayuzler;

public sealed record YedekDosyasi(string DosyaAdi, string TamYol, DateTime OlusturmaTarihi, long Boyut)
{
    public string BoyutMetni => Boyut >= 1024 * 1024 ? $"{Boyut / 1024d / 1024d:N1} MB" : $"{Boyut / 1024d:N0} KB";
}

public interface IVeritabaniYedekServisi
{
    string YedekKlasoru { get; }
    int SaklanacakYedekSayisi { get; }

    // Her yedeğin ikinci kopyasının yazıldığı klasör (harici disk, USB bellek, ağ klasörü); boşsa kopya alınmaz.
    // Disk bozulduğunda veri ile yedeğin birlikte kaybolmaması için.
    string? IkinciYedekKlasoru { get; }
    // Son ikinci kopya denemesinin sonucu veya uyarı (kullanıcıya gösterilir); yoksa boş.
    string IkinciKopyaDurumu { get; }
    // Klasör yazılabilir değilse hata verir; boş/null ikinci kopyayı kapatır. Sistem ayarlarını yönetme yetkisi gerekir.
    void IkinciYedekKlasoruAyarla(string? klasor);

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
