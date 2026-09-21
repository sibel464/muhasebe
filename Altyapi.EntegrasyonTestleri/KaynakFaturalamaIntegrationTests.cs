using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Irsaliye;
using EtkiAlani.Modeller.Kullanici;
using EtkiAlani.Modeller.Siparis;
using EtkiAlani.Modeller.Stok;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

[Collection("PostgreSQL integration")]
public sealed class KaynakFaturalamaIntegrationTests
{
    private readonly IsolatedPostgreSqlFixture _fixture;
    public KaynakFaturalamaIntegrationTests(IsolatedPostgreSqlFixture fixture) => _fixture = fixture;

    [Fact] public async Task Satis_siparisinden_tam_fatura() { var x=await Hazirla(10); await Dogrudan(x,10); await Kontrol(x,s=>Assert.Equal(10,s.Kalemler.Single().FaturalananMiktar)); }
    [Fact] public async Task Satis_siparisinden_kismi_fatura() { var x=await Hazirla(10); await Dogrudan(x,4); await Kontrol(x,s=>Assert.Equal(4,s.Kalemler.Single().FaturalananMiktar)); }
    [Fact] public async Task Ikinci_fatura_tamamlama() { var x=await Hazirla(10); await Dogrudan(x,4); await Dogrudan(x,6); await Kontrol(x,s=>Assert.Equal(10,s.Kalemler.Single().FaturalananMiktar)); }
    [Fact] public async Task Irsaliyeden_fatura_kaynaklari_saklar() { var x=await Hazirla(4); var i=await Sevk(x,4); var f=await Irsaliyeden(x,i,4); Assert.Equal(i.Id,f.IrsaliyeId); Assert.Equal(x.SiparisId,f.SiparisId); }
    [Fact] public async Task Irsaliyeli_faturada_stok_ikinci_kez_dusmez() { var x=await Hazirla(10,20); var i=await Sevk(x,10); await Stok(x,10); await Irsaliyeden(x,i,10); await Stok(x,10); }
    [Fact] public async Task Dogrudan_siparis_faturasi_stogu_dusurur() { var x=await Hazirla(5,20); await Dogrudan(x,5); await Stok(x,15); }
    [Fact] public async Task Kalandan_fazla_fatura_engellenir() { var x=await Hazirla(5); await Assert.ThrowsAsync<InvalidOperationException>(()=>Dogrudan(x,6)); }
    [Fact] public async Task Tam_faturalanmis_irsaliye_tekrar_faturalanamaz() { var x=await Hazirla(5); var i=await Sevk(x,5); await Irsaliyeden(x,i,5); await Assert.ThrowsAsync<InvalidOperationException>(()=>Irsaliyeden(x,i,5)); }
    [Fact] public async Task Satis_faturasi_cariyi_borclandirir() { var x=await Hazirla(2); var f=await Dogrudan(x,2); await using var c=Yeni(); Assert.Equal(f.GenelToplam,(await c.CariHareketler.SingleAsync(h=>h.IlgiliBelgeId==f.Id&&h.HareketTuru==EtkiAlani.Modeller.Cari.CariHareketTuru.SatisFaturasi)).Borc); }
    [Fact] public async Task Satis_yevmiye_yonu_dogru() { var x=await Hazirla(2); var f=await Dogrudan(x,2); await using var c=Yeni(); var y=await c.YevmiyeFisleri.Include(y=>y.Satirlar).ThenInclude(s=>s.Hesap).SingleAsync(y=>y.BelgeTipi=="Fatura"&&y.BelgeId==f.Id); Assert.Contains(y.Satirlar,s=>s.Hesap!.HesapKodu=="120"&&s.Borc==f.GenelToplam); Assert.Contains(y.Satirlar,s=>s.Hesap!.HesapKodu=="600"&&s.Alacak>0); }
    [Fact] public async Task Sifir_kdv_gereksiz_391_satiri_uretmez() { var x=await Hazirla(2,20,0); var f=await Dogrudan(x,2); await using var c=Yeni(); var y=await c.YevmiyeFisleri.Include(y=>y.Satirlar).ThenInclude(s=>s.Hesap).SingleAsync(y=>y.BelgeId==f.Id&&y.BelgeTipi=="Fatura"); Assert.DoesNotContain(y.Satirlar,s=>s.Hesap!.HesapKodu=="391"); }
    [Fact] public async Task Acik_hesap_kasa_banka_uretmez() { var x=await Hazirla(1); var f=await Dogrudan(x,1,FaturaOdemeSekli.AcikHesap); await using var c=Yeni(); Assert.False(await c.KasaHareketleri.AnyAsync(h=>h.FaturaId==f.Id)); Assert.False(await c.BankaHareketleri.AnyAsync(h=>h.FaturaId==f.Id)); }
    [Fact] public async Task Nakit_odeme_kasa_hareketi_uretir() { var x=await Hazirla(1); var f=await Dogrudan(x,1,FaturaOdemeSekli.Nakit); await using var c=Yeni(); Assert.True(await c.KasaHareketleri.AnyAsync(h=>h.FaturaId==f.Id)); }
    [Theory][InlineData(FaturaOdemeSekli.Banka)][InlineData(FaturaOdemeSekli.KrediKarti)] public async Task Banka_ve_kart_banka_hareketi_uretir(FaturaOdemeSekli o) { var x=await Hazirla(1); var f=await Dogrudan(x,1,o); await using var c=Yeni(); Assert.True(await c.BankaHareketleri.AnyAsync(h=>h.FaturaId==f.Id)); }
    [Fact] public async Task Fatura_finans_stok_ve_miktar_atomik_kaydedilir() { var x=await Hazirla(2); var f=await Dogrudan(x,2); await using var c=Yeni(); Assert.True(await c.Faturalar.AnyAsync(y=>y.Id==f.Id)); Assert.True(await c.StokHareketleri.AnyAsync(y=>y.IlgiliBelgeId==f.Id)); Assert.True(await c.YevmiyeFisleri.AnyAsync(y=>y.BelgeId==f.Id)); }
    [Fact] public async Task Kontrollu_hata_full_rollback() { var x=await Hazirla(2,1); await Assert.ThrowsAsync<InvalidOperationException>(()=>Dogrudan(x,2)); await using var c=Yeni(); Assert.False(await c.Faturalar.AnyAsync(y=>y.SiparisId==x.SiparisId)); await Stok(x,1); }
    [Fact] public async Task Son_faturalanabilir_miktarda_yalniz_bir_islem_kazanir() { var x=await Hazirla(5,20); var r=await Task.WhenAll(Dene(()=>Dogrudan(x,5)),Dene(()=>Dogrudan(x,5))); Assert.Single(r,v=>v); }
    [Fact] public async Task Irsaliyeli_fatura_iptalinde_stok_geri_eklenmez() { var x=await Hazirla(5,20); var i=await Sevk(x,5); var f=await Irsaliyeden(x,i,5); await IptalFatura(f.Id); await Stok(x,15); }
    [Fact] public async Task Dogrudan_fatura_iptalinde_stok_geri_gelir() { var x=await Hazirla(5,20); var f=await Dogrudan(x,5); await Stok(x,15); await IptalFatura(f.Id); await Stok(x,20); }
    [Fact] public async Task Iptalde_faturalanan_miktar_azalir_ve_kritik_zincir_dogrudur() { var x=await Hazirla(10,20); var i=await Sevk(x,10); await Stok(x,10); var f=await Irsaliyeden(x,i,10); await Stok(x,10); await IptalFatura(f.Id); await Stok(x,10); await Kontrol(x,s=>Assert.Equal(0,s.Kalemler.Single().FaturalananMiktar)); await IptalIrsaliye(i.Id); await Stok(x,20); }

