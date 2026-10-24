using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Finans;
using Masaustu.Gorunumler.Pencere;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.GorunumModelleri;

public sealed class TahsilatOdemeGorunumModeli : TemelGorunumModeli
{
    private readonly ITahsilatOdemeServisi _servis;
    private readonly ICariServisi _cariler;
    private readonly IBelgeKapamaServisi _kapama;

    // Cari
    private CariKart? _cari;
    // İşlem temel alanlar
    private decimal _tutar;
    private string _aciklama = "";
    private string _belgeNo = "";
    private OdemeYontemi _yontem;
    private DateTime _tarih = DateTime.Today;
    // Banka / Havale
    private string _bankaAdi = "";
    private string _hesapNo = "";
    // Kredi Kartı
    private string _posAdi = "";
    private int _taksitSayisi = 1;
    private decimal _komisyonOrani = 0;
    // Çek / Senet
    private string _cekSenetNo = "";
    private string _kesideci = "";
    private DateTime _vadeTarihi = DateTime.Today.AddDays(30);
    // Geçmiş seçimi
    private TahsilatOdemeKaydi? _secilenKayit;
    private AcikBelge? _secilenAcikBelge;

    // Belge Arama
    private string _belgeArama = "";
    private ICollectionView? _acikBelgelerView;

    // ─── İşlem Türü ───
    public FinansIslemTuru IslemTuru { get; }
    public string Baslik => IslemTuru == FinansIslemTuru.Tahsilat ? "Tahsilat" : "Ödeme";
    public string AltBaslik => IslemTuru == FinansIslemTuru.Tahsilat
        ? "Müşteriden alınan ödemeyi kaydedin ve faturaları kapatın"
        : "Tedarikçiye yapılan ödemeyi kaydedin";

    private bool _isCariKilitli;

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

    // ─── Cari ───
    public CariKart? SecilenCari
    {
        get => _cari;
        set 
        { 
            if (OzellikAyarla(ref _cari, value)) 
            { 
                BakiyeBildir(); 
                _ = CariYukleveAcikBelgeleriGetirAsync();
            } 
        }
    }
    public decimal MevcutBakiye => SecilenCari?.Bakiye ?? 0;
    public decimal YeniBakiye => MevcutBakiye + (IslemTuru == FinansIslemTuru.Tahsilat ? -Tutar : Tutar);

    // ─── Temel ───
    public decimal Tutar
    {
        get => _tutar;
        set
        {
            if (OzellikAyarla(ref _tutar, value))
            {
                BildirimGonder(nameof(YeniBakiye));
                BildirimGonder(nameof(NetTutar));
            }
        }
    }
    public decimal KomisyonTutar => Tutar * KomisyonOrani / 100;
    public decimal NetTutar => Tutar - KomisyonTutar;

    public string Aciklama { get => _aciklama; set => OzellikAyarla(ref _aciklama, value); }
    public string BelgeNo { get => _belgeNo; set => OzellikAyarla(ref _belgeNo, value); }
    public DateTime Tarih { get => _tarih; set => OzellikAyarla(ref _tarih, value); }

    // ─── Ödeme Yöntemi ───
    public OdemeYontemi OdemeYontemi
    {
        get => _yontem;
        set
        {
            if (OzellikAyarla(ref _yontem, value))
            {
                BildirimGonder(nameof(BankaHavaleGorunur));
                BildirimGonder(nameof(KrediKartiGorunur));
                BildirimGonder(nameof(CekSenetGorunur));
                BildirimGonder(nameof(KasaGorunur));
                BildirimGonder(nameof(BankaHesabiGorunur));
                HesapDegisti();
                // Kartla tahsilat POS hesabına girer; varsa POS hesabı önerilir.
                if (value == OdemeYontemi.KrediKarti && BankaHesaplari.FirstOrDefault(b => b.HesapTuru == BankaHesapTuru.POSHesabi) is BankaHesabi pos)
                    SeciliBankaHesabi = pos;
                else if (value != OdemeYontemi.KrediKarti && SeciliBankaHesabi?.HesapTuru == BankaHesapTuru.POSHesabi)
                    SeciliBankaHesabi = BankaHesaplari.FirstOrDefault(b => b.HesapTuru != BankaHesapTuru.POSHesabi) ?? SeciliBankaHesabi;
            }
        }
    }
    public IReadOnlyList<OdemeYontemi> OdemeYontemleri { get; } = Enum.GetValues<OdemeYontemi>();
    public bool BankaHavaleGorunur => OdemeYontemi is OdemeYontemi.Banka or OdemeYontemi.Havale or OdemeYontemi.Virman;
    public bool KrediKartiGorunur => OdemeYontemi == OdemeYontemi.KrediKarti;
    public bool CekSenetGorunur => OdemeYontemi is OdemeYontemi.Cek or OdemeYontemi.Senet;
    public bool KasaGorunur => OdemeYontemi == OdemeYontemi.Nakit;
    public bool BankaHesabiGorunur => OdemeYontemi is OdemeYontemi.Banka or OdemeYontemi.Havale or OdemeYontemi.Virman or OdemeYontemi.KrediKarti;

