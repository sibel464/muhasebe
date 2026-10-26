using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using EtkiAlani.Modeller.Finans;
using Masaustu.Gorunumler.Pencere;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.GorunumModelleri;

public class CekSenetGorunumModeli : TemelGorunumModeli
{
    private readonly ICekSenetServisi _cekSenetServisi;
    private readonly ICariServisi _cariServisi;
    private readonly IKasaServisi _kasaServisi;
    private readonly IBankaServisi _bankaServisi;
    
    private IReadOnlyList<CekSenet> _tumKayıtlar = new List<CekSenet>();

    private bool _yukleniyor;
    private string _aramaMetni = string.Empty;
    private CekSenetOzet? _seciliKayit;

    // Filtreler
    private string _seciliTur = "Tümü";
    private string _seciliDurum = "Tümü";
    private DateTime? _baslangicTarihi;
    private DateTime? _bitisTarihi;

    // KPI
    private decimal _portfoydekiToplam;
    private decimal _tahsilEdilenToplam;
    private int _yaklasanVadeSayisi;

    public bool Yukleniyor { get => _yukleniyor; set => OzellikAyarla(ref _yukleniyor, value); }
    public string AramaMetni { get => _aramaMetni; set { if (OzellikAyarla(ref _aramaMetni, value)) UygulaFiltre(); } }
    public CekSenetOzet? SeciliKayit { get => _seciliKayit; set => OzellikAyarla(ref _seciliKayit, value); }
    public string SeciliTur { get => _seciliTur; set { if (OzellikAyarla(ref _seciliTur, value)) UygulaFiltre(); } }
    public string SeciliDurum { get => _seciliDurum; set { if (OzellikAyarla(ref _seciliDurum, value)) UygulaFiltre(); } }
    public DateTime? BaslangicTarihi { get => _baslangicTarihi; set { if (OzellikAyarla(ref _baslangicTarihi, value)) UygulaFiltre(); } }
    public DateTime? BitisTarihi { get => _bitisTarihi; set { if (OzellikAyarla(ref _bitisTarihi, value)) UygulaFiltre(); } }
    
    public decimal PortfoydekiToplam { get => _portfoydekiToplam; set => OzellikAyarla(ref _portfoydekiToplam, value); }
    public decimal TahsilEdilenToplam { get => _tahsilEdilenToplam; set => OzellikAyarla(ref _tahsilEdilenToplam, value); }
    public int YaklasanVadeSayisi { get => _yaklasanVadeSayisi; set => OzellikAyarla(ref _yaklasanVadeSayisi, value); }

    public ObservableCollection<string> Turler { get; } = new() { "Tümü", "Alınan Çek", "Verilen Çek", "Alınan Senet", "Verilen Senet" };
    public ObservableCollection<string> Durumlar { get; } = new() { "Tümü", "Portföyde", "Ciro Edildi", "Tahsile Verildi", "Tahsil Edildi", "Ödendi", "Karşılıksız", "İade Edildi" };
    public ObservableCollection<CekSenetOzet> TabloVerisi { get; } = new();

    public ICommand YukleCommand { get; }
    public ICommand EkleCommand { get; }
    public ICommand DurumDegistirCommand { get; }
    public ICommand SilCommand { get; }
    public ICommand SifirlaCommand { get; }
    public ICommand ExcelAktarKomutu { get; }

    public CekSenetGorunumModeli(
        ICekSenetServisi cekSenetServisi, 
        ICariServisi cariServisi, 
        IKasaServisi kasaServisi, 
        IBankaServisi bankaServisi)
    {
        _cekSenetServisi = cekSenetServisi;
        _cariServisi = cariServisi;
        _kasaServisi = kasaServisi;
        _bankaServisi = bankaServisi;

        ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.Aktar(TabloVerisi, "Cek_Senet_Listesi"));

