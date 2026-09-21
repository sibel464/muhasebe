using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;

namespace Masaustu.GorunumModelleri;

// ═══════════════════════════════════════════════════
// RAPOR VERİ MODELLERİ
// ═══════════════════════════════════════════════════

public class UrunSatisOzet
{
    public string UrunAd { get; set; } = "";
    public string Kategori { get; set; } = "";
    public decimal SatilanMiktar { get; set; }
    public decimal ToplamTutar { get; set; }
    public decimal OrtalamaFiyat => SatilanMiktar > 0 ? ToplamTutar / SatilanMiktar : 0;
    public int FaturaAdedi { get; set; }
}

public class MusteriSatisOzet
{
    public string CariKod { get; set; } = "";
    public string CariUnvan { get; set; } = "";
    public decimal ToplamSatis { get; set; }
    public int FaturaAdedi { get; set; }
    public decimal OrtalamaFatura => FaturaAdedi > 0 ? ToplamSatis / FaturaAdedi : 0;
    public decimal MevcutBakiye { get; set; }
}

public class AylikSatisOzet
{
    public int Yil { get; set; }
    public int Ay { get; set; }
    public string AyAd => new DateTime(Yil, Ay, 1).ToString("MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
    public decimal SatisCirosu { get; set; }
    public decimal TahsilatToplami { get; set; }
    public decimal OdemeToplami { get; set; }
    public decimal NetNakit => TahsilatToplami - OdemeToplami;
    public int FaturaAdedi { get; set; }
}

public class YaslandirmaKalemi
{
    public string CariKod { get; set; } = "";
    public string CariUnvan { get; set; } = "";
    public string CariTur { get; set; } = "";
    public decimal Gun0_30 { get; set; }
    public decimal Gun31_60 { get; set; }
    public decimal Gun61_90 { get; set; }
    public decimal Gun90Plus { get; set; }
    public decimal GenelToplam => Gun0_30 + Gun31_60 + Gun61_90 + Gun90Plus;
    public string Risk => Gun90Plus > 0 ? "🔴 Yüksek" : Gun61_90 > 0 ? "🟠 Orta" : Gun31_60 > 0 ? "🟡 Düşük" : "🟢 Normal";
}

public class FaturaDetayOzet
{
    public int Id { get; set; }
    public string FaturaNo { get; set; } = "";
    public string Tur { get; set; } = "";
    public string CariKod { get; set; } = "";
    public string CariUnvan { get; set; } = "";
    public DateTime Tarih { get; set; }
    public DateTime? VadeTarihi { get; set; }
    public decimal GenelToplam { get; set; }
    public decimal KdvToplam { get; set; }
    public decimal KdvHaricToplam { get; set; }
    public string Durum { get; set; } = "";
    public string OdemeSekli { get; set; } = "";
    public int KalemAdedi { get; set; }
    public bool Vadesi => VadeTarihi.HasValue && VadeTarihi < DateTime.Today && Durum == "Onaylı";
}

public class NakitAkisKalemi
{
    public DateTime Tarih { get; set; }
    public string Aciklama { get; set; } = "";
    public string Kaynak { get; set; } = "";
    public string YontemAd { get; set; } = "";
    public string CariUnvan { get; set; } = "";
    public decimal GirenTutar { get; set; }
    public decimal CikanTutar { get; set; }
    public decimal NetTutar => GirenTutar - CikanTutar;
}

public class StokDurumKalemi
{
    public string UrunKod { get; set; } = "";
    public string UrunAd { get; set; } = "";
    public string Kategori { get; set; } = "";
    public decimal Miktar { get; set; }
    public decimal AlisFiyati { get; set; }
    public decimal SatisFiyati { get; set; }
    public decimal ToplamMaliyet => Miktar * AlisFiyati;
    public decimal ToplamSatisDegeri => Miktar * SatisFiyati;
    public decimal BrutKarMarji => SatisFiyati > 0 ? (SatisFiyati - AlisFiyati) / SatisFiyati * 100 : 0;
    public string DurumRenk => Miktar <= 0 ? "🔴 Tükenmiş" : Miktar <= 5 ? "🟠 Kritik" : "🟢 Normal";
}

// ═══════════════════════════════════════════════════
// ANA RAPOR GÖRÜNÜM MODELİ
// ═══════════════════════════════════════════════════

public class RaporGorunumModeli : TemelGorunumModeli
{
    private readonly ICariServisi? _cariServisi;
    private readonly IUrunServisi? _urunServisi;
    private readonly IFaturaServisi? _faturaServisi;
    private readonly IKasaServisi? _kasaServisi;
    private readonly IBankaServisi? _bankaServisi;
    private readonly ITahsilatOdemeServisi? _tahsilatOdemeServisi;

