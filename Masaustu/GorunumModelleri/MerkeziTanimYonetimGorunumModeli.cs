using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Tanimlar;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.GorunumModelleri;

public class MerkeziTanimYonetimGorunumModeli : TemelGorunumModeli
{
    private readonly IMerkeziTanimServisi _servis;
    private MerkeziTanim? _secili;
    private string _kod = "", _ad = "", _kisaAd = "", _aciklama = "", _hata = "";
    private bool _aktifMi = true;
    public string Grup { get; }
    public string Baslik { get; }
    public ObservableCollection<MerkeziTanim> Tanimlar { get; } = new();
    public MerkeziTanim? Secili { get => _secili; set { if (OzellikAyarla(ref _secili, value) && value != null) { Kod=value.Kod; Ad=value.Ad; KisaAd=value.KisaAd??""; Aciklama=value.Aciklama??""; AktifMi=value.AktifMi; } } }
    public string Kod { get=>_kod; set=>OzellikAyarla(ref _kod,value); }
    public string Ad { get=>_ad; set=>OzellikAyarla(ref _ad,value); }
    public string KisaAd { get=>_kisaAd; set=>OzellikAyarla(ref _kisaAd,value); }
    public string Aciklama { get=>_aciklama; set=>OzellikAyarla(ref _aciklama,value); }
    public bool AktifMi { get=>_aktifMi; set=>OzellikAyarla(ref _aktifMi,value); }
    public string Hata { get=>_hata; set=>OzellikAyarla(ref _hata,value); }
    public int? KaydedilenId { get; private set; }
    public ICommand YeniKomutu { get; }
    public ICommand KaydetKomutu { get; }
    public ICommand AktiflikKomutu { get; }

    public MerkeziTanimYonetimGorunumModeli(IMerkeziTanimServisi servis, string grup, string baslik)
    {
        _servis=servis; Grup=grup; Baslik=baslik;
        YeniKomutu=new KomutSarici(Yeni);
        KaydetKomutu=new KomutSarici(async _=>await KaydetAsync());
        AktiflikKomutu=new KomutSarici(async _=>await AktiflikAsync(), _=>Secili!=null);
        _=YukleAsync();
    }
    private async Task YukleAsync(int? id=null) { var l=await _servis.ListeleAsync(Grup,false); Tanimlar.Clear(); foreach(var x in l)Tanimlar.Add(x); if(id.HasValue)Secili=Tanimlar.FirstOrDefault(x=>x.Id==id); }
    private void Yeni(){Secili=null; Kod=Ad=KisaAd=Aciklama=Hata=""; AktifMi=true;}
    private async Task KaydetAsync(){try{Hata="";var x=await _servis.KaydetAsync(new MerkeziTanim{Id=Secili?.Id??0,Grup=Grup,Kod=Kod,Ad=Ad,KisaAd=KisaAd,Aciklama=Aciklama,AktifMi=AktifMi,SiraNo=Secili?.SiraNo??0});KaydedilenId=x.Id;await YukleAsync(x.Id);}catch(Exception ex){Hata=ex.Message;}}
    private async Task AktiflikAsync(){if(Secili==null)return;await _servis.AktiflikDegistirAsync(Secili.Id,!Secili.AktifMi);await YukleAsync(Secili.Id);}
}
