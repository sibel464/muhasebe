using System.Windows.Input;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using Masaustu.Servisler;
using System.Collections.ObjectModel;
using EtkiAlani.Modeller.Tanimlar;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.IO;

namespace Masaustu.GorunumModelleri;

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

    public ObservableCollection<MerkeziTanim> Kategoriler { get; }=new();
    public ObservableCollection<MerkeziTanim> Markalar { get; }=new();
    public ObservableCollection<MerkeziTanim> Birimler { get; }=new();
    public MerkeziTanim? SeciliKategori {get=>_seciliKategori;set{if(OzellikAyarla(ref _seciliKategori,value))Kategori=value?.Ad??"";}}
    public MerkeziTanim? SeciliMarka {get=>_seciliMarka;set=>OzellikAyarla(ref _seciliMarka,value);}
    public MerkeziTanim? SeciliBirim {get=>_seciliBirim;set{if(OzellikAyarla(ref _seciliBirim,value))Birim=value?.KisaAd??value?.Ad??"";}}
    
    public decimal AlisFiyati { get => _alisFiyati; set => OzellikAyarla(ref _alisFiyati, value); }
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
    public ICommand FormuTemizleKomutu { get; }
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
        FormuTemizleKomutu = new KomutSarici(FormuTemizle);
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
            
            var kodlar = await _urunServisi.KullanilanKodlariGetirAsync();
            KullanilanKodlar.Clear();
            foreach (var x in kodlar) KullanilanKodlar.Add(x);

            var barkodlar = await _urunServisi.KullanilanBarkodlariGetirAsync();
            KullanilanBarkodlar.Clear();
            foreach (var x in barkodlar) KullanilanBarkodlar.Add(x);

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

    public async Task BarkodlaAraAsync()
    {
        var aranan = BarkodArama.Trim(); if (aranan.Length == 0) return;
        var u = await _urunServisi.BarkodIleGetirAsync(aranan);
        if (u == null) { BarkodOtomatik = false; Barkod = aranan; HataMesaji = "Bu barkoda ait kayıtlı ürün bulunamadı. Yeni ürün olarak devam edebilirsiniz."; return; }
        _bulunanUrun = u; KodOtomatik = false; BarkodOtomatik = false; Kod = u.Kod; Barkod = u.Barkod ?? aranan; Ad = u.Ad; Aciklama = u.Aciklama ?? ""; KdvOrani = u.KdvOrani; AlisFiyati = u.AlisFiyati;
        SeciliKategori = Kategoriler.FirstOrDefault(x => x.Id == u.KategoriTanimiId); SeciliMarka = Markalar.FirstOrDefault(x => x.Id == u.MarkaTanimiId); SeciliBirim = Birimler.FirstOrDefault(x => x.Id == u.BirimTanimiId);
        FiyatTekCekim = u.PerakendeSatisFiyati;
        foreach (var f in u.SatisFiyatlari) FiyatAta(f.TaksitSayisi, f.Fiyat);
        MagazaStok = u.LokasyonStoklari.FirstOrDefault(x => x.DepoId == 1)?.MevcutMiktar ?? 0; Depo1Stok = u.LokasyonStoklari.FirstOrDefault(x => x.DepoId == 2)?.MevcutMiktar ?? 0; Depo2Stok = u.LokasyonStoklari.FirstOrDefault(x => x.DepoId == 3)?.MevcutMiktar ?? 0;
        BasariMesaji = $"Bu barkoda ait ürün bulundu: {u.Ad} ({u.Kod}). Düzenleme modunda açıldı."; BildirimGonder(nameof(DuzenlemeModu));
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
        
        if (SeciliBirim is null && Birimler.Count > 0)
        {
            SeciliBirim = Birimler.FirstOrDefault(x => x.Ad.Equals(Birim, StringComparison.OrdinalIgnoreCase) || (x.KisaAd != null && x.KisaAd.Equals(Birim, StringComparison.OrdinalIgnoreCase))) ?? Birimler.FirstOrDefault();
        }
        if (SeciliBirim is null) { HataMesaji = "Lütfen ölçü birimi seçiniz."; return; }

        string urunKodu = Kod.Trim();
        string? urunBarkod = string.IsNullOrWhiteSpace(Barkod) ? null : Barkod.Trim();

        Kaydediliyor = true;
        ((KomutSarici)KaydetKomutu).BildirimGonder();

        var urun = new Urun
        {
            Kod = urunKodu,
            Ad = Ad.Trim(),
            Barkod = urunBarkod,
            Aciklama = Aciklama?.Trim(),
            Kategori = Kategori,
            Birim = Birim,
            KategoriTanimiId=SeciliKategori?.Id,
            MarkaTanimiId=SeciliMarka?.Id,
            BirimTanimiId=SeciliBirim?.Id,
            AlisFiyati = AlisFiyati,
            PerakendeSatisFiyati = FiyatTekCekim,
            KdvOrani = KdvOrani,
            StokMiktari = ToplamStok, // Genel toplam görünüm amaçlı
            MinimumStok = MinimumStok,
            Aktif = Aktif
        };

        // 9 Taksit Fiyatları
        void FiyatEkle(int taksit, decimal fiyat) 
        {
            if(fiyat > 0) 
            {
                urun.SatisFiyatlari.Add(new UrunSatisFiyati { 
                    TaksitSayisi = taksit,
                    Fiyat = fiyat,
                    ParaBirimiId = 1,
                    AktifMi = true
                });
            }
        }
        FiyatEkle(1, FiyatTekCekim);
        FiyatEkle(2, Fiyat2Taksit);
        FiyatEkle(3, Fiyat3Taksit);
        FiyatEkle(4, Fiyat4Taksit);
        FiyatEkle(5, Fiyat5Taksit);
        FiyatEkle(6, Fiyat6Taksit);
        FiyatEkle(7, Fiyat7Taksit);
        FiyatEkle(8, Fiyat8Taksit);
        FiyatEkle(9, Fiyat9Taksit);

        // Stok Lokasyonları
        void LokasyonEkle(int depoId, decimal miktar)
        {
            if (miktar > 0)
            {
                urun.LokasyonStoklari.Add(new UrunLokasyonStogu {
                    DepoId = depoId,
                    MevcutMiktar = miktar
                });
            }
        }
        LokasyonEkle(1, MagazaStok);
        LokasyonEkle(2, Depo1Stok);
        LokasyonEkle(3, Depo2Stok);

        try
        {
            Urun kaydedilenUrun;
            if (_bulunanUrun == null) kaydedilenUrun = await _urunServisi.GuvenliEkleAsync(urun, KodOtomatik, BarkodOtomatik);
            else
            {
                urun.Id = _bulunanUrun.Id;
                kaydedilenUrun = await _urunServisi.GuncelleAsync(urun);
                await _urunServisi.FiyatlariKaydetAsync(urun.Id, urun.SatisFiyatlari.ToList());
                await _urunServisi.LokasyonStoklariniKaydetAsync(urun.Id, urun.LokasyonStoklari.ToList());
            }
            if (kaydedilenUrun.Id <= 0)
                throw new InvalidOperationException("Ürün kaydı kimlik değeri üretilmeden tamamlandı.");

            var yeniKayit = _bulunanUrun == null;
            _bulunanUrun = kaydedilenUrun;
            KodOtomatik = false;
            BarkodOtomatik = false;
            Kod = kaydedilenUrun.Kod;
            Barkod = kaydedilenUrun.Barkod ?? string.Empty;
            BildirimGonder(nameof(DuzenlemeModu));
            BasariMesaji = yeniKayit
                ? $"✓ Ürün başarıyla kaydedildi.\nÜrün Kodu: {Kod}\nBarkod: {Barkod}"
                : $"✓ Ürün başarıyla güncellendi.\nÜrün Kodu: {Kod}\nBarkod: {Barkod}";
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

    private static string KullaniciHatasiOlustur(Exception exception)
    {
        var postgres = PostgreSqlHatasiniBul(exception);
        return postgres?.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => "Bu ürün kodu veya barkod daha önce kullanılmış.",
            PostgresErrorCodes.ForeignKeyViolation => "Seçilen kategori, marka, birim, şirket veya depo artık geçerli değil.",
            PostgresErrorCodes.NotNullViolation => "Zorunlu ürün bilgilerinden biri eksik.",
            PostgresErrorCodes.NumericValueOutOfRange => "Girilen fiyat veya stok değeri izin verilen aralığın dışında.",
            _ when exception is UnauthorizedAccessException => "Bu işlemi yapmak için ürün ekleme yetkiniz yok.",
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
                $"Entity: Kod={urun.Kod}; Ad={urun.Ad}; Alis={urun.AlisFiyati}; Kdv={urun.KdvOrani}; MinimumStok={urun.MinimumStok}; SirketId={urun.SirketId}; DepoId={urun.DepoId}; KategoriTanimiId={urun.KategoriTanimiId}; MarkaTanimiId={urun.MarkaTanimiId}; BirimTanimiId={urun.BirimTanimiId}; Aktif={urun.Aktif}",
                $"Exception: {exception.GetType().FullName}: {exception.Message}",
                $"InnerException: {inner?.GetType().FullName}: {inner?.Message}",
                $"PostgreSQL: SqlState={postgres?.SqlState}; Constraint={postgres?.ConstraintName}; Table={postgres?.TableName}; Column={postgres?.ColumnName}",
                string.Empty
            };
            var yol = Path.Combine(AppContext.BaseDirectory, "urun-kayit.log");
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

    private void FormuTemizle()
    {
        Kod = string.Empty;
        Ad = string.Empty;
        Barkod = string.Empty;
        Aciklama = string.Empty;
        Kategori = string.Empty;
        AlisFiyati = 0;
        FiyatTekCekim = 0;
        Fiyat2Taksit = 0;
        Fiyat3Taksit = 0;
        Fiyat4Taksit = 0;
        Fiyat5Taksit = 0;
        Fiyat6Taksit = 0;
        Fiyat7Taksit = 0;
        Fiyat8Taksit = 0;
        Fiyat9Taksit = 0;
        MagazaStok = 0;
        Depo1Stok = 0;
        Depo2Stok = 0;
    }
}
