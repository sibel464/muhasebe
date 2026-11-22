using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.StokOperasyon;
using Masaustu.Yardimcilar;
using EtkiAlani.Modeller.Stok;


namespace Masaustu.GorunumModelleri;

public class StokSayimKalemModeli : TemelGorunumModeli
{
    private decimal? _sayilanMiktar;

    public int UrunId { get; init; }
    public string UrunKodu { get; init; } = string.Empty;
    public string UrunAdi { get; init; } = string.Empty;
    public string Barkod { get; init; } = string.Empty;
    public string Birim { get; init; } = string.Empty;
    public decimal SistemMiktari { get; init; }

    // Boş = bu ürün sayılmadı; onaya gönderilmez, stoğuna dokunulmaz.
    // Eskiden sayılan miktar sistem stoğuyla başlıyordu: barkod okutunca üstüne +1 eklenip 10 olan ürün 11 sayılıyordu;
    // sayılmayan ürünler de açılıştaki stokla gönderildiğinden sayım sırasında yapılan satışlar onayda geri alınıyordu.
    public decimal? SayilanMiktar
    {
        get => _sayilanMiktar;
        set
        {
            if (value < 0) value = 0;
            if (OzellikAyarla(ref _sayilanMiktar, value))
            {
                BildirimGonder(nameof(Sayildi));
                BildirimGonder(nameof(FarkMiktari));
                BildirimGonder(nameof(FarkTuru));
            }
        }
    }

    public bool Sayildi => SayilanMiktar.HasValue;
    public decimal? FarkMiktari => SayilanMiktar - SistemMiktari;
    public string FarkTuru => FarkMiktari switch
    {
        null => "Sayılmadı",
        > 0 => "Fazla +",
        < 0 => "Eksik -",
        _ => "Tam"
    };
}

public class StokSayimGorunumModeli : TemelGorunumModeli
{
    private readonly IStokOperasyonServisi _operasyonServisi;
    private readonly IUrunServisi _urunServisi;
    private readonly IDepoServisi _depoServisi;
    private readonly IAktifFirmaBaglami _firmaBaglami;
    private readonly IKullaniciServisi _kullaniciServisi;

    private int? _seciliDepoId;
    private int? _yukluDepoId;
    private DateTime _tarih = DateTime.Today;
    private string _barkodArama = string.Empty;
    private string _aramaMetni = string.Empty;
    private string _aciklama = string.Empty;
    private string _hataMesaji = string.Empty;
    private string _basariMesaji = string.Empty;
    private string _sonOkuma = string.Empty;
    private bool _kaydediliyor;
    private bool _yalnizSayilanlar;
    private bool _sayilmayanlariSifirla;
    private StokSayimKalemModeli? _seciliKalem;

    public ObservableCollection<DepoOption> Depolar { get; } = new();
    public TopluKoleksiyon<StokSayimKalemModeli> Kalemler { get; } = new();
    // Barkod okutmada ürünü bulmak için (on binlerce satırda her okutmada listeyi taramamak için).
    private readonly Dictionary<int, StokSayimKalemModeli> _kalemDizini = new();
    // Kayıtlı sayım yüklenirken her satır için özet yeniden hesaplanmasın.
    private bool _topluYukleme;
    public ICollectionView KalemGorunumu { get; }

    public int? SeciliDepoId
    {
        get => _seciliDepoId;
        set
        {
            if (value == _seciliDepoId) return;
            // Sayıma başlanmışken depo değişirse okutulanlar kaybolur; önce sor.
            if (_yukluDepoId.HasValue && Kalemler.Any(k => k.Sayildi) && !OnayIste(
                    "Bu depoda sayılan ürünler var. Depo değişirse girilen sayımlar silinir. Devam edilsin mi?"))
            {
                BildirimGonder(nameof(SeciliDepoId));
                return;
            }
            if (OzellikAyarla(ref _seciliDepoId, value)) _ = DepoUrunleriniYukleAsync();
        }
    }

