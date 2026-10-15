using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using Masaustu.Servisler;
using Masaustu.Gorunumler.Pencere;
using System.Windows;

namespace Masaustu.GorunumModelleri;

public class FaturaEkleGorunumModeli : TemelGorunumModeli
{
    private readonly IFaturaServisi _faturaServisi;
    private readonly ICariServisi _cariServisi;
    private readonly IUrunServisi _urunServisi;
    private readonly NavigasyonServisi _navigasyon;

    private FaturaTuru _tur = FaturaTuru.Satis;
    private string _faturaNo = string.Empty;
    private DateTime _tarih = DateTime.Today;
    private bool _isCariKilitli;
    private CariKart? _seciliCari;
    private Urun? _seciliUrun;
    private decimal _eklenecekMiktar = 1;
    private decimal _eklenecekBirimFiyat;
    private decimal _eklenecekIskonto;
    private string _aciklama = string.Empty;
    private decimal _genelIskontoOrani;

    private ObservableCollection<CariKart> _cariler = new();
    private ObservableCollection<Urun> _urunler = new();
    private ObservableCollection<FaturaKalemiGorunumModeli> _kalemler = new();
    private FaturaKalemiGorunumModeli? _seciliKalem;

    private string _hataMesaji = string.Empty;
    private string _basariMesaji = string.Empty;
    private bool _kaydediliyor;
    private DateTime? _vadeTarihi;
    private FaturaOdemeSekli? _odemeSekli;
    private string _referansNo = string.Empty;

    public bool IsCariKilitli
    {
        get => _isCariKilitli;
        set
        {
            if (OzellikAyarla(ref _isCariKilitli, value))
            {
                BildirimGonder(nameof(IsCariSecilebilir));
            }
        }
    }
    public bool IsCariSecilebilir => !IsCariKilitli;

    public FaturaTuru Tur
    {
        get => _tur;
        set
        {
            if (OzellikAyarla(ref _tur, value))
            {
                if (!IsCariKilitli) SeciliCari = null;
                _ = CarileriYukleAsync();
                if (SeciliUrun != null)
                {
                    EklenecekBirimFiyat = value == FaturaTuru.Alis ? SeciliUrun.AlisFiyati : SeciliUrun.PerakendeSatisFiyati;
                }
                BildirimGonder(nameof(IsSatisFaturasi));
                BildirimGonder(nameof(IsAlisFaturasi));
            }
        }
    }

    public bool IsSatisFaturasi { get => Tur == FaturaTuru.Satis; set { if (value) Tur = FaturaTuru.Satis; } }
    public bool IsAlisFaturasi { get => Tur == FaturaTuru.Alis; set { if (value) Tur = FaturaTuru.Alis; } }
    public Urun? SeciliKalemUrun { get => SeciliUrun; set => SeciliUrun = value; }
    private decimal _eklenecekKdvOrani;
    public decimal KalemMiktar { get => EklenecekMiktar; set => EklenecekMiktar = value; }
    public decimal KalemBirimFiyat { get => EklenecekBirimFiyat; set => EklenecekBirimFiyat = value; }
    public decimal KalemKdvOrani { get => EklenecekKdvOrani; set => EklenecekKdvOrani = value; }

    public string FaturaNo { get => _faturaNo; set => OzellikAyarla(ref _faturaNo, value); }
    public DateTime Tarih { get => _tarih; set => OzellikAyarla(ref _tarih, value); }
    
    public CariKart? SeciliCari
    {
        get => _seciliCari;
        set => OzellikAyarla(ref _seciliCari, value);
    }

    public Urun? SeciliUrun
    {
        get => _seciliUrun;
        set
        {
            if (OzellikAyarla(ref _seciliUrun, value))
            {
                BildirimGonder(nameof(SeciliUrunMetni));
                if (value != null)
                {
                    EklenecekBirimFiyat = Tur == FaturaTuru.Alis ? value.AlisFiyati : value.PerakendeSatisFiyati;
                    EklenecekKdvOrani = value.KdvOrani;
                    if (EklenecekMiktar <= 0) EklenecekMiktar = 1;
                }
            }
        }
    }

    public string SeciliUrunMetni => SeciliUrun != null 
        ? $"📦 {SeciliUrun.Kod} - {SeciliUrun.Ad} (Değiştir)" 
        : "🔍 + Ürün Seç";

