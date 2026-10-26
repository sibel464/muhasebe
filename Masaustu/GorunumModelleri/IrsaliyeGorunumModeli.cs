using Altyapi.Servisler;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Irsaliye;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using Masaustu.Servisler;

namespace Masaustu.GorunumModelleri;

public class IrsaliyeGorunumModeli : TemelGorunumModeli
{
    private readonly IIrsaliyeServisi _irsaliyeServisi;
    private readonly ICariServisi _cariServisi;
    private readonly IUrunServisi _urunServisi;
    private readonly IFaturaServisi _faturaServisi;
    private readonly IBelgeYazdirmaServisi? _yazdirmaServisi;
    private readonly NavigasyonServisi? _navigasyon;

    private ObservableCollection<Irsaliye> _irsaliyeler = new();
    private List<Irsaliye> _tumIrsaliyeler = new();
    private ObservableCollection<CariKart> _cariler = new();
    private Irsaliye? _seciliIrsaliye;

    // Filtreler
    private string _aramaMetni = string.Empty;
    private DateTime? _baslangicTarihi;
    private DateTime? _bitisTarihi;
    private CariKart? _seciliCariFiltre;
    private string _seciliTurFiltre = "Tümü";
    private string _seciliDurumFiltre = "Tümü";
    private string _siralamaFiltre = "Tarih: yeniden eskiye";
    private int _filtrelenmisKayitSayisi;
    private decimal _filtrelenmisToplam;
    private decimal? _minimumTutar;
    private decimal? _maksimumTutar;
    private bool _yalnizGecikenSevkler;

    // İstatistikler / Özet
    private int _toplamIrsaliyeSayisi;
    private int _taslakSayisi;
    private int _onayliSayisi;
    private int _faturalananSayisi;
    private decimal _toplamSatisTutari;
    private decimal _toplamAlisTutari;

    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;

    public ObservableCollection<Irsaliye> Irsaliyeler
    {
        get => _irsaliyeler;
        private set => OzellikAyarla(ref _irsaliyeler, value);
    }

    public ObservableCollection<CariKart> Cariler
    {
        get => _cariler;
        private set => OzellikAyarla(ref _cariler, value);
    }

    public Irsaliye? SeciliIrsaliye
    {
        get => _seciliIrsaliye;
        set
        {
            if (OzellikAyarla(ref _seciliIrsaliye, value))
            {
                KomutlariGuncelle();
            }
        }
    }

    public string AramaMetni
    {
        get => _aramaMetni;
        set
        {
            if (OzellikAyarla(ref _aramaMetni, value))
                Filtrele();
        }
    }

    public DateTime? BaslangicTarihi
    {
        get => _baslangicTarihi;
        set
        {
            if (OzellikAyarla(ref _baslangicTarihi, value))
                Filtrele();
        }
    }

    public DateTime? BitisTarihi
    {
        get => _bitisTarihi;
        set
        {
            if (OzellikAyarla(ref _bitisTarihi, value))
                Filtrele();
        }
    }

    public CariKart? SeciliCariFiltre
    {
        get => _seciliCariFiltre;
        set
        {
            if (OzellikAyarla(ref _seciliCariFiltre, value))
                Filtrele();
        }
    }

    public string SeciliTurFiltre
    {
        get => _seciliTurFiltre;
        set
        {
            if (OzellikAyarla(ref _seciliTurFiltre, value))
                Filtrele();
        }
    }

    public string SeciliDurumFiltre
    {
        get => _seciliDurumFiltre;
        set
        {
            if (OzellikAyarla(ref _seciliDurumFiltre, value))
                Filtrele();
        }
    }

