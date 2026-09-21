using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;
using Uygulama.Dto.Depo;

namespace Masaustu.GorunumModelleri;

public class DepoGorunumModeli : TemelGorunumModeli
{
    private readonly IDepoServisi _depoServisi;
    private readonly IUrunServisi _urunServisi;
    private readonly IBelgeYazdirmaServisi _yazdirmaServisi;

    private ObservableCollection<Depo> _depolar = new();
    private ObservableCollection<DepoStokMatrisDto> _stokMatrisi = new();
    private ObservableCollection<DepoHareketDetayDto> _hareketler = new();
    private ObservableCollection<DepoTransferFisi> _transferFisleri = new();
    private ObservableCollection<Urun> _urunler = new();
    private ObservableCollection<DepoTransferKalemi> _transferKalemleri = new();

    private readonly ICollectionView _stokMatrisiView;

    private decimal _toplamStokAdedi;
    private decimal _toplamStokDegeri;
    private decimal _magazaToplamStok;
    private decimal _depo1ToplamStok;
    private decimal _depo2ToplamStok;

    private string _aramaMetni = string.Empty;
    private string _stokDurumuFiltresi = "Tümü";
    private string _lokasyonFiltresi = "Tümü";
    private string _siralamaSecimi = "Ürün adına göre";
    private decimal? _minimumMiktar;
    private decimal? _maksimumMiktar;
    private bool _yalnizStoktaOlanlar;
    private int _filtrelenmisKayitSayisi;
    private Depo? _filtreDepo;

    // Transfer State
    private Depo? _seciliKaynakDepo;
    private Depo? _seciliHedefDepo;
    private Urun? _seciliTransferUrun;
    private decimal _transferMiktar = 1;
    private string _transferAciklama = string.Empty;
    private string _transferBelgeNo = string.Empty;
    private string _transferFisNo = string.Empty;

    // Sayım State
    private Depo? _seciliSayimDepo;
    private Urun? _seciliSayimUrun;
    private decimal _sayilanMiktar;
    private string _sayimAciklama = string.Empty;

    // Yeni Depo Tanımlama
    private string _yeniDepoKodu = string.Empty;
    private string _yeniDepoAdi = string.Empty;
    private DepoTuru _yeniDepoTuru = DepoTuru.Depo;
    private string _yeniSorumlu = string.Empty;
    private string _yeniTelefon = string.Empty;
    private string _yeniAdres = string.Empty;

    // Bildirim
    private string _basariMesaji = string.Empty;
    private string _hataMesaji = string.Empty;
    private bool _yukleniyor;

