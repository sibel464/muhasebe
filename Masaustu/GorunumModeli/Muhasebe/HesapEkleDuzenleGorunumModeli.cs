using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;
using EtkiAlani.Modeller.Muhasebe;

namespace Masaustu.GorunumModeli.Muhasebe;

public class UstHesapGosterimDto
{
    public int Id { get; set; }
    public string HesapAdKoduGosterimi { get; set; } = string.Empty;
}

public class HesapEkleDuzenleGorunumModeli : TemelGorunumModeli
{
    private readonly IHesapPlaniServisi _hesapServisi;
    
    private int? _id;
    private string _hesapKodu = string.Empty;
    private string _hesapAdi = string.Empty;
    private int? _seciliUstHesapId;
    private string _aciklama = string.Empty;
    private bool _aktifMi = true;
    
    private bool _islemYapiliyor;
    private string _hataMesaji = string.Empty;
    private string _durumMesaji = string.Empty;
    private bool _kritikAlanlarDegistirilebilir = true;
    private bool _hesapKoduDegistirilebilir = true;

    private HesapTipi _seciliHesapTipi = HesapTipi.Aktif;
    private BorcAlacakYonu _seciliBakiyeYonu = BorcAlacakYonu.Borc;
    private string _seciliParaBirimi = "TRY";

    public Action? PencereKapat;
    public Action? IptalEt;

    public ObservableCollection<UstHesapGosterimDto> TumHesaplar { get; } = new();
    
    public Dictionary<HesapTipi, string> HesapTipleri { get; } = new()
    {
        { HesapTipi.Aktif, "Aktif (Varlık)" },
        { HesapTipi.Pasif, "Pasif (Kaynak)" },
        { HesapTipi.Gelir, "Gelir" },
        { HesapTipi.Gider, "Gider" }
    };

    public Dictionary<BorcAlacakYonu, string> BakiyeYonleri { get; } = new()
    {
        { BorcAlacakYonu.Borc, "Borç Bakiyesi Verir" },
        { BorcAlacakYonu.Alacak, "Alacak Bakiyesi Verir" }
    };

    public List<string> ParaBirimleri { get; } = new() { "TRY", "USD", "EUR", "GBP" };

    public string PencereBasligi => _id.HasValue ? "Hesabı Düzenle" : "Yeni Hesap Ekle";

    public string HesapKodu
    {
        get => _hesapKodu;
        set => OzellikAyarla(ref _hesapKodu, value);
    }

    public string HesapAdi
    {
        get => _hesapAdi;
        set => OzellikAyarla(ref _hesapAdi, value);
    }

    public int? SeciliUstHesapId
    {
        get => _seciliUstHesapId;
        set => OzellikAyarla(ref _seciliUstHesapId, value);
    }

    public HesapTipi SeciliHesapTipi
    {
        get => _seciliHesapTipi;
        set => OzellikAyarla(ref _seciliHesapTipi, value);
    }

    public BorcAlacakYonu SeciliBakiyeYonu
    {
        get => _seciliBakiyeYonu;
        set => OzellikAyarla(ref _seciliBakiyeYonu, value);
    }

    public string SeciliParaBirimi
    {
        get => _seciliParaBirimi;
        set => OzellikAyarla(ref _seciliParaBirimi, value);
    }

    public string Aciklama
    {
        get => _aciklama;
        set => OzellikAyarla(ref _aciklama, value);
    }

    public bool AktifMi
    {
        get => _aktifMi;
        set => OzellikAyarla(ref _aktifMi, value);
    }

    public bool KritikAlanlarDegistirilebilir
    {
        get => _kritikAlanlarDegistirilebilir;
        set => OzellikAyarla(ref _kritikAlanlarDegistirilebilir, value);
    }

    public bool HesapKoduDegistirilebilir
    {
        get => _hesapKoduDegistirilebilir;
        set => OzellikAyarla(ref _hesapKoduDegistirilebilir, value);
    }

    public bool IslemYapiliyor
    {
        get => _islemYapiliyor;
        set => OzellikAyarla(ref _islemYapiliyor, value);
    }

    public string HataMesaji
    {
        get => _hataMesaji;
        set
        {
            OzellikAyarla(ref _hataMesaji, value);
            BildirimGonder(nameof(HataMesajiGorunurMu));
        }
    }

    public bool HataMesajiGorunurMu => !string.IsNullOrEmpty(HataMesaji);

    public string DurumMesaji
    {
        get => _durumMesaji;
        set => OzellikAyarla(ref _durumMesaji, value);
    }