    // ─── Paranın girdiği/çıktığı kasa veya banka hesabı ───
    private Kasa? _seciliKasa;
    private BankaHesabi? _seciliBankaHesabi;
    private bool _kaydediliyor;
    public ObservableCollection<Kasa> Kasalar { get; } = [];
    public ObservableCollection<BankaHesabi> BankaHesaplari { get; } = [];
    public Kasa? SeciliKasa { get => _seciliKasa; set { if (OzellikAyarla(ref _seciliKasa, value)) HesapDegisti(); } }
    public BankaHesabi? SeciliBankaHesabi { get => _seciliBankaHesabi; set { if (OzellikAyarla(ref _seciliBankaHesabi, value)) { BildirimGonder(nameof(KrediKartiBilgisi)); HesapDegisti(); } } }

    // ─── Dövizli kasa/banka: tutar hesabın para biriminde girilir, TL tutar döviz × kurdur (cari ve belge kapama TL ile) ───
    private decimal _dovizTutar;
    private decimal _kur;
    public string? HesapParaBirimi => KasaGorunur ? SeciliKasa?.ParaBirimi : BankaHesabiGorunur ? SeciliBankaHesabi?.ParaBirimi : null;
    public bool HesapDovizli => DovizKuruYardimcisi.DovizliMi(HesapParaBirimi);
    public string HesapParaSimgesi => DovizKuruYardimcisi.Simge(HesapParaBirimi);
    public decimal DovizTutar { get => _dovizTutar; set { if (OzellikAyarla(ref _dovizTutar, value)) TlTutariHesapla(); } }
    public decimal Kur { get => _kur; set { if (OzellikAyarla(ref _kur, value)) TlTutariHesapla(); } }

    private void TlTutariHesapla()
    {
        if (HesapDovizli) Tutar = Math.Round(DovizTutar * Kur, 2, MidpointRounding.AwayFromZero);
    }

    private void HesapDegisti()
    {
        BildirimGonder(nameof(HesapDovizli));
        BildirimGonder(nameof(HesapParaSimgesi));
        if (HesapDovizli && HesapParaBirimi is { } paraBirimi)
            _ = KurOnerAsync(paraBirimi);
    }

    private async Task KurOnerAsync(string paraBirimi)
    {
        var kur = await DovizKuruYardimcisi.GunlukKurAsync(paraBirimi, Tarih);
        if (kur > 0 && HesapParaBirimi == paraBirimi) Kur = kur;
    }
    public string KrediKartiBilgisi => SeciliBankaHesabi?.HesapTuru == BankaHesapTuru.POSHesabi
        ? $"Tutarın tamamı '{SeciliBankaHesabi.Ad}' POS hesabına (108) girer. Banka komisyonu (%{SeciliBankaHesabi.PosKomisyonOrani:N2}), para vadesiz hesaba geçirilirken Banka ekranındaki POS geçişinde gider yazılır."
        : "Seçilen hesap bir POS hesabı değil: tutar doğrudan bankaya (102) girer ve komisyon düşülmez. Kart tahsilatları için Banka ekranından bir POS hesabı tanımlamanız önerilir.";

    // ─── Banka / Havale ───
    public string BankaAdi { get => _bankaAdi; set => OzellikAyarla(ref _bankaAdi, value); }
    public string HesapNo { get => _hesapNo; set => OzellikAyarla(ref _hesapNo, value); }

