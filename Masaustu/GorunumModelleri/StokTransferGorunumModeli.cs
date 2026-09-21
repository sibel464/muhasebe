using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.StokOperasyon;
using Masaustu.Yardimcilar;
using EtkiAlani.Modeller.Stok;

namespace Masaustu.GorunumModelleri;

public class StokTransferGorunumModeli : TemelGorunumModeli
{
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
        set => OzellikAyarla(ref _kaynakDepoId, value);
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
            if (OzellikAyarla(ref _seciliUrun, value) && value != null)
                BirimFiyat = value.AlisFiyati;
        }
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
        _operasyonServisi = operasyonServisi;
        _urunServisi = urunServisi;
        _depoServisi = depoServisi;
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
            SeciliUrun = Urunler.FirstOrDefault();
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