    public ICommand KaydetKomutu { get; }
    public ICommand KapatKomutu { get; }

    public HesapEkleDuzenleGorunumModeli(IHesapPlaniServisi hesapServisi)
    {
        _hesapServisi = hesapServisi;
        
        KaydetKomutu = new KomutSarici(async () => await KaydetAsync());
        KapatKomutu = new KomutSarici(() => IptalEt?.Invoke());

        _ = UstHesaplariYukleAsync();
    }

    private async Task UstHesaplariYukleAsync()
    {
        try
        {
            var liste = await _hesapServisi.HesapPlaniGetirAsync("", true); // Sadece aktif hesapları getir
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                TumHesaplar.Clear();
                TumHesaplar.Add(new UstHesapGosterimDto { Id = 0, HesapAdKoduGosterimi = "-- Yok (Ana Hesap) --" }); // Null option
                
                foreach (var h in liste.OrderBy(x => x.HesapKodu))
                {
                    // Self check during load
                    if (_id.HasValue && h.Id == _id.Value) continue;
                    
                    TumHesaplar.Add(new UstHesapGosterimDto 
                    { 
                        Id = h.Id, 
                        HesapAdKoduGosterimi = $"{h.HesapKodu} - {h.HesapAdi}" 
                    });
                }
            });
        }
        catch (Exception)
        {
            // Sessizce yut
        }
    }

    public void Yukle(int hesapId)
    {
        _id = hesapId;
        BildirimGonder(nameof(PencereBasligi));
        _ = HesapDetayYukleAsync();
    }

    private async Task HesapDetayYukleAsync()
    {
        if (!_id.HasValue) return;

        IslemYapiliyor = true;
        try
        {
            // UI'dan erişmek için HesapPlaniGetir kullanılabilir
            var liste = await _hesapServisi.HesapPlaniGetirAsync("");
            var hesap = liste.FirstOrDefault(h => h.Id == _id.Value);
            if (hesap != null)
            {
                HesapKodu = hesap.HesapKodu;
                HesapAdi = hesap.HesapAdi;
                SeciliHesapTipi = hesap.Tip;
                SeciliBakiyeYonu = hesap.BakiyeYonu;
                SeciliParaBirimi = hesap.ParaBirimiKodu ?? "TRY";
                SeciliUstHesapId = hesap.UstHesapId ?? 0;
                Aciklama = hesap.Aciklama;
                AktifMi = hesap.AktifMi;
                
                if (hesap.SistemHesabi)
                {
                    HesapKoduDegistirilebilir = false;
                }

                if (hesap.HareketGordu)
                {
                    KritikAlanlarDegistirilebilir = false;
                    HesapKoduDegistirilebilir = false;
                }
            }
        }
        catch (Exception ex)
        {
            HataMesaji = $"Hesap detayları yüklenirken hata oluştu: {ex.Message}";
        }
        finally
        {
            IslemYapiliyor = false;
        }
    }

    private async Task KaydetAsync()
    {
        HataMesaji = string.Empty;
        DurumMesaji = string.Empty;

        if (string.IsNullOrWhiteSpace(HesapKodu))
        {
            HataMesaji = "Hesap Kodu zorunludur.";
            return;
        }

        if (string.IsNullOrWhiteSpace(HesapAdi))
        {
            HataMesaji = "Hesap Adı zorunludur.";
            return;
        }

        try
        {
            IslemYapiliyor = true;

            var dto = new HesapPlaniDto
            {
                Id = _id ?? 0,
                HesapKodu = HesapKodu,
                HesapAdi = HesapAdi,
                Tip = SeciliHesapTipi,
                BakiyeYonu = SeciliBakiyeYonu,
                ParaBirimiKodu = SeciliParaBirimi,
                Aciklama = Aciklama,
                AktifMi = AktifMi,
                UstHesapId = SeciliUstHesapId > 0 ? SeciliUstHesapId : null
            };

            if (_id.HasValue)
            {
                await _hesapServisi.GuncelleHesapAsync(dto, Guid.Empty); // Servis VarsayilanSirketId kullanacak
                DurumMesaji = "Hesap başarıyla güncellendi.";
            }
            else
            {
                await _hesapServisi.EkleHesapAsync(dto, Guid.Empty);
                DurumMesaji = "Hesap başarıyla eklendi.";
            }

            await Task.Delay(500); // UI tepkisi için ufak gecikme
            PencereKapat?.Invoke();
        }
        catch (Exception ex)
        {
            HataMesaji = ex.Message; // Domain exceptions are clean enough
        }
        finally
        {
            IslemYapiliyor = false;
        }
    }
}
