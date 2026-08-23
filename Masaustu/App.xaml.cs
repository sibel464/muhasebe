using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Altyapi.Veri;
using Masaustu.GorunumModelleri;
using Masaustu.Gorunumler.Giris;
using Masaustu.Gorunumler.Ana;
using Masaustu.Servisler;
using Uygulama.Arayuzler;
using Uygulama.Servisler;
using Altyapi.Servisler;

namespace Masaustu;

public partial class App : Application
{
    public static IServiceProvider HizmetSaglayici { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global hata yakalama
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"Beklenmeyen bir hata oluştu:\n\n{args.Exception.Message}",
                "Hata",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        // appsettings.json yükle
        var konfigurasyon = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
            .Build();

        // DI Servisleri Kaydet
        var servisler = new ServiceCollection();

        // Konfigürasyon
        servisler.AddSingleton<IConfiguration>(konfigurasyon);

        // Veritabanı (PostgreSQL)
        var baglantiDizesi = konfigurasyon["VeritabaniAyarlari:BaglantiDizesi"];
        servisler.AddDbContext<MuhasebeBaglami>(opts =>
            opts.UseNpgsql(baglantiDizesi));

        // Servisler
        servisler.AddSingleton<NavigasyonServisi>();
        servisler.AddTransient<IUrunServisi, UrunServisi>();
        servisler.AddTransient<ICariServisi, CariServisi>();

        // ViewModel'ler
        servisler.AddTransient<GirisGorunumModeli>();
        servisler.AddTransient<AnaGorunumModeli>();
        servisler.AddTransient<MusteriGorunumModeli>();
        servisler.AddTransient<MusteriDuzenleGorunumModeli>();

        // Pencereler
        servisler.AddTransient<GirisEkrani>();
        servisler.AddTransient<AnaPencere>();

        HizmetSaglayici = servisler.BuildServiceProvider();

        // Giriş ekranını göster
        var girisEkrani = HizmetSaglayici.GetRequiredService<GirisEkrani>();
        girisEkrani.Show();
    }
}
