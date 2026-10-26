using System.Collections.ObjectModel;
using System.Windows.Input;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.GorunumModelleri;

// Gönderilecek ek: PDF ancak "Gönder"e basılınca hazırlanır (dönüşüm birkaç saniye sürebilir).
public class EpostaEkSecenegi : TemelGorunumModeli
{
    private bool _secili;
    public EpostaEkSecenegi(string ad, bool secili, Func<Task<EpostaEki>> hazirla) { Ad = ad; _secili = secili; Hazirla = hazirla; }
    public string Ad { get; }
    public Func<Task<EpostaEki>> Hazirla { get; }
    public bool Secili { get => _secili; set => OzellikAyarla(ref _secili, value); }
}

public class EpostaGonderModeli : TemelGorunumModeli
{
    private readonly IEpostaServisi _servis;
    private string _alicilar, _konu, _metin, _durum = string.Empty;
    private bool _gonderiliyor;

    public EpostaGonderModeli(IEpostaServisi servis, string alicilar, string konu, string metin, IEnumerable<EpostaEkSecenegi> ekler)
    {
        _servis = servis;
        _alicilar = alicilar;
        _konu = konu;
        _metin = metin;
        foreach (var e in ekler) Ekler.Add(e);
        GonderKomutu = new KomutSarici(async _ => await GonderAsync(), _ => !_gonderiliyor);
    }

    public string Alicilar { get => _alicilar; set => OzellikAyarla(ref _alicilar, value); }
    public string Konu { get => _konu; set => OzellikAyarla(ref _konu, value); }
    public string Metin { get => _metin; set => OzellikAyarla(ref _metin, value); }
    public string Durum { get => _durum; private set => OzellikAyarla(ref _durum, value); }
    public ObservableCollection<EpostaEkSecenegi> Ekler { get; } = new();
    public ICommand GonderKomutu { get; }
    public Action? Gonderildi { get; set; }

    private async Task GonderAsync()
    {
        try
        {
            _gonderiliyor = true;
            CommandManager.InvalidateRequerySuggested();
            if (!await _servis.YapilandirildiMiAsync())
            {
                Durum = "E-posta ayarları yapılmamış. Ayarlar → E-posta sekmesinden SMTP bilgilerini girip kaydedin.";
                return;
            }
            Durum = "Ekler hazırlanıyor...";
            var mesaj = new EpostaMesaji { Alicilar = Alicilar, Konu = Konu, Metin = Metin };
            foreach (var ek in Ekler.Where(e => e.Secili)) mesaj.Ekler.Add(await ek.Hazirla());
            Durum = "Gönderiliyor...";
            await _servis.GonderAsync(mesaj);
            Durum = "Gönderildi.";
            Gonderildi?.Invoke();
        }
        catch (Exception ex)
        {
            Durum = "Gönderilemedi: " + ex.Message;
        }
        finally
        {
            _gonderiliyor = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