    public int ToplamIrsaliyeSayisi { get => _toplamIrsaliyeSayisi; private set => OzellikAyarla(ref _toplamIrsaliyeSayisi, value); }
    public int TaslakSayisi { get => _taslakSayisi; private set => OzellikAyarla(ref _taslakSayisi, value); }
    public int OnayliSayisi { get => _onayliSayisi; private set => OzellikAyarla(ref _onayliSayisi, value); }
    public int FaturalananSayisi { get => _faturalananSayisi; private set => OzellikAyarla(ref _faturalananSayisi, value); }
    public decimal ToplamSatisTutari { get => _toplamSatisTutari; private set => OzellikAyarla(ref _toplamSatisTutari, value); }
    public decimal ToplamAlisTutari { get => _toplamAlisTutari; private set => OzellikAyarla(ref _toplamAlisTutari, value); }
    public int FiltrelenmisKayitSayisi { get => _filtrelenmisKayitSayisi; private set => OzellikAyarla(ref _filtrelenmisKayitSayisi, value); }
    public decimal FiltrelenmisToplam { get => _filtrelenmisToplam; private set => OzellikAyarla(ref _filtrelenmisToplam, value); }
    public string FiltreSonucOzeti => $"{FiltrelenmisKayitSayisi} / {ToplamIrsaliyeSayisi} irsaliye • {FiltrelenmisToplam:N2} ₺";
    public IReadOnlyList<string> SiralamaSecenekleri { get; } = ["Tarih: yeniden eskiye", "Tarih: eskiden yeniye", "Tutar: yüksekten düşüğe", "Cari unvanına göre"];
    public string SiralamaFiltre { get => _siralamaFiltre; set { if (OzellikAyarla(ref _siralamaFiltre, value)) Filtrele(); } }
    public decimal? MinimumTutar { get => _minimumTutar; set { if (OzellikAyarla(ref _minimumTutar, value)) Filtrele(); } }
    public decimal? MaksimumTutar { get => _maksimumTutar; set { if (OzellikAyarla(ref _maksimumTutar, value)) Filtrele(); } }
    public bool YalnizGecikenSevkler { get => _yalnizGecikenSevkler; set { if (OzellikAyarla(ref _yalnizGecikenSevkler, value)) Filtrele(); } }
    public string SeciliBelgeAkisi => SeciliIrsaliye switch
    {
        null => "İşlem yapmak için bir irsaliye satırı seçin.",
        { Durum: IrsaliyeDurumu.Taslak } => "Taslak: Düzenlenebilir. Onaylandığında stok hareketi oluşur; henüz cari hesaba kayıt atılmaz.",
        { Durum: IrsaliyeDurumu.Onaylandi } => "Sevk tamamlandı: Stok işlendi. Belge faturaya dönüştürülerek cari hesap ve mali kayıt oluşturulabilir.",
        { Durum: IrsaliyeDurumu.Faturalandi } i => $"Faturalandı: Belge kilitli ve izlenebilir durumda. Bağlı fatura: {i.DonusturulenFatura?.FaturaNo ?? "oluşturuldu"}.",
        _ => "İptal: Belge işlem dışı; ilişkili stok hareketleri geri alınmıştır."
    };

    public bool Yukleniyor { get => _yukleniyor; private set => OzellikAyarla(ref _yukleniyor, value); }
    public string DurumMesaji { get => _durumMesaji; private set => OzellikAyarla(ref _durumMesaji, value); }

