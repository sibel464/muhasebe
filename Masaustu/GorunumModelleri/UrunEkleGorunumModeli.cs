using System.Windows.Input;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Kurumsal;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using Masaustu.Servisler;
using System.Collections.ObjectModel;
using EtkiAlani.Modeller.Tanimlar;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.IO;

namespace Masaustu.GorunumModelleri;

// Ürün kartı: hem yeni ürün kaydı hem de var olan ürünün düzenlenmesi bu formla yapılır.
public class UrunEkleGorunumModeli : TemelGorunumModeli
{
    private readonly IUrunServisi _urunServisi;
    private readonly NavigasyonServisi _navigasyon;
    private readonly IMerkeziTanimServisi _tanimServisi;

    private string _kod = string.Empty;
    private string _ad = string.Empty;
    private string _barkod = string.Empty;
    private string _aciklama = string.Empty;
    private string _kategori = string.Empty;
    private string _birim = "Adet";
    private MerkeziTanim? _seciliKategori, _seciliMarka, _seciliBirim;

    private string _kategoriAramaMetni = string.Empty;
    private string _markaAramaMetni = string.Empty;
    private List<MerkeziTanim> _tumKategoriler = new();
    private List<MerkeziTanim> _tumMarkalar = new();

    private bool _kodOtomatik = true;
    private bool _barkodOtomatik = true;
    private string _barkodArama = string.Empty;
    private Urun? _bulunanUrun;

    private decimal _alisFiyati = 0;
    private decimal _toptanSatisFiyati = 0;
    private decimal _fiyatTekCekim = 0;
    private decimal _fiyat2Taksit = 0;
    private decimal _fiyat3Taksit = 0;
    private decimal _fiyat4Taksit = 0;
    private decimal _fiyat5Taksit = 0;
    private decimal _fiyat6Taksit = 0;
    private decimal _fiyat7Taksit = 0;
    private decimal _fiyat8Taksit = 0;
    private decimal _fiyat9Taksit = 0;

    private decimal _kdvOrani = 20;
    private decimal _magazaStok = 0;
    private decimal _depo1Stok = 0;
    private decimal _depo2Stok = 0;
    private decimal _minimumStok = 0;
    private bool _aktif = true;

    // Stok sütunları Stok Operasyon Merkezi ile aynı kurala göre aktif depolardan seçilir:
    // ilk mağaza ve sırasıyla ilk iki depo.
    private Depo? _magazaDepo, _depo1, _depo2;

    private string _hataMesaji = string.Empty;
    private string _basariMesaji = string.Empty;
    private bool _kaydediliyor = false;

    public string Kod { get => _kod; set => OzellikAyarla(ref _kod, value); }
    public string Ad { get => _ad; set => OzellikAyarla(ref _ad, value); }
    public string Barkod { get => _barkod; set => OzellikAyarla(ref _barkod, value); }
    public string Aciklama { get => _aciklama; set => OzellikAyarla(ref _aciklama, value); }
    public string Kategori { get => _kategori; set => OzellikAyarla(ref _kategori, value); }
    public string Birim { get => _birim; set => OzellikAyarla(ref _birim, value); }

    public IReadOnlyList<string> KodlamaSecenekleri { get; } = ["Otomatik", "Manuel"];
    public ObservableCollection<string> KullanilanKodlar { get; } = new();
    public ObservableCollection<string> KullanilanBarkodlar { get; } = new();
    public bool KodOtomatik { get => _kodOtomatik; set { if (OzellikAyarla(ref _kodOtomatik, value)) { BildirimGonder(nameof(Kodlama)); BildirimGonder(nameof(KodSaltOkunur)); if (value) _ = OnizlemeleriYukleAsync(); else Kod = string.Empty; } } }
    public bool BarkodOtomatik { get => _barkodOtomatik; set { if (OzellikAyarla(ref _barkodOtomatik, value)) { BildirimGonder(nameof(BarkodKodlama)); BildirimGonder(nameof(BarkodSaltOkunur)); if (value) _ = OnizlemeleriYukleAsync(); else Barkod = string.Empty; } } }
    public string Kodlama { get => KodOtomatik ? "Otomatik" : "Manuel"; set => KodOtomatik = value == "Otomatik"; }
    public string BarkodKodlama { get => BarkodOtomatik ? "Otomatik" : "Manuel"; set => BarkodOtomatik = value == "Otomatik"; }
    public bool KodSaltOkunur => KodOtomatik; public bool BarkodSaltOkunur => BarkodOtomatik;
    public string BarkodArama { get => _barkodArama; set => OzellikAyarla(ref _barkodArama, value); }
    public string KategoriAramaMetni
    {
        get => _kategoriAramaMetni;
        set
        {
            if (OzellikAyarla(ref _kategoriAramaMetni, value))
                KategoriFiltrele();
        }
    }

