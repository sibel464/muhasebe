using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.StokOperasyon;
using Masaustu.Yardimcilar;
using EtkiAlani.Modeller.Stok;

namespace Masaustu.GorunumModelleri;

public class StokSayimKalemModeli : TemelGorunumModeli
{
    private decimal _sayilanMiktar;

    public int UrunId { get; init; }
    public string UrunKodu { get; init; } = string.Empty;
    public string UrunAdi { get; init; } = string.Empty;
    public string Barkod { get; init; } = string.Empty;
    public decimal SistemMiktari { get; init; }

    public decimal SayilanMiktar
    {
        get => _sayilanMiktar;
        set
        {
            if (OzellikAyarla(ref _sayilanMiktar, value))
            {
                BildirimGonder(nameof(FarkMiktari));
                BildirimGonder(nameof(FarkTuru));
            }
        }
    }

    public decimal FarkMiktari => SayilanMiktar - SistemMiktari;
    public string FarkTuru => FarkMiktari > 0 ? "Fazla +" : (FarkMiktari < 0 ? "Eksik -" : "Tam");
}

public class StokSayimGorunumModeli : TemelGorunumModeli
{
    private readonly IStokOperasyonServisi _operasyonServisi;
    private readonly IUrunServisi _urunServisi;
    private readonly IDepoServisi _depoServisi;
    private readonly IAktifFirmaBaglami _firmaBaglami;
    private readonly IKullaniciServisi _kullaniciServisi;

    private int? _seciliDepoId;
    private DateTime _tarih = DateTime.Today;
    private string _barkodArama = string.Empty;
    private string _aciklama = string.Empty;
    private string _hataMesaji = string.Empty;
    private string _basariMesaji = string.Empty;
    private bool _kaydediliyor;

    public ObservableCollection<DepoOption> Depolar { get; } = new();
    public ObservableCollection<StokSayimKalemModeli> Kalemler { get; } = new();

    public int? SeciliDepoId
    {
        get => _seciliDepoId;
        set { if (OzellikAyarla(ref _seciliDepoId, value)) _ = DepoUrunleriniYukleAsync(); }
    }

    public DateTime Tarih
    {
        get => _tarih;
        set => OzellikAyarla(ref _tarih, value);
    }

    public string BarkodArama
    {
        get => _barkodArama;
        set => OzellikAyarla(ref _barkodArama, value);
    }

    public string Aciklama
    {
        get => _aciklama;
        set => OzellikAyarla(ref _aciklama, value);
    }

    public string HataMesaji
    {
        get => _hataMesaji;
        set => OzellikAyarla(ref _hataMesaji, value);
    }

    public string BasariMesaji
    {
        get => _basariMesaji;
        set => OzellikAyarla(ref _basariMesaji, value);
    }

    public bool Kaydediliyor
    {
        get => _kaydediliyor;
        private set { OzellikAyarla(ref _kaydediliyor, value); CommandManager.InvalidateRequerySuggested(); }
    }

    public ICommand BarkodOkutKomutu { get; }
    public ICommand SayimOnaylaKomutu { get; }
    public ICommand IptalKomutu { get; }

    public Action? KapatAction { get; set; }

    public StokSayimGorunumModeli(
        IStokOperasyonServisi operasyonServisi,
        IUrunServisi urunServisi,
        IDepoServisi depoServisi,
        IAktifFirmaBaglami firmaBaglami,
        IKullaniciServisi kullaniciServisi)
    {
        _operasyonServisi = operasyonServisi;
        _urunServisi = urunServisi;
        _depoServisi = depoServisi;
        _firmaBaglami = firmaBaglami;
        _kullaniciServisi = kullaniciServisi;

        BarkodOkutKomutu = new KomutSarici(_ => BarkodOkut());
        SayimOnaylaKomutu = new KomutSarici(async _ => await SayimOnaylaAsync(), _ => !Kaydediliyor);
        IptalKomutu = new KomutSarici(() => KapatAction?.Invoke());
    }

    public async Task InitAsync()
    {
        Depolar.Clear();
        var depolar = await _depoServisi.DepolariGetirAsync();
        foreach (var d in depolar)
            Depolar.Add(new DepoOption(d.Id, $"{d.DepoAdi} ({d.DepoKodu})"));

        if (Depolar.Count > 0)
            SeciliDepoId = Depolar[0].Id;
    }

    private async Task DepoUrunleriniYukleAsync()
    {
        if (!SeciliDepoId.HasValue) return;

        Kalemler.Clear();
        var urunler = await _urunServisi.TumunuGetirAsync();
        var matris = await _operasyonServisi.GetMatrisOzetAsync(_firmaBaglami.SirketId, null, SeciliDepoId);

        foreach (var u in urunler)
        {
            var m = matris.FirstOrDefault(x => x.UrunId == u.Id);
            decimal sistemStok = m != null ? (SeciliDepoId == 1 ? m.MagazaFiziksel : (SeciliDepoId == 2 ? m.Depo1Fiziksel : m.Depo2Fiziksel)) : 0;

            Kalemler.Add(new StokSayimKalemModeli
            {
                UrunId = u.Id,
                UrunKodu = u.Kod,
                UrunAdi = u.Ad,
                Barkod = u.Barkod ?? "",
                SistemMiktari = sistemStok,
                SayilanMiktar = sistemStok
            });
        }
    }

    private void BarkodOkut()
    {
        if (string.IsNullOrWhiteSpace(BarkodArama)) return;
        var code = BarkodArama.Trim();

        var item = Kalemler.FirstOrDefault(x => x.Barkod.Equals(code, StringComparison.OrdinalIgnoreCase) || x.UrunKodu.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            item.SayilanMiktar += 1;
            BarkodArama = string.Empty;
        }
    }

    public async Task SayimOnaylaAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (!SeciliDepoId.HasValue)
        {
            HataMesaji = "Sayım yapılacak depo seçilmelidir.";
            return;
        }

        Kaydediliyor = true;

        try
        {
            var istek = new StokSayimIstegi(
                SeciliDepoId.Value,
                Tarih,
                Aciklama,
                Kalemler.Select(k => new StokSayimKalemIstegi(k.UrunId, k.SayilanMiktar)).ToList()
            );

            int kullaniciId = _kullaniciServisi.AktifKullanici?.Id ?? 1;
            var sayim = await _operasyonServisi.SayimOlusturAsync(istek, _firmaBaglami.SirketId, kullaniciId);
            await _operasyonServisi.SayimOnaylaAsync(sayim.Id, _firmaBaglami.SirketId, kullaniciId);

            BasariMesaji = $"Sayım başarıyla tamamlandı ve onaylandı ({sayim.SayimNo}).";
            await Task.Delay(1000);
            KapatAction?.Invoke();
        }
        catch (Exception ex)
        {
            HataMesaji = ex.Message;
        }
        finally
        {
            Kaydediliyor = false;
        }
    }
}
