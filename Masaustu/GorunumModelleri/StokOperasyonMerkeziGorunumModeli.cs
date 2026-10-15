using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.StokOperasyon;
using Masaustu.Yardimcilar;
using EtkiAlani.Modeller.Stok;

namespace Masaustu.GorunumModelleri;

public class StokOperasyonMerkeziGorunumModeli : TemelGorunumModeli
{
    private readonly IStokOperasyonServisi _operasyonServisi;
    private readonly IUrunServisi _urunServisi;
    private readonly IDepoServisi _depoServisi;
    private readonly IAktifFirmaBaglami _firmaBaglami;
    private readonly IKullaniciServisi _kullaniciServisi;

    private string _aramaMetni = string.Empty;
    private int? _seciliDepoId;
    private bool _sadeceKritikStok;
    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;
    private StokOperasyonMatrisOzetDto? _seciliSatir;

    public ObservableCollection<StokOperasyonMatrisOzetDto> Urunler { get; } = new();
    public ObservableCollection<DepoOption> Depolar { get; } = new();

    public string AramaMetni
    {
        get => _aramaMetni;
        set => OzellikAyarla(ref _aramaMetni, value);
    }

    public int? SeciliDepoId
    {
        get => _seciliDepoId;
        set { if (OzellikAyarla(ref _seciliDepoId, value)) _ = YukleAsync(); }
    }

    public bool SadeceKritikStok
    {
        get => _sadeceKritikStok;
        set { if (OzellikAyarla(ref _sadeceKritikStok, value)) _ = YukleAsync(); }
    }

    public bool Yukleniyor
    {
        get => _yukleniyor;
        private set { OzellikAyarla(ref _yukleniyor, value); CommandManager.InvalidateRequerySuggested(); }
    }

    public string DurumMesaji
    {
        get => _durumMesaji;
        private set => OzellikAyarla(ref _durumMesaji, value);
    }

    public StokOperasyonMatrisOzetDto? SeciliSatir
    {
        get => _seciliSatir;
        set => OzellikAyarla(ref _seciliSatir, value);
    }

    // KPI Summary
    public decimal ToplamFizikselStok => Urunler.Sum(x => x.ToplamFiziksel);
    public decimal ToplamKullanilabilirStok => Urunler.Sum(x => x.ToplamKullanilabilir);
    public decimal ToplamRezerveStok => Urunler.Sum(x => x.ToplamRezerve);
    public decimal MagazaToplamStok => Urunler.Sum(x => x.MagazaFiziksel);
    public decimal Depo1ToplamStok => Urunler.Sum(x => x.Depo1Fiziksel);
    public decimal Depo2ToplamStok => Urunler.Sum(x => x.Depo2Fiziksel);
    public int KritikStokSayisi => Urunler.Count(x => x.StokDurumu == "Kritik Stok" || x.StokDurumu == "Stok Yok");

    public ICommand AraKomutu { get; }
    public ICommand TemizleKomutu { get; }
    public ICommand YenileKomutu { get; }
    public ICommand YeniUrunKomutu { get; }
    public ICommand UrunDuzenleKomutu { get; }
    public ICommand Stok360AcKomutu { get; }
    public ICommand StokGirisiKomutu { get; }
    public ICommand StokCikisiKomutu { get; }
    public ICommand TransferKomutu { get; }
    public ICommand SayimKomutu { get; }

    public Action<int>? Stok360AcAction { get; set; }
    public Action<int, bool>? StokIslemAcAction { get; set; }
    public Action<int?>? TransferAcAction { get; set; }
    public Action? SayimAcAction { get; set; }