    // ─── Kredi Kartı ───
    public string PosAdi { get => _posAdi; set => OzellikAyarla(ref _posAdi, value); }
    public int TaksitSayisi
    {
        get => _taksitSayisi;
        set => OzellikAyarla(ref _taksitSayisi, value);
    }
    public decimal KomisyonOrani
    {
        get => _komisyonOrani;
        set
        {
            if (OzellikAyarla(ref _komisyonOrani, value))
            {
                BildirimGonder(nameof(KomisyonTutar));
                BildirimGonder(nameof(NetTutar));
            }
        }
    }
    public IReadOnlyList<int> TaksitSecenekleri { get; } = [1, 2, 3, 6, 9, 12];

    // ─── Çek / Senet ───
    public string CekSenetNo { get => _cekSenetNo; set => OzellikAyarla(ref _cekSenetNo, value); }
    public string Kesideci { get => _kesideci; set => OzellikAyarla(ref _kesideci, value); }
    public DateTime VadeTarihi { get => _vadeTarihi; set => OzellikAyarla(ref _vadeTarihi, value); }

    // ─── Listeler ───
    public ObservableCollection<TahsilatOdemeKaydi> Gecmis { get; } = [];
    public ObservableCollection<AcikBelge> AcikBelgeler { get; } = [];
    public ObservableCollection<AcikBelge> SeciliBelgeler { get; } = [];

    public ICollectionView AcikBelgelerFiltreli => _acikBelgelerView!;

    public string BelgeArama
    {
        get => _belgeArama;
        set
        {
            if (OzellikAyarla(ref _belgeArama, value))
                _acikBelgelerView?.Refresh();
        }
    }

    public decimal ToplamSeciliBelgeTutar => SeciliBelgeler.Sum(b => b.AcikTutar);

    public AcikBelge? SecilenAcikBelge
    {
        get => _secilenAcikBelge;
        set
        {
            if (OzellikAyarla(ref _secilenAcikBelge, value) && value != null)
                Tutar = value.AcikTutar;
        }
    }
    public TahsilatOdemeKaydi? SecilenKayit { get => _secilenKayit; set => OzellikAyarla(ref _secilenKayit, value); }

    // ─── Komutlar ───
    public ICommand CariSecKomutu { get; }
    public ICommand KaydetKomutu { get; }
    public ICommand IptalKomutu { get; }
    public ICommand YenileKomutu { get; }
    public ICommand BelgeSecKomutu { get; }
    public ICommand BelgeKaldirKomutu { get; }
    public ICommand TumSeciliBelgeleriKaldirKomutu { get; }

    public TahsilatOdemeGorunumModeli(
        ITahsilatOdemeServisi servis,
        ICariServisi cariler,
        IBelgeKapamaServisi kapama,
        FinansIslemTuru tur)
    {
        _servis = servis;
        _cariler = cariler;
        _kapama = kapama;
        IslemTuru = tur;

        CariSecKomutu = new KomutSarici(CariSec);
        KaydetKomutu = new KomutSarici(_ => Kaydet(), _ => !_kaydediliyor);
        IptalKomutu = new KomutSarici(_ => Iptal());
        YenileKomutu = new KomutSarici(_ => Yukle());
        BelgeSecKomutu = new KomutSarici(p => BelgeSec(p as AcikBelge));
        BelgeKaldirKomutu = new KomutSarici(p => BelgeKaldir(p as AcikBelge));
        TumSeciliBelgeleriKaldirKomutu = new KomutSarici(() => { SeciliBelgeler.Clear(); TutariGuncelle(); });

        _ = Yukle();
    }

    private void CariSec()
    {
        if (IsCariKilitli) return;
        var pencere = new CariSecimPenceresi(
            _cariler,
            IslemTuru == FinansIslemTuru.Tahsilat ? CariTuru.Musteri : CariTuru.Tedarikci)
        { Owner = Application.Current.MainWindow };

        if (pencere.ShowDialog() == true)
            SecilenCari = pencere.SecilenCari;
    }

    private readonly SemaphoreSlim _yuklemeKilidi = new(1, 1);

