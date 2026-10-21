using System.Windows;
using System.Windows.Controls;
using EtkiAlani.Modeller.Kurumsal;
using Masaustu.GorunumModelleri;

namespace Masaustu.Gorunumler.Sayfa;
public partial class FirmaIslemleriSayfasi : UserControl
{
 public FirmaIslemleriSayfasi(){InitializeComponent();Loaded+=(_,_)=>{if(DataContext is FirmaIslemleriGorunumModeli vm){vm.FirmaFormuAc=FormAcAsync;vm.SubeFormuAc=SubeFormAcAsync;vm.DonemFormuAc=DonemFormAcAsync;vm.SeriFormuAc=SeriFormAcAsync;}};}
 private async void Duzenle_Click(object s,RoutedEventArgs e){if(DataContext is FirmaIslemleriGorunumModeli vm&&s is Button{DataContext:Sirket x})await vm.FormAcAsync(x);}
 private Task<Sirket?> FormAcAsync(Sirket? kaynak)
 {
  var x=kaynak==null?new Sirket():new Sirket{Id=kaynak.Id,SirketKodu=kaynak.SirketKodu,Unvan=kaynak.Unvan,VergiNo=kaynak.VergiNo,VergiDairesi=kaynak.VergiDairesi,Telefon=kaynak.Telefon,Email=kaynak.Email,Adres=kaynak.Adres,Il=kaynak.Il,Ilce=kaynak.Ilce,PostaKodu=kaynak.PostaKodu,AktifMi=kaynak.AktifMi,OlusturmaTarihi=kaynak.OlusturmaTarihi};
  var il=new TextBox{Text=x.Il};var ilce=new TextBox{Text=x.Ilce};var postaKodu=new TextBox{Text=x.PostaKodu,MaxLength=5};
  var kod=new TextBox{Text=x.SirketKodu};var unvan=new TextBox{Text=x.Unvan};var vergi=new TextBox{Text=x.VergiNo};var daire=new TextBox{Text=x.VergiDairesi};var tel=new TextBox{Text=x.Telefon};var email=new TextBox{Text=x.Email};var adres=new TextBox{Text=x.Adres,AcceptsReturn=true,Height=65};
  var panel=new StackPanel{Margin=new Thickness(22)};void A(string a,Control c){panel.Children.Add(new TextBlock{Text=a,Margin=new Thickness(0,7,0,3),FontWeight=FontWeights.SemiBold});panel.Children.Add(c);}A("Firma Kodu (boş bırakırsanız otomatik)",kod);A("Firma Ünvanı *",unvan);A("VKN / TCKN",vergi);A("Vergi Dairesi",daire);A("Telefon",tel);A("E-Posta",email);A("Açık Adres",adres);A("İl (e-Fatura için zorunlu)",il);A("İlçe (e-Fatura için zorunlu)",ilce);A("Posta Kodu",postaKodu);var b=new Button{Content="Firmayı Kaydet",IsDefault=true,Height=38,Margin=new Thickness(0,15,0,0)};panel.Children.Add(b);var w=new Window{Title=kaynak==null?"Yeni Firma":"Firma Düzenle",Width=560,Height=780,Content=new ScrollViewer{Content=panel},Owner=Window.GetWindow(this),WindowStartupLocation=WindowStartupLocation.CenterOwner};b.Click+=(_,_)=>{x.SirketKodu=kod.Text;x.Unvan=unvan.Text;x.VergiNo=vergi.Text;x.VergiDairesi=daire.Text;x.Telefon=tel.Text;x.Email=email.Text;x.Adres=adres.Text;x.Il=il.Text;x.Ilce=ilce.Text;x.PostaKodu=postaKodu.Text;w.DialogResult=true;};return Task.FromResult(w.ShowDialog()==true?x:null);
 }

 private Task<Sube?> SubeFormAcAsync(Guid firmaId, Sube? kaynak)
 {
  var x=kaynak==null?new Sube{SirketId=firmaId}:new Sube{Id=kaynak.Id,SirketId=firmaId,SubeKodu=kaynak.SubeKodu,SubeAdi=kaynak.SubeAdi,Telefon=kaynak.Telefon,Adres=kaynak.Adres,AktifMi=kaynak.AktifMi,OlusturmaTarihi=kaynak.OlusturmaTarihi};
  var kod=new TextBox{Text=x.SubeKodu};var ad=new TextBox{Text=x.SubeAdi};var tel=new TextBox{Text=x.Telefon};var adres=new TextBox{Text=x.Adres,AcceptsReturn=true,Height=70};
  var w=FormPenceresi(kaynak==null?"Yeni Şube":"Şube Düzenle",("Şube Kodu *",kod),("Şube Adı *",ad),("Telefon",tel),("Adres",adres));
  if(w.ShowDialog()!=true)return Task.FromResult<Sube?>(null);x.SubeKodu=kod.Text;x.SubeAdi=ad.Text;x.Telefon=tel.Text;x.Adres=adres.Text;return Task.FromResult<Sube?>(x);
 }