    public string MarkaAramaMetni
    {
        get => _markaAramaMetni;
        set
        {
            if (OzellikAyarla(ref _markaAramaMetni, value))
                MarkaFiltrele();
        }
    }

    public bool DuzenlemeModu => _bulunanUrun != null;
    public bool StokGirilebilir => !DuzenlemeModu;
    public string SayfaBasligi => DuzenlemeModu ? $"Ürün Kartı Düzenle — {_bulunanUrun!.Ad}" : "Yeni Ürün Kaydı";
    public string SayfaAltBasligi => DuzenlemeModu
        ? $"Kod: {_bulunanUrun!.Kod}   •   Bilgileri ve fiyatları değiştirip kaydedin."
        : "Ürün bilgilerini, fiyatlarını ve açılış stoğunu girin.";
    public string KaydetDugmeMetni => DuzenlemeModu ? "Değişiklikleri Kaydet" : "Ürünü Kaydet";
    public string StokBilgiMetni => DuzenlemeModu
        ? "Kayıtlı bir ürünün stoğu buradan değiştirilemez. Stok; fatura, irsaliye veya Ürünler ekranındaki Stok Girişi / Stok Çıkışı / Stok Sayımı ile değişir. Böylece her değişikliğin kaydı tutulur."
        : "Buraya yalnızca ürünün şu an elinizde olan açılış stoğunu girin. Kayıttan sonra stok sadece fatura, irsaliye ve stok fişleriyle değişir.";

    public string MagazaEtiketi => _magazaDepo?.DepoAdi ?? "Mağaza (tanımlı değil)";
    public string Depo1Etiketi => _depo1?.DepoAdi ?? "Depo 1 (tanımlı değil)";
    public string Depo2Etiketi => _depo2?.DepoAdi ?? "Depo 2 (tanımlı değil)";
    public bool MagazaGirilebilir => StokGirilebilir && _magazaDepo != null;
    public bool Depo1Girilebilir => StokGirilebilir && _depo1 != null;
    public bool Depo2Girilebilir => StokGirilebilir && _depo2 != null;

    public ObservableCollection<MerkeziTanim> Kategoriler { get; }=new();
    public ObservableCollection<MerkeziTanim> Markalar { get; }=new();
    public ObservableCollection<MerkeziTanim> Birimler { get; }=new();
    public MerkeziTanim? SeciliKategori {get=>_seciliKategori;set{if(OzellikAyarla(ref _seciliKategori,value))Kategori=value?.Ad??"";}}
    public MerkeziTanim? SeciliMarka {get=>_seciliMarka;set=>OzellikAyarla(ref _seciliMarka,value);}
    public MerkeziTanim? SeciliBirim {get=>_seciliBirim;set{if(OzellikAyarla(ref _seciliBirim,value))Birim=value?.KisaAd??value?.Ad??"";}}

    public decimal AlisFiyati { get => _alisFiyati; set => OzellikAyarla(ref _alisFiyati, value); }
    public decimal ToptanSatisFiyati { get => _toptanSatisFiyati; set => OzellikAyarla(ref _toptanSatisFiyati, value); }
    public decimal FiyatTekCekim { get => _fiyatTekCekim; set => OzellikAyarla(ref _fiyatTekCekim, value); }
    public decimal Fiyat2Taksit { get => _fiyat2Taksit; set => OzellikAyarla(ref _fiyat2Taksit, value); }
    public decimal Fiyat3Taksit { get => _fiyat3Taksit; set => OzellikAyarla(ref _fiyat3Taksit, value); }
    public decimal Fiyat4Taksit { get => _fiyat4Taksit; set => OzellikAyarla(ref _fiyat4Taksit, value); }
    public decimal Fiyat5Taksit { get => _fiyat5Taksit; set => OzellikAyarla(ref _fiyat5Taksit, value); }
    public decimal Fiyat6Taksit { get => _fiyat6Taksit; set => OzellikAyarla(ref _fiyat6Taksit, value); }
    public decimal Fiyat7Taksit { get => _fiyat7Taksit; set => OzellikAyarla(ref _fiyat7Taksit, value); }
    public decimal Fiyat8Taksit { get => _fiyat8Taksit; set => OzellikAyarla(ref _fiyat8Taksit, value); }
    public decimal Fiyat9Taksit { get => _fiyat9Taksit; set => OzellikAyarla(ref _fiyat9Taksit, value); }

