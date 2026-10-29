using System.Windows.Input;
using EtkiAlani.Modeller.Muhasebe;
using Uygulama.Arayuzler;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class PersonelFormuModeli : TemelGorunumModeli
{
    public IReadOnlyList<DemirbasFormuModeli.Secenek> GiderHesaplari { get; } =
    [
        new("770", "Genel yönetim giderleri"), new("760", "Pazarlama satış dağıtım giderleri"), new("740", "Hizmet üretim maliyeti")
    ];

    private readonly IPersonelServisi _servis;
    private readonly Personel _kart;
    private string _hata = string.Empty;
    private decimal _net, _sgkIsci, _issizlikIsci, _gv, _dv, _sgkIsveren, _issizlikIsveren;

    public PersonelFormuModeli(IPersonelServisi servis, Personel? mevcut)
    {
        _servis = servis;
        _kart = mevcut ?? new Personel { IseGirisTarihi = DateTime.Today };
        Kod = _kart.Kod; AdSoyad = _kart.AdSoyad; TcKimlikNo = _kart.TcKimlikNo ?? ""; Gorev = _kart.Gorev ?? ""; Departman = _kart.Departman ?? "";
        IseGirisTarihi = _kart.IseGirisTarihi.Date; IstenCikisTarihi = _kart.IstenCikisTarihi?.Date; Telefon = _kart.Telefon ?? ""; Eposta = _kart.Eposta ?? "";
        Iban = _kart.Iban ?? ""; Aciklama = _kart.Aciklama;
        (_net, _sgkIsci, _issizlikIsci, _gv, _dv, _sgkIsveren, _issizlikIsveren) =
            (_kart.NetUcret, _kart.SgkIsciPayi, _kart.IssizlikIsciPayi, _kart.GelirVergisi, _kart.DamgaVergisi, _kart.SgkIsverenPayi, _kart.IssizlikIsverenPayi);
        GiderHesabi = GiderHesaplari.FirstOrDefault(x => x.Kod == _kart.GiderHesapKodu) ?? GiderHesaplari[0];
        KaydetKomutu = new KomutSarici(async () => await KaydetAsync());
    }

    public string Baslik => _kart.Id == 0 ? "Yeni Personel" : $"Personel: {_kart.Kod}";
    public string Kod { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public string TcKimlikNo { get; set; } = "";
    public string Gorev { get; set; } = "";
    public string Departman { get; set; } = "";
    public DateTime IseGirisTarihi { get; set; }
    public DateTime? IstenCikisTarihi { get; set; }
    public string Telefon { get; set; } = "";
    public string Eposta { get; set; } = "";
    public string Iban { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public DemirbasFormuModeli.Secenek GiderHesabi { get; set; }

    public decimal NetUcret { get => _net; set => Ayarla(ref _net, value); }
    public decimal SgkIsciPayi { get => _sgkIsci; set => Ayarla(ref _sgkIsci, value); }
    public decimal IssizlikIsciPayi { get => _issizlikIsci; set => Ayarla(ref _issizlikIsci, value); }
    public decimal GelirVergisi { get => _gv; set => Ayarla(ref _gv, value); }
    public decimal DamgaVergisi { get => _dv; set => Ayarla(ref _dv, value); }
    public decimal SgkIsverenPayi { get => _sgkIsveren; set => Ayarla(ref _sgkIsveren, value); }
    public decimal IssizlikIsverenPayi { get => _issizlikIsveren; set => Ayarla(ref _issizlikIsveren, value); }
    public string UcretOzeti => $"Brüt ücret: {NetUcret + SgkIsciPayi + IssizlikIsciPayi + GelirVergisi + DamgaVergisi:N2} TL · İşverene maliyet: {NetUcret + SgkIsciPayi + IssizlikIsciPayi + GelirVergisi + DamgaVergisi + SgkIsverenPayi + IssizlikIsverenPayi:N2} TL";

    private void Ayarla(ref decimal alan, decimal deger)
    {
        if (OzellikAyarla(ref alan, deger)) BildirimGonder(nameof(UcretOzeti));
    }

    public string Hata { get => _hata; private set => OzellikAyarla(ref _hata, value); }
    public ICommand KaydetKomutu { get; }
    public Action? Kaydedildi { get; set; }

    private async Task KaydetAsync()
    {
        Hata = string.Empty;
        _kart.Kod = Kod; _kart.AdSoyad = AdSoyad; _kart.TcKimlikNo = TcKimlikNo; _kart.Gorev = Gorev; _kart.Departman = Departman;
        _kart.IseGirisTarihi = IseGirisTarihi; _kart.IstenCikisTarihi = IstenCikisTarihi; _kart.Telefon = Telefon; _kart.Eposta = Eposta; _kart.Iban = Iban;
        _kart.Aciklama = Aciklama ?? ""; _kart.GiderHesapKodu = GiderHesabi.Kod;
        _kart.NetUcret = NetUcret; _kart.SgkIsciPayi = SgkIsciPayi; _kart.IssizlikIsciPayi = IssizlikIsciPayi; _kart.GelirVergisi = GelirVergisi;
        _kart.DamgaVergisi = DamgaVergisi; _kart.SgkIsverenPayi = SgkIsverenPayi; _kart.IssizlikIsverenPayi = IssizlikIsverenPayi;
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