    // ─── Yükleme ───
    private bool _yukleniyor;
    public bool Yukleniyor { get => _yukleniyor; set => OzellikAyarla(ref _yukleniyor, value); }

    // ─── Filtreler ───
    private DateTime _baslangicTarihi = new DateTime(DateTime.Today.Year, 1, 1);
    private DateTime _bitisTarihi = DateTime.Today;
    private string _aramaMetni = "";
    private string _secilenFaturaTuru = "Tümü";
    private string _secilenDurum = "Tümü";
    private string _secilenCariTuru = "Tümü";

    public DateTime BaslangicTarihi { get => _baslangicTarihi; set { if (OzellikAyarla(ref _baslangicTarihi, value)) UygulaTumFiltreler(); } }
    public DateTime BitisTarihi { get => _bitisTarihi; set { if (OzellikAyarla(ref _bitisTarihi, value)) UygulaTumFiltreler(); } }
    public string AramaMetni { get => _aramaMetni; set { if (OzellikAyarla(ref _aramaMetni, value)) UygulaTumFiltreler(); } }
    public string SecilenFaturaTuru { get => _secilenFaturaTuru; set { if (OzellikAyarla(ref _secilenFaturaTuru, value)) UygulaTumFiltreler(); } }
    public string SecilenDurum { get => _secilenDurum; set { if (OzellikAyarla(ref _secilenDurum, value)) UygulaTumFiltreler(); } }
    public string SecilenCariTuru { get => _secilenCariTuru; set { if (OzellikAyarla(ref _secilenCariTuru, value)) UygulaTumFiltreler(); } }

    public IReadOnlyList<string> FaturaTurleri { get; } = ["Tümü", "Satış", "Alış", "Satış İade", "Alış İade"];
    public IReadOnlyList<string> Durumlar { get; } = ["Tümü", "Onaylı", "Taslak", "İptal"];
    public IReadOnlyList<string> CariTurleri { get; } = ["Tümü", "Müşteri", "Tedarikçi"];
    public IReadOnlyList<string> HizliTarihler { get; } = ["Bu Ay", "Son 3 Ay", "Bu Yıl", "Geçen Yıl", "Tüm Zamanlar"];

    // ─── KPI KARTI DEĞERLERİ ───
    private decimal _toplamSatisCiro, _toplamAlisMaliyeti, _brutKar;
    private decimal _toplamKasaBanka, _toplamAlacak, _toplamBorc;
    private decimal _vadesiGecenAlacak, _tahsilatVerimi;
    private int _faturaSayisi, _aktifCariSayisi, _kritikStokSayisi;
    private decimal _envanterMaliyet, _envanterSatisDegeri;

