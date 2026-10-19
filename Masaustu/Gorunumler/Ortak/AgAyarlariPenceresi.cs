using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Altyapi.Veri;
using Masaustu.Servisler;

namespace Masaustu.Gorunumler.Ortak;

// Ayarlar > Ağ: bu bilgisayarı ağ sunucusu yapma/kaldırma, sunucuya istemci olarak bağlanma ya da tek bilgisayara dönme.
// Mod değişince program yeniden başlatılır.
public sealed class AgAyarlariPenceresi : Window
{
    private readonly StackPanel _panel = new() { Margin = new Thickness(22) };

    public AgAyarlariPenceresi()
    {
        Title = "Ağ Ayarları";
        Width = 600;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;
        Content = _panel;
        Olustur();
    }

    private void Olustur()
    {
        _panel.Children.Clear();
        Baslik("Ağ (çok kullanıcılı) kullanım");
        switch (App.AgModu)
        {
            case AgModu.Sunucu: SunucuGorunumu(); break;
            case AgModu.Istemci: IstemciGorunumu(); break;
            default: TekGorunumu(); break;
        }
        var kapat = Dugme("Kapat", (_, _) => Close());
        kapat.HorizontalAlignment = HorizontalAlignment.Right;
        kapat.IsCancel = true;
        kapat.Margin = new Thickness(0, 18, 0, 0);
        _panel.Children.Add(kapat);
    }

    private void TekGorunumu()
    {
        Metin("Şu an: TEK BİLGİSAYAR. Veriler bu bilgisayardadır; program açıkken çalışır, ağdan erişilemez.", FontWeights.SemiBold);
        Metin("Birden çok bilgisayarda aynı verilerle çalışmak için verilerin duracağı bilgisayarı (genellikle her gün açık olan) " +
              "sunucu yapın, diğer bilgisayarlarda \"Sunucuya Bağlan\" ile sunucunun verdiği kodu girin.");

        Ara("1) Bu bilgisayarı sunucu yap");
        Metin("Veritabanı Windows hizmeti olarak sürekli çalışır (program kapalıyken de), yalnız aynı yerel ağdaki bilgisayarlar " +
              "şifreyle bağlanabilir. Windows yönetici onayı istenir; program yeniden başlar.");
        var sunucu = Dugme("Bu Bilgisayarı Sunucu Yap", async (_, _) => await SunucuYapAsync());
        sunucu.IsEnabled = UygulamaKlasorleri.GomuluPostgresBin != null;
        if (!sunucu.IsEnabled) sunucu.ToolTip = "Yalnız kurulum paketiyle kurulmuş programda kullanılabilir.";
        _panel.Children.Add(sunucu);

        Ara("2) Bu bilgisayarı sunucuya bağla (istemci)");
        Metin("Bu bilgisayardaki veriler kullanılmaz (silinmez); program sunucudaki verilerle çalışır.");
        _panel.Children.Add(Dugme("Sunucuya Bağlan...", async (_, _) => await IstemciOlAsync()));
    }

    private void SunucuGorunumu()
    {
        Metin("Şu an: AĞ SUNUCUSU. Veriler bu bilgisayardadır; diğer bilgisayarlar aşağıdaki kodla bağlanır.", FontWeights.SemiBold);
        Metin("Bu bilgisayar kapalıyken diğerleri çalışamaz. Günlük yedek bu bilgisayarda alınır.");
        Ara("Bağlantı kodu");
        var kod = App.VeritabaniBaglantiDizesi is { } dize ? AgYoneticisi.BaglantiKoduUret(dize) : "";
        var kutu = new TextBox { Text = kod, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), Padding = new Thickness(6), MaxHeight = 90 };
        _panel.Children.Add(kutu);
        var kopyala = Dugme("Kodu Kopyala", (_, _) => { try { Clipboard.SetText(kod); } catch { } });
        kopyala.Margin = new Thickness(0, 6, 0, 0);
        _panel.Children.Add(kopyala);
        Metin($"Bilgisayar adı: {Environment.MachineName}    Adresler: {string.Join(", ", AgYoneticisi.YerelIpAdresleri())}", null, Brushes.DimGray);
        Metin("Kod veritabanına tam erişim verir: yalnız kendi bilgisayarlarınıza girin, kimseyle paylaşmayın.", null, Brushes.DarkRed);

