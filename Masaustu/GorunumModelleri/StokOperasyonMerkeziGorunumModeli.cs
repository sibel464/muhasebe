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
        set { if (OzellikAyarla(ref _aramaMetni, value)) _ = YazarkenAraAsync(value); }
    }

    private bool _pasifleriGoster;
    private int _yuklemeNo;
    private readonly SemaphoreSlim _sorguKilidi = new(1, 1);
    public bool PasifleriGoster
    {
        get => _pasifleriGoster;
        set { if (OzellikAyarla(ref _pasifleriGoster, value)) _ = YukleAsync(); }
    }

    // Her tuşta sorgu atmamak için yazma bitince (kısa bekleme sonrası) aranır.
    private async Task YazarkenAraAsync(string metin)
    {
        await Task.Delay(350);
        if (metin == AramaMetni)
            await YukleAsync();
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
    public ICommand ExcelAktarKomutu { get; }
    public ICommand YeniUrunKomutu { get; }
    public ICommand UrunDuzenleKomutu { get; }
    public ICommand Stok360AcKomutu { get; }
    public ICommand StokGirisiKomutu { get; }
    public ICommand StokCikisiKomutu { get; }
    public ICommand TransferKomutu { get; }
    public ICommand SayimKomutu { get; }
    public ICommand EtiketYazdirKomutu { get; }

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
        Masaustu.Servisler.NavigasyonServisi navigasyon,
        IBelgeYazdirmaServisi? yazdirmaServisi = null)
    {
        _operasyonServisi = operasyonServisi;
        // Raf etiketi: fiyat KDV dahil (kayıtlı satış fiyatı KDV hariçtir); her ürün istenen adet kadar basılır.
        EtiketYazdirKomutu = new KomutSarici(async _ =>
        {
            if (yazdirmaServisi == null) return;
            var secim = Masaustu.Gorunumler.Sayfa.EtiketPenceresi.Goster(SeciliSatir?.UrunAdi, Urunler.Count);
            if (secim == null) return;
            try
            {
                var idler = (secim.Tumu ? Urunler.Select(u => u.UrunId) : SeciliSatir != null ? [SeciliSatir.UrunId] : []).ToHashSet();
                var urunler = (await urunServisi.TumunuGetirAsync()).Where(u => idler.Contains(u.Id)).OrderBy(u => u.Ad).ToList();
                var etiketler = urunler.SelectMany(u => Enumerable.Repeat(new UrunEtiketi(u.Ad, u.Barkod,
                    decimal.Round(u.PerakendeSatisFiyati * (1 + u.KdvOrani / 100m), 2, MidpointRounding.AwayFromZero), u.Birim), secim.Adet)).ToList();
                if (etiketler.Count == 0) { System.Windows.MessageBox.Show("Etiketi basılacak ürün yok.", "Etiket", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information); return; }
                var fiyatsiz = urunler.Count(u => u.PerakendeSatisFiyati <= 0);
                if (fiyatsiz > 0 && System.Windows.MessageBox.Show($"{fiyatsiz} ürünün satış fiyatı girilmemiş; etiketlerinde 0,00 ₺ yazacak. Devam edilsin mi?", "Etiket",
                        System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;
                await yazdirmaServisi.EtiketYazdirAsync(etiketler, secim.Duzen);
            }
            catch (Exception ex) { System.Windows.MessageBox.Show("Etiketler oluşturulamadı: " + ex.Message, "Etiket", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning); }
        }, _ => Urunler.Count > 0);

        ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.Aktar(Urunler, "Urun_Stok_Listesi"), _ => Urunler.Count > 0);
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
        TemizleKomutu = new KomutSarici(async _ => { AramaMetni = string.Empty; SeciliDepoId = null; SadeceKritikStok = false; PasifleriGoster = false; await YukleAsync(); });
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
        // Üst üste başlayan yüklemelerde (hızlı yazma, filtre değişimi) yalnızca en sonuncunun sonucu gösterilir.
        var yuklemeNo = ++_yuklemeNo;
        Yukleniyor = true;
        DurumMesaji = "Stok matrisi yükleniyor...";

        // Servisin veritabanı bağlamı aynı anda tek sorgu çalıştırabilir; sorgular sıraya alınır.
        await _sorguKilidi.WaitAsync();
        try
        {
            if (yuklemeNo != _yuklemeNo)
                return;
            var result = await _operasyonServisi.GetMatrisOzetAsync(
                _firmaBaglami.SirketId,
                AramaMetni,
                SeciliDepoId,
                SadeceKritikStok
            );
            if (yuklemeNo != _yuklemeNo)
                return;

            Urunler.Clear();

            var pasifSayisi = 0;
            foreach (var r in result)
            {
                if (!r.Aktif && !PasifleriGoster) { pasifSayisi++; continue; }
                Urunler.Add(r);
            }

            BildirimGonder(nameof(ToplamFizikselStok));
            BildirimGonder(nameof(ToplamKullanilabilirStok));
            BildirimGonder(nameof(ToplamRezerveStok));
            BildirimGonder(nameof(MagazaToplamStok));
            BildirimGonder(nameof(Depo1ToplamStok));
            BildirimGonder(nameof(Depo2ToplamStok));
            BildirimGonder(nameof(KritikStokSayisi));

            DurumMesaji = pasifSayisi > 0
                ? $"{Urunler.Count} ürün listelendi ({pasifSayisi} pasif ürün gizli)."
                : $"{Urunler.Count} ürün listelendi.";
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "StokOperasyonMerkezi");
            DurumMesaji = "Stok verileri yüklenirken bir hata oluştu.";
        }
        finally
        {
            _sorguKilidi.Release();
            if (yuklemeNo == _yuklemeNo)
                Yukleniyor = false;
        }
    }
}

public record DepoOption(int? Id, string Ad);