    public decimal KdvOrani { get => _kdvOrani; set => OzellikAyarla(ref _kdvOrani, value); }
    public decimal MagazaStok
    {
        get => _magazaStok;
        set
        {
            OzellikAyarla(ref _magazaStok, value);
            BildirimGonder(nameof(ToplamStok));
        }
    }
    public decimal Depo1Stok
    {
        get => _depo1Stok;
        set
        {
            OzellikAyarla(ref _depo1Stok, value);
            BildirimGonder(nameof(ToplamStok));
        }
    }
    public decimal Depo2Stok
    {
        get => _depo2Stok;
        set
        {
            OzellikAyarla(ref _depo2Stok, value);
            BildirimGonder(nameof(ToplamStok));
        }
    }
    public decimal ToplamStok => MagazaStok + Depo1Stok + Depo2Stok;

    public decimal MinimumStok { get => _minimumStok; set => OzellikAyarla(ref _minimumStok, value); }
    public bool Aktif { get => _aktif; set => OzellikAyarla(ref _aktif, value); }

    public string HataMesaji { get => _hataMesaji; set => OzellikAyarla(ref _hataMesaji, value); }
    public string BasariMesaji { get => _basariMesaji; set => OzellikAyarla(ref _basariMesaji, value); }
    public bool Kaydediliyor { get => _kaydediliyor; set => OzellikAyarla(ref _kaydediliyor, value); }

    public ICommand KaydetKomutu { get; }
    public ICommand YeniUrunKomutu { get; }
    public ICommand IptalKomutu { get; }
    public ICommand BarkodAraKomutu { get; }
    public ICommand KodEkleKomutu { get; }
    public ICommand BarkodEkleKomutu { get; }

    public UrunEkleGorunumModeli(IUrunServisi urunServisi, IMerkeziTanimServisi tanimServisi, NavigasyonServisi navigasyon)
    {
        _urunServisi = urunServisi;
        _navigasyon = navigasyon;
        _tanimServisi=tanimServisi;

        KaydetKomutu = new KomutSarici(async () => await KaydetAsync(), () => !Kaydediliyor);
        YeniUrunKomutu = new KomutSarici(async () => await YeniUrunaGecAsync());
        IptalKomutu = new KomutSarici(() => _navigasyon.Git("Urun"));
        BarkodAraKomutu = new KomutSarici(_ => BarkodlaAraAsync());
        KodEkleKomutu = new KomutSarici(() => KodOtomatik = false);
        BarkodEkleKomutu = new KomutSarici(() => BarkodOtomatik = false);
        _=BaslatAsync();
    }

    private readonly SemaphoreSlim _yuklemeKilidi = new(1, 1);

    private async Task BaslatAsync()
    {
        await _yuklemeKilidi.WaitAsync();
        try
        {
            await TanimlariYukleAsync();
            await DepolariYukleAsync();
            await KullanilanKodlariYukleAsync();
            await OnizlemeleriYukleKilitsizAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Başlatılırken hata oluştu: {ex.Message}";
        }
        finally
        {
            _yuklemeKilidi.Release();
        }
    }

    private async Task KullanilanKodlariYukleAsync()
    {
        var kodlar = await _urunServisi.KullanilanKodlariGetirAsync();
        KullanilanKodlar.Clear();
        foreach (var x in kodlar) KullanilanKodlar.Add(x);

        var barkodlar = await _urunServisi.KullanilanBarkodlariGetirAsync();
        KullanilanBarkodlar.Clear();
        foreach (var x in barkodlar) KullanilanBarkodlar.Add(x);
    }