 private Task<MaliDonem?> DonemFormAcAsync(Guid firmaId, MaliDonem? kaynak)
 {
  var x=kaynak==null?new MaliDonem{SirketId=firmaId,Baslangic=new DateTime(DateTime.Today.Year,1,1),Bitis=new DateTime(DateTime.Today.Year,12,31)}:new MaliDonem{Id=kaynak.Id,SirketId=firmaId,DonemAdi=kaynak.DonemAdi,Baslangic=kaynak.Baslangic,Bitis=kaynak.Bitis,KilitliMi=kaynak.KilitliMi,OlusturmaTarihi=kaynak.OlusturmaTarihi};
  var ad=new TextBox{Text=x.DonemAdi};var bas=new DatePicker{SelectedDate=x.Baslangic};var bit=new DatePicker{SelectedDate=x.Bitis};
  var w=FormPenceresi(kaynak==null?"Yeni Mali Dönem":"Mali Dönem Düzenle",("Dönem Adı *",ad),("Başlangıç *",bas),("Bitiş *",bit));
  if(w.ShowDialog()!=true)return Task.FromResult<MaliDonem?>(null);x.DonemAdi=ad.Text;x.Baslangic=bas.SelectedDate??x.Baslangic;x.Bitis=bit.SelectedDate??x.Bitis;return Task.FromResult<MaliDonem?>(x);
 }

 private Task<BelgeSerisi?> SeriFormAcAsync(Guid firmaId, BelgeSerisi? kaynak)
 {
  var x=kaynak==null?new BelgeSerisi{SirketId=firmaId}:new BelgeSerisi{Id=kaynak.Id,SirketId=firmaId,BelgeTipi=kaynak.BelgeTipi,Seri=kaynak.Seri,Prefix=kaynak.Prefix,Format=kaynak.Format,SonNumara=kaynak.SonNumara,OlusturmaTarihi=kaynak.OlusturmaTarihi};
  var tip=new ComboBox{ItemsSource=new[]{"Fatura","Siparis","Irsaliye","Tahsilat","Odeme"},IsEditable=true,Text=x.BelgeTipi};var seri=new TextBox{Text=x.Seri};var prefix=new TextBox{Text=x.Prefix};var format=new TextBox{Text=x.Format,ToolTip="Örnek: FTR-{0:000000}"};var son=new TextBox{Text=x.SonNumara.ToString()};
  var w=FormPenceresi(kaynak==null?"Yeni Belge Serisi":"Belge Serisi Düzenle",("Belge Tipi *",tip),("Seri *",seri),("Ön Ek",prefix),("Format (örn. FTR-{0:000000})",format),("Son Numara",son));
  if(w.ShowDialog()!=true)return Task.FromResult<BelgeSerisi?>(null);x.BelgeTipi=tip.Text;x.Seri=seri.Text;x.Prefix=prefix.Text;x.Format=format.Text;if(long.TryParse(son.Text,out var n))x.SonNumara=n;return Task.FromResult<BelgeSerisi?>(x);
 }

 private Window FormPenceresi(string baslik,params (string Etiket,Control Alan)[] alanlar)
 {
  var panel=new StackPanel{Margin=new Thickness(24)};foreach(var (etiket,alan) in alanlar){panel.Children.Add(new TextBlock{Text=etiket,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,9,0,4)});alan.MinHeight=34;panel.Children.Add(alan);}var kaydet=new Button{Content="Kaydet",Height=40,Margin=new Thickness(0,18,0,0),IsDefault=true};panel.Children.Add(kaydet);var w=new Window{Title=baslik,Width=520,SizeToContent=SizeToContent.Height,MaxHeight=720,Content=new ScrollViewer{Content=panel},Owner=Window.GetWindow(this),WindowStartupLocation=WindowStartupLocation.CenterOwner};kaydet.Click+=(_,_)=>w.DialogResult=true;return w;
 }
}