        YukleCommand = new KomutSarici(async () => await YukleAsync());
        EkleCommand = new KomutSarici(Ekle);
        DurumDegistirCommand = new KomutSarici(param => DurumDegistir(param as CekSenetOzet));
        SilCommand = new KomutSarici(async param => await SilAsync(param as CekSenetOzet));
        SifirlaCommand = new KomutSarici(Sifirla);
    }

    private async Task YukleAsync()
    {
        if (Yukleniyor) return;
        Yukleniyor = true;

        try
        {
            _tumKayıtlar = await _cekSenetServisi.TumunuGetirAsync();
            UygulaFiltre();
            KpiHesapla();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private void Ekle()
    {
        var pencere = new CekSenetEklePenceresi(_cekSenetServisi, _cariServisi)
        {
            Owner = Application.Current.MainWindow
        };
        if (pencere.ShowDialog() == true)
        {
            _ = YukleAsync();
        }
    }

    private void DurumDegistir(CekSenetOzet? kayit)
    {
        var hedef = kayit ?? SeciliKayit;
        if (hedef == null)
        {
            MessageBox.Show("Lütfen işlem yapılacak belgeyi seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var pencere = new CekSenetDurumPenceresi(_cekSenetServisi, _kasaServisi, _bankaServisi, _cariServisi, hedef)
        {
            Owner = Application.Current.MainWindow
        };

        if (pencere.ShowDialog() == true)
        {
            _ = YukleAsync();
        }
    }

    private async Task SilAsync(CekSenetOzet? kayit)
    {
        var hedef = kayit ?? SeciliKayit;
        if (hedef == null) return;

        var cev = MessageBox.Show($"{hedef.Tur} (No: {hedef.BelgeNo}) kaydını silmek istediğinize emin misiniz?", "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (cev == MessageBoxResult.Yes)
        {
            try
            {
                await _cekSenetServisi.SilAsync(hedef.Id, "Kullanıcı isteği ile silindi.");
                await YukleAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Sifirla()
    {
        AramaMetni = string.Empty;
        SeciliTur = "Tümü";
        SeciliDurum = "Tümü";
        BaslangicTarihi = null;
        BitisTarihi = null;
        UygulaFiltre();
    }

    private void UygulaFiltre()
    {
        var filtrelenmis = _tumKayıtlar.AsEnumerable();

        if (SeciliTur != "Tümü")
        {
            var tur = SeciliTur switch
            {
                "Alınan Çek" => CekSenetTuru.AlinanCek,
                "Verilen Çek" => CekSenetTuru.VerilenCek,
                "Alınan Senet" => CekSenetTuru.AlinanSenet,
                "Verilen Senet" => CekSenetTuru.VerilenSenet,
                _ => (CekSenetTuru?)null
            };
            if (tur.HasValue) filtrelenmis = filtrelenmis.Where(x => x.Tur == tur.Value);
        }

        if (SeciliDurum != "Tümü")
        {
            var durum = SeciliDurum switch
            {
                "Portföyde" => CekSenetDurumu.Portfoyde,
                "Ciro Edildi" => CekSenetDurumu.CiroEdildi,
                "Tahsile Verildi" => CekSenetDurumu.TahsileVerildi,
                "Tahsil Edildi" => CekSenetDurumu.TahsilEdildi,
                "Ödendi" => CekSenetDurumu.Odendi,
                "Karşılıksız" => CekSenetDurumu.Karsiliksiz,
                "İade Edildi" => CekSenetDurumu.IadeEdildi,
                _ => (CekSenetDurumu?)null
            };
            if (durum.HasValue) filtrelenmis = filtrelenmis.Where(x => x.Durum == durum.Value);
        }

        if (BaslangicTarihi.HasValue)
        {
            filtrelenmis = filtrelenmis.Where(x => x.VadeTarihi.Date >= BaslangicTarihi.Value.Date);
        }

        if (BitisTarihi.HasValue)
        {
            filtrelenmis = filtrelenmis.Where(x => x.VadeTarihi.Date <= BitisTarihi.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(AramaMetni))
        {
            var ara = AramaMetni.ToLower();
            filtrelenmis = filtrelenmis.Where(x => 
                (x.BelgeNo != null && x.BelgeNo.ToLower().Contains(ara)) ||
                (x.Kesideci != null && x.Kesideci.ToLower().Contains(ara)) ||
                (x.CariKart?.Unvan != null && x.CariKart.Unvan.ToLower().Contains(ara)));
        }

        TabloVerisi.Clear();
        foreach (var item in filtrelenmis.OrderBy(x => x.VadeTarihi))
        {
            TabloVerisi.Add(new CekSenetOzet
            {
                Id = item.Id,
                Tur = item.Tur switch { CekSenetTuru.AlinanCek => "Alınan Çek", CekSenetTuru.VerilenCek => "Verilen Çek", CekSenetTuru.AlinanSenet => "Alınan Senet", _ => "Verilen Senet" },
                Durum = item.Durum switch { CekSenetDurumu.Portfoyde => "Portföyde", CekSenetDurumu.CiroEdildi => "Ciro Edildi", CekSenetDurumu.TahsileVerildi => "Tahsile Verildi", CekSenetDurumu.TahsilEdildi => "Tahsil Edildi", CekSenetDurumu.Odendi => "Ödendi", CekSenetDurumu.Karsiliksiz => "Karşılıksız", _ => "İade Edildi" },
                BelgeNo = item.BelgeNo,
                VadeTarihi = item.VadeTarihi,
                KalanGun = (item.VadeTarihi - DateTime.Today).Days,
                Tutar = item.Tutar,
                CariUnvan = item.CariKart?.Unvan ?? "",
                Kesideci = item.Kesideci,
                BankaAdi = item.BankaAdi ?? ""
            });
        }
    }

    private void KpiHesapla()
    {
        PortfoydekiToplam = _tumKayıtlar.Where(x => x.Durum == CekSenetDurumu.Portfoyde && (x.Tur == CekSenetTuru.AlinanCek || x.Tur == CekSenetTuru.AlinanSenet)).Sum(x => x.Tutar);
        TahsilEdilenToplam = _tumKayıtlar.Where(x => x.Durum == CekSenetDurumu.TahsilEdildi).Sum(x => x.Tutar);
        YaklasanVadeSayisi = _tumKayıtlar.Count(x => x.Durum == CekSenetDurumu.Portfoyde && (x.VadeTarihi - DateTime.Today).Days <= 7 && (x.VadeTarihi - DateTime.Today).Days >= 0);
    }
}

public class CekSenetOzet
{
    public int Id { get; set; }
    public string Tur { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public string BelgeNo { get; set; } = string.Empty;
    public DateTime VadeTarihi { get; set; }
    public int KalanGun { get; set; }
    public decimal Tutar { get; set; }
    public string CariUnvan { get; set; } = string.Empty;
    public string Kesideci { get; set; } = string.Empty;
    public string BankaAdi { get; set; } = string.Empty;
    
    public string VadeDurumuGorsel => KalanGun < 0 ? "Gecikti" : KalanGun <= 7 ? "Yaklaştı" : "Normal";
}