    private async Task<(int SiparisId,int KalemId,int UrunId)> Hazirla(decimal miktar,decimal stok=20,decimal kdv=20)
    { await using var c=Yeni(); var sirket=await c.Sirketler.FirstOrDefaultAsync(); if(sirket==null){sirket=new EtkiAlani.Modeller.Kurumsal.Sirket{Unvan="Fatura Test Şirketi",SirketKodu="FTS-01"};c.Sirketler.Add(sirket);await c.SaveChangesAsync();} var cariId=await c.CariKartlar.Select(x=>x.Id).FirstOrDefaultAsync(); if(cariId==0){var cari=new CariKart{SirketId=sirket.Id,Kod="F43-CARI",Unvan="Fatura Test Cari",Aktif=true};c.CariKartlar.Add(cari);await c.SaveChangesAsync();cariId=cari.Id;} var kod="F43"+Guid.NewGuid().ToString("N")[..10]; var u=new Urun{SirketId=sirket.Id,Kod=kod,Ad="Fatura Test Ürünü",Birim="Adet",Aktif=true,StokMiktari=stok,KdvOrani=kdv}; c.Urunler.Add(u); await c.SaveChangesAsync(); c.UrunLokasyonStoklari.AddRange(new UrunLokasyonStogu{UrunId=u.Id,DepoId=1,MevcutMiktar=stok},new UrunLokasyonStogu{UrunId=u.Id,DepoId=2},new UrunLokasyonStogu{UrunId=u.Id,DepoId=3}); var s=new Siparis{SiparisNo="S43-"+Guid.NewGuid().ToString("N")[..12],SiparisTuru=SiparisTuru.Satis,CariKartId=cariId,ParaBirimiId=1,Kur=1,Durum=SiparisDurumu.Onaylandi,OlusturanKullaniciId=1,Kalemler=[new SiparisKalemi{UrunId=u.Id,UrunKodu=u.Kod,UrunAdi=u.Ad,Birim=u.Birim,Miktar=miktar,BirimFiyat=100,KdvOrani=kdv}]}; c.Siparisler.Add(s); await c.SaveChangesAsync(); return(s.Id,s.Kalemler.Single().Id,u.Id); }
    private async Task<Irsaliye> Sevk((int SiparisId,int KalemId,int UrunId)x,decimal m){await using var c=Yeni();return await IS(c).SiparistenSevkEtAsync(new Irsaliye{SiparisId=x.SiparisId,SevkTarihi=DateTime.UtcNow,Kalemler=[new IrsaliyeKalemi{SiparisKalemiId=x.KalemId,Miktar=m,StokDagilimlari=[new IrsaliyeKalemiStokDagilimi{DepoId=1,Miktar=m}]}]});}
    private async Task<Fatura> Dogrudan((int SiparisId,int KalemId,int UrunId)x,decimal m,FaturaOdemeSekli o=FaturaOdemeSekli.AcikHesap){await using var c=Yeni();return await FS(c).KaynaktanFaturaOlusturAsync(new Fatura{KaynakTuru=FaturaKaynakTuru.Siparisten,SiparisId=x.SiparisId,Tarih=DateTime.UtcNow,OdemeSekli=o,Kalemler=[new FaturaKalemi{SiparisKalemiId=x.KalemId,Miktar=m,StokDagilimlari=[new FaturaKalemiStokDagilimi{DepoId=1,Miktar=m}]}]});}
    private async Task<Fatura> Irsaliyeden((int SiparisId,int KalemId,int UrunId)x,Irsaliye i,decimal m){await using var c=Yeni();var ik=await c.IrsaliyeKalemleri.SingleAsync(k=>k.IrsaliyeId==i.Id);return await FS(c).KaynaktanFaturaOlusturAsync(new Fatura{KaynakTuru=FaturaKaynakTuru.Irsaliyeden,IrsaliyeId=i.Id,Tarih=DateTime.UtcNow,Kalemler=[new FaturaKalemi{IrsaliyeKalemiId=ik.Id,Miktar=m}]});}
    private async Task IptalFatura(int id){await using var c=Yeni();await FS(c).IptalEtAsync(id);} private async Task IptalIrsaliye(int id){await using var c=Yeni();await IS(c).IptalEtAsync(id);}
    private async Task Stok((int SiparisId,int KalemId,int UrunId)x,decimal beklenen){await using var c=Yeni();Assert.Equal(beklenen,await c.UrunLokasyonStoklari.Where(s=>s.UrunId==x.UrunId&&s.DepoId==1).Select(s=>s.MevcutMiktar).SingleAsync());}
    private async Task Kontrol((int SiparisId,int KalemId,int UrunId)x,Action<Siparis>a){await using var c=Yeni();a(await c.Siparisler.Include(s=>s.Kalemler).SingleAsync(s=>s.Id==x.SiparisId));}
    private static async Task<bool> Dene(Func<Task<Fatura>> f){try{await f();return true;}catch{return false;}}
    private MuhasebeBaglami Yeni(){_fixture.RequireDatabase();return new(_fixture.Options);} private static FaturaServisi FS(MuhasebeBaglami c)=>new(c,new KS()); private static IrsaliyeServisi IS(MuhasebeBaglami c)=>new(c,new KS());
    private sealed class KS:IKullaniciServisi{public Kullanici? AktifKullanici{get;}=new(){Id=1,KullaniciAdi="admin"};public KullaniciYetkileri? AktifYetkiler=>null;public Task<bool> GirisYapAsync(string a,string s,CancellationToken c=default)=>Task.FromResult(true);public void CikisYap(){}public bool YetkiliMi(Func<KullaniciYetkileri,bool>f)=>true;public void YetkiKontrolEt(Func<KullaniciYetkileri,bool>f,string i){}public Task<List<Kullanici>> TumunuGetirAsync()=>throw new NotSupportedException();public Task<Kullanici?> GetirAsync(int id)=>throw new NotSupportedException();public Task<Kullanici>EkleAsync(Kullanici k,string s)=>throw new NotSupportedException();public Task GuncelleAsync(Kullanici k)=>throw new NotSupportedException();public Task SifreDegistirAsync(int id,string s)=>throw new NotSupportedException();public Task SilAsync(int id)=>throw new NotSupportedException();}
}
