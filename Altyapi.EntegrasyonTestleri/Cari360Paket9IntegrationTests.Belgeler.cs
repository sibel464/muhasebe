using Altyapi.Servisler;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Siparis;
using EtkiAlani.Modeller.Irsaliye;
using Uygulama.Arayuzler;
using Uygulama.Dto.Cari360;
using Xunit;
namespace Altyapi.EntegrasyonTestleri;
public sealed partial class Cari360Paket9IntegrationTests
{
    [Theory][InlineData(true)][InlineData(false)]
    public async Task Dogrudan_fatura_tahsilati_acik_belgeyi_kapatir(bool kasa)
    {
        var(db,c,s)=await Hazirla();await using var dispose=db;var f=await FaturaEkle(db,c,FaturaTuru.Satis);
        if(kasa)db.Add(new KasaHareketi {CariKartId=c.Id,FaturaId=f.Id,Tutar=1200,Tur=HareketTuru.Gelir});
        else db.Add(new BankaHareketi {CariKartId=c.Id,FaturaId=f.Id,Tutar=1200,Tur=HareketTuru.Gelir});
        await db.SaveChangesAsync();
        var row=Assert.Single((await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Faturalar,new())).Satirlar);
        Assert.Equal(1200,row.Odenen);Assert.Equal(0,row.Kalan);
        Assert.Equal(0,(await s.GetCariFinansOzetAsync(c.Id,c.SirketId.Value)).AcikFaturaSayisi);
    }
    [Fact] public async Task Hareket_baglantisi_olmayan_fatura_bilinmiyor_gosterilir()
    {
        var(db,c,s)=await Hazirla();await using var dispose=db;
        db.Add(new Fatura {CariKartId=c.Id,SirketId=c.SirketId,FaturaNo="UNK-"+Guid.NewGuid().ToString("N"),Durum=BelgeDurumu.Onaylandi,GenelToplam=900});
        await db.SaveChangesAsync();
        var row=Assert.Single((await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Faturalar,new())).Satirlar);
        Assert.Null(row.Odenen);Assert.Null(row.Kalan);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Urun_gecmisi_son_fiyat_ve_agirlikli_ortalama(bool alis)
    {
        var(db,c,s)=await Hazirla();await using var dispose=db;
        var urun=new Urun {Kod="P9U-"+Guid.NewGuid().ToString("N"),Ad="Test ürün",Birim="Adet",SirketId=c.SirketId};db.Add(urun);await db.SaveChangesAsync();
        var f1=await FaturaEkle(db,c,alis ? FaturaTuru.Alis : FaturaTuru.Satis);
        var f2=await FaturaEkle(db,c,alis ? FaturaTuru.Alis : FaturaTuru.Satis);f2.Tarih=Bugun;
        db.AddRange(new FaturaKalemi {FaturaId=f1.Id,UrunId=urun.Id,UrunKodu=urun.Kod,UrunAd=urun.Ad,Birim="Adet",Miktar=2,BirimFiyat=100,SatirToplami=240},
            new FaturaKalemi {FaturaId=f2.Id,UrunId=urun.Id,UrunKodu=urun.Kod,UrunAd=urun.Ad,Birim="Adet",Miktar=1,BirimFiyat=160,SatirToplami=192});
        await db.SaveChangesAsync();
        var r=Assert.Single(await s.GetUrunSayfasiAsync(c.Id,c.SirketId!.Value,alis,new()));
        Assert.Equal(160,r.SonFiyat);Assert.Equal(120,r.OrtalamaFiyat);Assert.Equal(Bugun,r.SonIslemTarihi);
        Assert.Equal(3,alis ? r.AlinanMiktar : r.SatilanMiktar);Assert.Equal(432,alis ? r.AlinanTutar : r.SatilanTutar);
    }
    [Theory][InlineData(CariSekme.Faturalar)][InlineData(CariSekme.Siparisler)][InlineData(CariSekme.Irsaliyeler)][InlineData(CariSekme.CekSenet)][InlineData(CariSekme.Sistem)]
    public async Task Sekme_yetkisi_olmadan_servis_erisimini_reddeder(CariSekme sekme)
    {
        var(db,c,s)=await Hazirla();await using var dispose=db;
        var user=new Cari360TestKullanici();user.AktifYetkiler!.FaturaGoruntule=false;user.AktifYetkiler.SatisGecmisiGoruntule=false;user.AktifYetkiler.IrsaliyeGoruntule=false;user.AktifYetkiler.CekSenetGoruntule=false;user.AktifYetkiler.AuditLogGoruntule=false;
        var active=new AktifFirmaBaglami();active.Degistir(c.SirketId!.Value,null,null,1);
        var servis=new CariDetayServisi(db,user,active);
        await Assert.ThrowsAsync<YetkisizErisimException>(()=>servis.GetSayfaAsync(c.Id,c.SirketId.Value,sekme,new()));
    }
    [Fact] public async Task Sistem_bilgisi_sorgusu_cevrilir()
    {
        var(db,c,s)=await Hazirla();await using var dispose=db;
        var r=await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Sistem,new());
        Assert.NotNull(r);
    }
    [Fact] public async Task Siparis_filtresi_ve_sevk_miktarlari()
    {
        var(db,c,s)=await Hazirla();await using var dispose=db;
        var sip=new Siparis {CariKartId=c.Id,SiparisNo="P9S-"+Guid.NewGuid().ToString("N")[..12],ParaBirimiId=1,OlusturanKullaniciId=1,SiparisTuru=SiparisTuru.Alis,SiparisTarihi=Bugun,GenelToplam=100};
        db.Add(sip);await db.SaveChangesAsync();
        var r=await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Siparisler,new(){Tur="Alış"});
        Assert.Equal(sip.Id,Assert.Single(r.Satirlar).Id);
        Assert.Empty((await s.GetSayfaAsync(c.Id,c.SirketId.Value,CariSekme.Siparisler,new(){Tur="Satış"})).Satirlar);
    }
    [Fact] public async Task Irsaliye_filtresi_ve_kaynak_siparis()
    {
        var(db,c,s)=await Hazirla();await using var dispose=db;
        var sip=new Siparis {CariKartId=c.Id,SiparisNo="P9S-"+Guid.NewGuid().ToString("N")[..12],ParaBirimiId=1,OlusturanKullaniciId=1,SiparisTarihi=Bugun};
        db.Add(sip);await db.SaveChangesAsync();
        db.Add(new Irsaliye {CariKartId=c.Id,SiparisId=sip.Id,IrsaliyeNo="P9I-"+Guid.NewGuid().ToString("N")[..12],Tur=IrsaliyeTuru.Satis,Tarih=Bugun});
        await db.SaveChangesAsync();
        var r=Assert.Single((await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Irsaliyeler,new(){Tur="Satış"})).Satirlar);
        Assert.Equal(sip.SiparisNo,r.Referans);
    }
    [Fact] public async Task Finansal_ozet_fatura_yetkisi_yokken_sifir_uydurmaz()
    {
        var(db,c,s)=await Hazirla();await using var dispose=db;
        var user=new Cari360TestKullanici();user.AktifYetkiler!.FaturaGoruntule=false;
        var active=new AktifFirmaBaglami();active.Degistir(c.SirketId!.Value,null,null,1);
        var r=await new CariDetayServisi(db,user,active).GetCariFinansOzetAsync(c.Id,c.SirketId.Value);
        Assert.Null(r.AcikFaturaSayisi);Assert.Null(r.VadesiGecmis);
    }
    [Fact] public async Task Ayni_cariye_yanlis_firma_faturasi_sizmaz()
    {
        var(db,c,s)=await Hazirla();await using var dispose=db;
        var other=new EtkiAlani.Modeller.Kurumsal.Sirket {Id=Guid.NewGuid(),SirketKodu="P9B-"+Guid.NewGuid().ToString("N"),Unvan="Diğer firma"};
        db.Add(other);await db.SaveChangesAsync();
        db.Add(new Fatura {CariKartId=c.Id,SirketId=other.Id,FaturaNo="P9B-"+Guid.NewGuid().ToString("N"),GenelToplam=1000});
        await db.SaveChangesAsync();
        Assert.Empty((await s.GetSayfaAsync(c.Id,c.SirketId!.Value,CariSekme.Faturalar,new(){IptalleriGoster=true})).Satirlar);
    }
}
