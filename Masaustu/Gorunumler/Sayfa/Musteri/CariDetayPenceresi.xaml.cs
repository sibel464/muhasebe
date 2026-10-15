using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Masaustu.GorunumModelleri;
using Masaustu.Servisler;
using Uygulama.Arayuzler;
using Uygulama.Dto.Cari360;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
namespace Masaustu.Gorunumler.Sayfa.Musteri;

public partial class CariDetayPenceresi : Window
{
    private readonly CariDetayGorunumModeli _model;
    private readonly Guid _firma;
    public CariDetayPenceresi(int cariId)
    {
        InitializeComponent();
        _firma=Get<IAktifFirmaBaglami>().SirketId;
        _model=Get<CariDetayGorunumModeli>();
        DataContext=_model;
        _model.Kapat=Close;
        _model.HizliIslem=HizliIslemAc;
        _model.BelgeGoster=BelgeGoster;
        _model.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(_model.SeciliSekme)) KolonlariKur();};
        Closed+=(_,_)=>_model.Dispose();
        Loaded+=(_,_)=>_model.Init(cariId);
        KolonlariKur();
    }
    private static T Get<T>() where T:notnull=>App.HizmetSaglayici.GetRequiredService<T>();
    private void KolonlariKur()
    {
        KayitTablosu.Columns.Clear();
        var sekme=_model.SeciliSekme?.Tur;
        if(sekme==CariSekme.Notlar || sekme==CariSekme.Sistem) {
            Kolon("Tarih","Tarih","dd.MM.yyyy HH:mm");Kolon("Kullanıcı","Kullanici");Kolon("Başlık / İşlem","BelgeNo");Kolon("Not / Açıklama","Aciklama",null,true);return;
        }
        var button=new FrameworkElementFactory(typeof(Button));
        button.SetBinding(ContentControl.ContentProperty,new Binding("BelgeNo"));
        button.SetValue(Control.ForegroundProperty,new SolidColorBrush(Color.FromRgb(30,80,145)));
        button.SetValue(Control.BackgroundProperty,Brushes.Transparent);button.SetValue(Control.BorderThicknessProperty,new Thickness(0));
        button.SetBinding(Button.CommandProperty,new Binding("DataContext.BelgeAcKomutu"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(Window),1)});
        button.SetBinding(Button.CommandParameterProperty,new Binding("."));
        KayitTablosu.Columns.Add(new DataGridTemplateColumn {Header="Belge No",CellTemplate=new DataTemplate {VisualTree=button},Width=145});
        Kolon("Tarih","Tarih","dd.MM.yyyy");
        Kolon("Tür","Tur");
        if(sekme is CariSekme.Ekstre or CariSekme.CariFisleri) {
            Kolon("Saat","Tarih","HH:mm");Kolon("Açıklama","Aciklama",null,true);Kolon("Borç","Borc","N2");Kolon("Alacak","Alacak","N2");
            Kolon("Yürüyen Bakiye","MutlakBakiye","N2");Kolon("Yön","BakiyeYonu");Kolon("Kaynak","Kaynak");
        } else if(sekme==CariSekme.Siparisler) {
            Kolon("Teslim","Vade","dd.MM.yyyy");Kolon("Toplam","Toplam","N2");Kolon("Sevk","SevkEdilen","N2");Kolon("Kalan Sevk","KalanSevk","N2");Kolon("Faturalanan","Faturalanan","N2");Kolon("Kalan Fatura","KalanFatura","N2");
        } else if(sekme==CariSekme.Irsaliyeler) {
            Kolon("Kaynak Sipariş","Referans");Kolon("Miktar","Miktar","N2");Kolon("Toplam","Toplam","N2");
        } else if(sekme==CariSekme.TahsilatOdeme) {
            Kolon("Yöntem","Yontem");Kolon("Kasa / Banka","KasaBanka");Kolon("Tutar (TL)","Toplam","N2");Kolon("Para Birimi","ParaBirimi");Kolon("Kur","Kur","N4");Kolon("Referans","Referans");Kolon("Ters Kayıt","TersKayitId");Kolon("Açıklama","Aciklama",null,true);
        } else if(sekme==CariSekme.CekSenet) {
            Kolon("Çek / Senet No","Referans");Kolon("Vade","Vade","dd.MM.yyyy");Kolon("Tutar (TL)","Toplam","N2");Kolon("Para Birimi","ParaBirimi");Kolon("Kalan Gün","KalanGun");
        } else if(sekme==CariSekme.Genel) {
            Kolon("Açıklama","Aciklama",null,true);Kolon("Tutar","Toplam","N2");
        } else {
            Kolon("Ürün Sayısı","Miktar");Kolon("Net","Net","N2");Kolon("KDV","Kdv","N2");Kolon("Toplam","Toplam","N2");
            Kolon("Ödenen / Tahsil","Odenen","N2");Kolon("Kalan","Kalan","N2");Kolon("Vade","Vade","dd.MM.yyyy");
            if(sekme==CariSekme.RiskVade) Kolon("Gecikme Günü","GecikmeGunu");
            Kolon("Kaynak","Referans");
        }
        Kolon("Durum","Durum");
    }
    private void Kolon(string ad,string alan,string? format=null,bool genis=false) =>
        KayitTablosu.Columns.Add(new DataGridTextColumn {Header=ad,Binding=new Binding(alan){StringFormat=format,TargetNullValue="—"},Width=genis ? new DataGridLength(1,DataGridLengthUnitType.Star) : DataGridLength.Auto,MinWidth=75});
    private void BelgeCiftTik(object sender,MouseButtonEventArgs e)
    {
        if(e.OriginalSource is DependencyObject source && FindRow(source)!=null && KayitTablosu.SelectedItem is CariMerkezSatirDto row)
            _model.BelgeAcKomutu.Execute(row);
    }
    private static DataGridRow? FindRow(DependencyObject source) {while(source!=null){if(source is DataGridRow row)return row;source=VisualTreeHelper.GetParent(source);}return null;}
    private async Task HizliIslemAc(string tur,int id)
    {
        var cariServisi=Get<ICariServisi>();
        var cari=await cariServisi.IdIleGetirAsync(id);
        if(cari==null || cari.SirketId!=_firma || Get<IAktifFirmaBaglami>().SirketId!=_firma) throw new InvalidOperationException("Seçilen cari aktif firmaya ait değil.");
        if(tur=="Düzenle") {
            new MusteriDuzenlePenceresi(cariServisi,Get<ICariTipiTanimiServisi>(),cari){Owner=this}.ShowDialog();return;
        }
        if(tur is "Fatura" or "İade") {
            var vm=new FaturaEkleGorunumModeli(Get<IFaturaServisi>(),cariServisi,Get<IUrunServisi>(),Get<NavigasyonServisi>()) {
                IsCariKilitli=true,Tur=tur=="İade" ? FaturaTuru.SatisIade : FaturaTuru.Satis,SeciliCari=cari };
            Ac(tur,new FaturaEkleSayfasi {DataContext=vm});return;
        }
        if(tur is "Tahsilat" or "Ödeme") {
            var vm=new TahsilatOdemeGorunumModeli(Get<ITahsilatOdemeServisi>(),cariServisi,Get<IBelgeKapamaServisi>(),tur=="Tahsilat" ? FinansIslemTuru.Tahsilat : FinansIslemTuru.Odeme) {IsCariKilitli=true,SecilenCari=cari};
            Ac(tur,new TahsilatOdemeSayfasi {DataContext=vm});return;
        }
        if(tur=="Satış") {
            var vm=Get<SatisGorunumModeli>();vm.CariSatisMi=true;vm.SeciliCari=cari;
            Ac("Yeni Satış",new ProfesyonelSatisSayfasi {DataContext=vm});return;
        }
        if(tur=="Sipariş") {
            var vm=Get<SiparisGorunumModeli>();vm.SeciliCari=cari;
            new Siparis.SiparisFormPenceresi {DataContext=vm,Owner=this,Width=Math.Min(1240,SystemParameters.WorkArea.Width-40),Height=Math.Min(720,SystemParameters.WorkArea.Height-40)}.ShowDialog();return;
        }
        if(tur=="Cari Fiş") await CariFisAc(cari);
    }
    private void Ac(string title,object content)
    {
        new Window {Title=title,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Width=Math.Min(1240,SystemParameters.WorkArea.Width-40),Height=Math.Min(720,SystemParameters.WorkArea.Height-40),Content=content,Background=Brushes.White}.ShowDialog();
    }
    private Task CariFisAc(CariKart cari)
    {
        if(Get<IAktifFirmaBaglami>().SirketId!=_firma)throw new InvalidOperationException("Seçilen cari aktif firmaya ait değil.");
        new CariFisPenceresi(cari,Get<ICariHareketServisi>(),_firma){Owner=this}.ShowDialog();
        return Task.CompletedTask;
    }
    private Task BelgeGoster(CariBelgeDetayDto dto)
    {
        var panel=new DockPanel {Margin=new Thickness(16)};
        var title=new TextBlock {Text=$"{dto.Belge.BelgeNo} • {dto.Belge.Tur} • {dto.Belge.Durum}\n{dto.Belge.Tarih:dd.MM.yyyy HH:mm} • {dto.Belge.Toplam:N2} ₺\n{dto.Belge.Aciklama}",FontSize=16,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,15)};
        DockPanel.SetDock(title,Dock.Top);panel.Children.Add(title);
        var links=new WrapPanel();
        foreach(var b in dto.Baglantilar) {
            var button=new Button {Content=b.Kaynak+" • "+b.BelgeNo,Margin=new Thickness(0,0,6,6),Padding=new Thickness(8)};
            button.Command=_model.BelgeAcKomutu;button.CommandParameter=b;links.Children.Add(button);
        }
        DockPanel.SetDock(links,Dock.Bottom);panel.Children.Add(links);
        var grid=new DataGrid {ItemsSource=dto.Kalemler,IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false};
        foreach(var p in new[]{("Ürün Kodu","BelgeNo"),("Ürün","Aciklama"),("Miktar","Miktar"),("Birim Fiyat","Net"),("KDV","Kdv"),("Toplam","Toplam")})
            grid.Columns.Add(new DataGridTextColumn {Header=p.Item1,Binding=new Binding(p.Item2),Width=DataGridLength.Auto});
        panel.Children.Add(grid);Ac("Belge Detayı",panel);return Task.CompletedTask;
    }
}