    // Commands
    public ICommand YenileKomutu { get; }
    public ICommand TransferKalemEkleKomutu { get; }
    public ICommand TransferKalemSilKomutu { get; }
    public ICommand TransferFisiKaydetKomutu { get; }
    public ICommand SayimKaydetKomutu { get; }
    public ICommand YeniDepoKaydetKomutu { get; }
    public ICommand DepoSilKomutu { get; }
    public ICommand DokumYazdirKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }
    public ICommand FiltreleriTemizleKomutu { get; }

    public DepoGorunumModeli(
        IDepoServisi depoServisi,
        IUrunServisi urunServisi,
        IBelgeYazdirmaServisi yazdirmaServisi)
    {
        _depoServisi = depoServisi;
        _urunServisi = urunServisi;
        _yazdirmaServisi = yazdirmaServisi;

        _stokMatrisiView = CollectionViewSource.GetDefaultView(_stokMatrisi);
        _stokMatrisiView.Filter = StokFiltrele;

        ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.AktarCsv(FiltrelenmisStokMatrisi.Cast<DepoStokMatrisDto>().Select(m => new { m.UrunKodu, m.UrunAdi, m.Barkod, m.MagazaMiktar, m.Depo1Miktar, m.Depo2Miktar, m.ToplamMiktar, m.MinStok, m.AlisFiyati, m.ToplamDeger, m.DurumMetni }), "Depo_Stok_Matrisi"));
        FiltreleriTemizleKomutu = new KomutSarici(FiltreleriTemizle);
        YenileKomutu = new KomutSarici(async () => await VerileriYukleAsync());
        TransferKalemEkleKomutu = new KomutSarici(TransferKalemEkle);
        TransferKalemSilKomutu = new KomutSarici(p => TransferKalemSil(p as DepoTransferKalemi));
        TransferFisiKaydetKomutu = new KomutSarici(async () => await TransferFisiKaydetAsync());
        SayimKaydetKomutu = new KomutSarici(async () => await SayimKaydetAsync());
        YeniDepoKaydetKomutu = new KomutSarici(async () => await YeniDepoKaydetAsync());
        DepoSilKomutu = new KomutSarici(async p => await DepoSilAsync(p as Depo));
        DokumYazdirKomutu = new KomutSarici(DokumYazdir);

        _ = IlkYuklemeAsync();
    }

    public ObservableCollection<Depo> Depolar
    {
        get => _depolar;
        set => OzellikAyarla(ref _depolar, value);
    }

    public ObservableCollection<DepoStokMatrisDto> StokMatrisi
    {
        get => _stokMatrisi;
        set => OzellikAyarla(ref _stokMatrisi, value);
    }

    public ICollectionView FiltrelenmisStokMatrisi => _stokMatrisiView;

    public ObservableCollection<DepoHareketDetayDto> Hareketler
    {
        get => _hareketler;
        set => OzellikAyarla(ref _hareketler, value);
    }

    public ObservableCollection<DepoTransferFisi> TransferFisleri
    {
        get => _transferFisleri;
        set => OzellikAyarla(ref _transferFisleri, value);
    }

    public ObservableCollection<Urun> Urunler
    {
        get => _urunler;
        set => OzellikAyarla(ref _urunler, value);
    }

    public ObservableCollection<DepoTransferKalemi> TransferKalemleri
    {
        get => _transferKalemleri;
        set => OzellikAyarla(ref _transferKalemleri, value);
    }

    public decimal ToplamStokAdedi
    {
        get => _toplamStokAdedi;
        set => OzellikAyarla(ref _toplamStokAdedi, value);
    }

    public decimal ToplamStokDegeri
    {
        get => _toplamStokDegeri;
        set => OzellikAyarla(ref _toplamStokDegeri, value);
    }

    public decimal MagazaToplamStok
    {
        get => _magazaToplamStok;
        set => OzellikAyarla(ref _magazaToplamStok, value);
    }

    public decimal Depo1ToplamStok
    {
        get => _depo1ToplamStok;
        set => OzellikAyarla(ref _depo1ToplamStok, value);
    }

    public decimal Depo2ToplamStok
    {
        get => _depo2ToplamStok;
        set => OzellikAyarla(ref _depo2ToplamStok, value);
    }

    public string AramaMetni
    {
        get => _aramaMetni;
        set
        {
            if (OzellikAyarla(ref _aramaMetni, value))
            {
                StokAramasiniYenile();
            }
        }
    }

    public IReadOnlyList<string> StokDurumuSecenekleri { get; } = ["Tümü", "Yeterli", "Kritik Seviye", "Tükendi"];
    public IReadOnlyList<string> LokasyonSecenekleri { get; } = ["Tümü", "Mağaza", "Depo 1", "Depo 2"];
    public IReadOnlyList<string> SiralamaSecenekleri { get; } = ["Ürün adına göre", "Stok: yüksekten düşüğe", "Stok: düşükten yükseğe", "Değer: yüksekten düşüğe"];

    public string StokDurumuFiltresi
    {
        get => _stokDurumuFiltresi;
        set { if (OzellikAyarla(ref _stokDurumuFiltresi, value)) StokAramasiniYenile(); }
    }

    public string LokasyonFiltresi
    {
        get => _lokasyonFiltresi;
        set { if (OzellikAyarla(ref _lokasyonFiltresi, value)) StokAramasiniYenile(); }
    }

    public string SiralamaSecimi
    {
        get => _siralamaSecimi;
        set { if (OzellikAyarla(ref _siralamaSecimi, value)) StokAramasiniYenile(); }
    }

    public decimal? MinimumMiktar
    {
        get => _minimumMiktar;
        set { if (OzellikAyarla(ref _minimumMiktar, value)) StokAramasiniYenile(); }
    }

    public decimal? MaksimumMiktar
    {
        get => _maksimumMiktar;
        set { if (OzellikAyarla(ref _maksimumMiktar, value)) StokAramasiniYenile(); }
    }

    public bool YalnizStoktaOlanlar
    {
        get => _yalnizStoktaOlanlar;
        set { if (OzellikAyarla(ref _yalnizStoktaOlanlar, value)) StokAramasiniYenile(); }
    }

    public int FiltrelenmisKayitSayisi
    {
        get => _filtrelenmisKayitSayisi;
        private set => OzellikAyarla(ref _filtrelenmisKayitSayisi, value);
    }

    public string FiltreSonucOzeti => $"{FiltrelenmisKayitSayisi} / {StokMatrisi.Count} ürün gösteriliyor";

    public Depo? FiltreDepo
    {
        get => _filtreDepo;
        set { if (OzellikAyarla(ref _filtreDepo, value)) _ = HareketleriFiltreleAsync(); }
    }

    public Depo? SeciliKaynakDepo
    {
        get => _seciliKaynakDepo;
        set => OzellikAyarla(ref _seciliKaynakDepo, value);
    }

    public Depo? SeciliHedefDepo
    {
        get => _seciliHedefDepo;
        set => OzellikAyarla(ref _seciliHedefDepo, value);
    }

    public Urun? SeciliTransferUrun
    {
        get => _seciliTransferUrun;
        set => OzellikAyarla(ref _seciliTransferUrun, value);
    }

    public decimal TransferMiktar
    {
        get => _transferMiktar;
        set => OzellikAyarla(ref _transferMiktar, value);
    }

    public string TransferAciklama
    {
        get => _transferAciklama;
        set => OzellikAyarla(ref _transferAciklama, value);
    }

    public string TransferBelgeNo
    {
        get => _transferBelgeNo;
        set => OzellikAyarla(ref _transferBelgeNo, value);
    }

    public string TransferFisNo
    {
        get => _transferFisNo;
        set => OzellikAyarla(ref _transferFisNo, value);
    }

    private decimal _mevcutSayimDepoStogu;
    public decimal MevcutSayimDepoStogu
    {
        get => _mevcutSayimDepoStogu;
        set => OzellikAyarla(ref _mevcutSayimDepoStogu, value);
    }

    private decimal _sayimFarki;
    public decimal SayimFarki
    {
        get => _sayimFarki;
        set => OzellikAyarla(ref _sayimFarki, value);
    }

    public Depo? SeciliSayimDepo
    {
        get => _seciliSayimDepo;
        set 
        {
            if (OzellikAyarla(ref _seciliSayimDepo, value))
            {
                GuncelleSayimHesaplamalari();
            }
        }
    }

    public Urun? SeciliSayimUrun
    {
        get => _seciliSayimUrun;
        set 
        {
            if (OzellikAyarla(ref _seciliSayimUrun, value))
            {
                GuncelleSayimHesaplamalari();
            }
        }
    }

    public decimal SayilanMiktar
    {
        get => _sayilanMiktar;
        set 
        {
            if (OzellikAyarla(ref _sayilanMiktar, value))
            {
                SayimFarki = _sayilanMiktar - MevcutSayimDepoStogu;
            }
        }
    }

    private void GuncelleSayimHesaplamalari()
    {
        if (SeciliSayimDepo == null || SeciliSayimUrun == null)
        {
            MevcutSayimDepoStogu = 0;
            SayimFarki = 0;
            return;
        }

        var matrisItem = StokMatrisi.FirstOrDefault(m => m.UrunId == SeciliSayimUrun.Id);
        if (matrisItem == null)
        {
            MevcutSayimDepoStogu = 0;
        }
        else
        {
            MevcutSayimDepoStogu = DepoMiktariBul(matrisItem, SeciliSayimDepo);
        }

        SayimFarki = SayilanMiktar - MevcutSayimDepoStogu;
    }

    public string SayimAciklama
    {
        get => _sayimAciklama;
        set => OzellikAyarla(ref _sayimAciklama, value);
    }

    public string YeniDepoKodu
    {
        get => _yeniDepoKodu;
        set => OzellikAyarla(ref _yeniDepoKodu, value);
    }

    public string YeniDepoAdi
    {
        get => _yeniDepoAdi;
        set => OzellikAyarla(ref _yeniDepoAdi, value);
    }

    public DepoTuru YeniDepoTuru
    {
        get => _yeniDepoTuru;
        set => OzellikAyarla(ref _yeniDepoTuru, value);
    }

    public string YeniSorumlu
    {
        get => _yeniSorumlu;
        set => OzellikAyarla(ref _yeniSorumlu, value);
    }

    public string YeniTelefon
    {
        get => _yeniTelefon;
        set => OzellikAyarla(ref _yeniTelefon, value);
    }

    public string YeniAdres
    {
        get => _yeniAdres;
        set => OzellikAyarla(ref _yeniAdres, value);
    }

    public string BasariMesaji
    {
        get => _basariMesaji;
        set => OzellikAyarla(ref _basariMesaji, value);
    }

    public string HataMesaji
    {
        get => _hataMesaji;
        set => OzellikAyarla(ref _hataMesaji, value);
    }

    public bool Yukleniyor
    {
        get => _yukleniyor;
        set => OzellikAyarla(ref _yukleniyor, value);
    }

    private bool StokFiltrele(object obj)
    {
        if (obj is not DepoStokMatrisDto item) return false;

        var metin = $"{item.UrunKodu} {item.UrunAdi} {item.Barkod} {item.Birim} {item.DurumMetni}";
        var kelimeler = (AramaMetni ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (kelimeler.Any(k => metin.IndexOf(k, StringComparison.CurrentCultureIgnoreCase) < 0)) return false;
        if (YalnizStoktaOlanlar && item.ToplamMiktar <= 0) return false;
        if (MinimumMiktar.HasValue && item.ToplamMiktar < MinimumMiktar.Value) return false;
        if (MaksimumMiktar.HasValue && item.ToplamMiktar > MaksimumMiktar.Value) return false;
        if (StokDurumuFiltresi != "Tümü" && item.DurumMetni != StokDurumuFiltresi) return false;

        return LokasyonFiltresi switch
        {
            "Mağaza" => item.MagazaMiktar > 0,
            "Depo 1" => item.Depo1Miktar > 0,
            "Depo 2" => item.Depo2Miktar > 0,
            _ => true
        };
    }

    private void StokAramasiniYenile()
    {
        _stokMatrisiView.SortDescriptions.Clear();
        var (alan, yon) = SiralamaSecimi switch
        {
            "Stok: yüksekten düşüğe" => (nameof(DepoStokMatrisDto.ToplamMiktar), ListSortDirection.Descending),
            "Stok: düşükten yükseğe" => (nameof(DepoStokMatrisDto.ToplamMiktar), ListSortDirection.Ascending),
            "Değer: yüksekten düşüğe" => (nameof(DepoStokMatrisDto.ToplamDeger), ListSortDirection.Descending),
            _ => (nameof(DepoStokMatrisDto.UrunAdi), ListSortDirection.Ascending)
        };
        _stokMatrisiView.SortDescriptions.Add(new SortDescription(alan, yon));
        _stokMatrisiView.Refresh();
        FiltrelenmisKayitSayisi = _stokMatrisiView.Cast<object>().Count();
        BildirimGonder(nameof(FiltreSonucOzeti));
    }

    private void FiltreleriTemizle()
    {
        AramaMetni = string.Empty;
        StokDurumuFiltresi = "Tümü";
        LokasyonFiltresi = "Tümü";
        MinimumMiktar = null;
        MaksimumMiktar = null;
        YalnizStoktaOlanlar = false;
        SiralamaSecimi = "Ürün adına göre";
        StokAramasiniYenile();
    }

    public async Task IlkYuklemeAsync()
    {
        await VerileriYukleAsync();
    }

    public async Task VerileriYukleAsync()
    {
        Yukleniyor = true;
        BasariMesaji = string.Empty;
        HataMesaji = string.Empty;

        try
        {
            var depolar = await _depoServisi.DepolariGetirAsync();
            Depolar.Clear();
            foreach (var d in depolar) Depolar.Add(d);

            if (SeciliKaynakDepo == null && Depolar.Count >= 2)
            {
                SeciliKaynakDepo = Depolar.FirstOrDefault(d => d.DepoKodu.Contains("DEP-01")) ?? Depolar[0];
                SeciliHedefDepo = Depolar.FirstOrDefault(d => d.DepoTuru == DepoTuru.Magaza) ?? Depolar[1];
            }

            if (SeciliSayimDepo == null && Depolar.Any())
            {
                SeciliSayimDepo = Depolar[0];
            }

            var urunler = await _urunServisi.TumunuGetirAsync();
            Urunler.Clear();
            foreach (var u in urunler) Urunler.Add(u);

            var matris = await _depoServisi.DepoStokMatrisiGetirAsync();
            StokMatrisi.Clear();
            foreach (var m in matris) StokMatrisi.Add(m);
            StokAramasiniYenile();

            // KPI Hesaplamaları
            ToplamStokAdedi = matris.Sum(m => m.ToplamMiktar);
            ToplamStokDegeri = matris.Sum(m => m.ToplamDeger);
            MagazaToplamStok = matris.Sum(m => m.MagazaMiktar);
            Depo1ToplamStok = matris.Sum(m => m.Depo1Miktar);
            Depo2ToplamStok = matris.Sum(m => m.Depo2Miktar);

            var hareketler = await _depoServisi.DepoHareketleriGetirAsync();
            Hareketler.Clear();
            foreach (var h in hareketler) Hareketler.Add(h);

            var transferler = await _depoServisi.TransferFisleriGetirAsync();
            TransferFisleri.Clear();
            foreach (var t in transferler) TransferFisleri.Add(t);
        }
        catch (Exception ex)
        {
            HataMesaji = $"Veriler yüklenirken hata oluştu: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    public void TransferKalemEkle()
    {
        if (SeciliTransferUrun == null)
        {
            HataMesaji = "Lütfen sevk edilecek bir ürün seçin.";
            return;
        }

        if (TransferMiktar <= 0)
        {
            HataMesaji = "Miktar 0'dan büyük olmalıdır.";
            return;
        }

        if (SeciliKaynakDepo != null)
        {
            var matrisItem = StokMatrisi.FirstOrDefault(m => m.UrunId == SeciliTransferUrun.Id);
            if (matrisItem != null)
            {
                var kaynakStok = DepoMiktariBul(matrisItem, SeciliKaynakDepo);

                var mevcutEklenenMiktar = TransferKalemleri.Where(k => k.UrunId == SeciliTransferUrun.Id).Sum(k => k.Miktar);
                if (mevcutEklenenMiktar + TransferMiktar > kaynakStok)
                {
                    HataMesaji = $"Kaynak depoda yeterli stok yok. Kullanılabilir: {kaynakStok:N2} {matrisItem.Birim}.";
                    return;
                }

                HataMesaji = string.Empty;
            }
        }

        var mevcutKalem = TransferKalemleri.FirstOrDefault(k => k.UrunId == SeciliTransferUrun.Id);
        if (mevcutKalem != null)
        {
            mevcutKalem.Miktar += TransferMiktar;
        }
        else
        {
            TransferKalemleri.Add(new DepoTransferKalemi
            {
                UrunId = SeciliTransferUrun.Id,
                Urun = SeciliTransferUrun,
                Miktar = TransferMiktar,
                BirimFiyat = SeciliTransferUrun.AlisFiyati,
                Not = TransferAciklama
            });
        }

        BasariMesaji = $"{SeciliTransferUrun.Ad} transfer listesine eklendi.";
        TransferMiktar = 1;
    }

    public void TransferKalemSil(DepoTransferKalemi? kalem)
    {
        if (kalem != null)
        {
            TransferKalemleri.Remove(kalem);
        }
    }

    public async Task TransferFisiKaydetAsync()
    {
        if (SeciliKaynakDepo == null || SeciliHedefDepo == null)
        {
            HataMesaji = "Lütfen Kaynak Depo ve Hedef Depoyu seçin.";
            return;
        }

        if (SeciliKaynakDepo.Id == SeciliHedefDepo.Id)
        {
            HataMesaji = "Kaynak ve Hedef depo aynı olamaz!";
            return;
        }

        if (!TransferKalemleri.Any())
        {
            HataMesaji = "Transfer edilecek hiçbir ürün eklenmedi!";
            return;
        }

        Yukleniyor = true;
        HataMesaji = string.Empty;

        try
        {
            var fis = new DepoTransferFisi
            {
                FisNo = TransferFisNo,
                KaynakDepoId = SeciliKaynakDepo.Id,
                HedefDepoId = SeciliHedefDepo.Id,
                Tarih = DateTime.UtcNow,
                Aciklama = string.IsNullOrWhiteSpace(TransferAciklama) ? $"{SeciliKaynakDepo.DepoAdi} ➔ {SeciliHedefDepo.DepoAdi} Sevk" : TransferAciklama,
                BelgeNo = TransferBelgeNo,
                Kalemler = TransferKalemleri.ToList()
            };

            var kaydedilen = await _depoServisi.TransferFisiKaydetAsync(fis);
            TransferKalemleri.Clear();
            TransferAciklama = string.Empty;
            TransferBelgeNo = string.Empty;
            TransferFisNo = string.Empty;

            BasariMesaji = $"✅ Transfer Fişi ({kaydedilen.FisNo}) başarıyla kaydedildi ve stoklar güncellendi.";
            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Transfer kaydedilirken hata oluştu: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    public async Task SayimKaydetAsync()
    {
        if (SeciliSayimDepo == null || SeciliSayimUrun == null)
        {
            HataMesaji = "Lütfen Sayım yapılacak Depo ve Ürünü seçin.";
            return;
        }

        Yukleniyor = true;
        HataMesaji = string.Empty;

        try
        {
            await _depoServisi.DepoSayimFarkiKaydetAsync(SeciliSayimDepo.Id, SeciliSayimUrun.Id, SayilanMiktar, SayimAciklama);
            BasariMesaji = $"✅ {SeciliSayimDepo.DepoAdi} için {SeciliSayimUrun.Ad} sayımı ({SayilanMiktar:N0} adet) işlendi.";
            SayilanMiktar = 0;
            SayimAciklama = string.Empty;
            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Sayım kaydedilirken hata: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    public async Task YeniDepoKaydetAsync()
    {
        if (string.IsNullOrWhiteSpace(YeniDepoKodu) || string.IsNullOrWhiteSpace(YeniDepoAdi))
        {
            HataMesaji = "Depo Kodu ve Depo Adı zorunludur.";
            return;
        }

        Yukleniyor = true;
        HataMesaji = string.Empty;

        try
        {
            var yeniDepo = new Depo
            {
                SubeId = 1,
                DepoKodu = YeniDepoKodu.Trim(),
                DepoAdi = YeniDepoAdi.Trim(),
                DepoTuru = YeniDepoTuru,
                Sorumlu = YeniSorumlu,
                Telefon = YeniTelefon,
                Adres = YeniAdres
            };

            await _depoServisi.DepoEkleAsync(yeniDepo);
            BasariMesaji = $"✅ Yeni depo/mağaza ({yeniDepo.DepoAdi}) başarıyla kaydedildi.";

            YeniDepoKodu = string.Empty;
            YeniDepoAdi = string.Empty;
            YeniSorumlu = string.Empty;
            YeniTelefon = string.Empty;
            YeniAdres = string.Empty;

            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Depo eklenirken hata: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    public bool DepoSilinebilirMi(Depo? depo)
    {
        if (depo == null) return true;
        if (depo.DepoKodu != null && (depo.DepoKodu.Contains("MAG-01") || depo.DepoKodu.Contains("DEP-01") || depo.DepoKodu.Contains("DEP-02")))
            return false;
        return true;
    }

    public async Task DepoSilAsync(Depo? depo)
    {
        if (depo == null) return;

        // 1. Teyit Kutusu
        var onay1 = System.Windows.MessageBox.Show(
            $"'{depo.DepoAdi}' deposunu silmek istediğinizden emin misiniz?",
            "Depo Silme Onayı (1/2)",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (onay1 != System.Windows.MessageBoxResult.Yes)
            return;

        // 2. Teyit Kutusu (Çift Onay)
        var onay2 = System.Windows.MessageBox.Show(
            $"⚠️ DİKKAT: '{depo.DepoAdi}' deposunu kaldırıyorsunuz.\n\nGerçekten silmeyi 2. kez onaylıyor musunuz?",
            "Depo Silme Onayı (2/2 - Çift Teyit)",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (onay2 != System.Windows.MessageBoxResult.Yes)
            return;

        Yukleniyor = true;
        HataMesaji = string.Empty;

        try
        {
            await _depoServisi.DepoSilAsync(depo.Id);
            BasariMesaji = $"✅ Depo ({depo.DepoAdi}) başarıyla silindi.";
            await VerileriYukleAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = ex.Message;
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    public async void DokumYazdir()
    {
        try
        {
            var satirlar = string.Join(Environment.NewLine, FiltrelenmisStokMatrisi.Cast<DepoStokMatrisDto>().Select(m =>
                $"<tr><td>{System.Net.WebUtility.HtmlEncode(m.UrunKodu)}</td><td>{System.Net.WebUtility.HtmlEncode(m.UrunAdi)}</td>" +
                $"<td>{m.MagazaMiktar:N2}</td><td>{m.Depo1Miktar:N2}</td><td>{m.Depo2Miktar:N2}</td><td>{m.DigerDepolarMiktar:N2}</td><td>{m.ToplamMiktar:N2}</td></tr>"));
            var html = $$"""
                <!doctype html><html><head><meta charset="utf-8"><title>Depo Stok Raporu</title>
                <style>body{font-family:Segoe UI,Arial;margin:28px;color:#172033}h1{font-size:22px}table{width:100%;border-collapse:collapse}th,td{border:1px solid #cbd5e1;padding:7px;text-align:right}th:nth-child(-n+2),td:nth-child(-n+2){text-align:left}th{background:#eef2ff}</style></head>
                <body><h1>Mağaza ve Depo Stok Raporu</h1><p>Oluşturma: {{DateTime.Now:dd.MM.yyyy HH:mm}}</p>
                <table><thead><tr><th>Ürün Kodu</th><th>Ürün Adı</th><th>Mağaza</th><th>Depo 1</th><th>Depo 2</th><th>Diğer</th><th>Toplam</th></tr></thead><tbody>{{satirlar}}</tbody></table></body></html>
                """;
            await _yazdirmaServisi.BelgeyiGoruntuleVeYazdirAsync(html, "Depo-Stok-Raporu");
            BasariMesaji = "Depo stok raporu hazırlandı.";
        }
        catch (Exception ex)
        {
            HataMesaji = $"Stok raporu oluşturulamadı: {ex.Message}";
        }
    }

    private decimal DepoMiktariBul(DepoStokMatrisDto satir, Depo depo)
    {
        var magaza = Depolar.Where(d => d.DepoTuru == DepoTuru.Magaza).OrderBy(d => d.Id).FirstOrDefault();
        var normal = Depolar.Where(d => d.DepoTuru == DepoTuru.Depo).OrderBy(d => d.Id).ToList();
        if (depo.Id == magaza?.Id) return satir.MagazaMiktar;
        if (depo.Id == normal.ElementAtOrDefault(0)?.Id) return satir.Depo1Miktar;
        if (depo.Id == normal.ElementAtOrDefault(1)?.Id) return satir.Depo2Miktar;
        return satir.DigerDepolarMiktar;
    }

    private async Task HareketleriFiltreleAsync()
    {
        try
        {
            var liste = await _depoServisi.DepoHareketleriGetirAsync(FiltreDepo?.Id);
            Hareketler.Clear(); foreach (var hareket in liste) Hareketler.Add(hareket);
        }
        catch (Exception ex) { HataMesaji = $"Hareketler filtrelenemedi: {ex.Message}"; }
    }
}
