using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using System.Windows;

namespace Masaustu.GorunumModelleri;

public class SatisGorunumModeli : TemelGorunumModeli
{
    private readonly IUrunServisi _urunServisi;
    private readonly ICariServisi _cariServisi;
    private readonly IFaturaServisi _faturaServisi;
    private readonly ICariHareketServisi _cariHareketServisi;
    private int? _geriYuklenenBekleyenFaturaId;

    private ObservableCollection<FaturaKalemiGorunumModeli> _sepet = new();
    private CariKart? _seciliCari;
    private string _barkod = string.Empty;
    private string _durumMesaji = string.Empty;
    private FaturaKalemiGorunumModeli? _seciliKalem;
    private bool _cariSatisMi;
    private string? _seciliTaksit = "Tek Çekim";
    private decimal _mevcutBakiye;
    private decimal _tahminiBakiye;
    private readonly Dictionary<int, Dictionary<int, decimal>> _urunFiyatlari = new();
    private decimal _hizliMiktar = 1;
    private string _seciliOdemeYontemi = "Nakit";

    public ObservableCollection<FaturaKalemiGorunumModeli> Sepet { get => _sepet; set => OzellikAyarla(ref _sepet, value); }
    public CariKart? SeciliCari
    {
        get => _seciliCari;
        set
        {
            OzellikAyarla(ref _seciliCari, value);
            BildirimGonder(nameof(SeciliCariAd));
            BildirimGonder(nameof(SeciliCariKodu));
            BildirimGonder(nameof(CariSatisMi));
            _ = BakiyeGuncelleAsync();
        }
    }
    public string SeciliCariAd => _seciliCari != null ? _seciliCari.Unvan : "Standart Müşteri (Perakende)";
    
    public bool CariSatisMi
    {
        get => _cariSatisMi;
        set
        {
            if (OzellikAyarla(ref _cariSatisMi, value))
            {
                BildirimGonder(nameof(CariBilgiGoster));
                BildirimGonder(nameof(HizliSatisMi));
                if (!value && SeciliCari != null) SeciliCari = null;
            }
        }
    }
    public Visibility CariBilgiGoster => CariSatisMi ? Visibility.Visible : Visibility.Collapsed;
    public bool HizliSatisMi { get => !CariSatisMi; set { if (value) CariSatisMi = false; } }

    public decimal MevcutBakiye { get => _mevcutBakiye; set => OzellikAyarla(ref _mevcutBakiye, value); }
    public decimal TahminiBakiye { get => _tahminiBakiye; set { OzellikAyarla(ref _tahminiBakiye, value); BildirimGonder(nameof(TahminiBakiyeRenk)); } }
    public string TahminiBakiyeRenk => TahminiBakiye < 0 ? "#EF4444" : "#10B981"; // Kırmızı veya Yeşil

    public string Barkod { get => _barkod; set => OzellikAyarla(ref _barkod, value); }
    public string DurumMesaji { get => _durumMesaji; set => OzellikAyarla(ref _durumMesaji, value); }
    public decimal HizliMiktar { get => _hizliMiktar; set => OzellikAyarla(ref _hizliMiktar, value < 1 ? 1 : value); }
    public IReadOnlyList<string> OdemeYontemleri { get; } = ["Nakit", "Kredi Kartı", "Banka", "Açık Hesap"];
    public string SeciliOdemeYontemi
    {
        get => _seciliOdemeYontemi;
        set
        {
            if (OzellikAyarla(ref _seciliOdemeYontemi, value))
            {
                BildirimGonder(nameof(TaksitAlaniAktif));
                if (value != "Kredi Kartı" && SeciliTaksit != "Tek Çekim")
                    SeciliTaksit = "Tek Çekim";
            }
        }
    }
    public bool TaksitAlaniAktif => SeciliOdemeYontemi == "Kredi Kartı";
    public bool SepetBos => Sepet.Count == 0;
    public string SeciliCariKodu => SeciliCari?.Kod ?? "PERAKENDE";
    