    private async Task DepolariYukleAsync()
    {
        var depolar = (await _urunServisi.DepolariGetirAsync()).OrderBy(d => d.Id).ToList();
        _magazaDepo = depolar.FirstOrDefault(d => d.DepoTuru == DepoTuru.Magaza);
        var normal = depolar.Where(d => d.DepoTuru == DepoTuru.Depo).ToList();
        _depo1 = normal.ElementAtOrDefault(0);
        _depo2 = normal.ElementAtOrDefault(1);
        StokAlanlariniBildir();
    }

    private void StokAlanlariniBildir()
    {
        foreach (var ad in new[] { nameof(MagazaEtiketi), nameof(Depo1Etiketi), nameof(Depo2Etiketi),
                     nameof(MagazaGirilebilir), nameof(Depo1Girilebilir), nameof(Depo2Girilebilir), nameof(StokGirilebilir), nameof(StokBilgiMetni) })
            BildirimGonder(ad);
    }

    private void ModBildir()
    {
        foreach (var ad in new[] { nameof(DuzenlemeModu), nameof(SayfaBasligi), nameof(SayfaAltBasligi), nameof(KaydetDugmeMetni) })
            BildirimGonder(ad);
        StokAlanlariniBildir();
    }

    private async Task OnizlemeleriYukleAsync()
    {
        await _yuklemeKilidi.WaitAsync();
        try
        {
            await OnizlemeleriYukleKilitsizAsync();
        }
        catch (Exception ex)
        {
            HataMesaji = $"Önizleme yüklenirken hata: {ex.Message}";
        }
        finally
        {
            _yuklemeKilidi.Release();
        }
    }

    private async Task OnizlemeleriYukleKilitsizAsync()
    {
        if (KodOtomatik) Kod = await _urunServisi.OtomatikKodOnizleAsync();
        if (BarkodOtomatik) Barkod = await _urunServisi.OtomatikBarkodOnizleAsync();
    }

    // Ürünler listesindeki "Düzenle" düğmesi formu bu yöntemle açar.
    public async Task DuzenlemeIcinYukleAsync(int urunId)
    {
        await _yuklemeKilidi.WaitAsync();
        try
        {
            var urun = await _urunServisi.IdIleGetirAsync(urunId);
            if (urun == null) { HataMesaji = "Ürün bulunamadı. Silinmiş olabilir."; return; }
            UrunuFormaYukle(urun);
        }
        catch (Exception ex)
        {
            HataMesaji = $"Ürün yüklenirken hata oluştu: {ex.Message}";
        }
        finally
        {
            _yuklemeKilidi.Release();
        }
    }

    public async Task BarkodlaAraAsync()
    {
        var aranan = BarkodArama.Trim(); if (aranan.Length == 0) return;
        HataMesaji = string.Empty; BasariMesaji = string.Empty;
        var u = await _urunServisi.BarkodIleGetirAsync(aranan);
        if (u == null)
        {
            if (DuzenlemeModu) { HataMesaji = "Bu barkoda ait kayıtlı ürün bulunamadı."; return; }
            BarkodOtomatik = false; Barkod = aranan;
            HataMesaji = "Bu barkoda ait kayıtlı ürün bulunamadı. Yeni ürün olarak devam edebilirsiniz.";
            return;
        }
        UrunuFormaYukle(u);
        BasariMesaji = $"Bu barkoda ait ürün bulundu: {u.Ad} ({u.Kod}). Düzenleme için açıldı.";
    }

    private void UrunuFormaYukle(Urun u)
    {
        _bulunanUrun = u;
        KodOtomatik = false; BarkodOtomatik = false;
        Kod = u.Kod; Barkod = u.Barkod ?? string.Empty; Ad = u.Ad; Aciklama = u.Aciklama ?? "";
        KdvOrani = u.KdvOrani; AlisFiyati = u.AlisFiyati; ToptanSatisFiyati = u.ToptanSatisFiyati;
        MinimumStok = u.MinimumStok; Aktif = u.Aktif;
        SeciliKategori = Kategoriler.FirstOrDefault(x => x.Id == u.KategoriTanimiId);
        SeciliMarka = Markalar.FirstOrDefault(x => x.Id == u.MarkaTanimiId);
        SeciliBirim = Birimler.FirstOrDefault(x => x.Id == u.BirimTanimiId) ?? SeciliBirim;

        for (var t = 1; t <= 9; t++) FiyatAta(t, 0);
        FiyatTekCekim = u.PerakendeSatisFiyati;
        foreach (var f in u.SatisFiyatlari.Where(f => f.AktifMi)) FiyatAta(f.TaksitSayisi, f.Fiyat);

        decimal Stok(Depo? d) => d == null ? 0 : u.LokasyonStoklari.Where(x => x.DepoId == d.Id).Sum(x => x.MevcutMiktar);
        MagazaStok = Stok(_magazaDepo); Depo1Stok = Stok(_depo1); Depo2Stok = Stok(_depo2);
        ModBildir();
    }

