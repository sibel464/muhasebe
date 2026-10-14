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

    IReadOnlyList<YedekDosyasi> YedekleriListele();
}
