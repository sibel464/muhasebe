using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.GorunumModelleri;

public class CariTipiYonetimGorunumModeli : TemelGorunumModeli
{
    private readonly ICariTipiTanimiServisi _servis;
    private CariTipiTanimi? _secili;
    private string _kod = string.Empty, _ad = string.Empty, _aciklama = string.Empty, _hata = string.Empty;
    private bool _aktifMi = true;

    public ObservableCollection<CariTipiTanimi> Tanimlar { get; } = new();
    public CariTipiTanimi? Secili { get => _secili; set { if (OzellikAyarla(ref _secili, value) && value != null) { Kod = value.Kod; Ad = value.Ad; Aciklama = value.Aciklama ?? ""; AktifMi = value.AktifMi; } } }
    public string Kod { get => _kod; set => OzellikAyarla(ref _kod, value); }
    public string Ad { get => _ad; set => OzellikAyarla(ref _ad, value); }
    public string Aciklama { get => _aciklama; set => OzellikAyarla(ref _aciklama, value); }
    public bool AktifMi { get => _aktifMi; set => OzellikAyarla(ref _aktifMi, value); }
    public string Hata { get => _hata; set => OzellikAyarla(ref _hata, value); }
    public int? KaydedilenId { get; private set; }
    public ICommand YeniKomutu { get; }
    public ICommand KaydetKomutu { get; }
    public ICommand AktiflikKomutu { get; }

    public CariTipiYonetimGorunumModeli(ICariTipiTanimiServisi servis)
    {
        _servis = servis;
        YeniKomutu = new KomutSarici(Yeni);
        KaydetKomutu = new KomutSarici(async _ => await KaydetAsync());
        AktiflikKomutu = new KomutSarici(async _ => await AktiflikDegistirAsync(), _ => Secili != null);
        _ = YukleAsync();
    }

    private async Task YukleAsync(int? secilecekId = null)
    {
        var liste = await _servis.ListeleAsync(false);
        Tanimlar.Clear(); foreach (var x in liste) Tanimlar.Add(x);
        Secili = secilecekId.HasValue ? Tanimlar.FirstOrDefault(x => x.Id == secilecekId) : Secili;
    }
    private void Yeni() { Secili = null; Kod = Ad = Aciklama = Hata = string.Empty; AktifMi = true; }
    private async Task KaydetAsync()
    {
        Hata = string.Empty;
        try
        {
            var kayit = await _servis.KaydetAsync(new CariTipiTanimi { Id = Secili?.Id ?? 0, Kod = Kod, Ad = Ad, Aciklama = Aciklama, AktifMi = AktifMi, SiraNo = Secili?.SiraNo ?? 0 });
            KaydedilenId = kayit.Id;
            await YukleAsync(kayit.Id);
        }
        catch (Exception ex) { Hata = ex.Message; }
    }
    private async Task AktiflikDegistirAsync()
    {
        if (Secili == null) return;
        await _servis.AktiflikDegistirAsync(Secili.Id, !Secili.AktifMi);
        await YukleAsync(Secili.Id);
    }
}
