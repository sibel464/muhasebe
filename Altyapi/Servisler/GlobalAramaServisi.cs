using Altyapi.Veri;using Microsoft.EntityFrameworkCore;using Uygulama.Arayuzler;
namespace Altyapi.Servisler;
// Arama sonuçları kullanıcının yetkisine göre süzülür: görme yetkisi olmayan modülün kaydı aramada da çıkmaz.
public sealed class GlobalAramaServisi(MuhasebeBaglami db,IAktifFirmaBaglami aktif,IKullaniciServisi? kullanici=null):IGlobalAramaServisi
{
 private bool Yetkili(Func<EtkiAlani.Modeller.Kullanici.KullaniciYetkileri,bool> y)=>kullanici!=null&&kullanici.YetkiliMi(y);
 public async Task<IReadOnlyList<GlobalAramaSonucu>> AraAsync(string m,CancellationToken t=default){m=m.Trim();if(m.Length<2)return[];var s=aktif.SirketId;var r=new List<GlobalAramaSonucu>();
 if(Yetkili(y=>y.CariGoruntule))
 r.AddRange(await db.CariKartlar.AsNoTracking().Where(x=>x.SirketId==s&&(EF.Functions.ILike(x.Kod,$"%{m}%")||EF.Functions.ILike(x.Unvan,$"%{m}%")||(x.Ad!=null&&EF.Functions.ILike(x.Ad,$"%{m}%"))||(x.Soyad!=null&&EF.Functions.ILike(x.Soyad,$"%{m}%"))||(x.Telefon!=null&&EF.Functions.ILike(x.Telefon,$"%{m}%"))||(x.VergiNo!=null&&EF.Functions.ILike(x.VergiNo,$"%{m}%"))||(x.TcNo!=null&&EF.Functions.ILike(x.TcNo,$"%{m}%")))).Take(10).Select(x=>new GlobalAramaSonucu("CARİLER",x.Id,x.Kod,x.Unvan,x.Telefon)).ToListAsync(t));
 if(Yetkili(y=>y.StokGoruntule||y.SatisYap))
 r.AddRange(await db.Urunler.AsNoTracking().Where(x=>x.SirketId==s&&(EF.Functions.ILike(x.Kod,$"%{m}%")||EF.Functions.ILike(x.Ad,$"%{m}%")||(x.Barkod!=null&&EF.Functions.ILike(x.Barkod,$"%{m}%")))).Take(10).Select(x=>new GlobalAramaSonucu("ÜRÜNLER",x.Id,x.Kod,x.Ad,x.Barkod)).ToListAsync(t));
 if(Yetkili(y=>y.FaturaGoruntule))
 r.AddRange(await db.Faturalar.AsNoTracking().Where(x=>x.SirketId==s&&EF.Functions.ILike(x.FaturaNo,$"%{m}%")).Take(10).Select(x=>new GlobalAramaSonucu("FATURALAR",x.Id,x.FaturaNo,x.FaturaNo,null)).ToListAsync(t));
 if(Yetkili(y=>y.FaturaGoruntule||y.SatisYap))
 r.AddRange(await db.Siparisler.AsNoTracking().Where(x=>x.CariKart!.SirketId==s&&EF.Functions.ILike(x.SiparisNo,$"%{m}%")).Take(10).Select(x=>new GlobalAramaSonucu("SİPARİŞLER",x.Id,x.SiparisNo,x.SiparisNo,null)).ToListAsync(t));
 if(Yetkili(y=>y.IrsaliyeGoruntule))
 r.AddRange(await db.Irsaliyeler.AsNoTracking().Where(x=>x.CariKart!.SirketId==s&&EF.Functions.ILike(x.IrsaliyeNo,$"%{m}%")).Take(10).Select(x=>new GlobalAramaSonucu("İRSALİYELER",x.Id,x.IrsaliyeNo,x.IrsaliyeNo,null)).ToListAsync(t));return r;}
}
