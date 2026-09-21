using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using EtkiAlani.Modeller.Muhasebe;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class YevmiyeFisEkleDuzenleGorunumModeli : TemelGorunumModeli
{
    private readonly IYevmiyeServisi _yevmiyeServisi;
    private readonly IHesapPlaniServisi _hesapServisi;

    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;

    private string _fisNo = string.Empty;
    private FisTuru _fisTuru = FisTuru.Mahsup;
    private DateTime _tarih = DateTime.UtcNow;
    private string _aciklama = string.Empty;
    private string _referans = string.Empty;

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

    public string FisNo
    {
        get => _fisNo;
        set => OzellikAyarla(ref _fisNo, value);
    }

    public FisTuru FisTuru
    {
        get => _fisTuru;
        set => OzellikAyarla(ref _fisTuru, value);
    }

    public DateTime Tarih
    {
        get => _tarih;
        set => OzellikAyarla(ref _tarih, value);
    }

    public string Aciklama
    {
        get => _aciklama;
        set => OzellikAyarla(ref _aciklama, value);
    }

    public string Referans
    {
        get => _referans;
        set => OzellikAyarla(ref _referans, value);
    }

    public ObservableCollection<YevmiyeSatiriDto> Satirlar { get; } = new();

    public decimal ToplamBorc => Satirlar.Sum(s => s.Borc);
    public decimal ToplamAlacak => Satirlar.Sum(s => s.Alacak);
    public decimal Fark => ToplamBorc - ToplamAlacak;
    public bool DengedeMi => Math.Abs(Fark) < 0.001m && Satirlar.Count >= 2;

    public ICommand SatirEkleKomutu { get; }
    public ICommand SatirSilKomutu { get; }
    public ICommand KaydetKomutu { get; }
    public ICommand OnaylaKomutu { get; }

    public YevmiyeFisEkleDuzenleGorunumModeli(IYevmiyeServisi yevmiyeServisi, IHesapPlaniServisi hesapServisi)
    {
        _yevmiyeServisi = yevmiyeServisi;
        _hesapServisi = hesapServisi;

        SatirEkleKomutu = new KomutSarici(SatirEkle);
        SatirSilKomutu = new KomutSarici(p => SatirSil(p as YevmiyeSatiriDto));
        KaydetKomutu = new KomutSarici(async () => await KaydetAsync(onayla: false));
        OnaylaKomutu = new KomutSarici(async () => await KaydetAsync(onayla: true), () => DengedeMi);

        // Seed 2 initial blank rows
        SatirEkle();
        SatirEkle();
    }

    private void SatirEkle()
    {
        var satir = new YevmiyeSatiriDto
        {
            HesapKodu = "100.01",
            HesapAdi = "KASA TRL",
            Aciklama = Aciklama,
            Borc = 0m,
            Alacak = 0m
        };
        Satirlar.Add(satir);
        GuncelleOzellikler();
    }

    private void SatirSil(YevmiyeSatiriDto? satir)
    {
        if (satir != null && Satirlar.Contains(satir))
        {
            Satirlar.Remove(satir);
            GuncelleOzellikler();
        }
    }

    public void GuncelleOzellikler()
    {
        BildirimGonder(nameof(ToplamBorc));
        BildirimGonder(nameof(ToplamAlacak));
        BildirimGonder(nameof(Fark));
        BildirimGonder(nameof(DengedeMi));
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task KaydetAsync(bool onayla)
    {
        if (!DengedeMi && onayla)
        {
            MessageBox.Show($"Fiş dengede değil! Fark: {Fark:N2} TL. Borç ve Alacak eşit olmadan fiş onaylanamaz.", "Denge Engeli", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            Yukleniyor = true;
            DurumMesaji = "Fiş kaydediliyor...";

            var dto = new YevmiyeFisDto
            {
                FisTuru = FisTuru,
                Tarih = Tarih,
                Aciklama = Aciklama,
                Referans = Referans,
                Durum = onayla ? YevmiyeDurumu.Onaylandi : YevmiyeDurumu.Taslak,
                Satirlar = Satirlar.ToList()
            };

            var sonuc = await _yevmiyeServisi.FisOlusturAsync(dto);
            DurumMesaji = $"Fiş [{sonuc.FisNo}] başarıyla kaydedildi.";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Kaydetme hatası: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }
}
