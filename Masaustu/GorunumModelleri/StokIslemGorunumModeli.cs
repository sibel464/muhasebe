using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.StokOperasyon;
using Masaustu.Yardimcilar;
using EtkiAlani.Modeller.Stok;

namespace Masaustu.GorunumModelleri;

public class StokIslemGorunumModeli : TemelGorunumModeli
{
    // Sayfanın servisleri tek veritabanı bağlamını paylaşır; çağrılar sıraya konur (bkz. SiraliServis).
    private readonly SemaphoreSlim _servisKilidi = new(1, 1);

    private readonly IStokOperasyonServisi _operasyonServisi;
    private readonly IUrunServisi _urunServisi;
    private readonly IDepoServisi _depoServisi;
    private readonly IAktifFirmaBaglami _firmaBaglami;
    private readonly IKullaniciServisi _kullaniciServisi;

    private bool _isGiris = true;
    private Urun? _seciliUrun;
    private int? _seciliDepoId;
    private decimal _miktar = 1;
    private decimal _birimFiyat;
    private DateTime _tarih = DateTime.Today;
    private string _islemNedeni = string.Empty;
    private string _referansNo = string.Empty;
    private string _aciklama = string.Empty;
    private string _hataMesaji = string.Empty;
    private string _basariMesaji = string.Empty;
    private bool _kaydediliyor;

    public ObservableCollection<Urun> Urunler { get; } = new();
    public ObservableCollection<DepoOption> Depolar { get; } = new();
    public ObservableCollection<string> Nedenler { get; } = new();

    public bool IsGiris
    {
        get => _isGiris;
        set
        {
            if (OzellikAyarla(ref _isGiris, value))
            {
                NedenleriYukle();
                BildirimGonder(nameof(BaslikMetni));
            }
        }
    }

    public string BaslikMetni => IsGiris ? "📥 MANUEL STOK GİRİŞİ" : "📤 MANUEL STOK ÇIKIŞI";

    public Urun? SeciliUrun
    {
        get => _seciliUrun;
        set
        {
            if (OzellikAyarla(ref _seciliUrun, value))
            {
                if (value != null) BirimFiyat = value.AlisFiyati;
                _ = DepoStokBilgisiGuncelleAsync();
            }
        }
    }

    public int? SeciliDepoId
    {
        get => _seciliDepoId;
        set { if (OzellikAyarla(ref _seciliDepoId, value)) _ = DepoStokBilgisiGuncelleAsync(); }
    }

    // Barkod okuyucu: ürünü bulur, terazi etiketiyse miktarı kilosuyla doldurur; "12*barkod" 12 adet.
    private string _barkodArama = string.Empty;
    public string BarkodArama { get => _barkodArama; set => OzellikAyarla(ref _barkodArama, value); }
    public ICommand BarkodOkutKomutu => _barkodOkutKomutu ??= new KomutSarici(async _ => await BarkodOkutAsync(), tekrarGirisli: true);
    private ICommand? _barkodOkutKomutu;

    public async Task BarkodOkutAsync()
    {
        var okunan = BarkodArama.Trim();
        if (okunan.Length == 0) return;
        HataMesaji = string.Empty;
        var bulunan = await StokBarkodu.BulAsync(_urunServisi, okunan);
        BarkodArama = string.Empty;
        if (bulunan == null)
        {
            HataMesaji = $"Barkod/kod bulunamadı: {okunan}";
            System.Media.SystemSounds.Exclamation.Play();
            return;
        }
        SeciliUrun = Urunler.FirstOrDefault(u => u.Id == bulunan.Value.Urun.Id) ?? EkleVeDon(bulunan.Value.Urun);
        Miktar = bulunan.Value.Miktar;
    }

    private Urun EkleVeDon(Urun u) { Urunler.Add(u); return u; }

    private string _depoStokMetni = string.Empty;
    public string DepoStokMetni { get => _depoStokMetni; private set => OzellikAyarla(ref _depoStokMetni, value); }

    private async Task DepoStokBilgisiGuncelleAsync()
    {
        var urun = SeciliUrun; var depoId = SeciliDepoId;
        if (urun == null || !depoId.HasValue) { DepoStokMetni = string.Empty; return; }
        try
        {
            var stoklar = await _urunServisi.DepoStoklariniGetirAsync(depoId.Value);
            if (SeciliUrun != urun || SeciliDepoId != depoId) return;
            DepoStokMetni = $"Bu depodaki stok: {(stoklar.TryGetValue(urun.Id, out var m) ? m : 0):#,##0.###} {urun.Birim}";
        }
        catch { DepoStokMetni = string.Empty; }
    }

    public decimal Miktar
    {
        get => _miktar;
        set => OzellikAyarla(ref _miktar, value);
    }

    public decimal BirimFiyat
    {
        get => _birimFiyat;
        set => OzellikAyarla(ref _birimFiyat, value);
    }

