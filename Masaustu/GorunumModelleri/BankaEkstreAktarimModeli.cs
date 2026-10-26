using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Finans;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;
using Uygulama.Dto.Finans360;

namespace Masaustu.GorunumModelleri;

// Ekstre satırının ekrandaki hali: kullanıcı seçimi, işlem türünü ve cariyi değiştirebilir.
public class EkstreSatirModeli : TemelGorunumModeli
{
    public EkstreSatirModeli(EkstreEslesmeDto e) => Kaynak = e;
    public EkstreEslesmeDto Kaynak { get; }
    public DateTime Tarih => Kaynak.Satir.Tarih;
    public string Aciklama => Kaynak.Satir.Aciklama;
    public decimal Tutar => Kaynak.Satir.Tutar;
    public bool Giris => Tutar > 0;
    public bool Duzenlenebilir => !Kaynak.ZatenKayitli && string.IsNullOrEmpty(Kaynak.Sonuc);
    public string Durum => string.IsNullOrEmpty(Kaynak.Sonuc) ? Kaynak.Durum : Kaynak.Sonuc;

    public bool Secili
    {
        get => Kaynak.Secili;
        set { if (Kaynak.Secili != value) { Kaynak.Secili = value; BildirimGonder(); } }
    }

    public EkstreIslemTuru Islem
    {
        get => Kaynak.Islem;
        set
        {
            if (Kaynak.Islem == value) return;
            Kaynak.Islem = value;
            if (value is not (EkstreIslemTuru.Tahsilat or EkstreIslemTuru.Odeme)) { Kaynak.CariId = null; BildirimGonder(nameof(CariId)); }
            Secili = value != EkstreIslemTuru.Atla && (value is not (EkstreIslemTuru.Tahsilat or EkstreIslemTuru.Odeme) || Kaynak.CariId != null);
            BildirimGonder();
        }
    }

    public int? CariId
    {
        get => Kaynak.CariId;
        set
        {
            if (Kaynak.CariId == value) return;
            Kaynak.CariId = value;
            if (value != null && Kaynak.Islem is not (EkstreIslemTuru.Tahsilat or EkstreIslemTuru.Odeme))
            {
                Kaynak.Islem = Giris ? EkstreIslemTuru.Tahsilat : EkstreIslemTuru.Odeme;
                BildirimGonder(nameof(Islem));
            }
            Secili = value != null;
            BildirimGonder();
        }
    }

    public void Yenile() { foreach (var p in new[] { nameof(Durum), nameof(Duzenlenebilir), nameof(Secili) }) BildirimGonder(p); }
}

public class BankaEkstreAktarimModeli : TemelGorunumModeli
{
    public sealed record Secenek<T>(T Deger, string Ad);

    private readonly IBankaEkstreServisi _ekstre;
    private BankaHesabi? _hesap;
    private string _sutunBilgisi = "Bankanızın internet şubesinden hesap hareketlerini Excel (.xlsx) ya da CSV olarak indirip seçin.";
    private string _durum = string.Empty;
    private bool _mesgul;
    private List<EkstreSatiriDto> _satirlar = new();

    public BankaEkstreAktarimModeli(IBankaEkstreServisi ekstre, IReadOnlyList<BankaHesabi> hesaplar, IEnumerable<(int Id, string Unvan)> cariler, int? seciliHesapId)
    {
        _ekstre = ekstre;
        foreach (var h in hesaplar.Where(h => h.ParaBirimi is "TRY" or "TL")) Hesaplar.Add(h);
        Cariler = cariler.OrderBy(c => c.Unvan).Select(c => new Secenek<int?>(c.Id, c.Unvan)).ToList();
        _hesap = Hesaplar.FirstOrDefault(h => h.Id == seciliHesapId) ?? Hesaplar.FirstOrDefault();
        DosyaSecKomutu = new KomutSarici(async () => await DosyaSecAsync(), () => !_mesgul && Hesap != null);
        KaydetKomutu = new KomutSarici(async () => await KaydetAsync(), () => !_mesgul && Satirlar.Any(s => s.Secili && s.Duzenlenebilir));
    }