    public ICommand YenileKomutu { get; }
    public ICommand YeniIrsaliyeKomutu { get; }
    public ICommand OnaylaKomutu { get; }
    public ICommand IptalEtKomutu { get; }
    public ICommand SilKomutu { get; }
    public ICommand FaturayaDonusturKomutu { get; }
    public ICommand DetayKomutu { get; }
    public ICommand DetayGoruntuleKomutu { get; }
    public ICommand YazdirKomutu { get; }
    public ICommand EIrsaliyeKomutu { get; }
    private readonly IEBelgeServisi? _eBelgeServisi;
    public ICommand FiltreTemizleKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }
    public ICommand HizliFiltreKomutu { get; }

    public IrsaliyeGorunumModeli(
        IIrsaliyeServisi irsaliyeServisi,
        ICariServisi cariServisi,
        IUrunServisi urunServisi,
        IFaturaServisi faturaServisi,
        NavigasyonServisi? navigasyon = null,
        IBelgeYazdirmaServisi? yazdirmaServisi = null,
        IEBelgeServisi? eBelgeServisi = null)
    {
        _eBelgeServisi = eBelgeServisi;
        // Onaylı sevk irsaliyesi e-İrsaliye olarak hazırlanır (taşıma bilgisi pencerede istenir).
        EIrsaliyeKomutu = new KomutSarici(async p =>
        {
            if (p is not Irsaliye i || _eBelgeServisi == null) return;
            if (await Masaustu.Gorunumler.Sayfa.EIrsaliyePenceresi.GosterAsync(_eBelgeServisi, i)) await ListeleAsync();
        }, p => p is Irsaliye { Tur: IrsaliyeTuru.Satis, Durum: IrsaliyeDurumu.Onaylandi or IrsaliyeDurumu.Faturalandi });
        _irsaliyeServisi = irsaliyeServisi;
        _cariServisi = cariServisi;
        _urunServisi = urunServisi;
        _faturaServisi = faturaServisi;
        _navigasyon = navigasyon;
        _yazdirmaServisi = yazdirmaServisi;

        ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.AktarCsv(Irsaliyeler.Select(i => new { i.IrsaliyeNo, i.Tarih, i.SevkTarihi, i.Tur, Cari = i.CariKart?.Unvan, SiparisNo = i.Siparis?.SiparisNo, i.ReferansNo, i.Tasiyici, i.AracPlaka, i.AraToplam, i.KdvToplam, i.GenelToplam, i.Durum, FaturaNo = i.DonusturulenFatura?.FaturaNo }), "Irsaliye_Listesi"));
        HizliFiltreKomutu = new KomutSarici(p => HizliFiltreUygula(p?.ToString()));
        YenileKomutu = new KomutSarici(async () => await ListeleAsync());
        YeniIrsaliyeKomutu = new KomutSarici(YeniIrsaliyeEkle);
        OnaylaKomutu = new KomutSarici(async p => { SeciliIrsaliye = p as Irsaliye ?? SeciliIrsaliye; await OnaylaAsync(); }, p => (p as Irsaliye ?? SeciliIrsaliye)?.Durum == IrsaliyeDurumu.Taslak);
        IptalEtKomutu = new KomutSarici(async p => { SeciliIrsaliye = p as Irsaliye ?? SeciliIrsaliye; await IptalEtAsync(); }, p => (p as Irsaliye ?? SeciliIrsaliye)?.Durum is IrsaliyeDurumu.Taslak or IrsaliyeDurumu.Onaylandi);
        SilKomutu = new KomutSarici(async () => await SilAsync(), () => SeciliIrsaliye != null && SeciliIrsaliye.Durum == IrsaliyeDurumu.Taslak);
        FaturayaDonusturKomutu = new KomutSarici(async p => { SeciliIrsaliye = p as Irsaliye ?? SeciliIrsaliye; await FaturayaDonusturAsync(); }, p => (p as Irsaliye ?? SeciliIrsaliye)?.Durum == IrsaliyeDurumu.Onaylandi);
        DetayKomutu = new KomutSarici(DetayGoster, () => SeciliIrsaliye != null);
        DetayGoruntuleKomutu = new KomutSarici(p => { SeciliIrsaliye = p as Irsaliye ?? SeciliIrsaliye; DetayGoster(); }, p => p is Irsaliye || SeciliIrsaliye != null);
        YazdirKomutu = new KomutSarici(async p => await YazdirAsync(p as Irsaliye ?? SeciliIrsaliye), p => p is Irsaliye || SeciliIrsaliye != null);
        FiltreTemizleKomutu = new KomutSarici(FiltreleriTemizle);

        _ = BaslatAsync();
    }

    private async Task BaslatAsync()
    {
        await CarileriYukleAsync();
        await ListeleAsync();
    }

    public async Task CarileriYukleAsync()
    {
        try
        {
            var cariler = await _cariServisi.TumunuGetirAsync();
            Cariler = new ObservableCollection<CariKart>(cariler.Where(c => c.Aktif));
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Cariler yüklenemedi: {ex.Message}";
        }
    }

    public async Task ListeleAsync()
    {
        Yukleniyor = true;
        DurumMesaji = "İrsaliyeler listeleniyor...";
        try
        {
            _tumIrsaliyeler = await _irsaliyeServisi.TumunuGetirAsync();
            OzetleriHesapla();
            Filtrele();
            DurumMesaji = $"Toplam {_tumIrsaliyeler.Count} irsaliye yüklendi.";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Hata: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private void OzetleriHesapla()
    {
        ToplamIrsaliyeSayisi = _tumIrsaliyeler.Count;
        TaslakSayisi = _tumIrsaliyeler.Count(i => i.Durum == IrsaliyeDurumu.Taslak);
        OnayliSayisi = _tumIrsaliyeler.Count(i => i.Durum == IrsaliyeDurumu.Onaylandi);
        FaturalananSayisi = _tumIrsaliyeler.Count(i => i.Durum == IrsaliyeDurumu.Faturalandi);
        ToplamSatisTutari = _tumIrsaliyeler.Where(i => i.Tur == IrsaliyeTuru.Satis && i.Durum != IrsaliyeDurumu.Iptal).Sum(i => i.GenelToplam);
        ToplamAlisTutari = _tumIrsaliyeler.Where(i => i.Tur == IrsaliyeTuru.Alis && i.Durum != IrsaliyeDurumu.Iptal).Sum(i => i.GenelToplam);
    }

    private void Filtrele()
    {
        var liste = _tumIrsaliyeler.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(AramaMetni))
        {
            var kelimeler = AramaMetni.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            liste = liste.Where(i =>
            {
                var kalemMetni = string.Join(' ', i.Kalemler.Select(k => $"{k.UrunKodu} {k.UrunAd} {k.Barkod} {k.Aciklama}"));
                var metin = $"{i.IrsaliyeNo} {i.Siparis?.SiparisNo} {i.DonusturulenFatura?.FaturaNo} {i.CariKart?.Unvan} {i.CariKart?.Kod} {i.CariKart?.Telefon} {i.ReferansNo} {i.Tasiyici} {i.AracPlaka} {i.SoforAdi} {i.SoforTelefon} {i.TeslimatAdSoyad} {i.TeslimatTelefon} {i.TeslimatAdres} {i.TeslimatIl} {i.TeslimatIlce} {i.Aciklama} {kalemMetni}";
                return kelimeler.All(k => metin.IndexOf(k, StringComparison.CurrentCultureIgnoreCase) >= 0);
            });
        }

        if (BaslangicTarihi.HasValue)
        {
            liste = liste.Where(i => i.Tarih.Date >= BaslangicTarihi.Value.Date);
        }

        if (BitisTarihi.HasValue)
        {
            liste = liste.Where(i => i.Tarih.Date <= BitisTarihi.Value.Date);
        }

        if (SeciliCariFiltre != null)
        {
            liste = liste.Where(i => i.CariKartId == SeciliCariFiltre.Id);
        }

        if (MinimumTutar.HasValue) liste = liste.Where(i => i.GenelToplam >= MinimumTutar.Value);
        if (MaksimumTutar.HasValue) liste = liste.Where(i => i.GenelToplam <= MaksimumTutar.Value);
        if (YalnizGecikenSevkler)
            liste = liste.Where(i => i.SevkTarihi != default && i.SevkTarihi.Date < DateTime.Today && i.Durum == IrsaliyeDurumu.Taslak);

        if (SeciliTurFiltre == "Satış İrsaliyesi")
        {
            liste = liste.Where(i => i.Tur == IrsaliyeTuru.Satis);
        }
        else if (SeciliTurFiltre == "Alış İrsaliyesi")
        {
            liste = liste.Where(i => i.Tur == IrsaliyeTuru.Alis);
        }

        if (SeciliDurumFiltre == "Taslak")
        {
            liste = liste.Where(i => i.Durum == IrsaliyeDurumu.Taslak);
        }
        else if (SeciliDurumFiltre == "Onaylandı")
        {
            liste = liste.Where(i => i.Durum == IrsaliyeDurumu.Onaylandi);
        }
        else if (SeciliDurumFiltre == "Faturalandı")
        {
            liste = liste.Where(i => i.Durum == IrsaliyeDurumu.Faturalandi);
        }
        else if (SeciliDurumFiltre == "İptal")
        {
            liste = liste.Where(i => i.Durum == IrsaliyeDurumu.Iptal);
        }

        liste = SiralamaFiltre switch
        {
            "Tarih: eskiden yeniye" => liste.OrderBy(i => i.Tarih),
            "Tutar: yüksekten düşüğe" => liste.OrderByDescending(i => i.GenelToplam),
            "Cari unvanına göre" => liste.OrderBy(i => i.CariKart?.Unvan),
            _ => liste.OrderByDescending(i => i.Tarih)
        };
        var sonuc = liste.ToList();
        Irsaliyeler = new ObservableCollection<Irsaliye>(sonuc);
        FiltrelenmisKayitSayisi = sonuc.Count;
        FiltrelenmisToplam = sonuc.Where(i => i.Durum != IrsaliyeDurumu.Iptal).Sum(i => i.GenelToplam);
        BildirimGonder(nameof(FiltreSonucOzeti));
    }

    private void FiltreleriTemizle()
    {
        AramaMetni = string.Empty;
        BaslangicTarihi = null;
        BitisTarihi = null;
        SeciliCariFiltre = null;
        SeciliTurFiltre = "Tümü";
        SeciliDurumFiltre = "Tümü";
        SiralamaFiltre = "Tarih: yeniden eskiye";
        MinimumTutar = null;
        MaksimumTutar = null;
        YalnizGecikenSevkler = false;
        Filtrele();
    }

    private void HizliFiltreUygula(string? filtre)
    {
        FiltreleriTemizle();
        switch (filtre)
        {
            case "Bugün": BaslangicTarihi = DateTime.Today; BitisTarihi = DateTime.Today; break;
            case "Bu Ay": BaslangicTarihi = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); BitisTarihi = DateTime.Today; break;
            case "Faturalanmamış": SeciliDurumFiltre = "Onaylandı"; break;
            case "Sevke Hazır": SeciliDurumFiltre = "Taslak"; break;
            case "Geciken": YalnizGecikenSevkler = true; break;
        }
        Filtrele();
    }

    private async Task YazdirAsync(Irsaliye? irsaliye)
    {
        if (irsaliye == null || _yazdirmaServisi == null) return;
        static string H(string? m) => System.Net.WebUtility.HtmlEncode(m ?? string.Empty);
        // Sevk irsaliyesinde düzenleyen ve alıcının unvanı, adresi, vergi bilgisi, düzenleme ve fiili sevk tarihi bulunmalıdır (VUK 230).
        var firma = await _yazdirmaServisi.FirmaBilgisiGetirAsync(irsaliye.SirketId);
        var cari = irsaliye.CariKart;
        var kalemler = string.Join("", irsaliye.Kalemler.Select((k, i) => $"<tr><td>{i + 1}</td><td>{H(k.UrunKodu)}</td><td>{H(k.UrunAd)}</td><td class=\"right\">{k.Miktar:N2} {H(k.Birim)}</td><td class=\"right\">{k.BirimFiyat:N2} ₺</td><td class=\"right\">{k.SatirToplami:N2} ₺</td></tr>"));
        var baslik = irsaliye.Tur == IrsaliyeTuru.Satis ? "Sevk İrsaliyesi" : "Alış İrsaliyesi";
        var teslimat = string.Join(" ", new[] { irsaliye.TeslimatAdres, irsaliye.TeslimatIlce, irsaliye.TeslimatIl }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var html = $$"""
            <!doctype html><html lang="tr"><head><meta charset="utf-8"><title>{{baslik}} {{H(irsaliye.IrsaliyeNo)}}</title><style>body{font-family:Segoe UI,Arial;margin:32px;color:#172033;font-size:13px}table{width:100%;border-collapse:collapse}.kalem th,.kalem td{border:1px solid #cbd5e1;padding:6px 8px}.kalem th{background:#eef2ff;text-align:left}.right{text-align:right}.ust td{vertical-align:top;padding:0 0 16px 0}.company-title{font-size:18px;font-weight:700}.company-info{color:#475569;font-size:12px}h1{margin:0;font-size:22px;color:#2563EB}.not{margin-top:24px;color:#64748B;font-size:11px;border-top:1px solid #e2e8f0;padding-top:8px}@media print{ body{margin:0} }</style></head><body>
            <table class="ust"><tr><td>{{BelgeYazdirmaServisi.FirmaKunyesiHtml(firma)}}</td><td class="right"><h1>{{baslik}}</h1><div><b>No:</b> {{H(irsaliye.IrsaliyeNo)}}</div><div><b>Düzenleme:</b> {{irsaliye.Tarih:dd.MM.yyyy HH:mm}}</div><div><b>Fiili sevk:</b> {{H(irsaliye.SevkTarihiMetni)}}</div></td></tr></table>
            <table class="ust"><tr><td><b>{{(irsaliye.Tur == IrsaliyeTuru.Satis ? "Alıcı" : "Gönderen")}}:</b> {{H(cari?.Unvan)}}<br>{{H(cari?.Adres)}}<br>V.D.: {{H(cari?.VergiDairesi)}} - No: {{H(cari?.VergiNo)}}</td><td><b>Teslimat:</b> {{H(irsaliye.TeslimatAdSoyad)}} {{H(irsaliye.TeslimatTelefon)}}<br>{{H(teslimat)}}<br><b>Taşıyıcı:</b> {{H(irsaliye.Tasiyici)}} &nbsp; <b>Plaka:</b> {{H(irsaliye.AracPlaka)}} &nbsp; <b>Şoför:</b> {{H(irsaliye.SoforAdi)}}</td></tr></table>
            <table class="kalem"><thead><tr><th>#</th><th>Kod</th><th>Malın cinsi</th><th class="right">Miktar</th><th class="right">Fiyat</th><th class="right">Tutar</th></tr></thead><tbody>{{kalemler}}</tbody></table>
            <p class="right"><b>Genel Toplam: {{irsaliye.GenelToplam:N2}} ₺</b></p>
            <table class="ust" style="margin-top:40px"><tr><td>Teslim Eden<br><br>İmza</td><td class="right">Teslim Alan<br><br>İmza</td></tr></table>
            <div class="not">{{H(string.IsNullOrWhiteSpace(irsaliye.Aciklama) ? firma.IrsaliyeNotu : irsaliye.Aciklama + " — " + firma.IrsaliyeNotu)}}</div></body></html>
            """;
        await _yazdirmaServisi.BelgeyiGoruntuleVeYazdirAsync(html, $"Irsaliye-{irsaliye.IrsaliyeNo}");
    }

    private void YeniIrsaliyeEkle()
    {
        var vm = new IrsaliyeEkleGorunumModeli(_irsaliyeServisi, _cariServisi, _urunServisi);
        var pencere = new Masaustu.Gorunumler.Sayfa.Irsaliye.IrsaliyeEklePenceresi(vm);
        if (pencere.ShowDialog() == true)
        {
            _ = ListeleAsync();
        }
    }

    private void DetayGoster()
    {
        if (SeciliIrsaliye == null) return;
        var pencere = new Masaustu.Gorunumler.Sayfa.Irsaliye.IrsaliyeDetayPenceresi(SeciliIrsaliye, _irsaliyeServisi, this);
        pencere.ShowDialog();
    }

    private async Task OnaylaAsync()
    {
        if (SeciliIrsaliye == null) return;

        var sonuc = MessageBox.Show(
            $"'{SeciliIrsaliye.IrsaliyeNo}' numaralı irsaliyeyi onaylamak istiyor musunuz?\n\nBu işlem ilgili ürünlerin stok hareketlerini oluşturacaktır.",
            "İrsaliye Onayı",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (sonuc != MessageBoxResult.Yes) return;

        Yukleniyor = true;
        try
        {
            await _irsaliyeServisi.OnaylaAsync(SeciliIrsaliye.Id);
            MessageBox.Show("İrsaliye başarıyla onaylandı ve stok hareketleri işlendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            await ListeleAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Onaylama hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task IptalEtAsync()
    {
        if (SeciliIrsaliye == null) return;

        var sonuc = MessageBox.Show(
            $"'{SeciliIrsaliye.IrsaliyeNo}' numaralı irsaliyeyi iptal etmek istiyor musunuz?\n\nBu işlem varsa ilgili stok hareketlerini otomatik geri alacaktır.",
            "İrsaliye İptali",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (sonuc != MessageBoxResult.Yes) return;

        Yukleniyor = true;
        try
        {
            await _irsaliyeServisi.IptalEtAsync(SeciliIrsaliye.Id);
            MessageBox.Show("İrsaliye iptal edildi ve stok hareketleri geri alındı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            await ListeleAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"İptal hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task SilAsync()
    {
        if (SeciliIrsaliye == null) return;

        var sonuc = MessageBox.Show(
            $"'{SeciliIrsaliye.IrsaliyeNo}' numaralı taslak irsaliyeyi silmek istiyor musunuz?",
            "İrsaliye Sil",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (sonuc != MessageBoxResult.Yes) return;

        Yukleniyor = true;
        try
        {
            await _irsaliyeServisi.SilAsync(SeciliIrsaliye.Id);
            MessageBox.Show("Taslak irsaliye silindi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            await ListeleAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Silme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    public async Task FaturayaDonusturAsync()
    {
        if (SeciliIrsaliye == null) return;

        if (SeciliIrsaliye.Durum != IrsaliyeDurumu.Onaylandi)
        {
            MessageBox.Show("Yalnızca 'Onaylandı' durumundaki irsaliyeler faturaya dönüştürülebilir.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sonuc = MessageBox.Show(
            $"'{SeciliIrsaliye.IrsaliyeNo}' numaralı irsaliye resmi faturaya dönüştürülecektir.\n\n" +
            $"• Cari: {SeciliIrsaliye.CariKart?.Unvan}\n" +
            $"• Toplam: {SeciliIrsaliye.GenelToplam:N2} ₺\n\n" +
            "Devam etmek istiyor musunuz?",
            "Faturaya Dönüştür",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (sonuc != MessageBoxResult.Yes) return;

        Yukleniyor = true;
        try
        {
            var fatura = await _irsaliyeServisi.FaturayaDonusturAsync(SeciliIrsaliye.Id);
            MessageBox.Show(
                $"İrsaliye başarıyla '{fatura.FaturaNo}' numaralı faturaya dönüştürüldü ve cari hesaba işlendi!",
                "Fatura Oluşturuldu",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await ListeleAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Faturaya dönüştürme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private void KomutlariGuncelle()
    {
        (OnaylaKomutu as KomutSarici)?.BildirimGonder();
        (IptalEtKomutu as KomutSarici)?.BildirimGonder();
        (SilKomutu as KomutSarici)?.BildirimGonder();
        (FaturayaDonusturKomutu as KomutSarici)?.BildirimGonder();
        (DetayKomutu as KomutSarici)?.BildirimGonder();
        BildirimGonder(nameof(SeciliBelgeAkisi));
    }
}