    public DateTime Tarih
    {
        get => _tarih;
        set => OzellikAyarla(ref _tarih, value);
    }

    public string IslemNedeni
    {
        get => _islemNedeni;
        set => OzellikAyarla(ref _islemNedeni, value);
    }

    public string ReferansNo
    {
        get => _referansNo;
        set => OzellikAyarla(ref _referansNo, value);
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

    public ICommand KaydetKomutu { get; }
    public ICommand IptalKomutu { get; }

    public Action? KapatAction { get; set; }

    public StokIslemGorunumModeli(
        IStokOperasyonServisi operasyonServisi,
        IUrunServisi urunServisi,
        IDepoServisi depoServisi,
        IAktifFirmaBaglami firmaBaglami,
        IKullaniciServisi kullaniciServisi)
    {
        _operasyonServisi = SiraliServis<IStokOperasyonServisi>.Sar(operasyonServisi, _servisKilidi);
        _urunServisi = SiraliServis<IUrunServisi>.Sar(urunServisi, _servisKilidi);
        _depoServisi = SiraliServis<IDepoServisi>.Sar(depoServisi, _servisKilidi);
        _firmaBaglami = firmaBaglami;
        _kullaniciServisi = kullaniciServisi;

        KaydetKomutu = new KomutSarici(async _ => await KaydetAsync(), _ => !Kaydediliyor);
        IptalKomutu = new KomutSarici(() => KapatAction?.Invoke());
    }

    public async Task InitAsync(int? seciliUrunId = null, bool isGiris = true)
    {
        IsGiris = isGiris;

        Depolar.Clear();
        var depolar = await _depoServisi.DepolariGetirAsync();
        foreach (var d in depolar)
            Depolar.Add(new DepoOption(d.Id, $"{d.DepoAdi} ({d.DepoKodu})"));

        if (Depolar.Count > 0)
            SeciliDepoId = Depolar[0].Id;

        Urunler.Clear();
        var urunler = await _urunServisi.TumunuGetirAsync();
        foreach (var u in urunler)
            Urunler.Add(u);

        if (seciliUrunId.HasValue && seciliUrunId > 0)
            SeciliUrun = Urunler.FirstOrDefault(u => u.Id == seciliUrunId.Value);
        else
            SeciliUrun = null; // ilk ürünü kendiliğinden seçmek yanlış ürüne fiş kesilmesine yol açıyordu

        NedenleriYukle();
    }

    private void NedenleriYukle()
    {
        Nedenler.Clear();
        if (IsGiris)
        {
            Nedenler.Add("Devir Girişi");
            Nedenler.Add("Sayım Fazlası");
            Nedenler.Add("Hediye / Numune Girişi");
            Nedenler.Add("Diğer Giriş");
        }
        else
        {
            Nedenler.Add("Sarf / Kullanım");
            Nedenler.Add("Fire / Bozulma");
            Nedenler.Add("Sayım Eksiği");
            Nedenler.Add("Numune / Promosyon Çıkışı");
            Nedenler.Add("Diğer Çıkış");
        }
        IslemNedeni = Nedenler.FirstOrDefault() ?? "";
    }

    public async Task KaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (SeciliUrun == null)
        {
            HataMesaji = "İşlem yapılacak ürün seçilmelidir.";
            return;
        }

        if (!SeciliDepoId.HasValue)
        {
            HataMesaji = "Lokasyon / Depo seçilmelidir.";
            return;
        }

        if (Miktar <= 0)
        {
            HataMesaji = "Miktar 0'dan büyük olmalıdır.";
            return;
        }

        if (string.IsNullOrWhiteSpace(IslemNedeni))
        {
            HataMesaji = "İşlem nedeni seçilmelidir.";
            return;
        }

        Kaydediliyor = true;

        try
        {
            int kullaniciId = _kullaniciServisi.AktifKullanici?.Id ?? 1;

            if (IsGiris)
            {
                var req = new StokGirisIstegi(
                    SeciliUrun.Id,
                    SeciliDepoId.Value,
                    Miktar,
                    BirimFiyat,
                    Tarih,
                    IslemNedeni,
                    ReferansNo,
                    Aciklama
                );
                await _operasyonServisi.StokGirisiYapAsync(req, _firmaBaglami.SirketId, kullaniciId);
                BasariMesaji = "Stok girişi başarıyla kaydedildi.";
            }
            else
            {
                var req = new StokCikisIstegi(
                    SeciliUrun.Id,
                    SeciliDepoId.Value,
                    Miktar,
                    BirimFiyat,
                    Tarih,
                    IslemNedeni,
                    ReferansNo,
                    Aciklama
                );
                await _operasyonServisi.StokCikisiYapAsync(req, _firmaBaglami.SirketId, kullaniciId);
                BasariMesaji = "Stok çıkışı başarıyla kaydedildi.";
            }

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
