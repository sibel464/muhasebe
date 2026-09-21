using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using Uygulama.Dto.Cari360;
namespace Altyapi.Servisler;
public sealed partial class CariDetayServisi
{
    public async Task<CariBelgeDetayDto> GetBelgeAsync(int cariId,Guid sirketId,string kaynak,int belgeId,CancellationToken cancellationToken=default)
    {
        var sekme=kaynak switch { "Fatura"=>CariSekme.Faturalar,"Sipariş"=>CariSekme.Siparisler,"İrsaliye"=>CariSekme.Irsaliyeler,"Kasa" or "Banka"=>CariSekme.TahsilatOdeme,"Çek / Senet"=>CariSekme.CekSenet,"Cari"=>CariSekme.Ekstre,_=>throw new ArgumentException("Belge kaynağı tanınmıyor.") };
        var sayfa=await GetSayfaAsync(cariId,sirketId,sekme,new CariSorgu { BelgeId=belgeId,Kaynak=kaynak=="Cari" ? null : kaynak,IptalleriGoster=true },cancellationToken);
        var row=sayfa.Satirlar.SingleOrDefault() ?? throw new InvalidOperationException("Belge aktif firma ve cariye ait değil.");
        var result=new CariBelgeDetayDto { Belge=row };
        if(kaynak=="Fatura") {
            result.Kalemler=await _baglam.FaturaKalemleri.IgnoreQueryFilters().AsNoTracking()
                .Where(x=>x.FaturaId==belgeId && x.Fatura!.CariKartId==cariId && x.Fatura.SirketId==sirketId)
                .OrderBy(x=>x.Id).Take(100).Select(x=>new CariMerkezSatirDto { Id=x.Id,BelgeNo=x.UrunKodu,Aciklama=x.UrunAd,Miktar=x.Miktar,Net=x.BirimFiyat,Kdv=x.KdvTutari,Toplam=x.SatirToplami }).ToListAsync(cancellationToken);
            var f=await Faturalar(cariId,sirketId).Where(x=>x.Id==belgeId).Select(x=>new { x.SiparisId,x.IrsaliyeId }).SingleAsync(cancellationToken);
            if(f.SiparisId.HasValue && _kullanici!.YetkiliMi(x=>x.SatisGecmisiGoruntule))
                result.Baglantilar.AddRange((await GetSayfaAsync(cariId,sirketId,CariSekme.Siparisler,new CariSorgu { BelgeId=f.SiparisId,IptalleriGoster=true },cancellationToken)).Satirlar);
            if(f.IrsaliyeId.HasValue && _kullanici!.YetkiliMi(x=>x.IrsaliyeGoruntule))
                result.Baglantilar.AddRange((await GetSayfaAsync(cariId,sirketId,CariSekme.Irsaliyeler,new CariSorgu { BelgeId=f.IrsaliyeId,IptalleriGoster=true },cancellationToken)).Satirlar);
            if(_kullanici!.YetkiliMi(x=>x.FinansRaporuGoruntule || x.TahsilatYap || x.OdemeYap)) {
                result.Baglantilar.AddRange(await _baglam.BelgeKapamalar.AsNoTracking()
                    .Where(x=>!x.Silindi && x.FaturaCariHareket!.CariKartId==cariId && x.FaturaCariHareket.CariKart!.SirketId==sirketId &&
                        x.FaturaCariHareket.IlgiliBelgeId==belgeId &&
                        (x.FaturaCariHareket.HareketTuru==CariHareketTuru.SatisFaturasi || x.FaturaCariHareket.HareketTuru==CariHareketTuru.AlisFaturasi ||
                         x.FaturaCariHareket.HareketTuru==CariHareketTuru.SatisIadesi || x.FaturaCariHareket.HareketTuru==CariHareketTuru.AlisIadesi) &&
                        x.OdenenCariHareket!.CariKartId==cariId && x.OdenenCariHareket.CariKart!.SirketId==sirketId)
                    .OrderByDescending(x=>x.KapamaTarihi).Take(100)
                    .Select(x=>new CariMerkezSatirDto { Id=x.OdenenCariHareketId,BelgeId=x.OdenenCariHareketId,Kaynak="Cari",BelgeNo=x.OdenenCariHareket!.BelgeNo ?? "",Tarih=x.OdenenCariHareket.Tarih,Toplam=x.KapananTutar,Tur="Kapama" }).ToListAsync(cancellationToken));
            }
        } else if(kaynak is "Sipariş" or "İrsaliye") {
            if(_kullanici!.YetkiliMi(x=>x.FaturaGoruntule)) {
                var ids=Faturalar(cariId,sirketId).Where(x=>kaynak=="Sipariş" ? x.SiparisId==belgeId : x.IrsaliyeId==belgeId).Select(x=>x.Id);
                result.Baglantilar.AddRange(await FaturaSatirlari(cariId,sirketId).Where(x=>ids.Contains(x.Id)).OrderBy(x=>x.Tarih).Take(100).ToListAsync(cancellationToken));
            }
            if(kaynak=="Sipariş" && _kullanici.YetkiliMi(x=>x.IrsaliyeGoruntule)) {
                result.Baglantilar.AddRange(await _baglam.Irsaliyeler.IgnoreQueryFilters().AsNoTracking()
                    .Where(x=>x.SiparisId==belgeId && x.CariKartId==cariId && x.CariKart!.SirketId==sirketId)
                    .OrderBy(x=>x.Tarih).Take(100).Select(x=>new CariMerkezSatirDto { Id=x.Id,BelgeId=x.Id,Kaynak="İrsaliye",BelgeNo=x.IrsaliyeNo,Tarih=x.Tarih,Toplam=x.GenelToplam }).ToListAsync(cancellationToken));
            }
        }
        if(row.TersKayitId.HasValue && kaynak is "Kasa" or "Banka")
            result.Baglantilar.AddRange((await GetSayfaAsync(cariId,sirketId,CariSekme.TahsilatOdeme,new CariSorgu { BelgeId=row.TersKayitId,Kaynak=kaynak,IptalleriGoster=true },cancellationToken)).Satirlar);
        return result;
    }
    public async Task<List<CariUrunAnalizDto>> GetCariUrunAnaliziAsync(int cariId,Guid sirketId,bool iptalleriGoster=false,CancellationToken cancellationToken=default)
        => await GetUrunSayfasiAsync(cariId,sirketId,false,new CariSorgu(),cancellationToken);
    public async Task<List<CariUrunAnalizDto>> GetUrunSayfasiAsync(int cariId,Guid sirketId,bool alis,CariSorgu sorgu,CancellationToken cancellationToken=default)
    {
        await Dogrula(cariId,sirketId,alis ? CariSekme.Alislar : CariSekme.Satislar,cancellationToken);
        sorgu.Dogrula();
        var tur=alis ? FaturaTuru.Alis : FaturaTuru.Satis;
        var q=_baglam.FaturaKalemleri.AsNoTracking().Where(x=>x.Fatura!=null && !x.Fatura.Silindi && x.Fatura.CariKartId==cariId &&
            x.Fatura.SirketId==sirketId && x.Fatura.CariKart!.SirketId==sirketId && x.Fatura.Durum==BelgeDurumu.Onaylandi && x.Fatura.Tur==tur);
        if(sorgu.Baslangic.HasValue) { var d=UtcGun(sorgu.Baslangic.Value);q=q.Where(x=>x.Fatura!.Tarih>=d); }
        if(sorgu.Bitis.HasValue) { var d=UtcGun(sorgu.Bitis.Value).AddDays(1);q=q.Where(x=>x.Fatura!.Tarih<d); }
        if(sorgu.ParaBirimiId.HasValue) q=q.Where(x=>x.Fatura!.ParaBirimiId==sorgu.ParaBirimiId);
        return await q.GroupBy(x=>new { x.UrunId,x.UrunKodu,x.UrunAd,x.Birim,x.Fatura!.ParaBirimiId })
            .OrderBy(g=>g.Key.UrunId).ThenBy(g=>g.Key.ParaBirimiId).Skip((sorgu.Sayfa-1)*sorgu.SayfaBoyutu).Take(sorgu.SayfaBoyutu)
            .Select(g=>new CariUrunAnalizDto {
                UrunKodu=g.Key.UrunKodu,UrunAdi=g.Key.UrunAd,Birim=g.Key.Birim,
                SatilanMiktar=alis ? 0 : g.Sum(x=>x.Miktar),AlinanMiktar=alis ? g.Sum(x=>x.Miktar) : 0,
                SatilanTutar=alis ? 0 : g.Sum(x=>x.SatirToplami),AlinanTutar=alis ? g.Sum(x=>x.SatirToplami) : 0,
                SonIslemTarihi=g.Max(x=>(DateTime?)x.Fatura!.Tarih),
                SonFiyat=g.OrderByDescending(x=>x.Fatura!.Tarih).ThenByDescending(x=>x.Id).Select(x=>x.BirimFiyat).First(),
                OrtalamaFiyat=g.Sum(x=>x.Miktar)==0 ? 0 : g.Sum(x=>x.Miktar*x.BirimFiyat)/g.Sum(x=>x.Miktar),
                ParaBirimi=_baglam.ParaBirimleri.Where(p=>p.Id==g.Key.ParaBirimiId).Select(p=>p.Kod).FirstOrDefault() ?? "TRY"
            }).ToListAsync(cancellationToken);
    }
    // Eski çağrı noktaları da aynı doğrulama ve sınırlı sorgu yolunu kullanır.
    public async Task<List<CariHareketDto>> GetCariHareketlerAsync(int cariId,Guid sirketId,bool iptalleriGoster=false,CancellationToken cancellationToken=default)
        => (await GetSayfaAsync(cariId,sirketId,CariSekme.Ekstre,new CariSorgu { IptalleriGoster=iptalleriGoster },cancellationToken)).Satirlar.OrderByDescending(x=>x.Tarih).ThenByDescending(x=>x.Id)
        .Select(x=>new CariHareketDto { Id=x.Id,Tarih=x.Tarih,BelgeNo=x.BelgeNo,IslemTuru=x.Tur,Aciklama=x.Aciklama,Borc=x.Borc,Alacak=x.Alacak,Bakiye=x.MutlakBakiye,BakiyeYonu=x.BakiyeYonu,Kaynak=x.Kaynak,Durum=x.Durum }).ToList();
    public async Task<List<CariFaturaDto>> GetCariFaturalarAsync(int cariId,Guid sirketId,string? tur=null,bool iptalleriGoster=false,CancellationToken cancellationToken=default)
        => (await GetSayfaAsync(cariId,sirketId,CariSekme.Faturalar,new CariSorgu { Tur=tur,IptalleriGoster=iptalleriGoster },cancellationToken)).Satirlar
        .Select(x=>new CariFaturaDto { Id=x.Id,FaturaNo=x.BelgeNo,Tarih=x.Tarih,Tur=x.Tur,Net=x.Net,Kdv=x.Kdv,GenelToplam=x.Toplam,Odenen=x.Odenen ?? 0,Durum=x.Durum,Kaynak=x.Referans }).ToList();
    public async Task<List<CariSiparisDto>> GetCariSiparislerAsync(int cariId,Guid sirketId,bool iptalleriGoster=false,CancellationToken cancellationToken=default)
        => (await GetSayfaAsync(cariId,sirketId,CariSekme.Siparisler,new CariSorgu { IptalleriGoster=iptalleriGoster },cancellationToken)).Satirlar
        .Select(x=>new CariSiparisDto { Id=x.Id,SiparisNo=x.BelgeNo,Tarih=x.Tarih,Tur=x.Tur,TeslimTarihi=x.Vade,Toplam=x.Toplam,Durum=x.Durum,SevkDurumu=x.KalanSevk==0 ? "Tamamlandı" : "Açık",FaturaDurumu=x.KalanFatura==0 ? "Tamamlandı" : "Açık" }).ToList();
    public async Task<List<CariIrsaliyeDto>> GetCariIrsaliyelerAsync(int cariId,Guid sirketId,bool iptalleriGoster=false,CancellationToken cancellationToken=default)
        => (await GetSayfaAsync(cariId,sirketId,CariSekme.Irsaliyeler,new CariSorgu { IptalleriGoster=iptalleriGoster },cancellationToken)).Satirlar
        .Select(x=>new CariIrsaliyeDto { Id=x.Id,IrsaliyeNo=x.BelgeNo,Tarih=x.Tarih,Tur=x.Tur,KaynakSiparis=x.Referans,ToplamMiktar=x.Miktar,Durum=x.Durum,FaturalandiMi=x.Durum=="Faturalandı" }).ToList();
    public async Task<List<CariTahsilatOdemeDto>> GetCariTahsilatOdemelerAsync(int cariId,Guid sirketId,bool iptalleriGoster=false,CancellationToken cancellationToken=default)
        => (await GetSayfaAsync(cariId,sirketId,CariSekme.TahsilatOdeme,new CariSorgu { IptalleriGoster=iptalleriGoster },cancellationToken)).Satirlar
        .Select(x=>new CariTahsilatOdemeDto { Id=x.Id,IslemNo=x.BelgeNo,Tarih=x.Tarih,Tur=x.Tur,OdemeYontemi=x.Yontem,KasaBanka=x.KasaBanka,Tutar=x.Toplam,ParaBirimi=x.ParaBirimi,Referans=x.Referans,Aciklama=x.Aciklama,Durum=x.Durum }).ToList();
    public async Task<List<CariIadeDto>> GetCariIadelerAsync(int cariId,Guid sirketId,bool iptalleriGoster=false,CancellationToken cancellationToken=default)
        => (await GetSayfaAsync(cariId,sirketId,CariSekme.Iadeler,new CariSorgu { IptalleriGoster=iptalleriGoster },cancellationToken)).Satirlar
        .Select(x=>new CariIadeDto { Id=x.Id,BelgeNo=x.BelgeNo,Tarih=x.Tarih,Tur=x.Tur,Toplam=x.Toplam,Durum=x.Durum,Aciklama=x.Aciklama }).ToList();
    public async Task<List<CariCekSenetDto>> GetCariCekSenetlerAsync(int cariId,Guid sirketId,bool iptalleriGoster=false,CancellationToken cancellationToken=default)
        => (await GetSayfaAsync(cariId,sirketId,CariSekme.CekSenet,new CariSorgu { IptalleriGoster=iptalleriGoster },cancellationToken)).Satirlar
        .Select(x=>new CariCekSenetDto { Id=x.Id,BelgeNo=x.BelgeNo,CekSenetNo=x.Referans,Tur=x.Tur,Vade=x.Vade ?? x.Tarih,Tutar=x.Toplam,Durum=x.Durum }).ToList();
}