    private async Task CariYukleveAcikBelgeleriGetirAsync()
    {
        await _yuklemeKilidi.WaitAsync();
        try
        {
            await AcikBelgeleriYukleKilitsiz();
            await YukleKilitsiz();
        }
        catch
        {
        }
        finally
        {
            _yuklemeKilidi.Release();
        }
    }

    private async Task AcikBelgeleriYukle()
    {
        await _yuklemeKilidi.WaitAsync();
        try
        {
            await AcikBelgeleriYukleKilitsiz();
        }
        catch
        {
        }
        finally
        {
            _yuklemeKilidi.Release();
        }
    }

    private async Task AcikBelgeleriYukleKilitsiz()
    {
        AcikBelgeler.Clear();
        SeciliBelgeler.Clear();
        BelgeArama = "";
        if (SecilenCari == null) return;
        var belgeler = await _kapama.AcikBelgeleriGetirAsync(SecilenCari.Id);
        // Tahsilat müşterinin borç belgelerini, ödeme tedarikçiye olan alacak belgelerini kapatır.
        foreach (var x in belgeler.Where(b => b.BorcMu == (IslemTuru == FinansIslemTuru.Tahsilat)))
            AcikBelgeler.Add(x);

        _acikBelgelerView = CollectionViewSource.GetDefaultView(AcikBelgeler);
        _acikBelgelerView.Filter = obj =>
        {
            if (obj is not AcikBelge belge) return false;
            if (string.IsNullOrWhiteSpace(BelgeArama)) return true;
            var ara = BelgeArama.ToLower(System.Globalization.CultureInfo.CurrentCulture);
            return (belge.Aciklama?.ToLower().Contains(ara) ?? false)
                || (belge.BelgeNo?.ToLower().Contains(ara) ?? false)
                || belge.AcikTutar.ToString("N2").Contains(ara)
                || belge.Tarih.ToString("dd.MM.yyyy").Contains(ara);
        };
        BildirimGonder(nameof(AcikBelgelerFiltreli));
        TutariGuncelle();
    }

    private void BelgeSec(AcikBelge? belge)
    {
        if (belge == null || SeciliBelgeler.Contains(belge)) return;
        SeciliBelgeler.Add(belge);
        TutariGuncelle();
    }

    private void BelgeKaldir(AcikBelge? belge)
    {
        if (belge == null) return;
        SeciliBelgeler.Remove(belge);
        TutariGuncelle();
    }

    private void TutariGuncelle()
    {
        if (SeciliBelgeler.Any())
            Tutar = SeciliBelgeler.Sum(b => b.AcikTutar);
        BildirimGonder(nameof(ToplamSeciliBelgeTutar));
    }

    private async Task Yukle()
    {
        await _yuklemeKilidi.WaitAsync();
        try
        {
            await YukleKilitsiz();
        }
        catch
        {
        }
        finally
        {
            _yuklemeKilidi.Release();
        }
    }

    private async Task YukleKilitsiz()
    {
        if (Kasalar.Count == 0 && BankaHesaplari.Count == 0)
        {
            foreach (var k in await _servis.AktifKasalariGetirAsync()) Kasalar.Add(k);
            foreach (var b in await _servis.AktifBankaHesaplariniGetirAsync()) BankaHesaplari.Add(b);
            SeciliKasa ??= Kasalar.FirstOrDefault();
            SeciliBankaHesabi ??= BankaHesaplari.FirstOrDefault(b => b.HesapTuru != BankaHesapTuru.POSHesabi) ?? BankaHesaplari.FirstOrDefault();
        }
        Gecmis.Clear();
        var tumGecmis = await _servis.GecmisiGetirAsync(IslemTuru);
        if (SecilenCari != null)
        {
            tumGecmis = tumGecmis.Where(x => x.CariKartId == SecilenCari.Id).ToList();
        }
        foreach (var x in tumGecmis)
            Gecmis.Add(x);
    }

