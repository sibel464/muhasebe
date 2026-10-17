using System.Windows;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Altyapi.Veri;
using Masaustu.GorunumModelleri;
using Masaustu.Gorunumler.Giris;
using Masaustu.Gorunumler.Ana;
using Masaustu.Servisler;
using Uygulama.Arayuzler;
using Altyapi.Servisler;
using System.Globalization;
using Masaustu.GorunumModeli.Muhasebe;

namespace Masaustu;

public partial class App : Application
{
    public static IServiceProvider HizmetSaglayici { get; private set; } = null!;
    private static readonly SemaphoreSlim LogKilidi = new(1, 1);

    protected override async void OnStartup(StartupEventArgs e)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        DispatcherUnhandledException += async (_, args) =>
        {
            await HatayiLoglaAsync(args.Exception, "DispatcherUnhandledException");
            
            if (args.Exception is DbUpdateConcurrencyException || args.Exception.InnerException is DbUpdateConcurrencyException)
            {
                MessageBox.Show(
                    "Bu kayıt başka bir kullanıcı tarafından güncellenmiştir. Lütfen sayfayı yenileyip işleminizi tekrar deneyiniz.",
                    "Eşzamanlılık Uyarısı (Concurrency)",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else if (args.Exception is YetkisizErisimException yetkiEx)
            {
                MessageBox.Show(
                    yetkiEx.Message,
                    "Yetkisiz Erişim Engellendi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(
                    "İşlem tamamlanamadı. Lütfen tekrar deneyin. Sorun sürerse sistem yöneticisine başvurun.",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += async (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                await HatayiLoglaAsync(exception, "AppDomain.UnhandledException");
        };
        TaskScheduler.UnobservedTaskException += async (_, args) =>
        {
            await HatayiLoglaAsync(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };

        try
        {
            var konfigurasyon = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
                .Build();

            var servisler = new ServiceCollection();
            servisler.AddSingleton<IConfiguration>(konfigurasyon);

            var baglantiDizesi = konfigurasyon["VeritabaniAyarlari:BaglantiDizesi"];
            if (string.IsNullOrWhiteSpace(baglantiDizesi))
                throw new InvalidOperationException("Veritabanı bağlantı dizesi bulunamadı.");

            // Yerel PostgreSQL sunucusu çalışmıyorsa otomatik başlat
            await PostgreSqlSunucuYoneticisi.SunucuyuHazirlaAsync(baglantiDizesi, konfigurasyon["VeritabaniAyarlari:VeriKlasoru"]);

            // AddDbContextFactory: hem IDbContextFactory<> hem de Scoped DbContext'i kayıt eder.
            // Singleton KullaniciServisi, kısa ömürlü context'ler oluşturmak için factory kullanır.
            var dbBaglanti = new Npgsql.NpgsqlConnectionStringBuilder(baglantiDizesi)
            {
                Timeout = 5,
                CommandTimeout = 10
            }.ConnectionString;
            servisler.AddDbContextFactory<MuhasebeBaglami>(opts =>
            {
                opts.UseNpgsql(dbBaglanti);

            });
            servisler.AddSingleton<NavigasyonServisi>();
            servisler.AddSingleton<IAktifFirmaBaglami, AktifFirmaBaglami>();
            servisler.AddTransient<IGlobalAramaServisi, GlobalAramaServisi>();
            // Singleton: AktifKullanici bilgisinin tüm uygulama boyunca tek instance'da tutulması için.
            // KullaniciServisi artık IDbContextFactory kullandığından Singleton olarak güvenle kayıt edilebilir.
            servisler.AddSingleton<Uygulama.Arayuzler.IKullaniciServisi, Altyapi.Servisler.KullaniciServisi>();
            servisler.AddTransient<ISistemAyarlariServisi, SistemAyarlariServisi>();
            servisler.AddSingleton<IVeritabaniYedekServisi>(sp => new VeritabaniYedekServisi(
                baglantiDizesi,
                konfigurasyon["YedekAyarlari:Klasor"],
                int.TryParse(konfigurasyon["YedekAyarlari:SaklanacakYedekSayisi"], out var saklanacak) ? saklanacak : 30,
                sp.GetRequiredService<IKullaniciServisi>()));
            servisler.AddTransient<IAuditLogServisi, AuditLogServisi>();
            servisler.AddTransient<IUrunServisi, UrunServisi>();
            servisler.AddTransient<ICariServisi, CariServisi>();
            servisler.AddTransient<ICariDetayServisi>(sp => new CariDetayServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>(), sp.GetRequiredService<IAktifFirmaBaglami>()));
            servisler.AddTransient<ICariTipiTanimiServisi, CariTipiTanimiServisi>();
            servisler.AddTransient<IMerkeziTanimServisi, MerkeziTanimServisi>();
            servisler.AddTransient<ICariHareketServisi, CariHareketServisi>();
            servisler.AddTransient<IFaturaServisi, FaturaServisi>();
            servisler.AddTransient<ISiparisServisi, SiparisServisi>();
            servisler.AddTransient<IFinancialTransactionService, FinancialTransactionService>();
            servisler.AddTransient<IIrsaliyeServisi, IrsaliyeServisi>();
            servisler.AddTransient<IKasaServisi, KasaServisi>();
            servisler.AddTransient<IBankaServisi, BankaServisi>();
            servisler.AddTransient<IDepoServisi, DepoServisi>();
            servisler.AddTransient<IStok360Servisi>(sp => new Stok360Servisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
            servisler.AddTransient<IStokOperasyonServisi>(sp => new StokOperasyonServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
            servisler.AddTransient<ITahsilatOdemeServisi, TahsilatOdemeServisi>();
            servisler.AddTransient<IFinansMerkeziServisi>(sp => new FinansMerkeziServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
            servisler.AddTransient<ICekSenetServisi, CekSenetServisi>();
            servisler.AddTransient<IBelgeKapamaServisi, BelgeKapamaServisi>();
            servisler.AddTransient<ISirketServisi, SirketServisi>();
            servisler.AddSingleton<IBelgeYazdirmaServisi, BelgeYazdirmaServisi>();
            servisler.AddTransient<GirisGorunumModeli>();
            servisler.AddTransient<AnaGorunumModeli>();
            servisler.AddTransient<MusteriGorunumModeli>();
            servisler.AddTransient<MusteriDuzenleGorunumModeli>();
            servisler.AddTransient<CariDetayGorunumModeli>();
            servisler.AddTransient<CariHareketGorunumModeli>();
            servisler.AddTransient<FaturaGorunumModeli>();
            servisler.AddTransient<SiparisGorunumModeli>();
            servisler.AddTransient<IrsaliyeGorunumModeli>();
            servisler.AddTransient<IrsaliyeEkleGorunumModeli>();
            servisler.AddTransient<AyarlarGorunumModeli>();
            servisler.AddTransient<SatisGorunumModeli>();
            servisler.AddTransient<CekSenetGorunumModeli>();
            servisler.AddTransient<DepoGorunumModeli>();
            servisler.AddTransient<StokOperasyonMerkeziGorunumModeli>();
            servisler.AddTransient<UrunStok360GorunumModeli>();
            servisler.AddTransient<StokTransferGorunumModeli>();
            servisler.AddTransient<StokSayimGorunumModeli>();
            servisler.AddTransient<StokIslemGorunumModeli>();
            servisler.AddTransient<FinansMerkeziGorunumModeli>();
            servisler.AddTransient<Kasa360GorunumModeli>();
            servisler.AddTransient<Banka360GorunumModeli>();
            servisler.AddTransient<FinansTransferGorunumModeli>();
            servisler.AddTransient<MutabakatGorunumModeli>();
            servisler.AddTransient<IRaporMerkeziServisi>(sp => new RaporMerkeziServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetService<IAuditLogServisi>(), sp.GetRequiredService<IKullaniciServisi>(), sp.GetRequiredService<IAktifFirmaBaglami>()));
            servisler.AddTransient<IMuhasebeBaglantiServisi>(sp => new MuhasebeBaglantiServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
            servisler.AddTransient<IHesapPlaniServisi>(sp => new HesapPlaniServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
            servisler.AddTransient<IYevmiyeServisi>(sp => new YevmiyeServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>(), sp.GetRequiredService<IMuhasebeBaglantiServisi>()));
            servisler.AddTransient<IMuhasebeRaporServisi>(sp => new MuhasebeRaporServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
            servisler.AddTransient<IExcelExportServisi, ExcelExportServisi>();
            servisler.AddTransient<IPdfRaporServisi, PdfRaporServisi>();
            servisler.AddTransient<ITcmbKurServisi>(sp => new TcmbKurServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext()));
            servisler.AddTransient<IKurServisi>(sp => new KurServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<ITcmbKurServisi>(), sp.GetRequiredService<IKullaniciServisi>()));
            servisler.AddTransient<IRevaluasyonServisi>(sp =>
            {
                // Revalüasyon ve yevmiye aynı context/transaction üzerinde atomik çalışır.
                var db = sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext();
                var kullanici = sp.GetRequiredService<IKullaniciServisi>();
                var baglanti = new MuhasebeBaglantiServisi(db, kullanici);
                var yevmiye = new YevmiyeServisi(db, kullanici, baglanti);
                var kur = new KurServisi(db, new TcmbKurServisi(db), kullanici);
                return new RevaluasyonServisi(db, kur, yevmiye, baglanti, kullanici, sp.GetRequiredService<IAktifFirmaBaglami>());
            });
            servisler.AddTransient<Masaustu.GorunumModeli.Doviz.DovizKurMerkeziGorunumModeli>();
            servisler.AddTransient<RaporMerkeziGorunumModeli>();
            servisler.AddTransient<HesapPlaniGorunumModeli>();
            servisler.AddTransient<HesapEkleDuzenleGorunumModeli>();
            servisler.AddTransient<Hesap360GorunumModeli>();
            servisler.AddTransient<YevmiyeFisGorunumModeli>();
            servisler.AddTransient<YevmiyeFisEkleDuzenleGorunumModeli>();
            servisler.AddTransient<YevmiyeDefteriGorunumModeli>();
            servisler.AddTransient<BuyukDefterGorunumModeli>();
            servisler.AddTransient<MuavinGorunumModeli>();
            servisler.AddTransient<MizanGorunumModeli>();
            servisler.AddTransient<MuhasebeBaglantiGorunumModeli>();
            servisler.AddTransient<MuhasebeSaglikGorunumModeli>();
            servisler.AddTransient<GirisEkrani>();
            servisler.AddTransient<AnaPencere>();

            HizmetSaglayici = servisler.BuildServiceProvider();
#if DEBUG
            const string varsayilanOrtam = "Development";
#else
            const string varsayilanOrtam = "Production";
#endif
            var ortam = konfigurasyon["UygulamaAyarlari:Ortam"] ?? varsayilanOrtam;
            await VeritabaniSaglikKontroluAsync(
                string.Equals(ortam, "Development", StringComparison.OrdinalIgnoreCase));

            if (!string.Equals(konfigurasyon["YedekAyarlari:OtomatikGunlukYedek"], "false", StringComparison.OrdinalIgnoreCase))
                OtomatikYedegiBaslat();

            var girisEkrani = HizmetSaglayici.GetRequiredService<GirisEkrani>();
            MainWindow = girisEkrani;
            girisEkrani.Show();
        }
        catch (Exception exception)
        {
            await HatayiLoglaAsync(exception, "OnStartup");
            MessageBox.Show(
                "Uygulama başlatılamadı. Lütfen veritabanı bağlantısını ve uygulama yapılandırmasını sistem yöneticinizle kontrol edin. Ayrıntılar log dosyasına yazıldı.",
                "Başlangıç Hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        PostgreSqlSunucuYoneticisi.SunucuyuDurdur();
    }

    // Günlük yedek arka planda alınır; başarısız olursa uygulama çalışmaya devam eder, hata log'a yazılır.
    private static void OtomatikYedegiBaslat()
    {
        var yedekServisi = HizmetSaglayici.GetRequiredService<IVeritabaniYedekServisi>();
        _ = Task.Run(async () =>
        {
            try
            {
                await yedekServisi.GerekirseGunlukYedekAlAsync();
            }
            catch (Exception ex)
            {
                await HatayiLoglaAsync(ex, "OtomatikGunlukYedek");
            }
        });
    }

    private static async Task VeritabaniSaglikKontroluAsync(bool otomatikMigration)
    {
        await using var scope = HizmetSaglayici.CreateAsyncScope();
        var baglam = scope.ServiceProvider.GetRequiredService<MuhasebeBaglami>();

        if (!await baglam.Database.CanConnectAsync())
            throw new InvalidOperationException("PostgreSQL bağlantısı kurulamadı. Sunucunun çalıştığından emin olun.");

        if (otomatikMigration)
            await baglam.Database.MigrateAsync();

        if (baglam.Database.HasPendingModelChanges())
            throw new InvalidOperationException("Veritabanı modeli ile migration dosyaları uyumlu değil. Lütfen sistem yöneticisine başvurun.");
        if ((await baglam.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Veritabanı güncellemesi gerekiyor. Lütfen sistem yöneticisine başvurun.");

        if (!await baglam.Kullanicilar.AnyAsync())
            throw new InvalidOperationException("Kullanicilar tablosu boş veya veritabanı şeması hazır değil.");

        // Otomatik fiş özelliğinden önce girilmiş kasa/banka hareketlerinin fişleri tamamlanır (tekrar çalışması güvenli).
        try
        {
            await FinansMuhasebeKaydi.EksikFisleriTamamlaAsync(baglam);
        }
        catch (Exception ex)
        {
            await HatayiLoglaAsync(ex, "EksikFisleriTamamla");
        }

        // Daha önce iptal edilmiş tahsilat/ödemelerin asıl ve ters cari kayıtları eşleştirilir (tekrar çalışması güvenli).
        try
        {
            await TahsilatOdemeServisi.EskiIptalleriEslestirAsync(baglam);
        }
        catch (Exception ex)
        {
            await HatayiLoglaAsync(ex, "EskiIptalleriEslestir");
        }
    }

    internal static async Task HatayiLoglaAsync(Exception exception, string kaynak)
    {
        await LogKilidi.WaitAsync();
        try
        {
            var log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {kaynak}\n{exception}\n\n";
            await File.AppendAllTextAsync(Path.Combine(AppContext.BaseDirectory, "uygulama-hatalari.log"), log);
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