    private void FiyatAta(int t, decimal f) { switch(t){case 1:FiyatTekCekim=f;break;case 2:Fiyat2Taksit=f;break;case 3:Fiyat3Taksit=f;break;case 4:Fiyat4Taksit=f;break;case 5:Fiyat5Taksit=f;break;case 6:Fiyat6Taksit=f;break;case 7:Fiyat7Taksit=f;break;case 8:Fiyat8Taksit=f;break;case 9:Fiyat9Taksit=f;break;} }

    public async Task TanimlariYukleAsync(string? grup=null,int? id=null)
    {
        try
        {
            async Task Y(string g,ObservableCollection<MerkeziTanim> h, List<MerkeziTanim>? tumListe = null){var l=await _tanimServisi.ListeleAsync(g);h.Clear();tumListe?.Clear();foreach(var x in l){h.Add(x);tumListe?.Add(x);}}
            if(grup==null||grup==TanimGruplari.UrunKategorisi){await Y(TanimGruplari.UrunKategorisi,Kategoriler, _tumKategoriler);SeciliKategori=Kategoriler.FirstOrDefault(x=>x.Id==id);}
            if(grup==null||grup==TanimGruplari.Marka){await Y(TanimGruplari.Marka,Markalar, _tumMarkalar);SeciliMarka=Markalar.FirstOrDefault(x=>x.Id==id);}
            if(grup==null||grup==TanimGruplari.Birim){await Y(TanimGruplari.Birim,Birimler);SeciliBirim=Birimler.FirstOrDefault(x=>x.Id==id)??Birimler.FirstOrDefault(x=>x.Kod=="ADET"||x.Kod.Equals("ADET", StringComparison.OrdinalIgnoreCase))??Birimler.FirstOrDefault();}
        }
        catch
        {
            Kategoriler.Clear();
            _tumKategoriler.Clear();
            Markalar.Clear();
            _tumMarkalar.Clear();
            Birimler.Clear();
        }
    }

    private void KategoriFiltrele()
    {
        if (string.IsNullOrWhiteSpace(KategoriAramaMetni))
        {
            Kategoriler.Clear();
            foreach (var item in _tumKategoriler) Kategoriler.Add(item);
            return;
        }

        var arama = KategoriAramaMetni.ToLowerInvariant();
        var filtreli = _tumKategoriler.Where(k =>
            k.Ad.ToLowerInvariant().Contains(arama) ||
            k.Kod.ToLowerInvariant().Contains(arama)).ToList();

        Kategoriler.Clear();
        foreach (var item in filtreli) Kategoriler.Add(item);
    }

    private void MarkaFiltrele()
    {
        if (string.IsNullOrWhiteSpace(MarkaAramaMetni))
        {
            Markalar.Clear();
            foreach (var item in _tumMarkalar) Markalar.Add(item);
            return;
        }

        var arama = MarkaAramaMetni.ToLowerInvariant();
        var filtreli = _tumMarkalar.Where(m =>
            m.Ad.ToLowerInvariant().Contains(arama) ||
            m.Kod.ToLowerInvariant().Contains(arama)).ToList();

        Markalar.Clear();
        foreach (var item in filtreli) Markalar.Add(item);
    }

