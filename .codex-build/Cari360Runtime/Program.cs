using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using Masaustu;
using Masaustu.GorunumModelleri;
using Masaustu.Gorunumler.Sayfa.Musteri;
using Masaustu.Servisler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Uygulama.Arayuzler;
using Uygulama.Dto.Cari360;

public static class Program
{
    private static int _exit;
    private static readonly string Connection="Host=127.0.0.1;Port=5432;Database=Muhasebe_Cari360_Runtime_Test;Username=postgres;";
    [STAThread] public static int Main()
    {
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("/Masaustu;component/Kaynaklar/Stiller.xaml",UriKind.Relative)});
        app.Dispatcher.InvokeAsync(async()=>{
            try {await Run(app);}
            catch(Exception ex){Console.WriteLine("RUNTIME_FAIL "+ex);_exit=1;}
            finally {Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}
        });
        Dispatcher.Run();return _exit;
    }
    private static void Check(bool condition,string name) {if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static async Task Run(Application app)
    {
        var builder=new Npgsql.NpgsqlConnectionStringBuilder(Connection);
        if(!builder.Database!.EndsWith("_Test"))throw new Exception("Test veritabanı zorunlu.");
        var options=new DbContextOptionsBuilder<MuhasebeBaglami>().UseNpgsql(Connection).Options;
        int id;int otherId;int faturaId;Guid sirket=Guid.NewGuid();
        await using(var db=new MuhasebeBaglami(options)) {
            await db.Database.EnsureDeletedAsync();await db.Database.MigrateAsync();
            var company=new Sirket {Id=sirket,SirketKodu="RUNTIME",Unvan="Cari 360 Deneme Firması"};
            var cari=new CariKart {SirketId=sirket,Kod="CRT-001",Ad="Ayşe",Soyad="Yılmaz",Unvan="Ayşe Yılmaz",CariTipiId=1,Telefon="0555 000 00 00",RiskLimiti=20000};
            var kurum=new CariKart {SirketId=sirket,Kod="CRT-002",Ad="Mehmet",Soyad="Demir",Unvan="Deneme Ticaret Ltd.",CariTipiId=2};
            db.AddRange(company,cari,kurum);await db.SaveChangesAsync();id=cari.Id;otherId=kurum.Id;
            var f=new Fatura {SirketId=sirket,CariKartId=id,FaturaNo="FAT-2026-001",Tarih=DateTime.UtcNow.AddDays(-40),VadeTarihi=DateTime.UtcNow.AddDays(-10),Tur=FaturaTuru.Satis,Durum=BelgeDurumu.Onaylandi,GenelToplam=1200,KdvToplam=200};
            db.Add(f);await db.SaveChangesAsync();faturaId=f.Id;
            db.AddRange(new CariHareket {CariKartId=id,Borc=1200,Tarih=f.Tarih,HareketTuru=CariHareketTuru.SatisFaturasi,IlgiliBelgeId=f.Id,BelgeNo=f.FaturaNo,KapaliTutar=400},
                new CariHareket {CariKartId=id,Alacak=400,Tarih=DateTime.UtcNow.AddDays(-2),HareketTuru=CariHareketTuru.Tahsilat,BelgeNo="THS-001"},
                new CariHareket {CariKartId=id,Borc=50,Tarih=DateTime.UtcNow,Silindi=true,BelgeNo="IPT-001"});
            await db.SaveChangesAsync();
        }
        var services=new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {["VeritabaniAyarlari:BaglantiDizesi"]=Connection}).Build());
        services.AddDbContextFactory<MuhasebeBaglami>(o=>o.UseNpgsql(Connection));
        var active=new AktifFirmaBaglami();active.Degistir(sirket,null,null,1);
        services.AddSingleton<IAktifFirmaBaglami>(active);services.AddSingleton<IKullaniciServisi>(new RuntimeUser());
        services.AddSingleton<NavigasyonServisi>();
        foreach(var t in typeof(CariServisi).Assembly.GetTypes().Where(t=>t.Namespace=="Altyapi.Servisler" && t.IsClass && !t.IsAbstract)) {
            foreach(var i in t.GetInterfaces().Where(i=>i.Namespace=="Uygulama.Arayuzler" && i!=typeof(IKullaniciServisi) && i!=typeof(ICariDetayServisi)))services.AddTransient(i,t);
        }
        foreach(var t in typeof(CariDetayGorunumModeli).Assembly.GetTypes().Where(t=>t.Namespace=="Masaustu.GorunumModelleri" && t.IsClass && !t.IsAbstract))services.AddTransient(t);
        services.AddTransient<ICariDetayServisi>(sp=>new CariDetayServisi(sp.GetRequiredService<IDbContextFactory<MuhasebeBaglami>>().CreateDbContext(),sp.GetRequiredService<IKullaniciServisi>(),active));
        using var provider=services.BuildServiceProvider();
        typeof(App).GetProperty(nameof(App.HizmetSaglayici))!.SetValue(null,provider);
        var w=new CariDetayPenceresi(id);app.MainWindow=w;w.Show();
        var vm=(CariDetayGorunumModeli)w.DataContext;
        await vm.YukleAsync();await Task.Delay(150);
        Check(vm.GenelBilgi?.Ad=="Ayşe","Kişi görünümü");
        Check(vm.FinansOzet?.GuncelBakiye==800,"Güncel bakiye 800");
        Check(vm.Sekmeler.Count==14,"14 sekme");
        foreach(var sekme in vm.Sekmeler.ToList()) {
            vm.SeciliSekme=sekme;await vm.YukleAsync(false);
            Check(!vm.DurumMesaji.Contains("yüklenemedi"),"Sekme "+sekme.Ad);
        }
        vm.SeciliSekme=vm.Sekmeler.Single(x=>x.Tur==CariSekme.Ekstre);await vm.YukleAsync(false);
        Check(vm.Satirlar.Count==2 && vm.Satirlar.Last().Bakiye==800,"Ekstre yürüyen bakiye");
        vm.IptalleriGoster=true;await vm.YukleAsync(false);Check(vm.Satirlar.Count==3,"İptal görünümü");
        foreach(var (width,height) in new[]{(1366,768),(1920,1080)}) {
            var dpi=VisualTreeHelper.GetDpi(w).DpiScaleX; w.Width=width/dpi;w.Height=height/dpi;w.UpdateLayout();await Task.Delay(100);
            var bitmap=new RenderTargetBitmap(width,height,96*dpi,96*dpi,PixelFormats.Pbgra32);bitmap.Render(w);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create(Path.Combine(Directory.GetCurrentDirectory(),$"cari360-{width}x{height}.png"));encoder.Save(file);
            Console.WriteLine($"SIZE requested={width}x{height} actual={w.ActualWidth}x{w.ActualHeight}"); Check(Math.Abs(w.ActualWidth*dpi-width)<2 && Math.Abs(w.ActualHeight*dpi-height)<2,"Yerleşim "+width+"x"+height);
        }
        foreach(var action in new[]{"Fatura","Tahsilat","Ödeme","Sipariş"}) {
            bool opened=false;bool prefill=false;Window? child=null;
            var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(350)};
            timer.Tick+=(_,_)=>{
                child=app.Windows.OfType<Window>().FirstOrDefault(x=>x!=w);
                if(child==null)return;
                var dc=child.DataContext ?? (child.Content as FrameworkElement)?.DataContext;
                prefill=dc switch {FaturaEkleGorunumModeli x=>x.SeciliCari?.Id==id,TahsilatOdemeGorunumModeli x=>x.SecilenCari?.Id==id,SiparisGorunumModeli x=>x.SeciliCari?.Id==id,_=>false};
                opened=true;timer.Stop();child.Close();
            };
            timer.Start();await vm.HizliIslem!(action,id);timer.Stop();
            Check(opened && prefill,"Cari prefill "+action);
        }
        bool belgeAcildi=false;
        var closeTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};
        closeTimer.Tick+=(_,_)=>{var child=app.Windows.OfType<Window>().FirstOrDefault(x=>x!=w);if(child!=null){belgeAcildi=child.Title=="Belge Detayı";closeTimer.Stop();child.Close();}};
        closeTimer.Start();var detail=await provider.GetRequiredService<ICariDetayServisi>().GetBelgeAsync(id,sirket,"Fatura",faturaId);
        await vm.BelgeGoster!(detail);closeTimer.Stop();Check(belgeAcildi,"Belge detay penceresi");
        vm.Init(otherId);await vm.YukleAsync();Check(vm.GenelBilgi?.IsKurum==true && vm.GenelBilgi.Ad=="Mehmet","Kurum görünümü ve ad korunması");
        for(int n=0;n<15;n++)vm.SeciliSekme=vm.Sekmeler[n%vm.Sekmeler.Count];
        await vm.YukleAsync();Check(vm.GenelBilgi?.Id==otherId,"Hızlı sekme geçişi");
        active.Degistir(Guid.NewGuid(),null,null,1);await Task.Delay(50);Check(!w.IsVisible && vm.GenelBilgi==null,"Firma değişiminde eski veri temizliği");
        await using(var db=new MuhasebeBaglami(options))await db.Database.EnsureDeletedAsync();
        Console.WriteLine("RUNTIME_COMPLETE");
    }
}
public sealed class RuntimeUser : IKullaniciServisi
{
 public Kullanici? AktifKullanici {get;}=new(){Id=1,KullaniciAdi="Runtime Test",Rol="Admin",Aktif=true};
 public KullaniciYetkileri? AktifYetkiler {get;}=new(){CariGoruntule=true,CariDuzenle=true,FaturaGoruntule=true,FaturaOlustur=true,SatisYap=true,SatisGecmisiGoruntule=true,IrsaliyeGoruntule=true,TahsilatYap=true,OdemeYap=true,CekSenetGoruntule=true,AuditLogGoruntule=true};
 public bool YetkiliMi(Func<KullaniciYetkileri,bool> f)=>f(AktifYetkiler!);
 public void YetkiKontrolEt(Func<KullaniciYetkileri,bool> f,string a){if(!YetkiliMi(f))throw new YetkisizErisimException(a);}
 public Task<bool> GirisYapAsync(string a,string b,CancellationToken c=default)=>Task.FromResult(true);
 public void CikisYap(){}
 public Task<List<Kullanici>> TumunuGetirAsync()=>Task.FromResult(new List<Kullanici>());
 public Task<Kullanici?> GetirAsync(int id)=>Task.FromResult(AktifKullanici);
 public Task<Kullanici> EkleAsync(Kullanici k,string s)=>throw new NotSupportedException();
 public Task GuncelleAsync(Kullanici k)=>throw new NotSupportedException();
 public Task SifreDegistirAsync(int id,string s)=>throw new NotSupportedException();
 public Task SilAsync(int id)=>throw new NotSupportedException();
}
