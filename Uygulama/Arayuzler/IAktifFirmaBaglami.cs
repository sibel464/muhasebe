namespace Uygulama.Arayuzler;
public interface IAktifFirmaBaglami
{
 Guid SirketId { get; } int? SubeId { get; } int? MaliDonemId { get; } int? KullaniciId { get; }
 event EventHandler? Degisti;
 void Degistir(Guid sirketId,int? subeId,int? maliDonemId,int? kullaniciId);
}
public sealed class AktifFirmaBaglami : IAktifFirmaBaglami
{
 public Guid SirketId{get;private set;}=Guid.Parse("11111111-1111-1111-1111-111111111111"); public int? SubeId{get;private set;}=1; public int? MaliDonemId{get;private set;}=1; public int? KullaniciId{get;private set;}
 public event EventHandler? Degisti;
 public void Degistir(Guid s,int? sube,int? donem,int? kullanici){SirketId=s;SubeId=sube;MaliDonemId=donem;KullaniciId=kullanici;Degisti?.Invoke(this,EventArgs.Empty);}
}