    private async Task KaydetAsync()
    {
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;

        if (!KodOtomatik && string.IsNullOrWhiteSpace(Kod)) { HataMesaji = "Lütfen ürün kodu giriniz veya otomatik seçeneğini işaretleyiniz."; return; }
        if (string.IsNullOrWhiteSpace(Ad)) { HataMesaji = "Lütfen ürün adını giriniz."; return; }
        if (KdvOrani < 0 || KdvOrani > 100) { HataMesaji = "KDV oranı 0 ile 100 arasında olmalıdır."; return; }
        if (AlisFiyati < 0 || ToptanSatisFiyati < 0 || FiyatlarListesi().Any(f => f.Fiyat < 0)) { HataMesaji = "Fiyatlar negatif olamaz."; return; }
        if (MinimumStok < 0) { HataMesaji = "Kritik stok seviyesi negatif olamaz."; return; }
        if (!DuzenlemeModu && (MagazaStok < 0 || Depo1Stok < 0 || Depo2Stok < 0)) { HataMesaji = "Açılış stoğu negatif olamaz."; return; }

        if (SeciliBirim is null && Birimler.Count > 0)
        {
            SeciliBirim = Birimler.FirstOrDefault(x => x.Ad.Equals(Birim, StringComparison.OrdinalIgnoreCase) || (x.KisaAd != null && x.KisaAd.Equals(Birim, StringComparison.OrdinalIgnoreCase))) ?? Birimler.FirstOrDefault();
        }
        if (SeciliBirim is null) { HataMesaji = "Lütfen ölçü birimi seçiniz."; return; }

        Kaydediliyor = true;
        ((KomutSarici)KaydetKomutu).BildirimGonder();

        var urun = new Urun
        {
            Kod = Kod.Trim(),
            Ad = Ad.Trim(),
            Barkod = string.IsNullOrWhiteSpace(Barkod) ? null : Barkod.Trim(),
            Aciklama = Aciklama?.Trim(),
            Kategori = Kategori,
            Birim = Birim,
            KategoriTanimiId=SeciliKategori?.Id,
            MarkaTanimiId=SeciliMarka?.Id,
            BirimTanimiId=SeciliBirim?.Id,
            AlisFiyati = AlisFiyati,
            PerakendeSatisFiyati = FiyatTekCekim,
            ToptanSatisFiyati = ToptanSatisFiyati,
            KdvOrani = KdvOrani,
            MinimumStok = MinimumStok,
            Aktif = Aktif
        };
        var fiyatlar = FiyatlarListesi().Where(f => f.Fiyat > 0).ToList();

        try
        {
            Urun kaydedilenUrun;
            var yeniKayit = _bulunanUrun == null;
            if (yeniKayit)
            {
                foreach (var f in fiyatlar) urun.SatisFiyatlari.Add(f);
                void AcilisStogu(Depo? depo, decimal miktar)
                {
                    if (depo != null && miktar > 0)
                        urun.LokasyonStoklari.Add(new UrunLokasyonStogu { DepoId = depo.Id, MevcutMiktar = miktar });
                }
                AcilisStogu(_magazaDepo, MagazaStok);
                AcilisStogu(_depo1, Depo1Stok);
                AcilisStogu(_depo2, Depo2Stok);
                kaydedilenUrun = await _urunServisi.GuvenliEkleAsync(urun, KodOtomatik, BarkodOtomatik);
            }
            else
            {
                urun.Id = _bulunanUrun!.Id;
                urun.DepoId = _bulunanUrun.DepoId;
                await _urunServisi.KartGuncelleAsync(urun, fiyatlar);
                kaydedilenUrun = await _urunServisi.IdIleGetirAsync(urun.Id) ?? urun;
            }

            UrunuFormaYukle(kaydedilenUrun);
            await KullanilanKodlariYukleAsync();
            BasariMesaji = yeniKayit
                ? $"✓ Ürün kaydedildi.  Kod: {Kod}   Barkod: {Barkod}\nYeni bir ürün eklemek için \"Yeni Ürün\" düğmesine basın."
                : $"✓ Değişiklikler kaydedildi.  Kod: {Kod}";
        }
        catch (Exception ex)
        {
            await KayitHatasiniLoglaAsync(urun, ex);
            HataMesaji = KullaniciHatasiOlustur(ex);
        }
        finally
        {
            Kaydediliyor = false;
            ((KomutSarici)KaydetKomutu).BildirimGonder();
        }
    }

