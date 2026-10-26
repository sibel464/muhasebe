using System.Windows.Input;
using EtkiAlani.Modeller.Muhasebe;
using Uygulama.Arayuzler;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class DemirbasFormuModeli : TemelGorunumModeli
{
    public sealed record Secenek(string Kod, string Ad) { public override string ToString() => $"{Kod} - {Ad}"; }

    public IReadOnlyList<Secenek> VarlikHesaplari { get; } =
    [
        new("255", "Demirbaşlar (bilgisayar, mobilya, cihaz)"), new("254", "Taşıtlar"), new("253", "Tesis, makine ve cihazlar"),
        new("252", "Binalar"), new("260", "Haklar (yazılım lisansı vb.)"), new("264", "Özel maliyetler (kiralık yer tadilatı)")
    ];
    public IReadOnlyList<Secenek> GiderHesaplari { get; } =
    [
        new("770", "Genel yönetim giderleri"), new("760", "Pazarlama satış dağıtım giderleri"), new("740", "Hizmet üretim maliyeti"),
        new("730", "Genel üretim giderleri")
    ];
    public IReadOnlyList<Secenek> Yontemler { get; } = [new("1", "Normal (eşit tutarlı)"), new("2", "Azalan bakiyeler")];

    private readonly ISabitKiymetServisi _servis;
    private readonly SabitKiymet _kart;
    private string _hata = string.Empty;

    public DemirbasFormuModeli(ISabitKiymetServisi servis, SabitKiymet? mevcut)
    {
        _servis = servis;
        _kart = mevcut ?? new SabitKiymet { AlisTarihi = DateTime.Today };
        Kod = _kart.Kod; Ad = _kart.Ad; Maliyet = _kart.Maliyet; FaydaliOmur = _kart.FaydaliOmurYil; AlisTarihi = _kart.AlisTarihi.Date;
        KistAmortisman = _kart.KistAmortisman; DevirBirikmis = _kart.DevirBirikmisAmortisman; DevirSonYil = _kart.DevirSonYil?.ToString() ?? "";
        SeriNo = _kart.SeriNo ?? ""; Konum = _kart.Konum ?? ""; Aciklama = _kart.Aciklama;
        VarlikHesabi = VarlikHesaplari.FirstOrDefault(x => x.Kod == _kart.HesapKodu) ?? VarlikHesaplari[0];
        GiderHesabi = GiderHesaplari.FirstOrDefault(x => x.Kod == _kart.GiderHesapKodu) ?? GiderHesaplari[0];
        Yontem = Yontemler.First(x => x.Kod == ((int)_kart.Yontem).ToString());
        KaydetKomutu = new KomutSarici(async () => await KaydetAsync());
        AmortismanAyrilmis = _kart.Kayitlar.Any(x => !x.Silindi);
    }

    public string Baslik => _kart.Id == 0 ? "Yeni Demirbaş" : $"Demirbaş: {_kart.Kod}";
    public bool AmortismanAyrilmis { get; }
    public bool HesapAlanlariAcik => !AmortismanAyrilmis;
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public decimal Maliyet { get; set; }
    public int FaydaliOmur { get; set; }
    public DateTime AlisTarihi { get; set; }
    public bool KistAmortisman { get; set; }
    public decimal DevirBirikmis { get; set; }
    public string DevirSonYil { get; set; } = "";
    public string SeriNo { get; set; } = "";
    public string Konum { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public Secenek VarlikHesabi { get; set; }
    public Secenek GiderHesabi { get; set; }
    public Secenek Yontem { get; set; }

    public string Hata { get => _hata; private set => OzellikAyarla(ref _hata, value); }
    public ICommand KaydetKomutu { get; }
    public Action? Kaydedildi { get; set; }

    private async Task KaydetAsync()
    {
        Hata = string.Empty;
        int? devirYili = null;
        if (!string.IsNullOrWhiteSpace(DevirSonYil))
        {
            if (!int.TryParse(DevirSonYil.Trim(), out var y) || y < 1950 || y > 2100) { Hata = "Önceki amortismanın son yılını 4 haneli yazın (ör. 2024)."; return; }
            devirYili = y;
        }
        _kart.Kod = Kod; _kart.Ad = Ad; _kart.Maliyet = Maliyet; _kart.FaydaliOmurYil = FaydaliOmur; _kart.AlisTarihi = AlisTarihi;
        _kart.Yontem = (AmortismanYontemi)int.Parse(Yontem.Kod); _kart.KistAmortisman = KistAmortisman;
        _kart.DevirBirikmisAmortisman = DevirBirikmis; _kart.DevirSonYil = devirYili;
        _kart.HesapKodu = VarlikHesabi.Kod;
        _kart.BirikmisAmortismanHesapKodu = VarlikHesabi.Kod.StartsWith("26") ? "268" : "257";
        _kart.GiderHesapKodu = GiderHesabi.Kod;
        _kart.SeriNo = string.IsNullOrWhiteSpace(SeriNo) ? null : SeriNo.Trim();
        _kart.Konum = string.IsNullOrWhiteSpace(Konum) ? null : Konum.Trim();
        _kart.Aciklama = Aciklama ?? "";
        try
        {
            await _servis.KaydetAsync(_kart);
            Kaydedildi?.Invoke();
        }
        catch (Exception ex)
        {
            Hata = ex.Message;
        }
    }
}