    // Yarıda bırakılıp kaydedilmiş ("Sayımda") sayım; doluysa onay ve kaydet bu fişi günceller.
    private int? _taslakSayimId;
    private int? _acilacakSayimId;
    private string _taslakBilgisi = string.Empty;
    public string TaslakBilgisi { get => _taslakBilgisi; private set => OzellikAyarla(ref _taslakBilgisi, value); }
    public ICommand TaslakKaydetKomutu => _taslakKaydetKomutu ??= new KomutSarici(async _ => await TaslakKaydetAsync(), _ => !Kaydediliyor);
    private ICommand? _taslakKaydetKomutu;

    private void TaslakAyarla(int? id, string? no)
    {
        _taslakSayimId = id;
        TaslakBilgisi = id.HasValue ? $"Kayıtlı sayım: {no} (devam ediliyor — stoğa işlenmedi)" : string.Empty;
    }

    public async Task TaslakKaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;
        if (!SeciliDepoId.HasValue) { HataMesaji = "Sayım yapılacak depo seçilmelidir."; return; }
        var kalemler = Kalemler.Where(k => k.Sayildi).Select(k => new StokSayimKalemIstegi(k.UrunId, k.SayilanMiktar!.Value)).ToList();
        if (kalemler.Count == 0) { HataMesaji = "Kaydedilecek sayım yok. Önce ürün okutun ya da miktar yazın."; return; }