    public FaturaKalemiGorunumModeli? SeciliKalem
    {
        get => _seciliKalem;
        set
        {
            OzellikAyarla(ref _seciliKalem, value);
            ((KomutSarici)SatirSilKomutu).BildirimGonder();
        }
    }

    public ObservableCollection<string> TaksitSecenekleri { get; } = new()
    {
        "Tek Çekim", "2 Taksit", "3 Taksit", "4 Taksit", "5 Taksit", "6 Taksit", "7 Taksit", "8 Taksit", "9 Taksit"
    };

    public string? SeciliTaksit
    {
        get => _seciliTaksit;
        set
        {
            if (OzellikAyarla(ref _seciliTaksit, value))
            {
                _ = TaksitFiyatlariniUygulaAsync();
            }
        }
    }

    // Toplamlar
    public decimal AraToplam => _sepet.Sum(k => k.Miktar * k.BirimFiyat);
    public decimal SatirIskontosuToplam => _sepet.Sum(k => k.IskontoTutari);
    public decimal KdvToplami => _sepet.Sum(k => k.KdvTutari);
    public decimal GenelToplam => _sepet.Sum(k => k.SatirToplami);
    public int ToplamUrunAdedi => _sepet.Count;

    // Komutlar
    public ICommand BarkodOkunduKomutu { get; }
    public ICommand UrunAraKomutu { get; }
    public ICommand CariSecKomutu { get; }
    public ICommand StokDagilimiAcKomutu { get; }
    public ICommand SatisiBekletKomutu { get; }
    public ICommand BekleyenSatisGetirKomutu { get; }
    public ICommand OdemeEkraniAcKomutu { get; }
    public ICommand OdemeYontemiSecKomutu { get; }
    public ICommand SatirSilKomutu { get; }
    public ICommand IptalKomutu { get; }
    public ICommand SatisTamamlaKomutu { get; }

    public Func<Task<CariKart?>>? CariSeciciAc { get; set; }
    public Func<Task<Urun?>>? UrunSeciciAc { get; set; }
    public Func<Task<Fatura?>>? BekleyenSatisSeciciAc { get; set; }
    public IUrunServisi UrunServisi => _urunServisi;
    public ICariServisi CariServisi => _cariServisi;
    public IFaturaServisi FaturaServisi => _faturaServisi;

    public SatisGorunumModeli(IUrunServisi urunServisi, ICariServisi cariServisi, IFaturaServisi faturaServisi, ICariHareketServisi cariHareketServisi)
    {
        _ = StokYerleri.YukleAsync(urunServisi);
        _urunServisi = urunServisi;
        _cariServisi = cariServisi;
        _faturaServisi = faturaServisi;
        _cariHareketServisi = cariHareketServisi;

        BarkodOkunduKomutu = new KomutSarici(async _ => await BarkodIsleAsync());
        UrunAraKomutu = new KomutSarici(async _ =>
        {
            if (UrunSeciciAc == null) return;
            var urun = await UrunSeciciAc();
            if (urun != null) await UrunuSepeteEkleAsync(urun, HizliMiktar);
        });
        
        CariSecKomutu = new KomutSarici(async _ => {
            if (CariSeciciAc != null)
            {
                var cari = await CariSeciciAc();
                if (cari != null) { SeciliCari = cari; CariSatisMi = true; }
            }
        });

        StokDagilimiAcKomutu = new KomutSarici(param => {
            var kalem = param as FaturaKalemiGorunumModeli ?? SeciliKalem;
            if (kalem != null) StokDagilimiPenceresiniAc(kalem);
        });

        SatisiBekletKomutu = new KomutSarici(async _ => await SatisiBekletAsync(), _ => Sepet.Count > 0);
        BekleyenSatisGetirKomutu = new KomutSarici(async _ => await BekleyenSatisGetirAsync());
        
        OdemeEkraniAcKomutu = new KomutSarici(_ => DurumMesaji = $"Ödeme yöntemi: {SeciliOdemeYontemi}", _ => Sepet.Count > 0);
        OdemeYontemiSecKomutu = new KomutSarici(param =>
        {
            if (param is string yontem) SeciliOdemeYontemi = yontem;
        });
        
        SatirSilKomutu = new KomutSarici(param => {
            var kalem = param as FaturaKalemiGorunumModeli ?? SeciliKalem;
            if (kalem != null) { Sepet.Remove(kalem); HesaplaVeGuncelle(); }
        }, _ => SeciliKalem != null || Sepet.Count > 0);
        
        IptalKomutu = new KomutSarici(_ => Temizle());

        SatisTamamlaKomutu = new KomutSarici(async _ => await SatisKapatAsync(SeciliOdemeYontemi), _ => Sepet.Count > 0);
    }

