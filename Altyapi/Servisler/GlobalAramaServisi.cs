using Altyapi.Veri;using Microsoft.EntityFrameworkCore;using Uygulama.Arayuzler;
namespace Altyapi.Servisler;
public sealed class GlobalAramaServisi(MuhasebeBaglami db,IAktifFirmaBaglami aktif):IGlobalAramaServisi
{
 public async Task<IReadOnlyList<GlobalAramaSonucu>> AraAsync(string m,CancellationToken t=default){m=m.Trim();if(m.Length<2)return[];var s=aktif.SirketId;var r=new List<GlobalAramaSonucu>();
 r.AddRange(await db.CariKartlar.AsNoTracking().Where(x=>x.SirketId==s&&(EF.Functions.ILike(x.Kod,$"%{m}%")||EF.Functions.ILike(x.Unvan,$"%{m}%")||(x.Ad!=null&&EF.Functions.ILike(x.Ad,$"%{m}%"))||(x.Soyad!=null&&EF.Functions.ILike(x.Soyad,$"%{m}%"))||(x.Telefon!=null&&EF.Functions.ILike(x.Telefon,$"%{m}%"))||(x.VergiNo!=null&&EF.Functions.ILike(x.VergiNo,$"%{m}%"))||(x.TcNo!=null&&EF.Functions.ILike(x.TcNo,$"%{m}%")))).Take(10).Select(x=>new GlobalAramaSonucu("CARİLER",x.Id,x.Kod,x.Unvan,x.Telefon)).ToListAsync(t));
 r.AddRange(await db.Urunler.AsNoTracking().Where(x=>x.SirketId==s&&(EF.Functions.ILike(x.Kod,$"%{m}%")||EF.Functions.ILike(x.Ad,$"%{m}%")||(x.Barkod!=null&&EF.Functions.ILike(x.Barkod,$"%{m}%")))).Take(10).Select(x=>new GlobalAramaSonucu("ÜRÜNLER",x.Id,x.Kod,x.Ad,x.Barkod)).ToListAsync(t));
 r.AddRange(await db.Faturalar.AsNoTracking().Where(x=>x.SirketId==s&&EF.Functions.ILike(x.FaturaNo,$"%{m}%")).Take(10).Select(x=>new GlobalAramaSonucu("FATURALAR",x.Id,x.FaturaNo,x.FaturaNo,null)).ToListAsync(t));
 r.AddRange(await db.Siparisler.AsNoTracking().Where(x=>x.CariKart!.SirketId==s&&EF.Functions.ILike(x.SiparisNo,$"%{m}%")).Take(10).Select(x=>new GlobalAramaSonucu("SİPARİŞLER",x.Id,x.SiparisNo,x.SiparisNo,null)).ToListAsync(t));
 r.AddRange(await db.Irsaliyeler.AsNoTracking().Where(x=>x.CariKart!.SirketId==s&&EF.Functions.ILike(x.IrsaliyeNo,$"%{m}%")).Take(10).Select(x=>new GlobalAramaSonucu("İRSALİYELER",x.Id,x.IrsaliyeNo,x.IrsaliyeNo,null)).ToListAsync(t));return r;}
}
