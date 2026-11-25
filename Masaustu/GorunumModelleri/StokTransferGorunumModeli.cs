using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.StokOperasyon;
using Masaustu.Yardimcilar;
using EtkiAlani.Modeller.Stok;

namespace Masaustu.GorunumModelleri;

public class StokTransferGorunumModeli : TemelGorunumModeli
{
    // Sayfanın servisleri tek veritabanı bağlamını paylaşır; çağrılar sıraya konur (bkz. SiraliServis).
    private readonly SemaphoreSlim _servisKilidi = new(1, 1);

    private readonly IStokOperasyonServisi _operasyonServisi;
    private readonly IUrunServisi _urunServisi;
    private readonly IDepoServisi _depoServisi;
    private readonly IAktifFirmaBaglami _firmaBaglami;
    private readonly IKullaniciServisi _kullaniciServisi;

    private int? _kaynakDepoId;
    private int? _hedefDepoId;
    private Urun? _seciliUrun;
    private decimal _miktar = 1;
    private decimal _birimFiyat;
    private DateTime _tarih = DateTime.Today;
    private string _transferNo = string.Empty;
    private string _aciklama = string.Empty;
    private string _hataMesaji = string.Empty;
    private string _basariMesaji = string.Empty;
    private bool _kaydediliyor;

    public ObservableCollection<DepoOption> Depolar { get; } = new();
    public ObservableCollection<Urun> Urunler { get; } = new();

    public int? KaynakDepoId
    {
        get => _kaynakDepoId;
        set { if (OzellikAyarla(ref _kaynakDepoId, value)) _ = DepoStokBilgisiGuncelleAsync(); }
    }

    public int? HedefDepoId
    {
        get => _hedefDepoId;
        set => OzellikAyarla(ref _hedefDepoId, value);
    }

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
        var urun = Urunler.FirstOrDefault(u => u.Id == bulunan.Value.Urun.Id);
        if (urun == null) { urun = bulunan.Value.Urun; Urunler.Add(urun); }
        SeciliUrun = urun;
        Miktar = bulunan.Value.Miktar;
    }

    private string _depoStokMetni = string.Empty;
    public string DepoStokMetni { get => _depoStokMetni; private set => OzellikAyarla(ref _depoStokMetni, value); }

    private async Task DepoStokBilgisiGuncelleAsync()
    {
        var urun = SeciliUrun; var depoId = KaynakDepoId;
        if (urun == null || !depoId.HasValue) { DepoStokMetni = string.Empty; return; }
        try
        {
            var stoklar = await _urunServisi.DepoStoklariniGetirAsync(depoId.Value);
            if (SeciliUrun != urun || KaynakDepoId != depoId) return;
            DepoStokMetni = $"Kaynak depodaki stok: {(stoklar.TryGetValue(urun.Id, out var m) ? m : 0):#,##0.###} {urun.Birim}";
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

    public string TransferNo
    {
        get => _transferNo;
        set => OzellikAyarla(ref _transferNo, value);
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

    public StokTransferGorunumModeli(
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

    public async Task InitAsync(int? seciliUrunId = null)
    {
        Depolar.Clear();
        var depolar = await _depoServisi.DepolariGetirAsync();
        foreach (var d in depolar)
            Depolar.Add(new DepoOption(d.Id, $"{d.DepoAdi} ({d.DepoKodu})"));

        if (Depolar.Count >= 2)
        {
            KaynakDepoId = Depolar[1].Id; // Depo 1
            HedefDepoId = Depolar[0].Id; // Mağaza
        }

        Urunler.Clear();
        var urunler = await _urunServisi.TumunuGetirAsync();
        foreach (var u in urunler)
            Urunler.Add(u);

        if (seciliUrunId.HasValue && seciliUrunId > 0)
            SeciliUrun = Urunler.FirstOrDefault(u => u.Id == seciliUrunId.Value);
        else
            SeciliUrun = null; // ilk ürünü kendiliğinden seçmek yanlış ürüne fiş kesilmesine yol açıyordu
    }

    public async Task KaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (!KaynakDepoId.HasValue || !HedefDepoId.HasValue)
        {
            HataMesaji = "Kaynak ve Hedef depo seçilmelidir.";
            return;
        }

        if (KaynakDepoId.Value == HedefDepoId.Value)
        {
            HataMesaji = "Kaynak ve Hedef depo aynı olamaz.";
            return;
        }

        if (SeciliUrun == null)
        {
            HataMesaji = "Transfer edilecek ürün seçilmelidir.";
            return;
        }

        if (Miktar <= 0)
        {
            HataMesaji = "Miktar 0'dan büyük olmalıdır.";
            return;
        }

        Kaydediliyor = true;

        try
        {
            var istek = new StokTransferIstegi(
                KaynakDepoId.Value,
                HedefDepoId.Value,
                SeciliUrun.Id,
                Miktar,
                BirimFiyat,
                Tarih,
                TransferNo,
                Aciklama
            );

            int kullaniciId = _kullaniciServisi.AktifKullanici?.Id ?? 1;
            var res = await _operasyonServisi.TransferOlusturAsync(istek, _firmaBaglami.SirketId, kullaniciId);

            BasariMesaji = $"Transfer başarıyla gerçekleşti ({res.FisNo}).";
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