    private async Task BakiyeGuncelleAsync()
    {
        if (SeciliCari == null)
        {
            MevcutBakiye = 0;
            TahminiBakiye = 0;
            return;
        }

        try
        {
            MevcutBakiye = await _cariHareketServisi.BakiyeHesaplaAsync(SeciliCari.Id);
            TahminiBakiye = MevcutBakiye + GenelToplam;
        }
        catch { }
    }

    private async Task TaksitFiyatlariniUygulaAsync()
    {
        if (string.IsNullOrEmpty(SeciliTaksit)) return;

        var taksit = TaksitSayisiniAl(SeciliTaksit);
        var yeniFiyatlar = new List<(FaturaKalemiGorunumModeli Kalem, decimal Fiyat)>();
        foreach (var kalem in Sepet)
        {
            if (kalem.Urun == null) continue;
            var fiyat = await FiyatAlAsync(kalem.Urun, taksit);
            if (!fiyat.HasValue)
            {
                DurumMesaji = $"⚠ Bu ürün için {taksit} taksit satış fiyatı tanımlanmamış.";
                return;
            }
            yeniFiyatlar.Add((kalem, fiyat.Value));
        }
        foreach (var (kalem, fiyat) in yeniFiyatlar)
            kalem.BirimFiyat = fiyat;
        HesaplaVeGuncelle();
    }

