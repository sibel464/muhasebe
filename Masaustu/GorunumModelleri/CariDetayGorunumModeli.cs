using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using EtkiAlani.Modeller.Kullanici;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;
using Uygulama.Dto.Cari360;
namespace Masaustu.GorunumModelleri;

public sealed record CariSekmeSecimi(CariSekme Tur,string Ad);
public sealed class CariDetayGorunumModeli : TemelGorunumModeli, IDisposable
{
    private readonly ICariDetayServisi _servis;
    private readonly IAktifFirmaBaglami _firma;
    private readonly IKullaniciServisi _kullanici;
    private readonly SemaphoreSlim _kilit=new(1,1);
    private CancellationTokenSource? _istek;
    private int _surum,_id,_sayfa=1,_boyut=50;
    private bool _kapandi,_yukleniyor,_iptal,_urun;
    private CariSekmeSecimi? _sekme;
    private CariDetayDto? _genel;
    private CariFinansOzetDto? _ozet;
    private CariRiskDto? _risk;
    private string _mesaj="",_bilgi="";
    public ObservableCollection<CariSekmeSecimi> Sekmeler { get; }=new();
    public ObservableCollection<CariMerkezSatirDto> Satirlar { get; }=new();
    public ObservableCollection<CariUrunAnalizDto> Urunler { get; }=new();
    public CariDetayDto? GenelBilgi { get=>_genel; private set=>OzellikAyarla(ref _genel,value); }
    public CariFinansOzetDto? FinansOzet { get=>_ozet; private set=>OzellikAyarla(ref _ozet,value); }
    public CariRiskDto? Risk { get=>_risk; private set=>OzellikAyarla(ref _risk,value); }
    public bool Yukleniyor { get=>_yukleniyor; private set { OzellikAyarla(ref _yukleniyor,value); BildirimGonder(nameof(Bos)); CommandManager.InvalidateRequerySuggested(); } }
    public string DurumMesaji { get=>_mesaj; private set=>OzellikAyarla(ref _mesaj,value); }
    public string Bilgi { get=>_bilgi; private set=>OzellikAyarla(ref _bilgi,value); }
    public bool Bos => !Yukleniyor && (UrunGecmisi ? Urunler.Count==0 : Satirlar.Count==0);
    public string BosMesaji => SeciliSekme?.Tur switch {
        CariSekme.Satislar=>"Bu cariye ait satış faturası bulunmuyor.",
        CariSekme.Alislar=>"Bu cariye ait alış faturası bulunmuyor.",
        CariSekme.CekSenet=>"Bu cariye ait çek/senet bulunmuyor.",
        CariSekme.RiskVade=>"Bu cariye ait açık vade bulunmuyor.",
        CariSekme.Notlar=>"Bu cariye henüz not eklenmemiş.",
        _=>"Seçilen filtrelere uygun kayıt bulunmuyor."
    };
    public CariSekmeSecimi? SeciliSekme { get=>_sekme; set {
        if(OzellikAyarla(ref _sekme,value)) {
            _sayfa=1;Tur=null;Durum=null;Yon=null;_urun=false;
            foreach(var p in new[]{nameof(EkstreAcik),nameof(Yon),nameof(GenelAcik),nameof(RiskAcik),nameof(NotlarAcik),nameof(SistemAcik),nameof(UrunSecimiAcik),nameof(UrunGecmisi),nameof(BosMesaji),nameof(Tur),nameof(Durum),nameof(TurSecenekleri)}) BildirimGonder(p);
            _=YukleAsync(false);
        }
    } }
    public bool GenelAcik=>SeciliSekme?.Tur==CariSekme.Genel;
    public bool RiskAcik=>SeciliSekme?.Tur==CariSekme.RiskVade;
    public bool NotlarAcik=>SeciliSekme?.Tur==CariSekme.Notlar;
    public bool SistemAcik=>SeciliSekme?.Tur==CariSekme.Sistem;
    public bool UrunSecimiAcik=>SeciliSekme?.Tur is CariSekme.Satislar or CariSekme.Alislar;
    public bool UrunGecmisi { get=>_urun; set { if(OzellikAyarla(ref _urun,value)) { _sayfa=1;_=YukleAsync(false); } } }
    public DateTime? Baslangic { get; set; }
    public DateTime? Bitis { get; set; }
    public string? BelgeNo { get; set; }
    public string? Tur { get; set; }
    public string? Durum { get; set; }
    public string? Yon { get; set; }
    public ObservableCollection<CariParaSecimi> ParaBirimleri { get; } = new() { new(null,"Tümü") };
    public bool EkstreAcik => SeciliSekme?.Tur is CariSekme.Ekstre or CariSekme.CariFisleri;
    public int? ParaBirimiId { get; set; }
    public bool IptalleriGoster { get=>_iptal; set { if(OzellikAyarla(ref _iptal,value)) { _sayfa=1;_=YukleAsync(false); } } }
    public int SayfaBoyutu { get=>_boyut; set { if(OzellikAyarla(ref _boyut,value)) { _sayfa=1;_=YukleAsync(false); } } }
    public int[] Boyutlar { get; }=[50,100];
    public int ToplamKayit { get; private set; }
    public string SayfaMetni=>$"Sayfa {_sayfa} • {ToplamKayit} kayıt";
    public string NotBasligi { get; set; }="";
    public string NotMetni { get; set; }="";
    public IReadOnlyList<CariSecenek> Yonler { get; }=[new(null,"Tümü"),new("Borç","Borç"),new("Alacak","Alacak")];
    public IReadOnlyList<CariSecenek> Durumlar { get; }=[new(null,"Tümü"),new("Aktif","Aktif"),new("Taslak","Taslak"),new("Beklemede","Beklemede"),new("Onaylandı","Onaylandı"),new("Tamamlandı","Tamamlandı"),new("İptal","İptal"),new("Ters Kayıt","Ters Kayıt"),new("Faturalandı","Faturalandı")];
    public IReadOnlyList<CariSecenek> TurSecenekleri => (SeciliSekme?.Tur switch {
        CariSekme.Ekstre=>new[]{"Satış Faturası","Alış Faturası","Tahsilat","Ödeme","Satış İadesi","Alış İadesi","Açılış / Devir","Diğer Cari Fiş"},
        CariSekme.TahsilatOdeme=>new[]{"Tahsilat","Ödeme"},
        CariSekme.CekSenet=>new[]{"Alınan Çek","Verilen Çek","Alınan Senet","Verilen Senet"},
        CariSekme.CariFisleri=>new[]{"Açılış / Devir","Diğer Cari Fiş"},
        _=>new[]{"Satış","Alış","Satış İadesi","Alış İadesi"}
    }).Select(x=>new CariSecenek(x,x)).Prepend(new CariSecenek(null,"Tümü")).ToList();
    public bool Duzenleyebilir=>_kullanici.YetkiliMi(x=>x.CariDuzenle);
    public bool FaturaOlusturabilir=>_kullanici.YetkiliMi(x=>x.FaturaOlustur);
    public bool SatisYapabilir=>_kullanici.YetkiliMi(x=>x.SatisYap);
    public bool TahsilatYapabilir=>_kullanici.YetkiliMi(x=>x.TahsilatYap);
    public bool OdemeYapabilir=>_kullanici.YetkiliMi(x=>x.OdemeYap);
    public bool SiparisOlusturabilir=>_kullanici.YetkiliMi(x=>x.SatisYap);
    public bool FaturaGorebilir=>_kullanici.YetkiliMi(x=>x.FaturaGoruntule);
    public ICommand YenileKomutu { get; }
    public ICommand FiltreleKomutu { get; }
    public ICommand TemizleKomutu { get; }
    public ICommand OncekiKomutu { get; }
    public ICommand SonrakiKomutu { get; }
    public ICommand NotEkleKomutu { get; }
    public ICommand DuzenleKomutu { get; }
    public ICommand YeniFaturaKomutu { get; }
    public ICommand YeniIadeKomutu { get; }
    public ICommand YeniTahsilatKomutu { get; }
    public ICommand YeniOdemeKomutu { get; }
    public ICommand YeniSatisKomutu { get; }
    public ICommand YeniSiparisKomutu { get; }
    public ICommand CariFisKomutu { get; }
    public ICommand KapatKomutu { get; }
    public ICommand BelgeAcKomutu { get; }
    public Action? Kapat { get; set; }
    public Func<string,int,Task>? HizliIslem { get; set; }
    public Func<CariBelgeDetayDto,Task>? BelgeGoster { get; set; }
    public CariDetayGorunumModeli(ICariDetayServisi servis,IAktifFirmaBaglami firma,IKullaniciServisi kullanici)
    {
        _servis=servis;_firma=firma;_kullanici=kullanici;
        string[] adlar=["Genel","Cari Ekstre","Satışlar","Alışlar","Siparişler","İrsaliyeler","Faturalar","Tahsilat / Ödeme","İadeler","Cari Hesap Fişleri","Çek / Senet","Risk & Vade","Notlar","Sistem Bilgisi"];
        foreach(var tur in Enum.GetValues<CariSekme>()) {
            bool izin=tur switch {
                CariSekme.Satislar or CariSekme.Alislar or CariSekme.Faturalar or CariSekme.Iadeler or CariSekme.RiskVade=>FaturaGorebilir,
                CariSekme.Siparisler=>kullanici.YetkiliMi(x=>x.SatisGecmisiGoruntule),
                CariSekme.Irsaliyeler=>kullanici.YetkiliMi(x=>x.IrsaliyeGoruntule),
                CariSekme.TahsilatOdeme=>kullanici.YetkiliMi(x=>x.FinansRaporuGoruntule || x.TahsilatYap || x.OdemeYap),
                CariSekme.CekSenet=>kullanici.YetkiliMi(x=>x.CekSenetGoruntule),_=>true };
            if(izin) Sekmeler.Add(new(tur,adlar[(int)tur]));
        }
        _sekme=Sekmeler.FirstOrDefault();
        _firma.Degisti+=FirmaDegisti;
        YenileKomutu=new KomutSarici(async _=>await YukleAsync());
        FiltreleKomutu=new KomutSarici(async _=>{_sayfa=1;await YukleAsync(false);});
        TemizleKomutu=new KomutSarici(async _=>{Baslangic=null;Bitis=null;BelgeNo=null;Tur=null;Durum=null;Yon=null;ParaBirimiId=null;_iptal=false;_sayfa=1;
            foreach(var p in new[]{nameof(Baslangic),nameof(Bitis),nameof(BelgeNo),nameof(Tur),nameof(Durum),nameof(Yon),nameof(ParaBirimiId),nameof(IptalleriGoster)}) BildirimGonder(p);
            await YukleAsync(false);});
        OncekiKomutu=new KomutSarici(async _=>{_sayfa--;await YukleAsync(false);},_=>!Yukleniyor && _sayfa>1);
        SonrakiKomutu=new KomutSarici(async _=>{_sayfa++;await YukleAsync(false);},_=>!Yukleniyor && (UrunGecmisi ? Urunler.Count==_boyut : _sayfa*_boyut<ToplamKayit));
        NotEkleKomutu=new KomutSarici(async _=>await NotKaydet(),_=>Duzenleyebilir && !Yukleniyor && GenelBilgi!=null);
        DuzenleKomutu=Hizli("Düzenle",()=>Duzenleyebilir);
        YeniFaturaKomutu=Hizli("Fatura",()=>FaturaOlusturabilir);YeniIadeKomutu=Hizli("İade",()=>FaturaOlusturabilir);
        YeniTahsilatKomutu=Hizli("Tahsilat",()=>TahsilatYapabilir);YeniOdemeKomutu=Hizli("Ödeme",()=>OdemeYapabilir);
        YeniSatisKomutu=Hizli("Satış",()=>SatisYapabilir);YeniSiparisKomutu=Hizli("Sipariş",()=>SiparisOlusturabilir);
        CariFisKomutu=Hizli("Cari Fiş",()=>Duzenleyebilir);
        KapatKomutu=new KomutSarici(()=>Kapat?.Invoke());
        BelgeAcKomutu=new KomutSarici(async p=>{if(p is CariMerkezSatirDto r) await BelgeAc(r);});
    }
    private ICommand Hizli(string tur,Func<bool> izin)=>new KomutSarici(async _=>{
        try { if(!izin() || GenelBilgi==null) return; await _servis.GetCariGenelBilgiAsync(_id,_firma.SirketId);
            if(HizliIslem!=null) await HizliIslem(tur,_id);await YukleAsync();
        } catch(Exception ex) { await Hata(ex); }
    },_=>izin() && !Yukleniyor && GenelBilgi?.Durum=="Aktif");
    public void Init(int id) { _id=id;_=YukleAsync(); }
    private void FirmaDegisti(object? sender,EventArgs e) {
        Application.Current.Dispatcher.Invoke(()=>{_istek?.Cancel();_surum++;GenelBilgi=null;FinansOzet=null;Risk=null;Satirlar.Clear();Urunler.Clear();DurumMesaji="Firma değişti. Cari hesap merkezini yeniden açın.";Kapat?.Invoke();});
    }
    private CariSorgu Sorgu()=>new() { Baslangic=Baslangic,Bitis=Bitis,BelgeNo=BelgeNo,Tur=Tur,Durum=Durum,Yon=Yon,ParaBirimiId=ParaBirimiId,IptalleriGoster=IptalleriGoster,Sayfa=_sayfa,SayfaBoyutu=_boyut };
    public async Task YukleAsync(bool ozet=true)
    {
        if(_id==0 || _kapandi || SeciliSekme==null)return;
        _istek?.Cancel();
        var cts=new CancellationTokenSource();_istek=cts;
        var version=++_surum;var id=_id;var firma=_firma.SirketId;var sekme=SeciliSekme.Tur;var sorgu=Sorgu();var urun=UrunGecmisi;
        bool Gecerli()=>!cts.IsCancellationRequested && version==_surum && id==_id && firma==_firma.SirketId && !_kapandi;
        Yukleniyor=true;DurumMesaji=SeciliSekme.Ad+" yükleniyor...";
        Satirlar.Clear();Urunler.Clear();Bilgi="";
        bool alindi=false;
        try {
            await _kilit.WaitAsync(cts.Token);alindi=true;
            if(ozet || GenelBilgi==null) {
                var genel=await _servis.GetCariGenelBilgiAsync(id,firma,cts.Token);
                var finans=await _servis.GetCariFinansOzetAsync(id,firma,cts.Token);
                if(!Gecerli())return;GenelBilgi=genel;FinansOzet=finans;
                if(ParaBirimleri.Count==1) { var currencies=await _servis.GetParaBirimleriAsync(id,firma,cts.Token);if(!Gecerli())return;foreach(var p in currencies)ParaBirimleri.Add(p); }
            }
            if(sekme==CariSekme.RiskVade) {
                var risk=await _servis.GetRiskAsync(id,firma,cts.Token);if(!Gecerli())return;Risk=risk;
            }
            if(sekme==CariSekme.Sistem && !_kullanici.YetkiliMi(x=>x.AuditLogGoruntule)) { DurumMesaji="Sistem bilgileri görüntüleniyor."; return; }
            if(urun) {
                var list=await _servis.GetUrunSayfasiAsync(id,firma,sekme==CariSekme.Alislar,sorgu,cts.Token);
                if(!Gecerli())return;foreach(var r in list)Urunler.Add(r);
                ToplamKayit=(_sayfa-1)*_boyut+list.Count;Bilgi="Ürün fiyatları para birimine göre ayrı gruplanır. Ortalama, miktar ağırlıklı birim fiyattır.";
            } else {
                var page=await _servis.GetSayfaAsync(id,firma,sekme,sorgu,cts.Token);
                if(!Gecerli())return;foreach(var r in page.Satirlar)Satirlar.Add(r);ToplamKayit=page.ToplamKayit;Bilgi=page.Aciklama;
                if(sekme==CariSekme.Ekstre) Bilgi=$"Devir bakiyesi: {Math.Abs(page.DevirBakiyesi):N2} ₺ {(page.DevirBakiyesi>=0 ? "Borç" : "Alacak")} • "+Bilgi;
                if(sekme is CariSekme.Satislar or CariSekme.Alislar) Bilgi=$"Toplam: {page.Toplam:N2} ₺ • Bu ay: {page.BuAy:N2} ₺ • İade: {page.Iade:N2} ₺ • Net: {page.Toplam-page.Iade:N2} ₺ • Son: {page.SonTarih:dd.MM.yyyy}";
            }
            DurumMesaji="Kayıtlar yüklendi.";
        } catch(OperationCanceledException) { }
        catch(Exception ex) {if(Gecerli())await Hata(ex);}
        finally { if(alindi)_kilit.Release();if(Gecerli()){Yukleniyor=false;BildirimGonder(nameof(SayfaMetni));BildirimGonder(nameof(Bos));} if(ReferenceEquals(_istek,cts))_istek=null;cts.Dispose(); }
    }
    private async Task NotKaydet() {
        try { await _kilit.WaitAsync();try {await _servis.NotEkleAsync(_id,_firma.SirketId,NotBasligi,NotMetni);}finally{_kilit.Release();}
            NotBasligi="";NotMetni="";BildirimGonder(nameof(NotBasligi));BildirimGonder(nameof(NotMetni));await YukleAsync(false);
        }catch(Exception ex){await Hata(ex);}
    }
    private async Task BelgeAc(CariMerkezSatirDto row) {
        try { CariBelgeDetayDto dto;var firma=_firma.SirketId;
            await _kilit.WaitAsync();try {dto=await _servis.GetBelgeAsync(_id,firma,row.Kaynak,row.Kaynak=="Cari" ? row.Id : row.BelgeId ?? row.Id);}finally{_kilit.Release();}
            if(firma==_firma.SirketId && !_kapandi && BelgeGoster!=null)await BelgeGoster(dto);
        }catch(Exception ex){await Hata(ex);}
    }
    private async Task Hata(Exception ex) {
        await App.HatayiLoglaAsync(ex,"Cari360");
        DurumMesaji=ex is UnauthorizedAccessException ? "Bu işlem için yetkiniz bulunmuyor." :
            ex is ArgumentException ? ex.Message :
            ex.Message=="Seçilen cari aktif firmaya ait değil." ? ex.Message : "Cari bilgileri yüklenemedi. Lütfen tekrar deneyin.";
    }
    public void Dispose() { _kapandi=true;_istek?.Cancel();_firma.Degisti-=FirmaDegisti;_=KaynagiKapat(); }
    private async Task KaynagiKapat() { await _kilit.WaitAsync();try { (_servis as IDisposable)?.Dispose(); }finally{_kilit.Release();} }
}
