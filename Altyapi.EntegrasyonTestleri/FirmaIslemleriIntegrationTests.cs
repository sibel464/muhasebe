using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Kurumsal;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;
[Collection("PostgreSQL integration")]
public sealed class FirmaIslemleriIntegrationTests
{
 private static SirketServisi Servis(MuhasebeBaglami db) => new(db, new Cari360TestKullanici { AktifYetkiler = new() { AyarlariYonet = true } });
 private readonly IsolatedPostgreSqlFixture _f; public FirmaIslemleriIntegrationTests(IsolatedPostgreSqlFixture f)=>_f=f;
 [Fact] public async Task Otomatik_firma_kodu_sequence_ile_benzersiz_uretilir(){await using var a=new MuhasebeBaglami(_f.Options);await using var b=new MuhasebeBaglami(_f.Options);var r=await Task.WhenAll(Servis(a).KaydetAsync(Yeni(""),true),Servis(b).KaydetAsync(Yeni(""),true));Assert.NotEqual(r[0].SirketKodu,r[1].SirketKodu);Assert.All(r,x=>Assert.Matches("^FRM-[0-9]{6}$",x.SirketKodu));}
 [Fact] public async Task Manuel_duplicate_firma_kodu_engellenir(){var k="FT-"+Guid.NewGuid().ToString("N")[..8];await using var db=new MuhasebeBaglami(_f.Options);var s=Servis(db);await s.KaydetAsync(Yeni(k),false);await Assert.ThrowsAsync<InvalidOperationException>(()=>s.KaydetAsync(Yeni(k.ToLowerInvariant()),false));}
 [Fact] public async Task Vkn_tckn_uzunlugu_dogrulanir(){await using var db=new MuhasebeBaglami(_f.Options);var x=Yeni("V-"+Guid.NewGuid().ToString("N")[..8]);x.VergiNo="123";await Assert.ThrowsAsync<InvalidOperationException>(()=>Servis(db).KaydetAsync(x,false));}
 [Fact] public async Task Firma_pasif_ve_tekrar_aktif_yapilabilir(){await using var db=new MuhasebeBaglami(_f.Options);var s=Servis(db);var x=await s.KaydetAsync(Yeni("D-"+Guid.NewGuid().ToString("N")[..8]),false);await s.DurumDegistirAsync(x.Id,false);Assert.False((await s.ListeleAsync(aktif:false)).Single(y=>y.Id==x.Id).AktifMi);await s.DurumDegistirAsync(x.Id,true);Assert.True((await s.ListeleAsync(aktif:true)).Single(y=>y.Id==x.Id).AktifMi);}
 private static Sirket Yeni(string kod)=>new(){SirketKodu=kod,Unvan="Paket 6 Test Firması",AktifMi=true};
}
