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

    // Uygulamanın servis kaydı; OnStartup ve ekran deneme düzeneği (tüm sayfaları gerçek servislerle açar) kullanır.
    public static IServiceProvider ServisleriOlustur(IConfiguration konfigurasyon, string baglantiDizesi)
    {
        var servisler = new ServiceCollection();
        servisler.AddSingleton<IConfiguration>(konfigurasyon);
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
        servisler.AddTransient<IKvkkServisi, KvkkServisi>();
        servisler.AddTransient<ICariDetayServisi>(sp => new CariDetayServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>(), sp.GetRequiredService<IAktifFirmaBaglami>()));
        servisler.AddTransient<ICariTipiTanimiServisi, CariTipiTanimiServisi>();
        servisler.AddTransient<IMerkeziTanimServisi, MerkeziTanimServisi>();
        servisler.AddTransient<ICariHareketServisi, CariHareketServisi>();
        servisler.AddTransient<IFaturaServisi, FaturaServisi>();
        servisler.AddTransient<ITekrarlayanFaturaServisi, TekrarlayanFaturaServisi>();
        servisler.AddTransient<ITopluAktarimServisi>(sp => new Altyapi.Aktarim.TopluAktarimServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
        servisler.AddTransient<IEBelgeServisi>(sp => new Altyapi.EBelge.EBelgeServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
        servisler.AddTransient<ISiparisServisi, SiparisServisi>();
        servisler.AddTransient<IFinancialTransactionService, FinancialTransactionService>();
        servisler.AddTransient<IIrsaliyeServisi, IrsaliyeServisi>();
        servisler.AddTransient<IKasaServisi, KasaServisi>();
        servisler.AddTransient<IBankaServisi, BankaServisi>();
        servisler.AddTransient<IEpostaServisi>(sp => new Altyapi.Eposta.EpostaServisi(sp.GetRequiredService<ISistemAyarlariServisi>()));
        servisler.AddTransient<IBankaEkstreServisi>(sp => new Altyapi.Aktarim.BankaEkstreServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IBankaServisi>(), sp.GetRequiredService<IKullaniciServisi>()));
        servisler.AddTransient<IDepoServisi, DepoServisi>();
        servisler.AddTransient<IStok360Servisi>(sp => new Stok360Servisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
        servisler.AddTransient<IStokOperasyonServisi>(sp => new StokOperasyonServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
        servisler.AddTransient<ITahsilatOdemeServisi, TahsilatOdemeServisi>();
        servisler.AddTransient<IFinansMerkeziServisi>(sp => new FinansMerkeziServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
        servisler.AddTransient<ICekSenetServisi, CekSenetServisi>();
        servisler.AddTransient<IBelgeKapamaServisi, BelgeKapamaServisi>();
        servisler.AddTransient<ISirketServisi, SirketServisi>();
        servisler.AddTransient<IBaslangicKontrolServisi>(sp => new BaslangicKontrolServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IAktifFirmaBaglami>(), sp.GetService<IVeritabaniYedekServisi>()));
        servisler.AddSingleton<IBelgeYazdirmaServisi>(sp => new BelgeYazdirmaServisi(null, sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>(), sp.GetRequiredService<IAktifFirmaBaglami>()));
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
        servisler.AddTransient<IMaliTabloServisi>(sp => new MaliTabloServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
        servisler.AddTransient<ISabitKiymetServisi>(sp => new SabitKiymetServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
        servisler.AddTransient<IKdvMahsupServisi>(sp => new KdvMahsupServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
        servisler.AddTransient<IYilSonuServisi>(sp => new YilSonuServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(), sp.GetRequiredService<IKullaniciServisi>()));
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
        servisler.AddTransient<YilSonuGorunumModeli>();
        servisler.AddTransient<KdvMahsupGorunumModeli>();
        servisler.AddTransient<MaliTablolarGorunumModeli>();
        servisler.AddTransient<DemirbasGorunumModeli>();
        servisler.AddTransient<GirisEkrani>();
        servisler.AddTransient<AnaPencere>();

        HizmetSaglayici = servisler.BuildServiceProvider();
        return HizmetSaglayici;
    }

    // WPF bağlamaları sayı/tarih biçiminde iş parçacığı kültürünü değil öğenin Language özelliğini kullanır (varsayılan
    // en-US): "25.000,00" yerine "25,000.00" görünüyordu. Metin parçaları (Run) bu ayarı almaz; sayı biçimleyen
    // bağlamalar TextBlock.Text üzerinde (gerekirse MultiBinding ile) yapılmalıdır.
    public static void TurkceBicimAyarla()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("tr-TR")));
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        TurkceBicimAyarla();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        // Ağ sunucusu kurma/kaldırma: program yönetici olarak bu argümanla açılır, işi yapıp kapanır (arayüz açılmaz).
        if (e.Args.Length == 1 && e.Args[0] is AgYoneticisi.SunucuKurArgumani or AgYoneticisi.SunucuKaldirArgumani)
        {
            Shutdown(AgYoneticisi.YukseltilmisIslem(e.Args[0]));
            return;
        }

        DispatcherUnhandledException += async (_, args) =>
        {
            await HatayiLoglaAsync(args.Exception, "DispatcherUnhandledException");
            
            if (args.Exception is DbUpdateConcurrencyException || args.Exception.InnerException is DbUpdateConcurrencyException)
            {
                MessageBox.Show(
                    Altyapi.Veri.MuhasebeBaglami.EszamanliDegisiklikMesaji,
                    "Kayıt Başka Bir İşlemle Değişti",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else if ((args.Exception as DbUpdateException ?? args.Exception.InnerException as DbUpdateException)?.InnerException is Npgsql.PostgresException pg
                     && Altyapi.Veri.MuhasebeBaglami.VeritabaniHataMesaji(pg) is { } vtMesaji)
            {
                MessageBox.Show(vtMesaji, "Kayıt Yapılamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                // Kuruluma özgü ayarlar (korumalı veritabanı şifresi vb.) yazılabilir ortak klasörde durur.
                .AddJsonFile(UygulamaKlasorleri.YerelAyarDosyasi, optional: true, reloadOnChange: false)
                .Build();


            // Yerel PostgreSQL sunucusu çalışmıyorsa başlatılır; kurulum paketinde ilk açılışta veritabanı da kurulur.
            AgModu = AgYoneticisi.Mod(konfigurasyon);
            var veritabani = await VeritabaniKurulumu.HazirlaAsync(konfigurasyon);
            var baglantiDizesi = veritabani.BaglantiDizesi;
            VeritabaniBaglantiDizesi = baglantiDizesi;

            ServisleriOlustur(konfigurasyon, baglantiDizesi);
#if DEBUG
            const string varsayilanOrtam = "Development";
#else
            const string varsayilanOrtam = "Production";
#endif
            var ortam = konfigurasyon["UygulamaAyarlari:Ortam"] ?? varsayilanOrtam;
            await VeritabaniSaglikKontroluAsync(
                string.Equals(ortam, "Development", StringComparison.OrdinalIgnoreCase));

            // Günlük yedeği verinin durduğu bilgisayar alır; istemciler almaz.
            if (AgModu != AgModu.Istemci && !string.Equals(konfigurasyon["YedekAyarlari:OtomatikGunlukYedek"], "false", StringComparison.OrdinalIgnoreCase))
                OtomatikYedegiBaslat();

            // Kurulum sürümünde lisans/deneme denetimi: deneme bitmişse yalnız lisans penceresi açılır.
            if (LisansYoneticisi.DenetimGerekli)
            {
                var lisans = LisansYoneticisi.DurumGetir();
                if (!lisans.Kullanilabilir && new Masaustu.Gorunumler.Ortak.LisansPenceresi(acilistaZorunlu: true).ShowDialog() != true)
                {
                    Shutdown(0);
                    return;
                }
                LisansMetni = LisansYoneticisi.DurumGetir().Aciklama;
            }

            var girisEkrani = HizmetSaglayici.GetRequiredService<GirisEkrani>();
            MainWindow = girisEkrani;
            girisEkrani.Show();
        }
        catch (Exception exception)
        {
            await HatayiLoglaAsync(exception, "OnStartup");
            MessageBox.Show(
                exception is EskiSurumException ? exception.Message
                : AgModu == AgModu.Istemci ? "Sunucu bilgisayardaki veritabanına bağlanılamadı. Sunucu bilgisayarın açık ve aynı ağda olduğundan emin olun. Ayrıntılar log dosyasına yazıldı."
                : "Uygulama başlatılamadı. Lütfen veritabanı bağlantısını ve uygulama yapılandırmasını sistem yöneticinizle kontrol edin. Ayrıntılar log dosyasına yazıldı.",
                "Başlangıç Hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    // Yedekten geri yüklemeden sonra program kendini yeniden açar: yeni süreç veritabanı sunucusu tamamen
    // durduktan sonra başlatılır (aksi halde kapanmakta olan sunucuya bağlanmaya çalışırdı).
    internal static bool YenidenBaslat { get; set; }

    // Tek bilgisayar / ağ sunucusu / ağ istemcisi (Ayarlar > Ağ).
    internal static AgModu AgModu { get; private set; }
    internal static string? VeritabaniBaglantiDizesi { get; private set; }

    // Ana pencere başlığında gösterilen lisans/deneme durumu (kurulum sürümünde).
    internal static string? LisansMetni { get; private set; }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        PostgreSqlSunucuYoneticisi.SunucuyuDurdur();
        // İndirilmiş güncelleme varsa kurulur; kurulum bitince program kendini yeniden açar.
        if (GuncellemeYoneticisi.BekleyenPaket != null)
        {
            GuncellemeYoneticisi.BekleyenPaketiKur();
            return;
        }
        if (YenidenBaslat && Environment.ProcessPath is { } exe)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true }); }
            catch { }
        }
    }

    // Günlük yedek arka planda alınır; başarısız olursa uygulama çalışmaya devam eder, hata log'a yazılır.
    // Dükkânda program günlerce kapatılmadan açık kalabildiği için yalnız açılışta değil, saatte bir de bakılır.
    private static void OtomatikYedegiBaslat()
    {
        var yedekServisi = HizmetSaglayici.GetRequiredService<IVeritabaniYedekServisi>();
        _ = Task.Run(async () =>
        {
            using var zamanlayici = new PeriodicTimer(TimeSpan.FromHours(1));
            do
            {
                try
                {
                    await yedekServisi.GerekirseGunlukYedekAlAsync();
                }
                catch (Exception ex)
                {
                    await HatayiLoglaAsync(ex, "OtomatikGunlukYedek");
                }
            } while (await zamanlayici.WaitForNextTickAsync());
        });
    }

    // Başlangıç yönetici kaydındaki (admin / admin123) şifre özeti; MuhasebeBaglami'ndaki başlangıç verisiyle aynıdır.
    private const string BaslangicYoneticiSifreOzeti = "$2a$11$bFJTJsZZdqJQwi75rB4wM.DQgaRKmvwOSvi0VYrjuKdFj1/QC2UfS";

    private static async Task VeritabaniSaglikKontroluAsync(bool otomatikMigration)
    {
        await using var scope = HizmetSaglayici.CreateAsyncScope();
        var baglam = scope.ServiceProvider.GetRequiredService<MuhasebeBaglami>();

        if (!await baglam.Database.CanConnectAsync())
            throw new InvalidOperationException("Veritabanına bağlanılamadı. Programı kapatıp yeniden açın; sorun sürerse bilgisayarı yeniden başlatın. " +
                "Program ağda başka bir bilgisayardaki verilerle çalışıyorsa o bilgisayarın açık ve ağa bağlı olduğundan emin olun.");

        // Geliştirmede her açılışta, kurulum sürümünde yeni sürüm kurulunca veritabanı yapısı güncellenir.
        // İçinde veri olan veritabanı güncellenmeden önce otomatik yedeklenir; yedek alınamazsa güncelleme yapılmaz.
        // Ağda başka bir bilgisayardaki daha yeni sürüm veritabanını güncellemişse eski program onu bozmasın.
        var bilinenler = baglam.Database.GetMigrations().ToHashSet();
        if ((await baglam.Database.GetAppliedMigrationsAsync()).Any(m => !bilinenler.Contains(m)))
            throw new EskiSurumException();

        var bekleyenler = (await baglam.Database.GetPendingMigrationsAsync()).ToList();
        if (bekleyenler.Count > 0)
        {
            if (!otomatikMigration && (await baglam.Database.GetAppliedMigrationsAsync()).Any())
                await HizmetSaglayici.GetRequiredService<IVeritabaniYedekServisi>().GuncellemeOncesiYedekAlAsync();
            await baglam.Database.MigrateAsync();
        }

        // Kurulum sürümünde her kopya aynı başlangıç şifresiyle gelir: admin bu şifreyi kullandıkça ilk girişte değiştirmek zorundadır.
        if (UygulamaKlasorleri.GomuluPostgresBin != null)
            await baglam.Database.ExecuteSqlRawAsync(
                "UPDATE \"Kullanicilar\" SET \"SifreDegistirmeli\" = true WHERE \"Id\" = 1 AND \"SifreHash\" = {0} AND NOT \"SifreDegistirmeli\"",
                BaslangicYoneticiSifreOzeti);

        if (baglam.Database.HasPendingModelChanges())
            throw new InvalidOperationException("Veritabanı modeli ile migration dosyaları uyumlu değil. Lütfen sistem yöneticisine başvurun.");
        if ((await baglam.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Veritabanı güncellemesi gerekiyor. Lütfen sistem yöneticisine başvurun.");

        if (!await baglam.Kullanicilar.AnyAsync())
            throw new InvalidOperationException("Kullanicilar tablosu boş veya veritabanı şeması hazır değil.");

        // Açılış bakımları ağda birden çok bilgisayar aynı anda açılırsa yalnız birinde çalışır (PostgreSQL danışma kilidi).
        await baglam.Database.OpenConnectionAsync();
        var bakimKilidi = await baglam.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock(731001) AS \"Value\"").SingleAsync();
        if (!bakimKilidi)
            return;

        // Otomatik fiş özelliğinden önce girilmiş kasa/banka hareketlerinin fişleri tamamlanır (tekrar çalışması güvenli).
        try
        {
            await FinansMuhasebeKaydi.EksikFisleriTamamlaAsync(baglam);
        }
        catch (Exception ex)
        {
            await HatayiLoglaAsync(ex, "EksikFisleriTamamla");
        }

        // Şubesi olmayan (bu özellikten önce açılmış) firmalara merkez şube, mağaza, kasa ve mali dönem açılır.
        try
        {
            await SirketServisi.EksikFirmaTanimlariniTamamlaAsync(baglam);
        }
        catch (Exception ex)
        {
            await HatayiLoglaAsync(ex, "EksikFirmaTanimlari");
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

        await baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(731001)");
    }

    private sealed class EskiSurumException() : InvalidOperationException(
        "Veritabanı, ağdaki başka bir bilgisayarda kurulu daha yeni bir program sürümüyle güncellenmiş. " +
        "Bu bilgisayardaki programı da aynı sürüme güncelleyin; eski sürümle çalışmak verileri bozabilir.");

    internal static async Task HatayiLoglaAsync(Exception exception, string kaynak)
    {
        await LogKilidi.WaitAsync();
        try
        {
            var log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {kaynak}\n{exception}\n\n";
            await File.AppendAllTextAsync(UygulamaKlasorleri.LogYolu("uygulama-hatalari.log"), log);
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