    public ObservableCollection<BankaHesabi> Hesaplar { get; } = new();
    public ObservableCollection<EkstreSatirModeli> Satirlar { get; } = new();
    public IReadOnlyList<Secenek<int?>> Cariler { get; }
    public IReadOnlyList<Secenek<EkstreIslemTuru>> Islemler { get; } =
    [
        new(EkstreIslemTuru.Atla, "Aktarma"), new(EkstreIslemTuru.Tahsilat, "Cari tahsilat"), new(EkstreIslemTuru.Odeme, "Cari ödeme"),
        new(EkstreIslemTuru.BankaMasrafi, "Banka masrafı (780)"), new(EkstreIslemTuru.FaizGeliri, "Faiz geliri (642)"), new(EkstreIslemTuru.KrediOdemesi, "Kredi ödemesi (300)")
    ];

    public ICommand DosyaSecKomutu { get; }
    public ICommand KaydetKomutu { get; }

    public BankaHesabi? Hesap
    {
        get => _hesap;
        set { if (OzellikAyarla(ref _hesap, value) && _satirlar.Count > 0) _ = EslestirAsync(); }
    }

    public string SutunBilgisi { get => _sutunBilgisi; private set => OzellikAyarla(ref _sutunBilgisi, value); }
    public string Durum { get => _durum; private set => OzellikAyarla(ref _durum, value); }
    public string Ozet => Satirlar.Count == 0 ? "" :
        $"{Satirlar.Count} satır · {Satirlar.Count(s => s.Kaynak.ZatenKayitli)} zaten kayıtlı · {Satirlar.Count(s => s.Secili && s.Duzenlenebilir)} satır aktarılacak";
    public bool Kaydedildi { get; private set; }

    private async Task DosyaSecAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Banka ekstresi (*.xlsx;*.csv)|*.xlsx;*.csv;*.txt", Title = "Banka ekstresini seçin" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var okuma = _ekstre.Oku(dialog.FileName);
            _satirlar = okuma.Satirlar;
            SutunBilgisi = $"{System.IO.Path.GetFileName(dialog.FileName)} — {okuma.SutunBilgisi}" + (okuma.Uyarilar.Count > 0 ? "  (" + string.Join(" ", okuma.Uyarilar) + ")" : "");
            await EslestirAsync();
        }
        catch (Exception ex)
        {
            Durum = "Ekstre okunamadı: " + ex.Message;
        }
    }

    private async Task EslestirAsync()
    {
        if (Hesap == null) return;
        try
        {
            _mesgul = true;
            Durum = "Kayıtlarla eşleştiriliyor...";
            Satirlar.Clear();
            foreach (var e in await _ekstre.EslestirAsync(Hesap.Id, _satirlar))
            {
                var m = new EkstreSatirModeli(e);
                m.PropertyChanged += (_, _) => { BildirimGonder(nameof(Ozet)); CommandManager.InvalidateRequerySuggested(); };
                Satirlar.Add(m);
            }
            Durum = string.Empty;
        }
        catch (Exception ex)
        {
            Durum = "Eşleştirilemedi: " + ex.Message;
        }
        finally
        {
            _mesgul = false;
            BildirimGonder(nameof(Ozet));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task KaydetAsync()
    {
        if (Hesap == null) return;
        var eksik = Satirlar.Where(s => s.Secili && s.Duzenlenebilir && s.Islem is EkstreIslemTuru.Tahsilat or EkstreIslemTuru.Odeme && s.CariId == null).ToList();
        if (eksik.Count > 0) { Durum = $"{eksik.Count} satırda cari seçilmedi."; return; }
        try
        {
            _mesgul = true;
            Durum = "Kaydediliyor...";
            var sonuc = await _ekstre.KaydetAsync(Hesap.Id, Satirlar.Where(s => s.Duzenlenebilir).Select(s => s.Kaynak).ToList());
            foreach (var s in Satirlar) s.Yenile();
            Kaydedildi |= sonuc.Kaydedilen > 0;
            Durum = $"{sonuc.Kaydedilen} hareket kaydedildi." + (sonuc.Hatali > 0 ? $" {sonuc.Hatali} satır kaydedilemedi (Durum sütununa bakın)." : "");
        }
        catch (Exception ex)
        {
            Durum = "Kaydedilemedi: " + ex.Message;
        }
        finally
        {
            _mesgul = false;
            BildirimGonder(nameof(Ozet));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