    public decimal EklenecekMiktar
    {
        get => _eklenecekMiktar;
        set
        {
            if (OzellikAyarla(ref _eklenecekMiktar, value))
                BildirimGonder(nameof(KalemMiktarMetni));
        }
    }
    public decimal EklenecekBirimFiyat
    {
        get => _eklenecekBirimFiyat;
        set
        {
            if (OzellikAyarla(ref _eklenecekBirimFiyat, value))
                BildirimGonder(nameof(KalemBirimFiyatMetni));
        }
    }
    public decimal EklenecekKdvOrani
    {
        get => _eklenecekKdvOrani;
        set
        {
            if (OzellikAyarla(ref _eklenecekKdvOrani, value))
                BildirimGonder(nameof(KalemKdvOraniMetni));
        }
    }
    public decimal EklenecekIskonto { get => _eklenecekIskonto; set => OzellikAyarla(ref _eklenecekIskonto, value); }

    public string KalemMiktarMetni { get => EklenecekMiktar == 0 ? string.Empty : EklenecekMiktar.ToString("0.####"); set => EklenecekMiktar = MetinDecimalParse(value); }
    public string KalemBirimFiyatMetni { get => EklenecekBirimFiyat == 0 ? string.Empty : EklenecekBirimFiyat.ToString("0.####"); set => EklenecekBirimFiyat = MetinDecimalParse(value); }
    public string KalemKdvOraniMetni { get => EklenecekKdvOrani == 0 ? string.Empty : EklenecekKdvOrani.ToString("0.####"); set => EklenecekKdvOrani = MetinDecimalParse(value); }
    public string GenelIskontoOraniMetni { get => GenelIskontoOrani == 0 ? string.Empty : GenelIskontoOrani.ToString("0.####"); set => GenelIskontoOrani = MetinDecimalParse(value); }