    private async Task Kaydet()
    {
        if (_kaydediliyor) return;
        _kaydediliyor = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            if (SecilenCari == null) throw new InvalidOperationException("Önce cari hesap seçiniz.");
            if (Tutar <= 0) throw new InvalidOperationException("Geçerli bir tutar giriniz.");
            if (KasaGorunur && SeciliKasa == null) throw new InvalidOperationException("Kasa seçiniz.");
            if (BankaHesabiGorunur && SeciliBankaHesabi == null) throw new InvalidOperationException("Banka hesabı seçiniz.");
            if (HesapDovizli && (DovizTutar <= 0 || Kur <= 0)) throw new InvalidOperationException($"{HesapParaSimgesi} tutarını ve kurunu giriniz.");

            // Kapatılacak belgeler: listeye eklenenler; eklenmediyse tabloda seçili tek belge.
            var kapatilacaklar = SeciliBelgeler.Any()
                ? SeciliBelgeler.Select(b => b.CariHareketId).ToList()
                : SecilenAcikBelge != null ? [SecilenAcikBelge.CariHareketId] : new List<int>();

            var talep = new TahsilatOdemeTalebi(
                IslemTuru: IslemTuru,
                OdemeYontemi: OdemeYontemi,
                CariKartId: SecilenCari.Id,
                Tutar: Tutar,
                Tarih: Tarih,
                Aciklama: Aciklama,
                Kur: HesapDovizli ? Kur : 1,
                DovizTutar: HesapDovizli ? DovizTutar : 0,
                BelgeNo: string.IsNullOrWhiteSpace(BelgeNo) ? null : BelgeNo,
                BankaAdi: string.IsNullOrWhiteSpace(BankaAdi) ? null : BankaAdi,
                HesapNo: string.IsNullOrWhiteSpace(HesapNo) ? null : HesapNo,
                TaksitSayisi: KrediKartiGorunur ? TaksitSayisi : null,
                KomisyonOrani: KrediKartiGorunur ? KomisyonOrani : 0,
                VadeTarihi: CekSenetGorunur ? VadeTarihi : null,
                CekSenetNo: CekSenetGorunur && !string.IsNullOrWhiteSpace(CekSenetNo) ? CekSenetNo : null,
                Kesideci: CekSenetGorunur && !string.IsNullOrWhiteSpace(Kesideci) ? Kesideci : null,
                KasaId: KasaGorunur ? SeciliKasa?.Id : null,
                BankaHesabiId: BankaHesabiGorunur ? SeciliBankaHesabi?.Id : null,
                KapatilacakCariHareketIdleri: kapatilacaklar);

            // Tahsilat ve belge kapama tek işlemde kaydedilir.
            await _servis.KaydetAsync(talep);

            MessageBox.Show($"{Baslik} başarıyla kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);

            // Formu sıfırla
            Tutar = 0;
            DovizTutar = 0;
            Aciklama = BelgeNo = BankaAdi = HesapNo = CekSenetNo = Kesideci = "";
            KomisyonOrani = 0;
            TaksitSayisi = 1;
            SecilenAcikBelge = null;
            SecilenCari = await _cariler.IdIleGetirAsync(SecilenCari.Id);

            await Yukle();
            await AcikBelgeleriYukle();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "İşlem yapılamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _kaydediliyor = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task Iptal()
    {
        try
        {
            if (SecilenKayit == null) throw new InvalidOperationException("İptal edilecek kaydı seçiniz.");
            if (SecilenKayit.Durum != FinansIslemDurumu.Tamamlandi) throw new InvalidOperationException("Bu kayıt zaten iptal edilmiş veya bir iptal ters kaydıdır.");
            var kayit = SecilenKayit;
            var pencere = new Masaustu.Gorunumler.Ortak.MetinGirisPenceresi(
                $"{Baslik} İptali",
                $"{kayit.Tarih:dd.MM.yyyy} - {kayit.CariUnvan} - {kayit.Tutar:N2} ₺\n\n" +
                "İşlem ters kayıtla iptal edilir: kasa/banka, cari ve muhasebe kaydı geri alınır; bu işlemle kapatılan faturalar yeniden açık olur.\n\nİptal nedenini yazın:")
            { Owner = Application.Current.MainWindow };
            if (pencere.ShowDialog() != true) return;
            await _servis.IptalEtAsync(kayit.Kaynak, kayit.HareketId, pencere.GirilenMetin);
            if (SecilenCari != null) SecilenCari = await _cariler.IdIleGetirAsync(SecilenCari.Id);
            await Yukle();
            await AcikBelgeleriYukle();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "İptal yapılamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BakiyeBildir()
    {
        BildirimGonder(nameof(MevcutBakiye));
        BildirimGonder(nameof(YeniBakiye));
    }
}
