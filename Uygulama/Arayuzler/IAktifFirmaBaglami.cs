namespace Uygulama.Arayuzler;
public interface IAktifFirmaBaglami
{
 Guid SirketId { get; } int? SubeId { get; } int? MaliDonemId { get; } int? KullaniciId { get; } string? KullaniciAdi { get; }
 string? SirketAdi { get; set; }
 event EventHandler? Degisti;
 void Degistir(Guid sirketId,int? subeId,int? maliDonemId,int? kullaniciId,string? kullaniciAdi = null);
}
public sealed class AktifFirmaBaglami : IAktifFirmaBaglami
{
 public Guid SirketId{get;private set;}=Guid.Parse("11111111-1111-1111-1111-111111111111"); public int? SubeId{get;private set;}=1; public int? MaliDonemId{get;private set;}=1; public int? KullaniciId{get;private set;} public string? KullaniciAdi{get;private set;}
 public string? SirketAdi{get;set;}
 public event EventHandler? Degisti;
 public void Degistir(Guid s,int? sube,int? donem,int? kullanici,string? kullaniciAdi = null){SirketId=s;SubeId=sube;MaliDonemId=donem;KullaniciAdi=kullanici is null ? null : kullaniciAdi ?? (KullaniciId == kullanici ? KullaniciAdi : null);KullaniciId=kullanici;Degisti?.Invoke(this,EventArgs.Empty);}
}