    private static decimal MetinDecimalParse(string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0;
        if (decimal.TryParse(val.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d1)) return Math.Max(0, d1);
        if (decimal.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), out var d2)) return Math.Max(0, d2);
        return 0;
    }

    public string Aciklama { get => _aciklama; set => OzellikAyarla(ref _aciklama, value); }
    public decimal GenelIskontoOrani
    {
        get => _genelIskontoOrani;
        set { if (OzellikAyarla(ref _genelIskontoOrani, value)) ToplamlariHesapla(); }
    }

    public ObservableCollection<CariKart> Cariler { get => _cariler; set => OzellikAyarla(ref _cariler, value); }
    public ObservableCollection<Urun> Urunler { get => _urunler; set => OzellikAyarla(ref _urunler, value); }
    public ObservableCollection<FaturaKalemiGorunumModeli> Kalemler { get => _kalemler; set => OzellikAyarla(ref _kalemler, value); }
    
    public FaturaKalemiGorunumModeli? SeciliKalem
    {
        get => _seciliKalem;
        set => OzellikAyarla(ref _seciliKalem, value);
    }

    // Dip Toplamlar
    public decimal AraToplam => _kalemler.Sum(k => k.Miktar * k.BirimFiyat);
    public decimal SatirIskontosuToplam => _kalemler.Sum(k => k.IskontoTutari);
    public decimal GenelIskontoTutari => decimal.Round((AraToplam - SatirIskontosuToplam) * GenelIskontoOrani / 100, 2, MidpointRounding.AwayFromZero);
    public decimal KdvToplam => _kalemler.Sum(k => k.KdvTutari);
    public decimal GenelToplam => AraToplam - SatirIskontosuToplam - GenelIskontoTutari + KdvToplam;

    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }
    public string BasariMesaji { get => _basariMesaji; set => OzellikAyarla(ref _basariMesaji, value); }
    public bool Kaydediliyor { get => _kaydediliyor; set => OzellikAyarla(ref _kaydediliyor, value); }
    public DateTime? VadeTarihi { get => _vadeTarihi; set => OzellikAyarla(ref _vadeTarihi, value); }
    public FaturaOdemeSekli? OdemeSekli { get => _odemeSekli; set => OzellikAyarla(ref _odemeSekli, value); }
    public string ReferansNo { get => _referansNo; set => OzellikAyarla(ref _referansNo, value); }
    public string ParaBirimi => "TRY - Türk Lirası";
    public decimal Kur => 1m;
    public Action? Kapat { get; set; }

    public ICommand KalemEkleKomutu { get; }
    public ICommand KalemSilKomutu { get; }
    public ICommand TaslakKaydetKomutu { get; }
    public ICommand OnaylaVeKaydetKomutu { get; }
    public ICommand FormuTemizleKomutu { get; }
    public ICommand CariSecKomutu { get; }
    public ICommand UrunSecKomutu { get; }
    public ICommand StokDagilimiKomutu { get; }
    public ICommand KaydetKomutu { get; }
    public ICommand IptalKomutu { get; }
    public ICommand SatisTuruSecKomutu { get; }
    public ICommand AlisTuruSecKomutu { get; }

    public FaturaEkleGorunumModeli(
        IFaturaServisi faturaServisi, 
        ICariServisi cariServisi, 
        IUrunServisi urunServisi, 
        NavigasyonServisi navigasyon)
    {
        _faturaServisi = faturaServisi;
        _cariServisi = cariServisi;
        _urunServisi = urunServisi;
        _navigasyon = navigasyon;

        FaturaNoOlustur();

        KalemEkleKomutu = new KomutSarici(KalemEkle);
        KalemSilKomutu = new KomutSarici(param => { if (param is FaturaKalemiGorunumModeli k) KalemSil(k); });
        TaslakKaydetKomutu = new KomutSarici(async () => await FaturaKaydetAsync(BelgeDurumu.Taslak), () => !Kaydediliyor);
        OnaylaVeKaydetKomutu = new KomutSarici(async () => await FaturaKaydetAsync(BelgeDurumu.Onaylandi), () => !Kaydediliyor);
        FormuTemizleKomutu = new KomutSarici(FormuTemizle);
        CariSecKomutu = new KomutSarici(CariSec);
        UrunSecKomutu = new KomutSarici(UrunSec);
        StokDagilimiKomutu = new KomutSarici(p => StokDagilimiAc(p as FaturaKalemiGorunumModeli));
        KaydetKomutu = new KomutSarici(async () => await FaturaKaydetAsync(BelgeDurumu.Onaylandi), () => !Kaydediliyor);
        IptalKomutu = new KomutSarici(() => { if (Kapat != null) Kapat(); else _navigasyon.Git("Fatura"); });
        SatisTuruSecKomutu = new KomutSarici(() => Tur = FaturaTuru.Satis);
        AlisTuruSecKomutu = new KomutSarici(() => Tur = FaturaTuru.Alis);

        _ = StokYerleri.YukleAsync(_urunServisi);
        _ = VerileriYukleAsync();
    }

    private void CariSec()
    {
        if (IsCariKilitli) return;
        var tur = Tur == FaturaTuru.Satis ? CariTuru.Musteri : CariTuru.Tedarikci;
        var pencere = new CariSecimPenceresi(_cariServisi, tur) { Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) };
        if (pencere.ShowDialog() == true) SeciliCari = pencere.SecilenCari;
    }

    private void UrunSec()
    {
        var pencere = new UrunSecimPenceresi(_urunServisi) { Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) };
        if (pencere.ShowDialog() != true || pencere.SecilenUrun == null) return;
        
        SeciliUrun = pencere.SecilenUrun;
        BasariMesaji = $"📌 Ürün seçildi: '{SeciliUrun.Ad}'. Lütfen Miktar, Birim Fiyat ve KDV alanlarını kontrol edip '+ Listeye Ekle' butonuna basınız.";
        HataMesaji = string.Empty;
    }

    private void StokDagilimiAc(FaturaKalemiGorunumModeli? kalem)
    {
        if (kalem == null) return;
        var vm = new StokDagilimiGorunumModeli(kalem, _urunServisi, Tur is FaturaTuru.Satis or FaturaTuru.AlisIade);
        new StokDagilimiPenceresi(vm) { Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) }.ShowDialog();
    }

    private void FaturaNoOlustur()
    {
        FaturaNo = $"FT{DateTime.Now:yyyyMMddHHmmss}";
    }

    private readonly SemaphoreSlim _yuklemeKilidi = new(1, 1);

    private async Task VerileriYukleAsync()
    {
        await _yuklemeKilidi.WaitAsync();
        try
        {
            var tur = Tur == FaturaTuru.Satis ? CariTuru.Musteri : CariTuru.Tedarikci;
            var tumCariler = await _cariServisi.TumunuGetirAsync(tur);
            Cariler = new ObservableCollection<CariKart>(tumCariler.Where(c => c.Aktif));

            var tumUrunler = await _urunServisi.TumunuGetirAsync();
            Urunler = new ObservableCollection<Urun>(tumUrunler.Where(u => u.Aktif));
        }
        catch (Exception ex)
        {
            HataMesaji = $"Veriler yüklenirken hata oluştu: {ex.Message}";
        }
        finally
        {
            _yuklemeKilidi.Release();
        }
    }

    private async Task CarileriYukleAsync()
    {
        await _yuklemeKilidi.WaitAsync();
        try
        {
            var tur = Tur == FaturaTuru.Satis ? CariTuru.Musteri : CariTuru.Tedarikci;
            var tumCariler = await _cariServisi.TumunuGetirAsync(tur);
            Cariler = new ObservableCollection<CariKart>(tumCariler.Where(c => c.Aktif));
        }
        catch (Exception ex)
        {
            HataMesaji = $"Cariler yüklenirken hata oluştu: {ex.Message}";
        }
        finally
        {
            _yuklemeKilidi.Release();
        }
    }

    public int ToplamKalemSayisi => Kalemler.Count;
    public decimal ToplamUrunMiktari => Kalemler.Sum(k => k.Miktar);
    public bool HasKalemler => Kalemler.Any();
    public bool IsKalemlerEmpty => !Kalemler.Any();
    public string ListeBaslikMetni => ToplamKalemSayisi > 0 ? $"📋 FATURA KALEM LİSTESİ ({ToplamKalemSayisi} Çeşit Ürün, Toplam {ToplamUrunMiktari:N2} Adet)" : "📋 FATURA KALEM LİSTESİ (Henüz ürün eklenmedi)";

    private void KalemEkle()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;
        if (SeciliUrun == null) { HataMesaji = "Lütfen eklenecek ürünü seçiniz."; return; }
        if (EklenecekMiktar <= 0) { HataMesaji = "Miktar 0'dan büyük olmalıdır."; return; }

        var eklenenUrunAd = SeciliUrun.Ad;
        var birim = SeciliUrun.Birim ?? "Adet";
        var eklenenMiktar = EklenecekMiktar;
        var eklenenFiyat = EklenecekBirimFiyat;

        var mevcut = Kalemler.FirstOrDefault(k => k.Urun?.Id == SeciliUrun.Id);
        if (mevcut != null)
        {
            mevcut.Miktar += EklenecekMiktar;
            mevcut.Hesapla();
            BasariMesaji = $"ℹ️ '{eklenenUrunAd}' zaten listede mevcuttu; miktarı artırılarak {mevcut.Miktar:N2} {birim} yapıldı.";
        }
        else
        {
            var yeniKalem = new FaturaKalemiGorunumModeli
            {
                Urun = SeciliUrun,
                IskontoOrani = EklenecekIskonto,
                KdvOrani = EklenecekKdvOrani,
                Degisti = ToplamlariHesapla
            };
            yeniKalem.UrunBilgileriniUygula();
            yeniKalem.Miktar = EklenecekMiktar;
            yeniKalem.BirimFiyat = EklenecekBirimFiyat;
            yeniKalem.KdvOrani = EklenecekKdvOrani;
            yeniKalem.VarsayilanStokDagilimiOlustur();
            yeniKalem.Hesapla();
            Kalemler.Add(yeniKalem);
            BasariMesaji = $"✅ '{eklenenUrunAd}' ({eklenenMiktar:N2} {birim} - Birim Fiyat: {eklenenFiyat:N2} ₺ - KDV: %{EklenecekKdvOrani:N2}) fatura kalem listesine başarıyla eklendi.";
        }

        EklenecekMiktar = 1;
        EklenecekBirimFiyat = 0;
        EklenecekKdvOrani = 0;
        EklenecekIskonto = 0;
        SeciliUrun = null;
        ToplamlariHesapla();
    }

    private void KalemSil(FaturaKalemiGorunumModeli kalem)
    {
        var urunAd = kalem.Urun?.Ad ?? "Ürün";
        Kalemler.Remove(kalem);
        BasariMesaji = $"🗑️ '{urunAd}' fatura kalem listesinden çıkarıldı.";
        HataMesaji = string.Empty;
        ToplamlariHesapla();
    }

    private void FormuTemizle()
    {
        Kalemler.Clear();
        if (!IsCariKilitli) SeciliCari = null;
        SeciliUrun = null;
        EklenecekMiktar = 1;
        EklenecekBirimFiyat = 0;
        EklenecekIskonto = 0;
        Aciklama = string.Empty;
        ReferansNo = string.Empty;
        GenelIskontoOrani = 0;
        HataMesaji = string.Empty;
        BasariMesaji = "🧹 Fatura kalem listesi ve formu temizlendi.";
        FaturaNoOlustur();
        ToplamlariHesapla();
    }

    private void ToplamlariHesapla()
    {
        BildirimGonder(nameof(AraToplam));
        BildirimGonder(nameof(SatirIskontosuToplam));
        BildirimGonder(nameof(GenelIskontoTutari));
        BildirimGonder(nameof(KdvToplam));
        BildirimGonder(nameof(GenelToplam));
        BildirimGonder(nameof(ToplamKalemSayisi));
        BildirimGonder(nameof(ToplamUrunMiktari));
        BildirimGonder(nameof(HasKalemler));
        BildirimGonder(nameof(IsKalemlerEmpty));
        BildirimGonder(nameof(ListeBaslikMetni));
    }

    private async Task FaturaKaydetAsync(BelgeDurumu durum)
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (string.IsNullOrWhiteSpace(FaturaNo)) { HataMesaji = "Lütfen fatura numarasını giriniz."; return; }
        if (SeciliCari == null) { HataMesaji = "Lütfen cari kart seçiniz."; return; }
        if (!Kalemler.Any()) { HataMesaji = "Faturaya en az bir ürün kalemi eklemelisiniz."; return; }
        if (OdemeSekli == null) { HataMesaji = "Lütfen ödeme şeklini seçiniz."; return; }
        if (Kalemler.Any(k => k.Miktar <= 0 || k.BirimFiyat < 0 || k.KdvOrani < 0 || k.KdvOrani > 100 || k.IskontoOrani < 0 || k.IskontoOrani > 100)) { HataMesaji = "Miktar, fiyat, iskonto veya KDV değerlerinden biri geçersiz."; return; }

        foreach (var k in Kalemler)
        {
            if (k.StokDagilimlari.Count == 0)
                k.VarsayilanStokDagilimiOlustur();
            else if (k.StokDagilimlari.Sum(d => d.Miktar) != k.Miktar)
                k.StokDagiliminiGuncelle();
        }

        // Dağılımı boş kalem sunucuda ilk aktif mağazadan düşülür; doluysa toplamı miktara eşit olmalı.
        if (Kalemler.Any(k => k.StokDagilimlari.Count > 0 && k.StokDagilimlari.Sum(d => d.Miktar) != k.Miktar)) { HataMesaji = "Stok dağılımı ürün miktarına eşit olmalıdır."; return; }

        Kaydediliyor = true;
        (KaydetKomutu as KomutSarici)?.BildirimGonder();

        try
        {
            var fatura = new Fatura
            {
                FaturaNo = FaturaNo,
                Tarih = DateTime.SpecifyKind(Tarih, DateTimeKind.Utc),
                CariKartId = SeciliCari.Id,
                SirketId = SeciliCari.SirketId,
                Tur = Tur,
                Durum = durum,
                GenelIskontoOrani = GenelIskontoOrani,
                Aciklama = string.IsNullOrWhiteSpace(ReferansNo) ? Aciklama : $"Referans: {ReferansNo} - {Aciklama}".TrimEnd(' ', '-'),
                VadeTarihi = VadeTarihi,
                OdemeSekli = OdemeSekli.Value,
                KaynakTuru = FaturaKaynakTuru.Manuel,
                Kalemler = Kalemler.Select(k => k.ToEntity()).ToList()
            };

            Fatura eklenenFatura;
            if (durum == BelgeDurumu.Onaylandi && Tur == FaturaTuru.Satis)
            {
                eklenenFatura = await _faturaServisi.PosSatisiniTamamlaAsync(fatura, OdemeAdi(OdemeSekli.Value));
            }
            else
            {
                eklenenFatura = await _faturaServisi.EkleAsync(fatura);
                if (durum == BelgeDurumu.Onaylandi) await _faturaServisi.OnaylaAsync(eklenenFatura.Id);
            }
            MessageBox.Show($"{eklenenFatura.FaturaNo} numaralı fatura başarıyla oluşturuldu.", "Fatura oluşturuldu", MessageBoxButton.OK, MessageBoxImage.Information);
            Kapat?.Invoke();
            _navigasyon.Git("Fatura");
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException != null ? $"{ex.Message} ({ex.InnerException.Message})" : ex.Message;
            HataMesaji = string.IsNullOrWhiteSpace(msg) ? "Fatura oluşturulamadı. Bilgileri kontrol edip yeniden deneyin." : msg;
        }
        finally
        {
            Kaydediliyor = false;
            (KaydetKomutu as KomutSarici)?.BildirimGonder();
        }
    }

    private static string OdemeAdi(FaturaOdemeSekli x) => x switch { FaturaOdemeSekli.AcikHesap => "Açık Hesap", FaturaOdemeSekli.KrediKarti => "Kredi Kartı", FaturaOdemeSekli.Nakit => "Nakit", _ => "Banka" };
}
