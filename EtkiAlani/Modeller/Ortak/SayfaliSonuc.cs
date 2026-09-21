namespace EtkiAlani.Modeller.Ortak;

public class SayfaliSonuc<T>
{
    public List<T> Veri { get; set; } = new();
    public int ToplamKayit { get; set; }
    public int MevcutSayfa { get; set; } = 1;
    public int SayfaBoyutu { get; set; } = 50;
    public int ToplamSayfa => SayfaBoyutu > 0 ? (int)Math.Ceiling((double)ToplamKayit / SayfaBoyutu) : 0;
    public List<T> Liste => Veri;
    public int Sayfa => MevcutSayfa;
    public bool ÖncekiSayfaVar => OncekiSayfaVar;
    public bool OncekiSayfaVar => MevcutSayfa > 1;
    public bool SonrakiSayfaVar => MevcutSayfa < ToplamSayfa;
}
