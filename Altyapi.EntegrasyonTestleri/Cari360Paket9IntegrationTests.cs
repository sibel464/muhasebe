using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Cari360;
using Xunit;
namespace Altyapi.EntegrasyonTestleri;

internal sealed class Cari360TestKullanici : IKullaniciServisi
{
    public Kullanici? AktifKullanici {get;set;}=new() {Id=1,KullaniciAdi="Cari360 test",Aktif=true};
    public KullaniciYetkileri? AktifYetkiler {get;set;}=new() {CariGoruntule=true,CariDuzenle=true,FaturaGoruntule=true,SatisGecmisiGoruntule=true,IrsaliyeGoruntule=true,TahsilatYap=true,OdemeYap=true,CekSenetGoruntule=true,AuditLogGoruntule=true};
    public bool YetkiliMi(Func<KullaniciYetkileri,bool> f)=>AktifKullanici!=null && AktifYetkiler!=null && f(AktifYetkiler);
    public void YetkiKontrolEt(Func<KullaniciYetkileri,bool> f,string a){if(!YetkiliMi(f))throw new YetkisizErisimException(a);}
    public Task<bool> GirisYapAsync(string a,string b,CancellationToken c=default)=>Task.FromResult(false);
    public void CikisYap()=>AktifKullanici=null;
    public Task<List<Kullanici>> TumunuGetirAsync()=>throw new NotSupportedException();
    public Task<Kullanici?> GetirAsync(int id)=>throw new NotSupportedException();
    public Task<Kullanici> EkleAsync(Kullanici k,string s)=>throw new NotSupportedException();
    public Task GuncelleAsync(Kullanici k)=>throw new NotSupportedException();
    public Task SifreDegistirAsync(int id,string s)=>throw new NotSupportedException();
    public Task SilAsync(int id)=>throw new NotSupportedException();
    public static CariDetayServisi Servis(MuhasebeBaglami db,Guid sirket) {
        var f=new AktifFirmaBaglami();f.Degistir(sirket,null,null,1);return new(db,new Cari360TestKullanici(),f);
    }
}
[Collection("PostgreSQL integration")]
public sealed partial class Cari360Paket9IntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public Cari360Paket9IntegrationTests(IsolatedPostgreSqlFixture fixture)=>_fixture=fixture;
    private static DateTime Bugun=>DateTime.SpecifyKind(DateTime.Today,DateTimeKind.Utc);
    private async Task<(MuhasebeBaglami Db,CariKart Cari,CariDetayServisi Servis)> Hazirla()
    {
        var db=new MuhasebeBaglami(_fixture.Options);
        var s=new Sirket {Id=Guid.NewGuid(),SirketKodu="P9-"+Guid.NewGuid().ToString("N"),Unvan="Paket 9"};
        var c=new CariKart {SirketId=s.Id,Kod="P9-"+Guid.NewGuid().ToString("N"),Unvan="Deneme",Ad="Ayşe",Soyad="Yılmaz",RiskLimiti=5000,CariTipiId=1};
        db.AddRange(s,c);await db.SaveChangesAsync();
        return (db,c,Cari360TestKullanici.Servis(db,s.Id));
    }
    private static async Task<Fatura> FaturaEkle(MuhasebeBaglami db,CariKart c,FaturaTuru tur,int? gun=10,decimal kapali=0)
    {
        var f=new Fatura {SirketId=c.SirketId,CariKartId=c.Id,FaturaNo="P9F-"+Guid.NewGuid().ToString("N"),Tarih=Bugun.AddDays(-120),VadeTarihi=gun.HasValue ? Bugun.AddDays(-gun.Value) : null,Tur=tur,Durum=BelgeDurumu.Onaylandi,GenelToplam=1200,KdvToplam=200};
        db.Add(f);await db.SaveChangesAsync();
        var ht=tur switch {FaturaTuru.Satis=>CariHareketTuru.SatisFaturasi,FaturaTuru.Alis=>CariHareketTuru.AlisFaturasi,FaturaTuru.SatisIade=>CariHareketTuru.SatisIadesi,_=>CariHareketTuru.AlisIadesi};
        db.Add(new CariHareket {CariKartId=c.Id,IlgiliBelgeId=f.Id,BelgeNo=f.FaturaNo,Tarih=f.Tarih,HareketTuru=ht,Borc=tur is FaturaTuru.Satis or FaturaTuru.AlisIade ? 1200 : 0,Alacak=tur is FaturaTuru.Alis or FaturaTuru.SatisIade ? 1200 : 0,KapaliTutar=kapali});
        await db.SaveChangesAsync();return f;
    }
    [Theory][InlineData(1,false)][InlineData(2,true)]
    public async Task Kisi_kurum_tipten_belirlenir(int tip,bool kurum)
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        c.CariTipiId=tip;await db.SaveChangesAsync();
        var r=await s.GetCariGenelBilgiAsync(c.Id,c.SirketId!.Value);
        Assert.Equal(kurum,r!.IsKurum);Assert.Equal("Ayşe",r.Ad);Assert.Equal("Yılmaz",r.Soyad);
    }
    [Fact] public async Task Bakiye_onbellekten_degil_hareketten_hesaplanir()
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        db.AddRange(new CariHareket {CariKartId=c.Id,Borc=1000,Tarih=Bugun},new CariHareket {CariKartId=c.Id,Alacak=400,Tarih=Bugun});await db.SaveChangesAsync();
        await db.CariKartlar.Where(x=>x.Id==c.Id).ExecuteUpdateAsync(p=>p.SetProperty(x=>x.Borc,999999));
        var r=await s.GetCariFinansOzetAsync(c.Id,c.SirketId!.Value);
        Assert.Equal(1000,r.ToplamBorc);Assert.Equal(400,r.ToplamAlacak);Assert.Equal(600,r.GuncelBakiye);Assert.Equal("BORÇLU",r.BakiyeYonu);Assert.Equal(600,r.KullanilanRisk);
    }
    [Theory][InlineData(null,3)][InlineData("Tahsilat",1)][InlineData("Satış Faturası",1)][InlineData("Açılış / Devir",1)]
    public async Task Ekstre_tur_filtresi_ve_yuruyen_bakiye(string? tur,int count)
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        db.AddRange(new CariHareket {CariKartId=c.Id,Borc=350,Tarih=Bugun.AddDays(-3),HareketTuru=CariHareketTuru.Devir},
            new CariHareket {CariKartId=c.Id,Borc=1000,Tarih=Bugun.AddDays(-2),HareketTuru=CariHareketTuru.SatisFaturasi},
            new CariHareket {CariKartId=c.Id,Alacak=400,Tarih=Bugun.AddDays(-1),HareketTuru=CariHareketTuru.Tahsilat});
        await db.SaveChangesAsync();
        var r=await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Ekstre,new(){Tur=tur});
        Assert.Equal(count,r.ToplamKayit);
        if(tur=="Tahsilat" || tur==null)Assert.Equal(950,r.Satirlar.Last().Bakiye);
    }
    [Fact] public async Task Tarih_araligi_devir_ve_bitis_gunu()
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        db.AddRange(new CariHareket {CariKartId=c.Id,Borc=350,Tarih=Bugun.AddDays(-1)},
            new CariHareket {CariKartId=c.Id,Borc=100,Tarih=Bugun.AddHours(23)},
            new CariHareket {CariKartId=c.Id,Borc=999,Tarih=Bugun.AddDays(1)});
        await db.SaveChangesAsync();
        var r=await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Ekstre,new(){Baslangic=Bugun,Bitis=Bugun});
        Assert.Equal(350,r.DevirBakiyesi);Assert.Single(r.Satirlar);Assert.Equal(450,r.Satirlar[0].Bakiye);
    }
    [Theory][InlineData(false,1)][InlineData(true,2)]
    public async Task Iptal_bakiyeye_eklenmez(bool goster,int count)
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        db.AddRange(new CariHareket {CariKartId=c.Id,Borc=100,Tarih=Bugun},new CariHareket {CariKartId=c.Id,Borc=800,Tarih=Bugun.AddHours(1),Silindi=true});
        await db.SaveChangesAsync();
        var r=await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Ekstre,new(){IptalleriGoster=goster});
        Assert.Equal(count,r.ToplamKayit);Assert.Equal(100,r.Satirlar.Last().Bakiye);
    }
    [Theory][InlineData(FaturaTuru.Satis,"Satış")][InlineData(FaturaTuru.Alis,"Alış")][InlineData(FaturaTuru.SatisIade,"Satış İadesi")][InlineData(FaturaTuru.AlisIade,"Alış İadesi")]
    public async Task Fatura_tur_net_kdv_ve_kismi_kapama(FaturaTuru tur,string ad)
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        await FaturaEkle(db,c,tur,10,400);
        var r=await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Faturalar,new(){Tur=ad});
        var f=Assert.Single(r.Satirlar);Assert.Equal(1000,f.Net);Assert.Equal(200,f.Kdv);Assert.Equal(400,f.Odenen);Assert.Equal(800,f.Kalan);
        var o=await s.GetCariFinansOzetAsync(c.Id,c.SirketId.Value);Assert.Equal(1,o.AcikFaturaSayisi);
    }
    [Theory][InlineData(-1,0)][InlineData(0,0)][InlineData(1,1)][InlineData(30,1)][InlineData(31,2)][InlineData(60,2)][InlineData(61,3)][InlineData(90,3)][InlineData(91,4)]
    public async Task Yaslandirma_sinirlari(int gun,int bolum)
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;await FaturaEkle(db,c,FaturaTuru.Satis,gun,200);
        var r=await s.GetRiskAsync(c.Id,c.SirketId!.Value);
        var degerler=new[]{r.VadesiGelmemis,r.Gun1_30,r.Gun31_60,r.Gun61_90,r.Gun90Ustu};
        Assert.Equal(1000,degerler[bolum]);Assert.Equal(1000,degerler.Sum());Assert.Equal(Bugun.AddDays(-120),r.EnEskiAcikBelge);
    }
    [Fact] public async Task Vadesiz_belge_gecikmis_sayilmaz()
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;await FaturaEkle(db,c,FaturaTuru.Satis,null);
        var r=await s.GetRiskAsync(c.Id,c.SirketId!.Value);Assert.Equal(1200,r.Vadesiz);Assert.Equal(0,r.Gun90Ustu);
    }
    [Theory][InlineData(CariSekme.Ekstre)][InlineData(CariSekme.Faturalar)][InlineData(CariSekme.Siparisler)][InlineData(CariSekme.Irsaliyeler)][InlineData(CariSekme.TahsilatOdeme)][InlineData(CariSekme.CekSenet)][InlineData(CariSekme.Notlar)][InlineData(CariSekme.CariFisleri)]
    public async Task Cross_company_tum_sekmelerde_engellenir(CariSekme sekme)
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>s.GetSayfaAsync(c.Id,Guid.NewGuid(),sekme,new()));
    }
    [Theory][InlineData(CariSekme.Ekstre)][InlineData(CariSekme.Faturalar)][InlineData(CariSekme.Siparisler)][InlineData(CariSekme.Irsaliyeler)][InlineData(CariSekme.TahsilatOdeme)][InlineData(CariSekme.CekSenet)][InlineData(CariSekme.Notlar)]
    public async Task Oturumsuz_fail_closed(CariSekme sekme)
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        var kapali=new CariDetayServisi(db);
        await Assert.ThrowsAsync<YetkisizErisimException>(()=>kapali.GetSayfaAsync(c.Id,c.SirketId!.Value,sekme,new()));
    }
    [Theory][InlineData(50,1,50)][InlineData(50,2,50)][InlineData(50,3,21)][InlineData(100,1,100)][InlineData(100,2,21)]
    public async Task Buyuk_ekstre_sayfalama(int boyut,int page,int count)
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        db.AddRange(Enumerable.Range(1,121).Select(i=>new CariHareket {CariKartId=c.Id,Borc=1,Tarih=Bugun.AddSeconds(i)}));await db.SaveChangesAsync();
        var r=await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Ekstre,new(){Sayfa=page,SayfaBoyutu=boyut});
        Assert.Equal(121,r.ToplamKayit);Assert.Equal(count,r.Satirlar.Count);Assert.Equal((page-1)*boyut+count,r.Satirlar.Last().Bakiye);
    }
    [Fact] public async Task Not_ekleme_ve_firma_izolasyonu()
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        await s.NotEkleAsync(c.Id,c.SirketId!.Value,"Görüşme","Vade görüşüldü.");
        var r=await s.GetSayfaAsync(c.Id,c.SirketId.Value,CariSekme.Notlar,new());
        Assert.Equal("Vade görüşüldü.",Assert.Single(r.Satirlar).Aciklama);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>s.NotEkleAsync(c.Id,Guid.NewGuid(),"Başlık","Not"));
    }
    [Fact] public async Task Pasif_cari_gecmisi_acilir()
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;c.Aktif=false;await db.SaveChangesAsync();
        Assert.Equal("Pasif",(await s.GetCariGenelBilgiAsync(c.Id,c.SirketId!.Value))!.Durum);
    }
    [Fact] public async Task Cancellation_sorguyu_durdurur()
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        using var cts=new CancellationTokenSource();cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Ekstre,new(),cts.Token));
    }
    [Fact] public async Task Belge_navigation_dogru_cari_ile_sinirli()
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        var f=await FaturaEkle(db,c,FaturaTuru.Satis);
        Assert.Equal(f.FaturaNo,(await s.GetBelgeAsync(c.Id,c.SirketId!.Value,"Fatura",f.Id)).Belge.BelgeNo);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>s.GetBelgeAsync(c.Id,c.SirketId.Value,"Fatura",int.MaxValue));
    }
    [Theory][InlineData(CekSenetTuru.AlinanCek)][InlineData(CekSenetTuru.VerilenCek)][InlineData(CekSenetTuru.AlinanSenet)][InlineData(CekSenetTuru.VerilenSenet)]
    public async Task Cek_senet_gercek_kayitlari(CekSenetTuru tur)
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        db.Add(new CekSenet {CariKartId=c.Id,Tur=tur,Durum=CekSenetDurumu.Portfoyde,BelgeNo="P9C-"+Guid.NewGuid().ToString("N"),Tutar=700,DuzenlemeTarihi=Bugun,VadeTarihi=Bugun.AddDays(10)});
        await db.SaveChangesAsync();var r=await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.CekSenet,new());
        Assert.Equal(700,Assert.Single(r.Satirlar).Toplam);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task Tahsilat_odeme_kasa_banka(bool kasa)
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        if(kasa)db.Add(new KasaHareketi {CariKartId=c.Id,Tur=HareketTuru.Gelir,FisTuru=KasaFisTuru.Tahsilat,Tutar=400,Tarih=Bugun});
        else db.Add(new BankaHareketi {CariKartId=c.Id,Tur=HareketTuru.Gider,FisTuru=BankaFisTuru.GidenHavale,Tutar=400,Tarih=Bugun});
        await db.SaveChangesAsync();var r=await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.TahsilatOdeme,new());
        Assert.Equal(kasa ? "Tahsilat" : "Ödeme",Assert.Single(r.Satirlar).Tur);
    }
    [Fact] public async Task Taslak_fatura_ekstreye_sahte_borc_eklemez()
    {
        var (db,c,s)=await Hazirla();await using var dispose=db;
        db.Add(new Fatura {CariKartId=c.Id,SirketId=c.SirketId,FaturaNo="P9T-"+Guid.NewGuid().ToString("N"),GenelToplam=1000});
        await db.SaveChangesAsync();
        Assert.Empty((await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Ekstre,new())).Satirlar);
        Assert.Equal(0,(await s.GetCariFinansOzetAsync(c.Id,c.SirketId.Value)).GuncelBakiye);
    }
}