    public decimal ToplamSatisCiro { get => _toplamSatisCiro; set => OzellikAyarla(ref _toplamSatisCiro, value); }
    public decimal ToplamAlisMaliyeti { get => _toplamAlisMaliyeti; set => OzellikAyarla(ref _toplamAlisMaliyeti, value); }
    public decimal BrutKar { get => _brutKar; set => OzellikAyarla(ref _brutKar, value); }
    public decimal BrutKarMarji => ToplamSatisCiro > 0 ? BrutKar / ToplamSatisCiro * 100 : 0;
    public decimal ToplamKasaBanka { get => _toplamKasaBanka; set => OzellikAyarla(ref _toplamKasaBanka, value); }
    public decimal ToplamAlacak { get => _toplamAlacak; set => OzellikAyarla(ref _toplamAlacak, value); }
    public decimal ToplamBorc { get => _toplamBorc; set => OzellikAyarla(ref _toplamBorc, value); }
    public decimal VadesiGecenAlacak { get => _vadesiGecenAlacak; set => OzellikAyarla(ref _vadesiGecenAlacak, value); }
    public decimal TahsilatVerimi { get => _tahsilatVerimi; set => OzellikAyarla(ref _tahsilatVerimi, value); }
    public int FaturaSayisi { get => _faturaSayisi; set => OzellikAyarla(ref _faturaSayisi, value); }
    public int AktifCariSayisi { get => _aktifCariSayisi; set => OzellikAyarla(ref _aktifCariSayisi, value); }
    public int KritikStokSayisi { get => _kritikStokSayisi; set => OzellikAyarla(ref _kritikStokSayisi, value); }
    public decimal EnvanterMaliyet { get => _envanterMaliyet; set => OzellikAyarla(ref _envanterMaliyet, value); }
    public decimal EnvanterSatisDegeri { get => _envanterSatisDegeri; set => OzellikAyarla(ref _envanterSatisDegeri, value); }
    public decimal PotansiyelKar => EnvanterSatisDegeri - EnvanterMaliyet;

    // ─── TABLOLAR ───
    private List<Fatura> _tumFaturalar = [];
    private List<TahsilatOdemeKaydi> _tumNakit = [];
    private List<CariKart> _tumCariler = [];

    public ObservableCollection<FaturaDetayOzet> FaturaTablosu { get; } = [];
    public ObservableCollection<MusteriSatisOzet> MusteriSatisTablosu { get; } = [];
    public ObservableCollection<UrunSatisOzet> UrunSatisTablosu { get; } = [];
    public ObservableCollection<AylikSatisOzet> AylikSatisTablosu { get; } = [];
    public ObservableCollection<NakitAkisKalemi> NakitAkisTablosu { get; } = [];
    public decimal NakitGirenToplam => NakitAkisTablosu.Sum(x => x.GirenTutar);
    public decimal NakitCikanToplam => NakitAkisTablosu.Sum(x => x.CikanTutar);
    public decimal NakitNetToplam => NakitGirenToplam - NakitCikanToplam;
    public ObservableCollection<YaslandirmaKalemi> MusteriYaslandirma { get; } = [];
    public ObservableCollection<YaslandirmaKalemi> TedarikciYaslandirma { get; } = [];
    public ObservableCollection<StokDurumKalemi> StokTablosu { get; } = [];
    public ObservableCollection<StokDurumKalemi> KritikStokTablosu { get; } = [];

    // ─── YAŞLANDIRMA TOPLAMLAR ───
    public decimal YaslandirmaMusteri0_30 => MusteriYaslandirma.Sum(x => x.Gun0_30);
    public decimal YaslandirmaMusteri31_60 => MusteriYaslandirma.Sum(x => x.Gun31_60);
    public decimal YaslandirmaMusteri61_90 => MusteriYaslandirma.Sum(x => x.Gun61_90);
    public decimal YaslandirmaMusteri90Plus => MusteriYaslandirma.Sum(x => x.Gun90Plus);
    public decimal YaslandirmaMusteriToplam => MusteriYaslandirma.Sum(x => x.GenelToplam);

    // ─── KOMUTLAR ───
    public ICommand YenileKomutu { get; }
    public ICommand BuAyKomutu { get; }
    public ICommand Son3AyKomutu { get; }
    public ICommand BuYilKomutu { get; }
    public ICommand GecenYilKomutu { get; }
    public ICommand TumZamanlarKomutu { get; }
    public ICommand DisaAktarExcelKomutu { get; }