    private static int TaksitSayisiniAl(string secenek) => secenek.StartsWith("Tek", StringComparison.OrdinalIgnoreCase)
        ? 1
        : int.TryParse(secenek.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0], out var sayi) ? sayi : 1;

    private async Task<decimal?> FiyatAlAsync(Urun urun, int taksit)
    {
        if (!_urunFiyatlari.TryGetValue(urun.Id, out var fiyatlar))
        {
            fiyatlar = (await _urunServisi.FiyatlariGetirAsync(urun.Id))
                .Where(f => f.AktifMi && f.Fiyat >= 0)
                .GroupBy(f => f.TaksitSayisi)
                .ToDictionary(g => g.Key, g => g.First().Fiyat);
            _urunFiyatlari[urun.Id] = fiyatlar;
        }

        if (fiyatlar.TryGetValue(taksit, out var fiyat)) return fiyat;
        return taksit == 1 ? urun.PerakendeSatisFiyati : null;
    }

    private async Task BarkodIsleAsync()
    {
        if (string.IsNullOrWhiteSpace(Barkod)) return;

        var arama = Barkod.Trim();
        Barkod = string.Empty;

        var bulunanlar = await _urunServisi.AraAsync(arama);
        var urun = bulunanlar.FirstOrDefault(u => u.Barkod == arama || u.Kod == arama);

        if (urun != null)
        {
            await UrunuSepeteEkleAsync(urun, HizliMiktar);
        }
        else
        {
            DurumMesaji = "Ürün bulunamadı!";
            DurumMesaji = "Bu barkoda ait ürün bulunamadı.";
        }
    }

    public async Task UrunuSepeteEkleAsync(Urun urun, decimal miktar = 1)
    {
        var taksit = TaksitSayisiniAl(SeciliTaksit ?? "Tek Çekim");
        var seciliFiyat = await FiyatAlAsync(urun, taksit);
        if (!seciliFiyat.HasValue)
        {
            DurumMesaji = $"⚠ Bu ürün için {taksit} taksit satış fiyatı tanımlanmamış.";
            return;
        }

        var mevcutKalem = Sepet.FirstOrDefault(k => k.Urun?.Id == urun.Id);
        if (mevcutKalem != null)
            mevcutKalem.Miktar += miktar;
        else
        {
            var yeniKalem = new FaturaKalemiGorunumModeli { Degisti = HesaplaVeGuncelle, Urun = urun };
            yeniKalem.BirimFiyat = seciliFiyat.Value;
            yeniKalem.Miktar = miktar;
            yeniKalem.VarsayilanStokDagilimiOlustur();
            Sepet.Add(yeniKalem);
        }
        DurumMesaji = $"✓ {urun.Ad} sepete eklendi.";
        HesaplaVeGuncelle();
    }

    private void HesaplaVeGuncelle()
    {
        for (var i = 0; i < Sepet.Count; i++)
            Sepet[i].SiraNo = i + 1;
        BildirimGonder(nameof(AraToplam));
        BildirimGonder(nameof(SatirIskontosuToplam));
        BildirimGonder(nameof(KdvToplami));
        BildirimGonder(nameof(GenelToplam));
        BildirimGonder(nameof(ToplamUrunAdedi));
        BildirimGonder(nameof(SepetBos));
        
        if (CariSatisMi)
        {
            TahminiBakiye = MevcutBakiye + GenelToplam;
        }

        ((KomutSarici)SatisTamamlaKomutu).BildirimGonder();
        ((KomutSarici)SatisiBekletKomutu).BildirimGonder();
    }

    private void Temizle()
    {
        _geriYuklenenBekleyenFaturaId = null;
        Sepet.Clear();
        SeciliCari = null;
        CariSatisMi = false;
        SeciliKalem = null;
        SeciliTaksit = "Tek Çekim";
        SeciliOdemeYontemi = "Nakit";
        DurumMesaji = "Sepet temizlendi.";
        HesaplaVeGuncelle();
    }

    private void StokDagilimiPenceresiniAc(FaturaKalemiGorunumModeli kalem)
    {
        var vm = new StokDagilimiGorunumModeli(kalem, _urunServisi);
        var pencere = new Masaustu.Gorunumler.Pencere.StokDagilimiPenceresi(vm);
        pencere.ShowDialog();
    }

    private async Task SatisiBekletAsync()
    {
        try
        {
            var cariId = SeciliCari?.Id ?? await VarsayilanCariAlAsync();
            var fatura = new Fatura
            {
                Tur = FaturaTuru.Satis,
                Durum = BelgeDurumu.Beklemede,
                CariKartId = cariId,
                Tarih = DateTime.Now,
                FaturaNo = $"BEKLE-{DateTime.Now:yyyyMMddHHmmss}",
                Aciklama = "Bekleyen Hızlı Satış",
                Kalemler = Sepet.Select(s => s.ToEntity()).ToList()
            };

            await _faturaServisi.EkleAsync(fatura);
            Temizle();
            DurumMesaji = "Satış beklemeye alındı.";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"⚠ Satış bekletilemedi: {ex.Message}";
        }
    }

    private async Task BekleyenSatisGetirAsync()
    {
        if (BekleyenSatisSeciciAc == null) return;
        var fatura = await BekleyenSatisSeciciAc();
        if (fatura == null) return;

        var cariler = await _cariServisi.TumunuGetirAsync();
        var urunler = await _urunServisi.TumunuGetirAsync();
        Sepet.Clear();
        foreach (var kalem in fatura.Kalemler)
        {
            var urun = urunler.FirstOrDefault(u => u.Id == kalem.UrunId);
            if (urun == null) continue;
            var satir = new FaturaKalemiGorunumModeli { Urun = urun, Degisti = HesaplaVeGuncelle };
            satir.Miktar = kalem.Miktar;
            satir.BirimFiyat = kalem.BirimFiyat;
            satir.IskontoOrani = kalem.IskontoOrani;
            satir.KdvOrani = kalem.KdvOrani;
            foreach (var dagilim in kalem.StokDagilimlari)
                satir.StokDagilimlari.Add(new FaturaKalemiStokDagilimi { DepoId = dagilim.DepoId, Miktar = dagilim.Miktar });
            satir.StokDagilimiOzetiGuncelle();
            Sepet.Add(satir);
        }
        SeciliCari = cariler.FirstOrDefault(c => c.Id == fatura.CariKartId && !c.Unvan.Contains("PERAKENDE", StringComparison.OrdinalIgnoreCase));
        CariSatisMi = SeciliCari != null;
        _geriYuklenenBekleyenFaturaId = fatura.Id;
        DurumMesaji = $"✓ {fatura.FaturaNo} numaralı bekleyen satış açıldı.";
        HesaplaVeGuncelle();
    }

    private async Task<int> VarsayilanCariAlAsync()
    {
        var cariler = await _cariServisi.TumunuGetirAsync();
        var perakendeCari = cariler.FirstOrDefault(c => c.Unvan.Contains("PERAKENDE"));
        
        if (perakendeCari == null)
        {
            perakendeCari = new CariKart { Kod = "120.00.001", Unvan = "PERAKENDE MÜŞTERİ", Tur = CariTuru.Musteri };
            perakendeCari = await _cariServisi.EkleAsync(perakendeCari);
        }
        return perakendeCari.Id;
    }

    private async Task SatisKapatAsync(string odemeYontemi)
    {
        try
        {
            // İş Kuralları Doğrulaması
            if (CariSatisMi && SeciliCari == null)
            {
                DurumMesaji = "⚠ Cari satış için cari seçmeniz gerekiyor.";
                return;
            }

            if (odemeYontemi == "Açık Hesap" && SeciliCari == null)
            {
                DurumMesaji = "⚠ Açık hesap için cari seçmeniz gerekiyor.";
                return;
            }
            
            if (odemeYontemi == "Açık Hesap" && SeciliTaksit != "Tek Çekim")
            {
                DurumMesaji = "⚠ Açık hesapta kredi kartı taksiti kullanılamaz.";
                SeciliTaksit = "Tek Çekim";
                return;
            }

            foreach (var kalem in Sepet)
            {
                if (kalem.StokDagilimlari == null || kalem.StokDagilimlari.Count == 0)
                {
                    kalem.VarsayilanStokDagilimiOlustur();
                }
                else if (kalem.StokDagilimlari.Sum(d => d.Miktar) != kalem.Miktar)
                {
                    kalem.StokDagiliminiGuncelle();
                }
            }

            if (odemeYontemi == "Açık Hesap" && SeciliCari != null &&
                !await RiskKontrolu.OnayAlAsync(_cariServisi, SeciliCari.Id, GenelToplam))
            {
                DurumMesaji = "⚠ Risk limiti nedeniyle açık hesap satış yapılmadı.";
                return;
            }

            DurumMesaji = "Satış kaydediliyor...";

            var cariId = SeciliCari?.Id ?? await VarsayilanCariAlAsync();

            var fatura = new Fatura
            {
                Tur = FaturaTuru.Satis,
                Durum = BelgeDurumu.Taslak,
                FaturaNo = string.Empty, // sunucu müteselsil numara verir (aynı saniyedeki iki satış çakışmaz)
                CariKartId = cariId,
                Tarih = DateTime.Now,
                Aciklama = $"POS Satış - {odemeYontemi} ({(odemeYontemi == "Kredi Kartı" ? SeciliTaksit : "")})"
            };

            foreach (var s in Sepet)
            {
                fatura.Kalemler.Add(s.ToEntity());
            }

            var eklenenFatura = await _faturaServisi.PosSatisiniTamamlaAsync(
                fatura, odemeYontemi, _geriYuklenenBekleyenFaturaId);

            var tamamlananTutar = GenelToplam;
            Temizle();
            DurumMesaji = $"✓ Satış tamamlandı — {eklenenFatura.FaturaNo} — {tamamlananTutar:N2} ₺ — {odemeYontemi}";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"⚠ Satış tamamlanamadı: {ex.Message}";
        }
    }
}