        Ara("Sunucu modunu kapat");
        Metin("Ağ erişimi kapatılır, bu bilgisayar tek bilgisayar moduna döner. Bağlı bilgisayarlar çalışamaz hale gelir.");
        _panel.Children.Add(Dugme("Sunucu Modunu Kapat", async (_, _) => await SunucuKaldirAsync()));
    }

    private void IstemciGorunumu()
    {
        var host = App.VeritabaniBaglantiDizesi is { } dize ? new Npgsql.NpgsqlConnectionStringBuilder(dize).Host : "?";
        Metin($"Şu an: AĞ İSTEMCİSİ. Veriler sunucu bilgisayardadır ({host}).", FontWeights.SemiBold);
        Metin("Yedekleme ve geri yükleme sunucu bilgisayardan yapılır.");
        Ara("Başka sunucuya bağlan");
        _panel.Children.Add(Dugme("Sunucuya Bağlan...", async (_, _) => await IstemciOlAsync()));
        Ara("Tek bilgisayar moduna dön");
        Metin("Program bu bilgisayardaki kendi verileriyle çalışır (sunucudaki veriler buraya gelmez).");
        _panel.Children.Add(Dugme("Tek Bilgisayar Moduna Dön", (_, _) =>
        {
            if (Sor("Bu bilgisayar sunucudan ayrılacak ve kendi verileriyle çalışacak. Program yeniden başlatılacak. Devam edilsin mi?"))
            {
                AgYoneticisi.TekBilgisayaraDon();
                YenidenBaslat();
            }
        }));
    }

    private async Task SunucuYapAsync()
    {
        if (!Sor("Bu bilgisayar ağ sunucusu yapılacak:\n\n• Veritabanı Windows hizmeti olarak sürekli çalışacak\n• Yerel ağdan şifreli bağlantıya izin verilecek (güvenlik duvarında yalnız özel ağ)\n\n" +
                 "Windows yönetici onayı istenecek, ardından program yeniden başlayacak. Devam edilsin mi?")) return;
        await YoneticiIslemiAsync(AgYoneticisi.SunucuKurArgumani, "Bu bilgisayar artık ağ sunucusu. Program yeniden başlatılıyor; ardından Ayarlar > Ağ bölümünden bağlantı kodunu alın.");
    }

    private async Task SunucuKaldirAsync()
    {
        if (!Sor("Sunucu modu kapatılacak; diğer bilgisayarlar bağlanamayacak. Windows yönetici onayı istenecek ve program yeniden başlayacak. Devam edilsin mi?")) return;
        await YoneticiIslemiAsync(AgYoneticisi.SunucuKaldirArgumani, "Sunucu modu kapatıldı. Program yeniden başlatılıyor.");
    }

    private async Task YoneticiIslemiAsync(string arguman, string basariMesaji)
    {
        IsEnabled = false;
        try
        {
            // Yönetici işlemi bu programın başlattığı veritabanını durdurur; işlem bitince program yeniden açılmalıdır.
            if (!await AgYoneticisi.YoneticiOlarakCalistirAsync(arguman)) { IsEnabled = true; return; }
            if (arguman == AgYoneticisi.SunucuKurArgumani) Altyapi.Veri.PostgreSqlSunucuYoneticisi.SahipligiBirak();
            MessageBox.Show(this, basariMesaji, "Ağ Ayarları", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message + $"\n\nAyrıntılar: {AgYoneticisi.KurulumLogu}\nProgram yeniden başlatılacak.", "Ağ Ayarları", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        YenidenBaslat();
    }

    private async Task IstemciOlAsync()
    {
        var pencere = new MetinGirisPenceresi("Sunucuya Bağlan",
            "Sunucu bilgisayarda Ayarlar > Ağ bölümündeki bağlantı kodunu (MYS1- ile başlar) yapıştırın:") { Owner = this };
        if (pencere.ShowDialog() != true) return;
        IsEnabled = false;
        try
        {
            var adres = await AgYoneticisi.IstemciOlarakBaglanAsync(pencere.GirilenMetin);
            MessageBox.Show(this, $"Sunucuya bağlanıldı ({adres}). Program yeniden başlatılıyor.", "Ağ Ayarları", MessageBoxButton.OK, MessageBoxImage.Information);
            YenidenBaslat();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, "Sunucuya Bağlan", MessageBoxButton.OK, MessageBoxImage.Warning);
            IsEnabled = true;
        }
    }

    private static void YenidenBaslat()
    {
        App.YenidenBaslat = true;
        Application.Current.Shutdown();
    }

    private bool Sor(string mesaj) => MessageBox.Show(this, mesaj, "Ağ Ayarları", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private void Baslik(string metin) => _panel.Children.Add(new TextBlock { Text = metin, FontSize = 17, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });

    private void Ara(string metin) => _panel.Children.Add(new TextBlock { Text = metin, FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 16, 0, 4) });

    private void Metin(string metin, FontWeight? kalinlik = null, Brush? renk = null) => _panel.Children.Add(new TextBlock
    {
        Text = metin, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6), FontWeight = kalinlik ?? FontWeights.Normal, Foreground = renk ?? Brushes.Black
    });

    private static Button Dugme(string metin, RoutedEventHandler tikla)
    {
        var d = new Button { Content = metin, Height = 34, Padding = new Thickness(14, 0, 14, 0), HorizontalAlignment = HorizontalAlignment.Left };
        d.Click += tikla;
        return d;
    }
}