    public RaporGorunumModeli(
        ICariServisi? cariServisi = null,
        IUrunServisi? urunServisi = null,
        IFaturaServisi? faturaServisi = null,
        IKasaServisi? kasaServisi = null,
        IBankaServisi? bankaServisi = null,
        ITahsilatOdemeServisi? tahsilatOdemeServisi = null)
    {
        _cariServisi = cariServisi;
        _urunServisi = urunServisi;
        _faturaServisi = faturaServisi;
        _kasaServisi = kasaServisi;
        _bankaServisi = bankaServisi;
        _tahsilatOdemeServisi = tahsilatOdemeServisi;

        DisaAktarExcelKomutu = new KomutSarici(DisaAktarExcel);
        YenileKomutu = new KomutSarici(async () => await TumVeriYukleAsync());
        BuAyKomutu = new KomutSarici(() => { BaslangicTarihi = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); BitisTarihi = DateTime.Today; });
        Son3AyKomutu = new KomutSarici(() => { BaslangicTarihi = DateTime.Today.AddMonths(-3); BitisTarihi = DateTime.Today; });
        BuYilKomutu = new KomutSarici(() => { BaslangicTarihi = new DateTime(DateTime.Today.Year, 1, 1); BitisTarihi = DateTime.Today; });
        GecenYilKomutu = new KomutSarici(() => { BaslangicTarihi = new DateTime(DateTime.Today.Year - 1, 1, 1); BitisTarihi = new DateTime(DateTime.Today.Year - 1, 12, 31); });
        TumZamanlarKomutu = new KomutSarici(() => { BaslangicTarihi = new DateTime(2000, 1, 1); BitisTarihi = DateTime.Today; });

        _ = TumVeriYukleAsync();
    }