    private List<UrunSatisFiyati> FiyatlarListesi()
    {
        var degerler = new[] { FiyatTekCekim, Fiyat2Taksit, Fiyat3Taksit, Fiyat4Taksit, Fiyat5Taksit, Fiyat6Taksit, Fiyat7Taksit, Fiyat8Taksit, Fiyat9Taksit };
        return degerler.Select((fiyat, i) => new UrunSatisFiyati { TaksitSayisi = i + 1, Fiyat = fiyat, ParaBirimiId = 1, AktifMi = true }).ToList();
    }

    private static string KullaniciHatasiOlustur(Exception exception)
    {
        var postgres = PostgreSqlHatasiniBul(exception);
        return postgres?.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => "Bu ürün kodu veya barkod daha önce kullanılmış (silinmiş bir üründe de olabilir). Farklı bir kod/barkod girin.",
            PostgresErrorCodes.ForeignKeyViolation => "Seçilen kategori, marka, birim, şirket veya depo artık geçerli değil.",
            PostgresErrorCodes.NotNullViolation => "Zorunlu ürün bilgilerinden biri eksik.",
            PostgresErrorCodes.NumericValueOutOfRange => "Girilen fiyat veya stok değeri izin verilen aralığın dışında.",
            _ when exception is UnauthorizedAccessException => "Bu işlem için yetkiniz yok. Yöneticinizden ürün ekleme/düzenleme yetkisi isteyin.",
            // Servisin iş kuralı mesajları kullanıcıya olduğu gibi gösterilir.
            _ when exception is ArgumentException or InvalidOperationException or KeyNotFoundException => exception.Message,
            _ when exception is DbUpdateException => "Ürün veritabanına kaydedilemedi. Bilgileri kontrol edip tekrar deneyin.",
            _ => "Ürün kaydedilemedi. Lütfen bilgileri kontrol edip tekrar deneyin."
        };
    }

    private static async Task KayitHatasiniLoglaAsync(Urun urun, Exception exception)
    {
        try
        {
            var postgres = PostgreSqlHatasiniBul(exception);
            var inner = exception.InnerException;
            var satirlar = new[]
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Urun kaydi basarisiz",
                $"Entity: Id={urun.Id}; Kod={urun.Kod}; Ad={urun.Ad}; Alis={urun.AlisFiyati}; Kdv={urun.KdvOrani}; MinimumStok={urun.MinimumStok}; SirketId={urun.SirketId}; DepoId={urun.DepoId}; KategoriTanimiId={urun.KategoriTanimiId}; MarkaTanimiId={urun.MarkaTanimiId}; BirimTanimiId={urun.BirimTanimiId}; Aktif={urun.Aktif}",
                $"Exception: {exception.GetType().FullName}: {exception.Message}",
                $"InnerException: {inner?.GetType().FullName}: {inner?.Message}",
                $"PostgreSQL: SqlState={postgres?.SqlState}; Constraint={postgres?.ConstraintName}; Table={postgres?.TableName}; Column={postgres?.ColumnName}",
                string.Empty
            };
            var yol = Altyapi.Veri.UygulamaKlasorleri.LogYolu("urun-kayit.log");
            await File.AppendAllLinesAsync(yol, satirlar);
        }
        catch
        {
        }
    }

    private static PostgresException? PostgreSqlHatasiniBul(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException postgres)
                return postgres;
        return null;
    }

    // Formu tamamen boşaltıp yeni ürün kaydına hazırlar (otomatik kod/barkod yeniden önerilir).
    private async Task YeniUrunaGecAsync()
    {
        _bulunanUrun = null;
        HataMesaji = string.Empty;
        BasariMesaji = string.Empty;
        BarkodArama = string.Empty;
        Ad = string.Empty;
        Aciklama = string.Empty;
        SeciliKategori = null;
        SeciliMarka = null;
        KdvOrani = 20;
        AlisFiyati = 0;
        ToptanSatisFiyati = 0;
        for (var t = 1; t <= 9; t++) FiyatAta(t, 0);
        MagazaStok = 0;
        Depo1Stok = 0;
        Depo2Stok = 0;
        MinimumStok = 0;
        Aktif = true;
        ModBildir();
        // Otomatik moda geçiş önizlemeyi kendisi yükler; zaten otomatikse elle yüklenir.
        var kodZatenOtomatik = KodOtomatik && BarkodOtomatik;
        KodOtomatik = true;
        BarkodOtomatik = true;
        if (kodZatenOtomatik) await OnizlemeleriYukleAsync();
    }
}
