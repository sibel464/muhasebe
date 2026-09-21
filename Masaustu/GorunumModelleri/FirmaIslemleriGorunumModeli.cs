using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Kurumsal;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.GorunumModelleri;

internal sealed class EskiFirmaIslemleriGorunumModeli : TemelGorunumModeli
{
    private readonly ISirketServisi _servis; private string _arama="", _mesaj=""; private Sirket? _secili; private bool _kaydediliyor;
    public ObservableCollection<Sirket> Firmalar { get; } = new();
    public string Arama { get=>_arama; set=>OzellikAyarla(ref _arama,value); }
    public string Mesaj { get=>_mesaj; set=>OzellikAyarla(ref _mesaj,value); }
    public Sirket? SeciliFirma { get=>_secili; set=>OzellikAyarla(ref _secili,value); }
    public int ToplamFirma => Firmalar.Count; public int AktifFirma => Firmalar.Count(x=>x.AktifMi);
    public ICommand YenileKomutu { get; } public ICommand YeniKomutu { get; } public ICommand DurumKomutu { get; }
    public Func<Sirket?,Task<Sirket?>>? FirmaFormuAc { get; set; }
    public EskiFirmaIslemleriGorunumModeli(ISirketServisi servis) { _servis=servis; YenileKomutu=new KomutSarici(async()=>await YukleAsync()); YeniKomutu=new KomutSarici(async()=>await FormAcAsync(null)); DurumKomutu=new KomutSarici(async p=>await DurumAsync((Sirket)p!)); _=YukleAsync(); }
    public async Task YukleAsync(){var l=await _servis.ListeleAsync(Arama);Firmalar.Clear();foreach(var x in l)Firmalar.Add(x);BildirimGonder(nameof(ToplamFirma));BildirimGonder(nameof(AktifFirma));}
    public async Task FormAcAsync(Sirket? x){if(FirmaFormuAc==null||_kaydediliyor)return;var sonuc=await FirmaFormuAc(x);if(sonuc==null)return;_kaydediliyor=true;try{var yeni=await _servis.KaydetAsync(sonuc,string.IsNullOrWhiteSpace(sonuc.SirketKodu));Mesaj=$"✓ Firma başarıyla kaydedildi. Firma Kodu: {yeni.SirketKodu}";await YukleAsync();}catch(Exception e){Mesaj=e.Message;}finally{_kaydediliyor=false;}}
    private async Task DurumAsync(Sirket x){await _servis.DurumDegistirAsync(x.Id,!x.AktifMi);Mesaj=x.AktifMi?"Firma pasife alındı.":"Firma tekrar aktif edildi.";await YukleAsync();}
}