        Kaydediliyor = true;
        try
        {
            int kullaniciId = _kullaniciServisi.AktifKullanici?.Id ?? 1;
            var sayim = await _operasyonServisi.SayimTaslakKaydetAsync(_taslakSayimId,
                new StokSayimIstegi(SeciliDepoId.Value, Tarih, Aciklama, kalemler), _firmaBaglami.SirketId, kullaniciId);
            TaslakAyarla(sayim.Id, sayim.SayimNo);
            KaydedilmemisDegisiklik = false;
            BasariMesaji = $"Sayım kaydedildi ({sayim.SayimNo}, {kalemler.Count} ürün). Stok değişmedi; daha sonra kaldığınız yerden devam edebilirsiniz.";
        }
        catch (Exception ex) { HataMesaji = ex.Message; }
        finally { Kaydediliyor = false; }
    }

    // Depo yüklenince: açılması istenen sayım ya da bu depoda yarım kalmış sayım varsa sayılanlar geri yüklenir.
    private async Task TaslagiYukleAsync(int depoId)
    {
        TaslakAyarla(null, null);
        int? id = _acilacakSayimId;
        _acilacakSayimId = null;
        if (id == null)
        {
            var acik = (await _operasyonServisi.SayimlariListeleAsync(_firmaBaglami.SirketId))
                .FirstOrDefault(s => s.DepoId == depoId && s.Durum is StokSayimDurumu.Sayimda or StokSayimDurumu.Taslak);
            if (acik == null || !OnayIste($"Bu depoda yarım kalmış sayım var ({acik.SayimNo}, {acik.Tarih:dd.MM.yyyy}, {acik.KalemSayisi} ürün). Kaldığınız yerden devam edilsin mi?\n\nHayır derseniz yeni sayım başlar; eskisi Sayım Geçmişi'nde kalır."))
                return;
            id = acik.Id;
        }
        var sayim = await _operasyonServisi.SayimGetirAsync(id.Value, _firmaBaglami.SirketId);
        if (sayim == null || sayim.DepoId != depoId || sayim.Durum is StokSayimDurumu.Onaylandi or StokSayimDurumu.Iptal) return;
        foreach (var sk in sayim.Kalemler.Where(k => !k.Silindi))
        {
            _kalemDizini.TryGetValue(sk.UrunId, out var kalem);
            if (kalem != null) kalem.SayilanMiktar = sk.SayilanMiktar;
        }
        Tarih = sayim.Tarih;
        Aciklama = sayim.Aciklama;
        TaslakAyarla(sayim.Id, sayim.SayimNo);
    }

    public DateTime Tarih { get => _tarih; set => OzellikAyarla(ref _tarih, value); }
    public string BarkodArama { get => _barkodArama; set => OzellikAyarla(ref _barkodArama, value); }
    public string Aciklama { get => _aciklama; set => OzellikAyarla(ref _aciklama, value); }
    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }
    public string BasariMesaji { get => _basariMesaji; set => OzellikAyarla(ref _basariMesaji, value); }
    public string SonOkuma { get => _sonOkuma; set => OzellikAyarla(ref _sonOkuma, value); }

    public string AramaMetni
    {
        get => _aramaMetni;
        set { if (OzellikAyarla(ref _aramaMetni, value)) KalemGorunumu.Refresh(); }
    }

    public bool YalnizSayilanlar
    {
        get => _yalnizSayilanlar;
        set { if (OzellikAyarla(ref _yalnizSayilanlar, value)) KalemGorunumu.Refresh(); }
    }

    // İşaretlenirse sayılmayan (okutulmayan) ürünlerin bu depodaki stoğu sıfırlanır: tam (genel) sayım içindir.
    public bool SayilmayanlariSifirla
    {
        get => _sayilmayanlariSifirla;
        set { if (OzellikAyarla(ref _sayilmayanlariSifirla, value)) OzetGuncelle(); }
    }

    public StokSayimKalemModeli? SeciliKalem
    {
        get => _seciliKalem;
        set => OzellikAyarla(ref _seciliKalem, value);
    }

    public string Ozet { get; private set; } = string.Empty;

    public bool Kaydediliyor
    {
        get => _kaydediliyor;
        private set { OzellikAyarla(ref _kaydediliyor, value); CommandManager.InvalidateRequerySuggested(); }
    }

    public ICommand BarkodOkutKomutu { get; }
    public ICommand SayimOnaylaKomutu { get; }
    public ICommand IptalKomutu { get; }
    public ICommand SistemStokunuYazKomutu { get; }
    public ICommand SayimiTemizleKomutu { get; }

    public Action? KapatAction { get; set; }
    public Action<StokSayimKalemModeli>? KalemeGitAction { get; set; }
    public Func<string, bool> OnayIste { get; set; } = mesaj =>
        MessageBox.Show(mesaj, "Stok Sayımı", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public StokSayimGorunumModeli(
        IStokOperasyonServisi operasyonServisi,
        IUrunServisi urunServisi,
        IDepoServisi depoServisi,
        IAktifFirmaBaglami firmaBaglami,
        IKullaniciServisi kullaniciServisi)
    {
        _operasyonServisi = operasyonServisi;
        _urunServisi = urunServisi;
        _depoServisi = depoServisi;
        _firmaBaglami = firmaBaglami;
        _kullaniciServisi = kullaniciServisi;

        KalemGorunumu = CollectionViewSource.GetDefaultView(Kalemler);
        KalemGorunumu.Filter = Suz;

        BarkodOkutKomutu = new KomutSarici(async _ => await BarkodOkutAsync(), tekrarGirisli: true);
        SayimOnaylaKomutu = new KomutSarici(async _ => await SayimOnaylaAsync(), _ => !Kaydediliyor);
        IptalKomutu = new KomutSarici(() => KapatAction?.Invoke());
        // Seçili satırda sayılan = sistem stoğu (ürün tam çıktıysa elle yazmamak için).
        SistemStokunuYazKomutu = new KomutSarici(_ =>
        {
            if (SeciliKalem == null) return;
            SeciliKalem.SayilanMiktar = SeciliKalem.SistemMiktari;
            OzetGuncelle();
        });
        SayimiTemizleKomutu = new KomutSarici(_ =>
        {
            if (!Kalemler.Any(k => k.Sayildi) || !OnayIste("Girilen bütün sayımlar silinsin mi?")) return;
            foreach (var k in Kalemler) k.SayilanMiktar = null;
            SonOkuma = string.Empty;
            OzetGuncelle();
        });
    }

    public async Task InitAsync(int? devamEdilecekSayimId = null)
    {
        Depolar.Clear();
        var depolar = await _depoServisi.DepolariGetirAsync();
        foreach (var d in depolar)
            Depolar.Add(new DepoOption(d.Id, $"{d.DepoAdi} ({d.DepoKodu})"));

        int? depoId = Depolar.Count > 0 ? Depolar[0].Id : null;
        if (devamEdilecekSayimId is > 0)
        {
            var s = await _operasyonServisi.SayimGetirAsync(devamEdilecekSayimId.Value, _firmaBaglami.SirketId);
            if (s != null && Depolar.Any(d => d.Id == s.DepoId))
            {
                _acilacakSayimId = s.Id;
                depoId = s.DepoId;
            }
        }
        if (depoId.HasValue) SeciliDepoId = depoId;
    }

    private bool Suz(object o)
    {
        if (o is not StokSayimKalemModeli k) return false;
        if (YalnizSayilanlar && !k.Sayildi) return false;
        var a = AramaMetni.Trim();
        if (a.Length == 0) return true;
        return k.UrunAdi.Contains(a, StringComparison.CurrentCultureIgnoreCase)
            || k.UrunKodu.Contains(a, StringComparison.OrdinalIgnoreCase)
            || k.Barkod.Contains(a, StringComparison.OrdinalIgnoreCase);
    }

    // Satırda elle miktar girilince/silinince özet tazelensin.
    // Pencere kapanırken sorulur: uzun sayımlarda yanlışlıkla kapatınca okutulanlar kaybolmasın.
    public bool KaydedilmemisDegisiklik { get; private set; }

    private void KalemDegisti(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(StokSayimKalemModeli.SayilanMiktar) || _topluYukleme) return;
        KaydedilmemisDegisiklik = true;
        OzetGuncelle();
    }

    public void OzetGuncelle()
    {
        var sayilan = Kalemler.Where(k => k.Sayildi).ToList();
        var fazla = sayilan.Count(k => k.FarkMiktari > 0);
        var eksik = sayilan.Count(k => k.FarkMiktari < 0);
        var metin = $"Sayılan: {sayilan.Count} / {Kalemler.Count} ürün   •   Fazla: {fazla}   •   Eksik: {eksik}";
        if (SayilmayanlariSifirla)
        {
            var sifirlanacak = Kalemler.Count(k => !k.Sayildi && k.SistemMiktari != 0);
            metin += $"   •   Sıfırlanacak (sayılmayan): {sifirlanacak}";
        }
        Ozet = metin;
        BildirimGonder(nameof(Ozet));
    }

    private async Task DepoUrunleriniYukleAsync()
    {
        if (!SeciliDepoId.HasValue) return;
        var depoId = SeciliDepoId.Value;

        foreach (var k in Kalemler) k.PropertyChanged -= KalemDegisti;
        Kalemler.HepsiniDegistir([]);
        _kalemDizini.Clear();
        SonOkuma = string.Empty;
        var urunler = await _urunServisi.TumunuGetirAsync();
        // Sistem stoğu doğrudan seçilen deponun kaydından okunur.
        var depoStoklari = await _urunServisi.DepoStoklariniGetirAsync(depoId);
        if (SeciliDepoId != depoId) return; // bu arada başka depo seçildi

        // On binlerce ürün tek bildirimle eklenir; satır satır eklemek pencereyi uzun süre dondururdu.
        var yeniler = urunler.OrderBy(u => u.Ad).Select(u => new StokSayimKalemModeli
        {
            UrunId = u.Id,
            UrunKodu = u.Kod,
            UrunAdi = u.Ad,
            Barkod = u.Barkod ?? "",
            Birim = u.Birim ?? "",
            SistemMiktari = depoStoklari.TryGetValue(u.Id, out var miktar) ? miktar : 0
        }).ToList();
        foreach (var k in yeniler) { k.PropertyChanged += KalemDegisti; _kalemDizini[k.UrunId] = k; }
        Kalemler.HepsiniDegistir(yeniler);
        _yukluDepoId = depoId;
        _topluYukleme = true;
        try { await TaslagiYukleAsync(depoId); KaydedilmemisDegisiklik = false; }
        catch (Exception ex) { HataMesaji = "Kayıtlı sayım yüklenemedi: " + ex.Message; }
        finally { _topluYukleme = false; }
        OzetGuncelle();
    }

    public async Task BarkodOkutAsync()
    {
        var okunan = BarkodArama.Trim();
        if (okunan.Length == 0) return;
        HataMesaji = string.Empty;

        var bulunan = await StokBarkodu.BulAsync(_urunServisi, okunan);
        if (bulunan == null)
        {
            HataMesaji = $"Barkod/kod bulunamadı: {okunan}";
            System.Media.SystemSounds.Exclamation.Play();
            BarkodArama = string.Empty;
            return;
        }

        var (urun, eklenen) = bulunan.Value;
        _kalemDizini.TryGetValue(urun.Id, out var kalem);
        if (kalem == null)
        {
            // Pencere açıldıktan sonra eklenen ürün: sistem stoğu sıfır kabul edilir; onayda gerçek depo stoğu esas alınır.
            kalem = new StokSayimKalemModeli
            {
                UrunId = urun.Id,
                UrunKodu = urun.Kod,
                UrunAdi = urun.Ad,
                Barkod = urun.Barkod ?? "",
                Birim = urun.Birim ?? "",
                SistemMiktari = urun.LokasyonStoklari?.FirstOrDefault(l => l.DepoId == SeciliDepoId)?.MevcutMiktar ?? 0
            };
            kalem.PropertyChanged += KalemDegisti;
            Kalemler.Add(kalem);
            _kalemDizini[kalem.UrunId] = kalem;
        }

        kalem.SayilanMiktar = (kalem.SayilanMiktar ?? 0) + eklenen;
        SonOkuma = $"{kalem.UrunAdi}  +{eklenen:#,##0.###} {kalem.Birim}  →  sayılan {kalem.SayilanMiktar:#,##0.###}";
        BarkodArama = string.Empty;
        SeciliKalem = kalem;
        KalemeGitAction?.Invoke(kalem);
        OzetGuncelle();
    }

    // Onaya gidecek satırlar: yalnız sayılanlar; "sayılmayanları sıfırla" işaretliyse stoğu olan sayılmayanlar 0 olarak.
    public List<StokSayimKalemIstegi> GonderilecekKalemler() =>
        Kalemler.Where(k => k.Sayildi || (SayilmayanlariSifirla && k.SistemMiktari != 0))
            .Select(k => new StokSayimKalemIstegi(k.UrunId, k.SayilanMiktar ?? 0))
            .ToList();

    public async Task SayimOnaylaAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (!SeciliDepoId.HasValue)
        {
            HataMesaji = "Sayım yapılacak depo seçilmelidir.";
            return;
        }

        var kalemler = GonderilecekKalemler();
        if (kalemler.Count == 0)
        {
            HataMesaji = "Henüz sayılan ürün yok. Barkod okutun ya da SAYILAN sütununa miktar yazın.";
            return;
        }

        var sifirlanan = Kalemler.Count(k => !k.Sayildi && SayilmayanlariSifirla && k.SistemMiktari != 0);
        if (sifirlanan > 0 && !OnayIste($"Sayılmayan {sifirlanan} ürünün bu depodaki stoğu SIFIRLANACAK. Emin misiniz?"))
            return;

        Kaydediliyor = true;

        try
        {
            var istek = new StokSayimIstegi(SeciliDepoId.Value, Tarih, Aciklama, kalemler);

            int kullaniciId = _kullaniciServisi.AktifKullanici?.Id ?? 1;
            // Kayıtlı (yarım) sayım varsa o fiş güncellenip onaylanır; yoksa yeni fiş açılıp onaylanır.
            var sayim = _taslakSayimId.HasValue
                ? await _operasyonServisi.SayimTaslakKaydetAsync(_taslakSayimId, istek, _firmaBaglami.SirketId, kullaniciId)
                : await _operasyonServisi.SayimOlusturAsync(istek, _firmaBaglami.SirketId, kullaniciId);
            await _operasyonServisi.SayimOnaylaAsync(sayim.Id, _firmaBaglami.SirketId, kullaniciId);
            TaslakAyarla(null, null);
            KaydedilmemisDegisiklik = false;

            BasariMesaji = $"Sayım tamamlandı ve onaylandı ({sayim.SayimNo}, {kalemler.Count} ürün).";
            await Task.Delay(1000);
            KapatAction?.Invoke();
        }
        catch (Exception ex)
        {
            HataMesaji = ex.Message;
        }
        finally
        {
            Kaydediliyor = false;
        }
    }
}