    public StokOperasyonMerkeziGorunumModeli(
        IStokOperasyonServisi operasyonServisi,
        IUrunServisi urunServisi,
        IDepoServisi depoServisi,
        IAktifFirmaBaglami firmaBaglami,
        IKullaniciServisi kullaniciServisi,
        Masaustu.Servisler.NavigasyonServisi navigasyon)
    {
        _operasyonServisi = operasyonServisi;

        YeniUrunKomutu = new KomutSarici(_ => navigasyon.Git("UrunEkle"),
            _ => _kullaniciServisi!.YetkiliMi(x => x.StokEkle));
        UrunDuzenleKomutu = new KomutSarici(_ =>
        {
            var id = SeciliSatir?.UrunId ?? 0;
            if (id > 0)
                navigasyon.Git("UrunEkle", gm => _ = ((UrunEkleGorunumModeli)gm).DuzenlemeIcinYukleAsync(id));
        }, _ => SeciliSatir != null && _kullaniciServisi!.YetkiliMi(x => x.StokDuzenle));
        _urunServisi = urunServisi;
        _depoServisi = depoServisi;
        _firmaBaglami = firmaBaglami;
        _kullaniciServisi = kullaniciServisi;

        AraKomutu = new KomutSarici(async _ => await YukleAsync());
        TemizleKomutu = new KomutSarici(async _ => { AramaMetni = string.Empty; SeciliDepoId = null; SadeceKritikStok = false; await YukleAsync(); });
        YenileKomutu = new KomutSarici(async _ => await YukleAsync());

        Stok360AcKomutu = new KomutSarici(p => {
            int id = p is int i ? i : (SeciliSatir?.UrunId ?? 0);
            if (id > 0) Stok360AcAction?.Invoke(id);
        });

        StokGirisiKomutu = new KomutSarici(p => {
            int id = p is int i ? i : (SeciliSatir?.UrunId ?? 0);
            StokIslemAcAction?.Invoke(id, true);
        }, _ => _kullaniciServisi.YetkiliMi(x => x.StokEkle || x.StokDuzenle));

        StokCikisiKomutu = new KomutSarici(p => {
            int id = p is int i ? i : (SeciliSatir?.UrunId ?? 0);
            StokIslemAcAction?.Invoke(id, false);
        }, _ => _kullaniciServisi.YetkiliMi(x => x.StokSil || x.StokDuzenle));

        TransferKomutu = new KomutSarici(p => {
            int? id = p is int i && i > 0 ? i : SeciliSatir?.UrunId;
            TransferAcAction?.Invoke(id);
        }, _ => _kullaniciServisi.YetkiliMi(x => x.StokDuzenle));

        SayimKomutu = new KomutSarici(_ => {
            SayimAcAction?.Invoke();
        }, _ => _kullaniciServisi.YetkiliMi(x => x.StokEkle || x.StokDuzenle));
    }

    public async Task InitAsync()
    {
        Depolar.Clear();
        Depolar.Add(new DepoOption(null, "Tüm Lokasyonlar"));
        var list = await _depoServisi.DepolariGetirAsync();
        foreach (var d in list)
            Depolar.Add(new DepoOption(d.Id, $"{d.DepoAdi} ({d.DepoKodu})"));

        await YukleAsync();
    }

    public async Task YukleAsync()
    {
        Yukleniyor = true;
        DurumMesaji = "Stok matrisi yükleniyor...";
        Urunler.Clear();

        try
        {
            var result = await _operasyonServisi.GetMatrisOzetAsync(
                _firmaBaglami.SirketId,
                AramaMetni,
                SeciliDepoId,
                SadeceKritikStok
            );

            foreach (var r in result)
                Urunler.Add(r);

            BildirimGonder(nameof(ToplamFizikselStok));
            BildirimGonder(nameof(ToplamKullanilabilirStok));
            BildirimGonder(nameof(ToplamRezerveStok));
            BildirimGonder(nameof(MagazaToplamStok));
            BildirimGonder(nameof(Depo1ToplamStok));
            BildirimGonder(nameof(Depo2ToplamStok));
            BildirimGonder(nameof(KritikStokSayisi));

            DurumMesaji = $"{Urunler.Count} ürün listelendi.";
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "StokOperasyonMerkezi");
            DurumMesaji = "Stok verileri yüklenirken bir hata oluştu.";
        }
        finally
        {
            Yukleniyor = false;
        }
    }
}

public record DepoOption(int? Id, string Ad);