    private async Task TumVeriYukleAsync()
    {
        Yukleniyor = true;
        try
        {
            // Ham veri yükle
            if (_faturaServisi != null)
                _tumFaturalar = await _faturaServisi.TumunuGetirAsync() ?? [];
            if (_tahsilatOdemeServisi != null)
            {
                var nakitVerisi = await _tahsilatOdemeServisi.GecmisiGetirAsync();
                _tumNakit = nakitVerisi?.ToList() ?? [];
            }
            if (_cariServisi != null)
                _tumCariler = await _cariServisi.TumunuGetirAsync() ?? [];

            // KPI hesapla
            await KpiHesaplaAsync();
            // Tabloları doldur
            UygulaTumFiltreler();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Rapor yükleme hatası: {ex.Message}", "Hata", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task KpiHesaplaAsync()
    {
        // Cari bakiyeler
        ToplamBorc = _tumCariler.Sum(c => c.Bakiye > 0 ? c.Bakiye : 0);
        ToplamAlacak = _tumCariler.Sum(c => c.Bakiye < 0 ? Math.Abs(c.Bakiye) : 0);
        AktifCariSayisi = _tumCariler.Count(c => c.Aktif);

        // Kasa + Banka
        decimal kasa = _kasaServisi != null ? await _kasaServisi.BakiyeHesaplaAsync() : 0;
        decimal banka = _bankaServisi != null ? await _bankaServisi.BakiyeHesaplaAsync() : 0;
        ToplamKasaBanka = kasa + banka;

        // Fatura metrikleri
        var onayliFaturalar = _tumFaturalar.Where(f => f.Durum == BelgeDurumu.Onaylandi).ToList();
        var satisFaturalar = onayliFaturalar.Where(f => f.Tur == FaturaTuru.Satis).ToList();
        var alisFaturalar = onayliFaturalar.Where(f => f.Tur == FaturaTuru.Alis).ToList();

        ToplamSatisCiro = satisFaturalar.Sum(f => f.GenelToplam);
        ToplamAlisMaliyeti = alisFaturalar.Sum(f => f.GenelToplam);
        BrutKar = ToplamSatisCiro - ToplamAlisMaliyeti;
        FaturaSayisi = onayliFaturalar.Count;

        VadesiGecenAlacak = _tumFaturalar
            .Where(f => f.Durum == BelgeDurumu.Onaylandi && f.Tur == FaturaTuru.Satis
                     && f.VadeTarihi.HasValue && f.VadeTarihi < DateTime.Today)
            .Sum(f => f.GenelToplam);

        // Tahsilat verimi
        if (ToplamSatisCiro > 0)
        {
            var toplamTahsilat = _tumNakit
                .Where(n => n.IslemTuru == FinansIslemTuru.Tahsilat && n.Durum == FinansIslemDurumu.Tamamlandi)
                .Sum(n => n.Tutar);
            TahsilatVerimi = Math.Min(toplamTahsilat / ToplamSatisCiro * 100, 100);
        }

        // Stok
        if (_urunServisi != null)
        {
            var urunler = await _urunServisi.TumunuGetirAsync();
            var aktifler = urunler.Where(u => u.Aktif).ToList();
            EnvanterMaliyet = aktifler.Sum(u => u.StokMiktari * u.AlisFiyati);
            EnvanterSatisDegeri = aktifler.Sum(u => u.StokMiktari * u.PerakendeSatisFiyati);
            KritikStokSayisi = aktifler.Count(u => u.StokMiktari <= 5);

            // Stok tablosu
            StokTablosu.Clear();
            KritikStokTablosu.Clear();
            foreach (var u in aktifler.OrderBy(u => u.StokMiktari))
            {
                var k = new StokDurumKalemi
                {
                    UrunKod = u.Kod,
                    UrunAd = u.Ad,
                    Kategori = u.KategoriTanimi?.Ad ?? u.Kategori ?? "",
                    Miktar = u.StokMiktari,
                    AlisFiyati = u.AlisFiyati,
                    SatisFiyati = u.PerakendeSatisFiyati
                };
                StokTablosu.Add(k);
                if (u.StokMiktari <= 5) KritikStokTablosu.Add(k);
            }
        }

        // Yaşlandırma raporu
        HesaplaYaslandirma();
    }

    private void HesaplaYaslandirma()
    {
        MusteriYaslandirma.Clear();
        TedarikciYaslandirma.Clear();
        var bugun = DateTime.Today;

        foreach (var cari in _tumCariler.Where(c => c.Aktif && c.Bakiye != 0))
        {
            // Açık fatura bazlı yaşlandırma
            var aciklarSatis = _tumFaturalar
                .Where(f => f.CariKartId == cari.Id && f.Durum == BelgeDurumu.Onaylandi
                         && f.Tur == FaturaTuru.Satis && f.VadeTarihi.HasValue)
                .ToList();

            if (!aciklarSatis.Any() && cari.Bakiye <= 0) continue;

            decimal gun0_30 = 0, gun31_60 = 0, gun61_90 = 0, gun90Plus = 0;
            foreach (var f in aciklarSatis)
            {
                var gun = (bugun - f.VadeTarihi!.Value.Date).Days;
                if (gun <= 0) gun0_30 += f.GenelToplam;
                else if (gun <= 30) gun0_30 += f.GenelToplam;
                else if (gun <= 60) gun31_60 += f.GenelToplam;
                else if (gun <= 90) gun61_90 += f.GenelToplam;
                else gun90Plus += f.GenelToplam;
            }

            if (gun0_30 + gun31_60 + gun61_90 + gun90Plus == 0 && cari.Bakiye > 0)
                gun0_30 = cari.Bakiye; // fatura bazlı yoksa bakiye bazlı

            var kalemi = new YaslandirmaKalemi
            {
                CariKod = cari.Kod,
                CariUnvan = cari.Unvan,
                CariTur = cari.Tur.ToString(),
                Gun0_30 = gun0_30,
                Gun31_60 = gun31_60,
                Gun61_90 = gun61_90,
                Gun90Plus = gun90Plus
            };

            if (cari.Tur == CariTuru.Musteri || cari.Tur == CariTuru.MusteriTedarikci)
                MusteriYaslandirma.Add(kalemi);
            if (cari.Tur == CariTuru.Tedarikci || cari.Tur == CariTuru.MusteriTedarikci)
                TedarikciYaslandirma.Add(kalemi);
        }

        BildirimGonder(nameof(YaslandirmaMusteri0_30));
        BildirimGonder(nameof(YaslandirmaMusteri31_60));
        BildirimGonder(nameof(YaslandirmaMusteri61_90));
        BildirimGonder(nameof(YaslandirmaMusteri90Plus));
        BildirimGonder(nameof(YaslandirmaMusteriToplam));
    }

    private void UygulaTumFiltreler()
    {
        var tumFaturalarFiltreli = _tumFaturalar.AsEnumerable();

        // Tarih filtresi
        tumFaturalarFiltreli = tumFaturalarFiltreli.Where(f => f.Tarih.Date >= BaslangicTarihi.Date && f.Tarih.Date <= BitisTarihi.Date);

        // Tür filtresi
        if (SecilenFaturaTuru != "Tümü")
        {
            var tur = SecilenFaturaTuru switch
            {
                "Satış" => FaturaTuru.Satis,
                "Alış" => FaturaTuru.Alis,
                "Satış İade" => FaturaTuru.SatisIade,
                "Alış İade" => FaturaTuru.AlisIade,
                _ => (FaturaTuru?)null
            };
            if (tur.HasValue) tumFaturalarFiltreli = tumFaturalarFiltreli.Where(f => f.Tur == tur.Value);
        }

        // Durum filtresi
        if (SecilenDurum != "Tümü")
        {
            var durum = SecilenDurum switch
            {
                "Onaylı" => BelgeDurumu.Onaylandi,
                "Taslak" => BelgeDurumu.Taslak,
                "İptal" => BelgeDurumu.Iptal,
                _ => (BelgeDurumu?)null
            };
            if (durum.HasValue) tumFaturalarFiltreli = tumFaturalarFiltreli.Where(f => f.Durum == durum.Value);
        }

        // Arama filtresi
        if (!string.IsNullOrWhiteSpace(AramaMetni))
        {
            var ara = AramaMetni.ToLower();
            tumFaturalarFiltreli = tumFaturalarFiltreli.Where(f =>
                f.FaturaNo.ToLower().Contains(ara) ||
                (f.CariKart?.Unvan?.ToLower().Contains(ara) ?? false) ||
                (f.CariKart?.Kod?.ToLower().Contains(ara) ?? false));
        }

        var liste = tumFaturalarFiltreli.OrderByDescending(f => f.Tarih).ToList();

        // ─── FATURA TABLOSU ───
        FaturaTablosu.Clear();
        foreach (var f in liste)
        {
            FaturaTablosu.Add(new FaturaDetayOzet
            {
                Id = f.Id,
                FaturaNo = f.FaturaNo,
                Tur = f.Tur switch { FaturaTuru.Satis => "Satış", FaturaTuru.Alis => "Alış", FaturaTuru.SatisIade => "Satış İade", _ => "Alış İade" },
                CariKod = f.CariKart?.Kod ?? "",
                CariUnvan = f.CariKart?.Unvan ?? "",
                Tarih = f.Tarih,
                VadeTarihi = f.VadeTarihi,
                GenelToplam = f.GenelToplam,
                KdvToplam = f.KdvToplam,
                KdvHaricToplam = f.GenelToplam - f.KdvToplam,
                Durum = f.Durum switch { BelgeDurumu.Onaylandi => "Onaylı", BelgeDurumu.Taslak => "Taslak", BelgeDurumu.Iptal => "İptal", _ => "Beklemede" },
                OdemeSekli = f.OdemeSekli.ToString(),
                KalemAdedi = f.Kalemler?.Count ?? 0
            });
        }

        // ─── ÜRÜN SATIŞ TABLOSU ───
        UrunSatisTablosu.Clear();
        var satisFaturalar = liste.Where(f => f.Tur == FaturaTuru.Satis && f.Durum == BelgeDurumu.Onaylandi).ToList();
        try
        {
            var urunGruplari = satisFaturalar
                .Where(f => f.Kalemler != null && f.Kalemler.Count > 0)
                .SelectMany(f => f.Kalemler!.Select(k => new { k.UrunAd, k.Miktar, SatirToplami = k.SatirToplami, FaturaId = f.Id }))
                .GroupBy(k => k.UrunAd ?? "Bilinmiyor")
                .Select(g => new UrunSatisOzet
                {
                    UrunAd = g.Key,
                    SatilanMiktar = g.Sum(k => k.Miktar),
                    ToplamTutar = g.Sum(k => k.SatirToplami),
                    FaturaAdedi = g.Select(k => k.FaturaId).Distinct().Count()
                })
                .OrderByDescending(x => x.ToplamTutar);

            foreach (var u in urunGruplari) UrunSatisTablosu.Add(u);
        }
        catch { /* Kalemler yüklenmemiş olabilir, sessizce geç */ }

        // ─── MÜŞTERİ SATIŞ TABLOSU ───
        MusteriSatisTablosu.Clear();
        try
        {
            var musteriGruplari = satisFaturalar
                .Where(f => f.CariKart != null)
                .GroupBy(f => new { f.CariKart!.Id, f.CariKart.Kod, f.CariKart.Unvan, f.CariKart.Bakiye })
                .Select(g => new MusteriSatisOzet
                {
                    CariKod = g.Key.Kod,
                    CariUnvan = g.Key.Unvan,
                    ToplamSatis = g.Sum(f => f.GenelToplam),
                    FaturaAdedi = g.Count(),
                    MevcutBakiye = g.Key.Bakiye
                })
                .OrderByDescending(x => x.ToplamSatis);

            foreach (var m in musteriGruplari) MusteriSatisTablosu.Add(m);
        }
        catch { /* Sessizce geç */ }

        // ─── AYLIK SATIŞ TABLOSU ───
        AylikSatisTablosu.Clear();
        var aylikVeriler = liste
            .Where(f => f.Durum == BelgeDurumu.Onaylandi)
            .GroupBy(f => new { f.Tarih.Year, f.Tarih.Month })
            .Select(g =>
            {
                var tahsilatlar = _tumNakit
                    .Where(n => n.Tarih.Year == g.Key.Year && n.Tarih.Month == g.Key.Month)
                    .ToList();
                return new AylikSatisOzet
                {
                    Yil = g.Key.Year,
                    Ay = g.Key.Month,
                    SatisCirosu = g.Where(f => f.Tur == FaturaTuru.Satis).Sum(f => f.GenelToplam),
                    TahsilatToplami = tahsilatlar.Where(n => n.IslemTuru == FinansIslemTuru.Tahsilat).Sum(n => n.Tutar),
                    OdemeToplami = tahsilatlar.Where(n => n.IslemTuru == FinansIslemTuru.Odeme).Sum(n => n.Tutar),
                    FaturaAdedi = g.Count()
                };
            })
            .OrderByDescending(x => x.Yil).ThenByDescending(x => x.Ay);

        foreach (var a in aylikVeriler) AylikSatisTablosu.Add(a);

        // ─── NAKİT AKIŞ TABLOSU ───
        NakitAkisTablosu.Clear();
        var nakitFiltreli = _tumNakit
            .Where(n => n.Tarih.Date >= BaslangicTarihi.Date && n.Tarih.Date <= BitisTarihi.Date)
            .OrderByDescending(n => n.Tarih);

        foreach (var n in nakitFiltreli)
        {
            NakitAkisTablosu.Add(new NakitAkisKalemi
            {
                Tarih = n.Tarih,
                Aciklama = n.Aciklama,
                Kaynak = n.Kaynak,
                YontemAd = n.OdemeYontemi.ToString(),
                CariUnvan = n.CariUnvan,
                GirenTutar = n.IslemTuru == FinansIslemTuru.Tahsilat ? n.Tutar : 0,
                CikanTutar = n.IslemTuru == FinansIslemTuru.Odeme ? n.Tutar : 0
            });
        }
    }

    private void DisaAktarExcel()
    {
        if (FaturaTablosu.Count > 0)
        {
            ExcelDisaAktarYardimcisi.AktarCsv(FaturaTablosu, "Fatura_Raporu");
        }
        else if (StokTablosu.Count > 0)
        {
            ExcelDisaAktarYardimcisi.AktarCsv(StokTablosu, "Stok_Raporu");
        }
        else if (MusteriSatisTablosu.Count > 0)
        {
            ExcelDisaAktarYardimcisi.AktarCsv(MusteriSatisTablosu, "Musteri_Satis_Raporu");
        }
        else
        {
            System.Windows.MessageBox.Show("Dışa aktarılacak veri tablosu bulunamadı.", "Uyarı", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }
}
