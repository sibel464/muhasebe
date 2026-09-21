using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Kullanici;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Cari360;

namespace Altyapi.Servisler;

public sealed partial class CariDetayServisi : ICariDetayServisi, IDisposable
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullanici;
    private readonly IAktifFirmaBaglami? _firma;
    public CariDetayServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullanici = null, IAktifFirmaBaglami? firma = null)
    { _baglam = baglam; _kullanici = kullanici; _firma = firma; }
    public void Dispose() => _baglam.Dispose();

    public async Task<List<CariParaSecimi>> GetParaBirimleriAsync(int cariId, Guid sirketId, CancellationToken cancellationToken = default)
    {
        await Dogrula(cariId, sirketId, CariSekme.Genel, cancellationToken);
        return await _baglam.ParaBirimleri.AsNoTracking().OrderBy(x=>x.Kod).Select(x=>new CariParaSecimi(x.Id,x.Kod)).ToListAsync(cancellationToken);
    }
    private async Task Dogrula(int id, Guid sirket, CariSekme sekme, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_kullanici?.AktifKullanici is not { Aktif: true } || _firma == null)
            throw new YetkisizErisimException("Cari görüntüle");
        _kullanici.YetkiKontrolEt(x => x.CariGoruntule, "Cari görüntüle");
        if (_firma.SirketId != sirket || !await _baglam.CariKartlar.AsNoTracking().AnyAsync(x => x.Id == id && x.SirketId == sirket, ct))
            throw new InvalidOperationException("Seçilen cari aktif firmaya ait değil.");
        Func<KullaniciYetkileri, bool> yetki = sekme switch {
            CariSekme.Satislar or CariSekme.Alislar or CariSekme.Faturalar or CariSekme.Iadeler or CariSekme.RiskVade => x => x.FaturaGoruntule,
            CariSekme.Siparisler => x => x.SatisGecmisiGoruntule,
            CariSekme.Irsaliyeler => x => x.IrsaliyeGoruntule,
            CariSekme.TahsilatOdeme => x => x.FinansRaporuGoruntule || x.TahsilatYap || x.OdemeYap,
            CariSekme.CekSenet => x => x.CekSenetGoruntule,
            _ => x => x.CariGoruntule
        };
        _kullanici.YetkiKontrolEt(yetki, "Cari hesap merkezi");
    }
    private IQueryable<CariHareket> Hareketler(int id, Guid sirket) => _baglam.CariHareketler.IgnoreQueryFilters().AsNoTracking()
        .Where(x => x.CariKartId == id && x.CariKart != null && x.CariKart.SirketId == sirket && !x.CariKart.Silindi);
    private IQueryable<Fatura> Faturalar(int id, Guid sirket) => _baglam.Faturalar.IgnoreQueryFilters().AsNoTracking()
        .Where(x => x.CariKartId == id && x.SirketId == sirket && x.CariKart != null && x.CariKart.SirketId == sirket && !x.CariKart.Silindi);
    private static DateTime UtcGun(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    private static IQueryable<CariMerkezSatirDto> Filtrele(IQueryable<CariMerkezSatirDto> q, CariSorgu s)
    {
        if (s.BelgeId.HasValue) q = q.Where(x => x.Id == s.BelgeId);
        if (s.Kaynak != null) q = q.Where(x => x.Kaynak == s.Kaynak);
        if (s.Baslangic.HasValue) { var d = UtcGun(s.Baslangic.Value); q = q.Where(x => x.Tarih >= d); }
        if (s.Bitis.HasValue) { var d = UtcGun(s.Bitis.Value).AddDays(1); q = q.Where(x => x.Tarih < d); }
        if (!string.IsNullOrWhiteSpace(s.BelgeNo)) { var no = s.BelgeNo.Trim(); q = q.Where(x => x.BelgeNo.Contains(no)); }
        if (s.Tur != null) q = q.Where(x => x.Tur == s.Tur);
        if (s.Durum != null) q = q.Where(x => x.Durum == s.Durum);
        if (s.Yon == "Borç") q = q.Where(x => x.Borc > 0);
        if (s.Yon == "Alacak") q = q.Where(x => x.Alacak > 0);
        return q;
    }
    public async Task<CariDetayDto?> GetCariGenelBilgiAsync(int cariId, Guid sirketId, CancellationToken cancellationToken = default)
    {
        await Dogrula(cariId, sirketId, CariSekme.Genel, cancellationToken);
        return await _baglam.CariKartlar.AsNoTracking().Where(c => c.Id == cariId && c.SirketId == sirketId)
            .Select(c => new CariDetayDto {
                Id=c.Id, Kod=c.Kod, CariTipi=c.CariTipi.Ad, GrupKodu=c.GrupKodu ?? "",
                Ad=c.Ad ?? "", Soyad=c.Soyad ?? "", FirmaUnvani=c.Unvan,
                IsKurum=c.CariTipiId != CariTipiTanimi.BireyselId,
                YetkiliKisi=((c.Ad ?? "") + " " + (c.Soyad ?? "")).Trim(),
                Tckn=c.TcNo ?? "", Vkn=c.VergiNo ?? "", VergiDairesi=c.VergiDairesi ?? "",
                Telefon=c.Telefon ?? "", Telefon2=c.EvTelefonu ?? c.IsTelefonu ?? "",
                CepTelefonu=c.CepTelefonu ?? "", Eposta=c.Eposta ?? "", Il=c.Il ?? "", Ilce=c.Ilce ?? "",
                AcikAdres=c.Adres ?? "", Iban=c.Iban ?? "", Aciklama=c.Aciklama ?? "",
                Durum=c.Aktif ? "Aktif" : "Pasif", OlusturmaTarihi=c.OlusturmaTarihi,
                GuncellemeTarihi=c.GuncellemeTarihi,
                Firma=c.Sirket != null ? c.Sirket.Unvan : "", Sube=c.Sube != null ? c.Sube.SubeAdi : "",
                Olusturan=c.OlusturanKullanici != null ? c.OlusturanKullanici.KullaniciAdi : "Kayıt yok",
                Guncelleyen=c.GuncelleyenKullanici != null ? c.GuncelleyenKullanici.KullaniciAdi : "Kayıt yok",
                RiskLimiti=c.RiskLimiti, VadeGunu=c.VadeGunu
            }).SingleAsync(cancellationToken);
    }
    private IQueryable<CariMerkezSatirDto> FaturaSatirlari(int id, Guid sirket)
    {
        var hs = Hareketler(id, sirket).Where(h => !h.Silindi);
        var ham = from f in Faturalar(id, sirket)
            let ht = f.Tur == FaturaTuru.Satis ? CariHareketTuru.SatisFaturasi : f.Tur == FaturaTuru.Alis ? CariHareketTuru.AlisFaturasi : f.Tur == FaturaTuru.SatisIade ? CariHareketTuru.SatisIadesi : CariHareketTuru.AlisIadesi
            let dogrudan = _baglam.KasaHareketleri.AsNoTracking().Where(k => k.CariKartId == id && k.FaturaId == f.Id && k.IslemDurumu == FinansIslemDurumu.Tamamlandi).Sum(k => k.Tutar) + _baglam.BankaHareketleri.AsNoTracking().Where(k => k.CariKartId == id && k.FaturaId == f.Id && k.IslemDurumu == FinansIslemDurumu.Tamamlandi).Sum(k => k.Tutar)
            let tekrar = _baglam.BelgeKapamalar.AsNoTracking().Where(k => !k.Silindi &&
                hs.Where(h => h.IlgiliBelgeId == f.Id && h.HareketTuru == ht).Select(h => h.Id).Contains(k.FaturaCariHareketId) && k.OdenenCariHareket != null &&
                !k.OdenenCariHareket.Silindi && k.OdenenCariHareket.CariKartId == id &&
                k.OdenenCariHareket.IlgiliBelgeId == f.Id && k.OdenenCariHareket.BelgeNo == f.FaturaNo &&
                (k.OdenenCariHareket.HareketTuru == CariHareketTuru.Tahsilat || k.OdenenCariHareket.HareketTuru == CariHareketTuru.Odeme) &&
                (_baglam.KasaHareketleri.AsNoTracking().Where(k => k.CariKartId == id && k.FaturaId == f.Id && k.IslemDurumu == FinansIslemDurumu.Tamamlandi).Any() || _baglam.BankaHareketleri.AsNoTracking().Where(k => k.CariKartId == id && k.FaturaId == f.Id && k.IslemDurumu == FinansIslemDurumu.Tamamlandi).Any())).Sum(k => k.KapananTutar)
            let odenen = hs.Where(h => h.IlgiliBelgeId == f.Id && h.HareketTuru == ht).Sum(h => h.KapaliTutar) + dogrudan - tekrar
            select new { f, Biliniyor = hs.Where(h => h.IlgiliBelgeId == f.Id && h.HareketTuru == ht).Any() || _baglam.KasaHareketleri.AsNoTracking().Where(k => k.CariKartId == id && k.FaturaId == f.Id && k.IslemDurumu == FinansIslemDurumu.Tamamlandi).Any() || _baglam.BankaHareketleri.AsNoTracking().Where(k => k.CariKartId == id && k.FaturaId == f.Id && k.IslemDurumu == FinansIslemDurumu.Tamamlandi).Any(), Odenen = odenen };
        return ham.Select(x => new CariMerkezSatirDto {
            Id=x.f.Id, BelgeId=x.f.Id, BelgeNo=x.f.FaturaNo, Tarih=x.f.Tarih, Vade=x.f.VadeTarihi, Kaynak="Fatura",
            Tur=x.f.Tur == FaturaTuru.Satis ? "Satış" : x.f.Tur == FaturaTuru.Alis ? "Alış" : x.f.Tur == FaturaTuru.SatisIade ? "Satış İadesi" : "Alış İadesi",
            Durum=x.f.Silindi || x.f.Durum == BelgeDurumu.Iptal ? "İptal" : x.f.Durum == BelgeDurumu.Onaylandi ? "Onaylandı" : x.f.Durum == BelgeDurumu.Beklemede ? "Beklemede" : "Taslak",
            Net=x.f.GenelToplam-x.f.KdvToplam, Kdv=x.f.KdvToplam, Toplam=x.f.GenelToplam,
            Miktar=x.f.Kalemler.Count(k => !k.Silindi), Aciklama=x.f.Aciklama,
            ParaBirimi=x.f.ParaBirimi != null ? x.f.ParaBirimi.Kod : "TRY",
            Referans=x.f.KaynakTuru == FaturaKaynakTuru.Pos ? "POS" : x.f.KaynakTuru == FaturaKaynakTuru.Siparisten ? "Sipariş" : x.f.KaynakTuru == FaturaKaynakTuru.Irsaliyeden ? "İrsaliye" : "Manuel",
            Odenen=x.Biliniyor && x.f.Durum==BelgeDurumu.Onaylandi && !x.f.Silindi ? x.Odenen : null,
            Kalan=x.Biliniyor && x.f.Durum==BelgeDurumu.Onaylandi && !x.f.Silindi ? Math.Max(0,x.f.GenelToplam-x.Odenen) : null
        });
    }
    public async Task<CariFinansOzetDto> GetCariFinansOzetAsync(int cariId, Guid sirketId, CancellationToken cancellationToken = default)
    {
        await Dogrula(cariId, sirketId, CariSekme.Genel, cancellationToken);
        var q = Hareketler(cariId,sirketId).Where(x => !x.Silindi);
        var totals = await q.GroupBy(x => 1).Select(g => new { Borc=g.Sum(x=>x.Borc), Alacak=g.Sum(x=>x.Alacak), Son=g.Max(x=>(DateTime?)x.Tarih) }).SingleOrDefaultAsync(cancellationToken);
        var limit=await _baglam.CariKartlar.Where(x=>x.Id==cariId && x.SirketId==sirketId).Select(x=>x.RiskLimiti).SingleAsync(cancellationToken);
        var result=new CariFinansOzetDto { ToplamBorc=totals?.Borc ?? 0, ToplamAlacak=totals?.Alacak ?? 0, GuncelBakiye=(totals?.Borc ?? 0)-(totals?.Alacak ?? 0), SonIslemTarihi=totals?.Son, RiskLimiti=limit };
        result.KullanilanRisk=Math.Max(0,result.GuncelBakiye);
        if (_kullanici!.YetkiliMi(x=>x.FaturaGoruntule)) {
            var acik=FaturaSatirlari(cariId,sirketId).Where(x=>x.Durum=="Onaylandı" && x.Kalan>0);
            result.AcikFaturaSayisi=await acik.CountAsync(cancellationToken);
            var bugun=UtcGun(DateTime.Today);
            result.VadesiGecmis=await acik.Where(x=>x.Tur=="Satış" && x.Vade<bugun).SumAsync(x=>x.Kalan ?? 0,cancellationToken);
        }
        return result;
    }
    private async Task<CariSayfaDto> Ekstre(int id,Guid sirket,CariSorgu s,bool fis,CancellationToken ct)
    {
        var all=Hareketler(id,sirket);
        if(s.ParaBirimiId.HasValue) all=all.Where(x=>x.ParaBirimiId==s.ParaBirimiId);
        var q=all;
        if(!s.IptalleriGoster) q=q.Where(x=>!x.Silindi);
        if(fis) q=q.Where(x=>x.HareketTuru==CariHareketTuru.Devir || x.HareketTuru==CariHareketTuru.Diger);
        var rows=q.Select(h=>new CariMerkezSatirDto {
            Id=h.Id, BelgeId=h.IlgiliBelgeId ?? h.Id, BelgeNo=h.BelgeNo ?? "", Tarih=h.Tarih,
            Tur=h.HareketTuru==CariHareketTuru.Devir ? "Açılış / Devir" :
                h.HareketTuru==CariHareketTuru.SatisFaturasi ? "Satış Faturası" :
                h.HareketTuru==CariHareketTuru.AlisFaturasi ? "Alış Faturası" :
                h.HareketTuru==CariHareketTuru.Tahsilat ? "Tahsilat" :
                h.HareketTuru==CariHareketTuru.Odeme ? "Ödeme" :
                h.HareketTuru==CariHareketTuru.SatisIadesi ? "Satış İadesi" :
                h.HareketTuru==CariHareketTuru.AlisIadesi ? "Alış İadesi" : "Diğer Cari Fiş",
            Kaynak=h.HareketTuru==CariHareketTuru.SatisFaturasi || h.HareketTuru==CariHareketTuru.AlisFaturasi || h.HareketTuru==CariHareketTuru.SatisIadesi || h.HareketTuru==CariHareketTuru.AlisIadesi ? "Fatura" : "Cari",
            Durum=h.Silindi ? "İptal" : "Aktif", Aciklama=h.Aciklama, Borc=h.Borc, Alacak=h.Alacak,
            Toplam=h.Borc+h.Alacak, Kur=h.Kur, ParaBirimi=h.ParaBirimi!=null ? h.ParaBirimi.Kod : "TRY",
            Bakiye=all.Where(p=>!p.Silindi && (p.Tarih<h.Tarih || (p.Tarih==h.Tarih && p.Id<=h.Id))).Sum(p=>p.Borc-p.Alacak)
        });
        rows=Filtrele(rows,s);
        var result=new CariSayfaDto { ToplamKayit=await rows.CountAsync(ct) };
        if(s.Baslangic.HasValue) { var bas=UtcGun(s.Baslangic.Value); result.DevirBakiyesi=await all.Where(x=>!x.Silindi && x.Tarih<bas).SumAsync(x=>x.Borc-x.Alacak,ct); }
        result.Satirlar=await rows.OrderBy(x=>x.Tarih).ThenBy(x=>x.Id).Skip((s.Sayfa-1)*s.SayfaBoyutu).Take(s.SayfaBoyutu).ToListAsync(ct);
        result.Aciklama="Bakiye yalnız etkin cari hareketlerinden hesaplanır. Tür/belge filtreleri yürüyen hesap bakiyesini değiştirmez; iptal satırları bakiyeye katılmaz.";
        if(fis) result.Aciklama="Mevcut açılış/devir ve diğer cari fişleri. Ayrı dekont/virman türü ve hedef cari ilişkisi mevcut modelde bulunmuyor.";
        return result;
    }
    public async Task<CariSayfaDto> GetSayfaAsync(int cariId,Guid sirketId,CariSekme sekme,CariSorgu sorgu,CancellationToken cancellationToken=default)
    {
        sorgu.Dogrula();
        await Dogrula(cariId,sirketId,sekme,cancellationToken);
        if(sekme is CariSekme.Ekstre or CariSekme.CariFisleri)
            return await Ekstre(cariId,sirketId,sorgu,sekme==CariSekme.CariFisleri,cancellationToken);
        IQueryable<CariMerkezSatirDto> q;
        var result=new CariSayfaDto();
        if(sekme is CariSekme.Faturalar or CariSekme.Satislar or CariSekme.Alislar or CariSekme.Iadeler or CariSekme.RiskVade) {
            q=FaturaSatirlari(cariId,sirketId);
            if(sorgu.ParaBirimiId.HasValue) { var ids=Faturalar(cariId,sirketId).Where(x=>x.ParaBirimiId==sorgu.ParaBirimiId).Select(x=>x.Id); q=q.Where(x=>ids.Contains(x.Id)); }
            if(sekme is CariSekme.Satislar or CariSekme.Alislar) {
                var tur=sekme==CariSekme.Satislar ? "Satış" : "Alış";
                var ay=UtcGun(new DateTime(DateTime.Today.Year,DateTime.Today.Month,1));
                var onay=q.Where(x=>x.Durum=="Onaylandı");
                result.Toplam=await onay.Where(x=>x.Tur==tur).SumAsync(x=>x.Toplam,cancellationToken);
                result.BuAy=await onay.Where(x=>x.Tur==tur && x.Tarih>=ay).SumAsync(x=>x.Toplam,cancellationToken);
                result.Iade=await onay.Where(x=>x.Tur==tur+" İadesi").SumAsync(x=>x.Toplam,cancellationToken);
                result.SonTarih=await onay.Where(x=>x.Tur==tur).MaxAsync(x=>(DateTime?)x.Tarih,cancellationToken);
                q=q.Where(x=>x.Tur==tur);
            }
            if(sekme==CariSekme.Iadeler) q=q.Where(x=>x.Tur=="Satış İadesi" || x.Tur=="Alış İadesi");
            if(sekme==CariSekme.RiskVade) q=q.Where(x=>x.Durum=="Onaylandı" && x.Kalan>0);
            result.Aciklama="Ödenen ve kalan tutarlar mevcut cari hareket kapamalarından alınır. İade kaynak faturası için modelde doğrudan ilişki bulunmuyor.";
        } else if(sekme==CariSekme.Siparisler) {
            var sq=_baglam.Siparisler.IgnoreQueryFilters().AsNoTracking().Where(x=>x.CariKartId==cariId && x.CariKart!=null && x.CariKart.SirketId==sirketId && !x.CariKart.Silindi);
            if(sorgu.ParaBirimiId.HasValue) sq=sq.Where(x=>x.ParaBirimiId==sorgu.ParaBirimiId);
            q=sq.Select(x=>new CariMerkezSatirDto {
                Id=x.Id,BelgeId=x.Id,Kaynak="Sipariş",BelgeNo=x.SiparisNo,Tarih=x.SiparisTarihi,Vade=x.TeslimTarihi,
                Tur=x.SiparisTuru==EtkiAlani.Modeller.Siparis.SiparisTuru.Satis ? "Satış" : "Alış",
                Durum=x.Silindi || x.Durum==EtkiAlani.Modeller.Siparis.SiparisDurumu.Iptal ? "İptal" :
                    x.Durum==EtkiAlani.Modeller.Siparis.SiparisDurumu.Onaylandi ? "Onaylandı" :
                    x.Durum==EtkiAlani.Modeller.Siparis.SiparisDurumu.Tamamlandi ? "Tamamlandı" :
                    x.Durum==EtkiAlani.Modeller.Siparis.SiparisDurumu.KismenKarsilandi ? "Kısmen Karşılandı" :
                    x.Durum==EtkiAlani.Modeller.Siparis.SiparisDurumu.Beklemede ? "Beklemede" : "Taslak",
                Toplam=x.GenelToplam, Miktar=x.Kalemler.Where(k=>!k.Silindi).Sum(k=>k.Miktar),
                SevkEdilen=x.Kalemler.Where(k=>!k.Silindi).Sum(k=>k.SevkEdilenMiktar),
                KalanSevk=x.Kalemler.Where(k=>!k.Silindi).Sum(k=>k.Miktar-k.SevkEdilenMiktar),
                Faturalanan=x.Kalemler.Where(k=>!k.Silindi).Sum(k=>k.FaturalananMiktar),
                KalanFatura=x.Kalemler.Where(k=>!k.Silindi).Sum(k=>k.Miktar-k.FaturalananMiktar),
                Aciklama=x.Aciklama,Referans=x.ReferansNo
            });
        } else if(sekme==CariSekme.Irsaliyeler) {
            q=_baglam.Irsaliyeler.IgnoreQueryFilters().AsNoTracking().Where(x=>x.CariKartId==cariId && x.CariKart!=null && x.CariKart.SirketId==sirketId && !x.CariKart.Silindi)
            .Select(x=>new CariMerkezSatirDto {
                Id=x.Id,BelgeId=x.Id,Kaynak="İrsaliye",BelgeNo=x.IrsaliyeNo,Tarih=x.Tarih,
                Tur=x.Tur==EtkiAlani.Modeller.Irsaliye.IrsaliyeTuru.Satis ? "Satış" : "Alış",
                Durum=x.Silindi || x.Durum==EtkiAlani.Modeller.Irsaliye.IrsaliyeDurumu.Iptal ? "İptal" :
                    x.Durum==EtkiAlani.Modeller.Irsaliye.IrsaliyeDurumu.Faturalandi ? "Faturalandı" :
                    x.Durum==EtkiAlani.Modeller.Irsaliye.IrsaliyeDurumu.Onaylandi ? "Onaylandı" : "Taslak",
                Referans=x.Siparis!=null && x.Siparis.CariKartId==cariId ? x.Siparis.SiparisNo : "",
                Miktar=x.Kalemler.Where(k=>!k.Silindi).Sum(k=>k.Miktar), Toplam=x.GenelToplam,Aciklama=x.Aciklama
            });
        } else if(sekme==CariSekme.TahsilatOdeme) {
            q=FinansSatirlari(cariId,sirketId,sorgu.ParaBirimiId);
            var etkin=q.Where(x=>x.Durum=="Tamamlandı");
            result.Toplam=await etkin.Where(x=>x.Tur=="Tahsilat").SumAsync(x=>x.Toplam,cancellationToken);
            result.Iade=await etkin.Where(x=>x.Tur=="Ödeme").SumAsync(x=>x.Toplam,cancellationToken);
            result.Aciklama="Toplam tahsilat: "+result.Toplam.Value.ToString("N2")+" ₺ • Toplam ödeme: "+result.Iade.Value.ToString("N2")+" ₺";
        } else if(sekme==CariSekme.CekSenet) {
            var cq=_baglam.CekSenetler.IgnoreQueryFilters().AsNoTracking().Where(x=>x.CariKartId==cariId && x.CariKart!=null && x.CariKart.SirketId==sirketId && !x.CariKart.Silindi);
            if(sorgu.ParaBirimiId.HasValue) cq=cq.Where(x=>x.ParaBirimiId==sorgu.ParaBirimiId);
            q=cq.Select(x=>new CariMerkezSatirDto {
                Id=x.Id,BelgeId=x.Id,Kaynak="Çek / Senet",BelgeNo=x.BelgeNo,Referans=x.BelgeNo,Tarih=x.DuzenlemeTarihi,Vade=x.VadeTarihi,Toplam=x.Tutar,Kur=x.Kur,
                ParaBirimi=_baglam.ParaBirimleri.Where(p=>p.Id==x.ParaBirimiId).Select(p=>p.Kod).FirstOrDefault() ?? "TRY",
                Tur=x.Tur==CekSenetTuru.AlinanCek ? "Alınan Çek" : x.Tur==CekSenetTuru.VerilenCek ? "Verilen Çek" : x.Tur==CekSenetTuru.AlinanSenet ? "Alınan Senet" : "Verilen Senet",
                Durum=x.SilindiMi ? "İptal" : x.Durum==CekSenetDurumu.Portfoyde ? "Portföyde" : x.Durum==CekSenetDurumu.CiroEdildi ? "Ciro Edildi" : x.Durum==CekSenetDurumu.TahsileVerildi ? "Tahsile Verildi" : x.Durum==CekSenetDurumu.TeminataVerildi ? "Teminata Verildi" : x.Durum==CekSenetDurumu.TahsilEdildi ? "Tahsil Edildi" : x.Durum==CekSenetDurumu.Odendi ? "Ödendi" : x.Durum==CekSenetDurumu.Karsiliksiz ? "Karşılıksız" : "İade Edildi",
                Aciklama=x.Aciklama,KasaBanka=x.BankaAdi ?? ""
            });
        } else if(sekme==CariSekme.Notlar) {
            q=_baglam.CariNotlari.AsNoTracking().Where(x=>x.CariKartId==cariId && x.SirketId==sirketId)
                .Select(x=>new CariMerkezSatirDto { Id=x.Id,Tarih=x.OlusturmaTarihi,BelgeNo=x.Baslik,Aciklama=x.Metin,Kullanici=x.KullaniciAdi,Kaynak="Not",Durum="Aktif" });
        } else if(sekme==CariSekme.Sistem) {
            _kullanici!.YetkiKontrolEt(x=>x.AuditLogGoruntule,"Denetim izi");
            var key=cariId.ToString();
            q=_baglam.AuditLoglar.AsNoTracking().Where(x=>x.TabloAdi=="CariKart" && x.KayitId==key)
                .Select(x=>new CariMerkezSatirDto { Id=x.Id,Tarih=x.Tarih,Durum="Aktif",Kaynak="Denetim",Kullanici=x.KullaniciAdi ?? "Sistem",Aciklama=x.IslemDetayi ?? "",BelgeNo=x.KayitId,Tur=x.IslemTuru==EtkiAlani.Modeller.Guvenlik.AuditIslemTuru.Ekleme ? "Ekleme" : x.IslemTuru==EtkiAlani.Modeller.Guvenlik.AuditIslemTuru.Guncelleme ? "Güncelleme" : "Silme" });
        } else {
            var e=new CariSayfaDto();
            var son=await Hareketler(cariId,sirketId).Where(x=>!x.Silindi).OrderByDescending(x=>x.Tarih).ThenByDescending(x=>x.Id).Take(10)
                .Select(x=>new CariMerkezSatirDto { Id=x.Id,BelgeId=x.Id,Kaynak="Cari",BelgeNo=x.BelgeNo ?? "",Tarih=x.Tarih,
                    Tur=x.HareketTuru==CariHareketTuru.Tahsilat ? "Tahsilat" : x.HareketTuru==CariHareketTuru.Odeme ? "Ödeme" :
                        x.HareketTuru==CariHareketTuru.SatisFaturasi ? "Satış Faturası" : x.HareketTuru==CariHareketTuru.AlisFaturasi ? "Alış Faturası" : "Cari Hareket",
                    Aciklama=x.Aciklama,Toplam=x.Borc+x.Alacak }).ToListAsync(cancellationToken);
            if(_kullanici!.YetkiliMi(x=>x.SatisGecmisiGoruntule))
                son.AddRange(await _baglam.Siparisler.AsNoTracking().Where(x=>x.CariKartId==cariId && x.CariKart!.SirketId==sirketId)
                    .OrderByDescending(x=>x.SiparisTarihi).ThenByDescending(x=>x.Id).Take(10)
                    .Select(x=>new CariMerkezSatirDto {Id=x.Id,BelgeId=x.Id,Kaynak="Sipariş",BelgeNo=x.SiparisNo,Tarih=x.SiparisTarihi,Tur="Sipariş",Toplam=x.GenelToplam,Aciklama=x.Aciklama}).ToListAsync(cancellationToken));
            if(_kullanici.YetkiliMi(x=>x.IrsaliyeGoruntule))
                son.AddRange(await _baglam.Irsaliyeler.AsNoTracking().Where(x=>x.CariKartId==cariId && x.CariKart!.SirketId==sirketId)
                    .OrderByDescending(x=>x.Tarih).ThenByDescending(x=>x.Id).Take(10)
                    .Select(x=>new CariMerkezSatirDto {Id=x.Id,BelgeId=x.Id,Kaynak="İrsaliye",BelgeNo=x.IrsaliyeNo,Tarih=x.Tarih,Tur="İrsaliye",Toplam=x.GenelToplam,Aciklama=x.Aciklama}).ToListAsync(cancellationToken));
            e.Satirlar=son.OrderByDescending(x=>x.Tarih).ThenBy(x=>x.Kaynak).ThenByDescending(x=>x.Id).Take(10).ToList();
            e.ToplamKayit=e.Satirlar.Count;e.Aciklama="Son 10 işlem; ilgili modüller görüntüleme yetkilerinize göre gösterilir.";
            return e;
        }
        if(!sorgu.IptalleriGoster) q=q.Where(x=>x.Durum!="İptal");
        q=Filtrele(q,sorgu);
        result.ToplamKayit=await q.CountAsync(cancellationToken);
        result.Satirlar=await q.OrderByDescending(x=>x.Tarih).ThenBy(x=>x.Kaynak).ThenByDescending(x=>x.Id)
            .Skip((sorgu.Sayfa-1)*sorgu.SayfaBoyutu).Take(sorgu.SayfaBoyutu).ToListAsync(cancellationToken);
        return result;
    }
    private IQueryable<CariMerkezSatirDto> FinansSatirlari(int id,Guid sirket,int? para=null)
    {
        var kasa=_baglam.KasaHareketleri.IgnoreQueryFilters().AsNoTracking()
            .Where(x=>x.CariKartId==id && x.CariKart!=null && x.CariKart.SirketId==sirket && !x.CariKart.Silindi && (x.FisTuru==KasaFisTuru.Tahsilat || x.FisTuru==KasaFisTuru.Odeme));
        var banka=_baglam.BankaHareketleri.IgnoreQueryFilters().AsNoTracking()
            .Where(x=>x.CariKartId==id && x.CariKart!=null && x.CariKart.SirketId==sirket && !x.CariKart.Silindi && (x.FisTuru==BankaFisTuru.GelenHavale || x.FisTuru==BankaFisTuru.GidenHavale));
        if(para.HasValue) { kasa=kasa.Where(x=>x.ParaBirimiId==para); banka=banka.Where(x=>x.ParaBirimiId==para); }
        var k=kasa.Select(x=>new CariMerkezSatirDto {
            Id=x.Id,BelgeId=x.Id,Kaynak="Kasa",BelgeNo=x.FisNo ?? x.MakbuzNo ?? x.Belge ?? "",Referans=x.Belge ?? "",Tarih=x.Tarih,
            Tur=x.Tur==HareketTuru.Gelir ? "Tahsilat" : "Ödeme",Yontem="Nakit",
            KasaBanka=x.Kasa!=null ? x.Kasa.Ad : "",Toplam=x.Tutar,Kur=x.Kur,ParaBirimi=x.ParaBirimi!=null ? x.ParaBirimi.Kod : "TRY",
            Aciklama=x.Aciklama,TersKayitId=x.TersKayitId,
            Durum=x.Silindi || x.IslemDurumu==FinansIslemDurumu.IptalEdildi ? "İptal" : x.IslemDurumu==FinansIslemDurumu.TersKayit ? "Ters Kayıt" : "Tamamlandı"
        });
        var b=banka.Select(x=>new CariMerkezSatirDto {
            Id=x.Id,BelgeId=x.Id,Kaynak="Banka",BelgeNo=x.FisNo ?? x.DekontNo ?? "",Referans=x.DekontNo ?? "",Tarih=x.Tarih,
            Tur=x.Tur==HareketTuru.Gelir ? "Tahsilat" : "Ödeme",Yontem=x.OdemeYontemi==OdemeYontemi.KrediKarti ? "Kredi Kartı" : x.OdemeYontemi==OdemeYontemi.Havale ? "Havale" : "Banka",
            KasaBanka=x.BankaAdi ?? "",Toplam=x.Tutar,Kur=x.Kur,ParaBirimi=x.ParaBirimi!=null ? x.ParaBirimi.Kod : "TRY",
            Aciklama=x.Aciklama,TersKayitId=x.TersKayitId,
            Durum=x.Silindi || x.IslemDurumu==FinansIslemDurumu.IptalEdildi ? "İptal" : x.IslemDurumu==FinansIslemDurumu.TersKayit ? "Ters Kayıt" : "Tamamlandı"
        });
        return k.Concat(b);
    }
    public async Task NotEkleAsync(int cariId,Guid sirketId,string baslik,string metin,CancellationToken cancellationToken=default)
    {
        await Dogrula(cariId,sirketId,CariSekme.Notlar,cancellationToken);
        _kullanici!.YetkiKontrolEt(x=>x.CariDuzenle,"Cari not ekle");
        if(string.IsNullOrWhiteSpace(baslik) || baslik.Trim().Length>150 || string.IsNullOrWhiteSpace(metin) || metin.Trim().Length>4000)
            throw new ArgumentException("Başlık 1–150, not 1–4000 karakter olmalıdır.");
        _baglam.CariNotlari.Add(new CariNotu { SirketId=sirketId,CariKartId=cariId,KullaniciId=_kullanici.AktifKullanici!.Id,KullaniciAdi=_kullanici.AktifKullanici.KullaniciAdi,Baslik=baslik.Trim(),Metin=metin.Trim() });
        await _baglam.SaveChangesAsync(cancellationToken);
    }
    public async Task<CariRiskDto> GetRiskAsync(int cariId,Guid sirketId,CancellationToken cancellationToken=default)
    {
        await Dogrula(cariId,sirketId,CariSekme.RiskVade,cancellationToken);
        var q=FaturaSatirlari(cariId,sirketId).Where(x=>x.Durum=="Onaylandı" && x.Kalan>0);
        var bugun=UtcGun(DateTime.Today); var d30=bugun.AddDays(-30);var d60=bugun.AddDays(-60);var d90=bugun.AddDays(-90);
        var r=await q.GroupBy(x=>1).Select(g=>new CariRiskDto {
            VadesiGelmemis=g.Where(x=>x.Tur=="Satış" && x.Vade>=bugun).Sum(x=>x.Kalan ?? 0),
            Gun1_30=g.Where(x=>x.Tur=="Satış" && x.Vade<bugun && x.Vade>=d30).Sum(x=>x.Kalan ?? 0),
            Gun31_60=g.Where(x=>x.Tur=="Satış" && x.Vade<d30 && x.Vade>=d60).Sum(x=>x.Kalan ?? 0),
            Gun61_90=g.Where(x=>x.Tur=="Satış" && x.Vade<d60 && x.Vade>=d90).Sum(x=>x.Kalan ?? 0),
            Gun90Ustu=g.Where(x=>x.Tur=="Satış" && x.Vade<d90).Sum(x=>x.Kalan ?? 0),
            Vadesiz=g.Where(x=>x.Tur=="Satış" && x.Vade==null).Sum(x=>x.Kalan ?? 0),
            VadesiGecmisBorc=g.Where(x=>x.Tur=="Alış" && x.Vade<bugun).Sum(x=>x.Kalan ?? 0),
            EnEskiAcikBelge=g.Min(x=>(DateTime?)x.Tarih)
        }).SingleOrDefaultAsync(cancellationToken) ?? new CariRiskDto();
        var kapama=_baglam.BelgeKapamalar.AsNoTracking().Where(x=>!x.Silindi && x.FaturaCariHareket!=null && !x.FaturaCariHareket.Silindi &&
            x.OdenenCariHareket!=null && !x.OdenenCariHareket.Silindi &&
            x.FaturaCariHareket.CariKartId==cariId && x.OdenenCariHareket.CariKartId==cariId &&
            x.FaturaCariHareket.CariKart!.SirketId==sirketId && x.KapananTutar>0);
        var performans=await kapama.GroupBy(x=>x.FaturaCariHareket!.HareketTuru)
            .Select(g=>new { Tur=g.Key,Tutar=g.Sum(x=>x.KapananTutar),Gun=g.Sum(x=>(double)x.KapananTutar*(x.OdenenCariHareket!.Tarih-x.FaturaCariHareket!.Tarih).TotalDays) }).ToListAsync(cancellationToken);
        foreach(var p in performans) {
            var gun=(decimal)p.Gun/p.Tutar;
            if(p.Tur==CariHareketTuru.SatisFaturasi) r.OrtalamaTahsilatGunu=gun;
            if(p.Tur==CariHareketTuru.AlisFaturasi) r.OrtalamaOdemeGunu=gun;
        }
        return r;
    }
}
