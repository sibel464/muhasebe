using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Siparis;
using EtkiAlani.Modeller.Stok;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;
using Altyapi.Servisler;
using EtkiAlani.Modeller.Kullanici;
using Masaustu.Gorunumler.Pencere;
using Masaustu.Gorunumler.Sayfa.Siparis;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace Masaustu.GorunumModelleri;

public class SiparisGorunumModeli : TemelGorunumModeli
{
    private readonly ISiparisServisi _servis;
    private readonly ICariServisi _cariServisi;
    private readonly IUrunServisi _urunServisi;
    private readonly IBelgeYazdirmaServisi? _yazdirma;
    private Siparis? _seciliSiparis;
    private CariKart? _seciliCari;
    private Urun? _seciliUrun;
    private string _arama = string.Empty;
    private SiparisTuru? _turFiltre;
    private SiparisDurumu? _durumFiltre;
    private string _mesaj = string.Empty;
    private string _siparisNo = string.Empty;
    private CariKart? _filtreCari;
    private Kullanici? _filtreOlusturan;
    private DateTime? _baslangicTarihi;
    private DateTime? _bitisTarihi;
    private DateTime? _teslimTarihi;
    private bool _kaydediliyor;
    private bool _formAcik;

    public ObservableCollection<Siparis> Siparisler { get; } = new();
    public ObservableCollection<CariKart> Cariler { get; } = new();
    public ObservableCollection<Urun> Urunler { get; } = new();
    public ObservableCollection<Kullanici> Olusturanlar { get; } = new();
    public ObservableCollection<SiparisKalemi> Kalemler { get; } = new();
    public Array Turler => Enum.GetValues(typeof(SiparisTuru));
    public Array Durumlar => Enum.GetValues(typeof(SiparisDurumu));
    public Array OdemeSekilleri => Enum.GetValues(typeof(SiparisOdemeSekli));
    public Siparis Form { get; private set; } = YeniForm();
    public Siparis? SeciliSiparis { get => _seciliSiparis; set => OzellikAyarla(ref _seciliSiparis, value); }
    public CariKart? SeciliCari { get => _seciliCari; set { if (OzellikAyarla(ref _seciliCari, value) && value != null) { Form.CariKartId = value.Id; Form.TeslimatAdSoyad = value.Unvan; Form.TeslimatTelefon = value.Telefon ?? ""; Form.TeslimatAdres = value.Adres ?? ""; FormuYenile(); } } }
    public Urun? SeciliUrun { get => _seciliUrun; set => OzellikAyarla(ref _seciliUrun, value); }
    public string Arama { get => _arama; set { if (OzellikAyarla(ref _arama, value)) _ = YukleAsync(); } }
    public string SiparisNoFiltre { get => _siparisNo; set { if (OzellikAyarla(ref _siparisNo, value)) _ = YukleAsync(); } }
    public CariKart? FiltreCari { get => _filtreCari; set { if (OzellikAyarla(ref _filtreCari, value)) _ = YukleAsync(); } }
    public Kullanici? FiltreOlusturan { get => _filtreOlusturan; set { if (OzellikAyarla(ref _filtreOlusturan, value)) _ = YukleAsync(); } }
    public DateTime? BaslangicTarihi { get => _baslangicTarihi; set { if (OzellikAyarla(ref _baslangicTarihi, value)) _ = YukleAsync(); } }
    public DateTime? BitisTarihi { get => _bitisTarihi; set { if (OzellikAyarla(ref _bitisTarihi, value)) _ = YukleAsync(); } }
    public DateTime? TeslimTarihiFiltre { get => _teslimTarihi; set { if (OzellikAyarla(ref _teslimTarihi, value)) _ = YukleAsync(); } }
    public SiparisTuru? TurFiltre { get => _turFiltre; set { if (OzellikAyarla(ref _turFiltre, value)) _ = YukleAsync(); } }
    public SiparisDurumu? DurumFiltre { get => _durumFiltre; set { if (OzellikAyarla(ref _durumFiltre, value)) _ = YukleAsync(); } }
    public string Mesaj { get => _mesaj; private set => OzellikAyarla(ref _mesaj, value); }
    public bool FormAcik { get => _formAcik; private set { if (OzellikAyarla(ref _formAcik, value)) { BildirimGonder(nameof(ListeAcik)); BildirimGonder(nameof(FormBasligi)); } } }
    public bool ListeAcik => !FormAcik;
    public string FormBasligi => $"{(Form.Id == 0 ? "YENİ" : "DÜZENLE")} {(Form.SiparisTuru == SiparisTuru.Satis ? "SATIŞ SİPARİŞİ" : "ALIŞ SİPARİŞİ")}";
    public SiparisTuru FormSiparisTuru { get => Form.SiparisTuru; set { Form.SiparisTuru = value; BildirimGonder(); BildirimGonder(nameof(FormBasligi)); } }
    public bool Duzenlenebilir => Form.Id == 0 || Form.Durum is SiparisDurumu.Taslak or SiparisDurumu.Beklemede;
    public int BugunkuSiparis => Siparisler.Count(x => x.SiparisTarihi.Date == DateTime.Today);
    public int Bekleyen => Siparisler.Count(x => x.Durum == SiparisDurumu.Beklemede);
    public int Onaylanan => Siparisler.Count(x => x.Durum == SiparisDurumu.Onaylandi);
    public int Tamamlanan => Siparisler.Count(x => x.Durum == SiparisDurumu.Tamamlandi);
    public decimal ToplamTutar => Siparisler.Where(x => x.Durum != SiparisDurumu.Iptal).Sum(x => x.GenelToplam);

    public ICommand YeniKomutu { get; }
    public ICommand YenileKomutu { get; }
    public ICommand UrunEkleKomutu { get; }
    public ICommand KalemSilKomutu { get; }
    public ICommand TaslakKaydetKomutu { get; }
    public ICommand KaydetKomutu { get; }
    public ICommand FormaGeriDonKomutu { get; }
    public ICommand SiparisIptalKomutu { get; }
    public ICommand CariSecKomutu { get; }
    public ICommand UrunSecKomutu { get; }
    public ICommand GoruntuleKomutu { get; }
    public ICommand DuzenleKomutu { get; }
    public ICommand FiltreleriTemizleKomutu { get; }
    public ICommand ExcelAktarKomutu { get; }
    // Onaylanmamış satış siparişi fiyat teklifi, onaylı olan sipariş formu, alış siparişi satın alma siparişi olarak basılır.
    public ICommand YazdirKomutu { get; }

    public SiparisGorunumModeli(ISiparisServisi servis, ICariServisi cariServisi, IUrunServisi urunServisi, IBelgeYazdirmaServisi? yazdirma = null)
    {
        _servis = servis; _cariServisi = cariServisi; _urunServisi = urunServisi; _yazdirma = yazdirma;
        YazdirKomutu = new KomutSarici(async x =>
        {
            if (x is not Siparis s || _yazdirma == null) return;
            try
            {
                var tam = await _servis.GetirAsync(s.Id) ?? throw new InvalidOperationException("Sipariş bulunamadı.");
                await _yazdirma.SiparisYazdirAsync(tam);
            }
            catch (Exception ex) { System.Windows.MessageBox.Show("Belge yazdırılamadı: " + ex.Message, "Yazdırma", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning); }
        });
        ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.AktarCsv(Siparisler.Select(s => new { s.SiparisNo, s.SiparisTarihi, s.SiparisTuru, s.GenelToplam, s.Durum }), "Siparis_Listesi"));
        YeniKomutu = new KomutSarici(Yeni);
        YenileKomutu = new KomutSarici(async _ => await YukleAsync());
        UrunEkleKomutu = new KomutSarici(UrunEkle);
        KalemSilKomutu = new KomutSarici(x => { if (x is SiparisKalemi k) Kalemler.Remove(k); Hesapla(); });
        TaslakKaydetKomutu = new KomutSarici(async _ => await KaydetAsync(true));
        KaydetKomutu = new KomutSarici(async _ => await KaydetAsync(false));
        FormaGeriDonKomutu = new KomutSarici(ListeyeDon);
        SiparisIptalKomutu = new KomutSarici(async x => { if (x is Siparis s) { await DuzenleAsync(s.Id, false); await IptalAsync(); FormAcik = false; } });
        CariSecKomutu = new KomutSarici(CariSec);
        UrunSecKomutu = new KomutSarici(UrunSec);
        GoruntuleKomutu = new KomutSarici(async x => await GoruntuleAsync(x as Siparis));
        DuzenleKomutu = new KomutSarici(async x => { if (x is Siparis s) await DuzenleAsync(s.Id); });
        FiltreleriTemizleKomutu = new KomutSarici(FiltreleriTemizle);
        _ = YukleAsync();
    }

    public async Task YukleAsync()
    {
        try
        {
            var liste = await _servis.ListeleAsync(new SiparisFiltre { Arama = Arama, SiparisNo = SiparisNoFiltre, Tur = TurFiltre, Durum = DurumFiltre,
                CariKartId = FiltreCari?.Id, OlusturanKullaniciId = FiltreOlusturan?.Id, BaslangicTarihi = BaslangicTarihi,
                BitisTarihi = BitisTarihi, TeslimTarihi = TeslimTarihiFiltre });
            Siparisler.Clear(); foreach (var x in liste) Siparisler.Add(x);
            if (Cariler.Count == 0) foreach (var x in await _cariServisi.TumunuGetirAsync()) Cariler.Add(x);
            if (Urunler.Count == 0) foreach (var x in (await _urunServisi.TumunuGetirAsync()).Where(x => x.Aktif)) Urunler.Add(x);
            foreach (var kullanici in liste.Select(x => x.OlusturanKullanici).Where(x => x != null).Cast<Kullanici>().DistinctBy(x => x.Id))
                if (Olusturanlar.All(x => x.Id != kullanici.Id)) Olusturanlar.Add(kullanici);
            OzetleriYenile(); Mesaj = $"{liste.Count} sipariş listelendi.";
        }
        catch (Exception ex) { Mesaj = $"Siparişler yüklenemedi: {ex.Message}"; }
    }

    private void Yeni()
    {
        _seciliSiparis = null; Form = YeniForm(); SeciliCari = null; Kalemler.Clear(); FormAcik = true; FormuYenile(); FormPenceresiniAc();
    }

    private async Task DuzenleAsync(int id, bool formuAc = true)
    {
        var kayit = await _servis.GetirAsync(id); if (kayit == null) return;
        Form = kayit; SeciliCari = Cariler.FirstOrDefault(x => x.Id == kayit.CariKartId);
        Kalemler.Clear(); foreach (var x in kayit.Kalemler) Kalemler.Add(x);
        FormAcik = formuAc; FormuYenile(); if (formuAc) FormPenceresiniAc();
    }

    private void UrunEkle()
    {
        if (SeciliUrun == null || !Duzenlenebilir) return;
        Kalemler.Add(new SiparisKalemi { UrunId = SeciliUrun.Id, UrunKodu = SeciliUrun.Kod, Barkod = SeciliUrun.Barkod ?? "", UrunAdi = SeciliUrun.Ad, Birim = SeciliUrun.Birim, Miktar = 1, BirimFiyat = Form.SiparisTuru == SiparisTuru.Satis ? SeciliUrun.PerakendeSatisFiyati : SeciliUrun.AlisFiyati, KdvOrani = SeciliUrun.KdvOrani });
        Hesapla();
    }

    private async Task KaydetAsync(bool taslak)
    {
        if (_kaydediliyor) return;
        _kaydediliyor = true;
        try
        {
            Form.CariKartId = SeciliCari?.Id ?? 0; Form.Kalemler = Kalemler.ToList();
            Form = Form.Id == 0 ? await _servis.EkleAsync(Form, taslak) : await _servis.GuncelleAsync(Form);
            Mesaj = taslak ? "Sipariş taslak olarak kaydedildi." : "Sipariş beklemeye alındı.";
            await YukleAsync(); FormAcik = false; FormuYenile();
        }
        catch (DbUpdateConcurrencyException) { Mesaj = "Bu sipariş başka bir kullanıcı tarafından güncellendi. Lütfen kayıtları yenileyip tekrar deneyin."; }
        catch (Exception ex) { Mesaj = $"Sipariş kaydedilemedi: {KullaniciMesaji(ex)}"; }
        finally { _kaydediliyor = false; }
    }

    private async Task IptalAsync()
    {
        if (Form.Id == 0) { Yeni(); return; }
        try { Form = await _servis.IptalEtAsync(Form.Id, Form.Versiyon); Mesaj = "Sipariş iptal edildi."; await YukleAsync(); FormuYenile(); }
        catch (Exception ex) { Mesaj = $"Sipariş iptal edilemedi: {KullaniciMesaji(ex)}"; }
    }

    private void Hesapla() { Form.Kalemler = Kalemler.ToList(); SiparisServisi.Hesapla(Form); FormuYenile(); }
    public void KalemleriHesapla() => Hesapla();
    private void FormuYenile() { BildirimGonder(nameof(Form)); BildirimGonder(nameof(FormSiparisTuru)); BildirimGonder(nameof(Duzenlenebilir)); BildirimGonder(nameof(FormBasligi)); }
    private void OzetleriYenile() { BildirimGonder(nameof(BugunkuSiparis)); BildirimGonder(nameof(Bekleyen)); BildirimGonder(nameof(Onaylanan)); BildirimGonder(nameof(Tamamlanan)); BildirimGonder(nameof(ToplamTutar)); }
    private static Siparis YeniForm() => new() { SiparisTarihi = DateTime.Today, TeslimTarihi = DateTime.Today, ParaBirimiId = 1, Kur = 1, SiparisTuru = SiparisTuru.Satis, OdemeSekli = SiparisOdemeSekli.Belirtilmedi };

    private void CariSec()
    {
        var pencere = new CariSecimPenceresi(_cariServisi) { Owner = Application.Current.MainWindow };
        if (pencere.ShowDialog() == true) SeciliCari = pencere.SecilenCari;
    }

    private void UrunSec()
    {
        var pencere = new UrunSecimPenceresi(_urunServisi) { Owner = Application.Current.MainWindow };
        if (pencere.ShowDialog() == true) { SeciliUrun = pencere.SecilenUrun; UrunEkle(); }
    }

    private async Task GoruntuleAsync(Siparis? siparis)
    {
        if (siparis == null) return;
        var detay = await _servis.GetirAsync(siparis.Id);
        if (detay != null) new SiparisDetayPenceresi(detay) { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    private void FiltreleriTemizle()
    {
        _arama = _siparisNo = string.Empty; _turFiltre = null; _durumFiltre = null; _filtreCari = null;
        _filtreOlusturan = null; _baslangicTarihi = _bitisTarihi = _teslimTarihi = null;
        BildirimGonder(string.Empty); _ = YukleAsync();
    }

    private void ListeyeDon()
    {
        FormAcik = false;
        Mesaj = string.Empty;
    }

    private void FormPenceresiniAc()
    {
        var pencere = new SiparisFormPenceresi { DataContext = this, Owner = Application.Current.MainWindow };
        pencere.ShowDialog();
    }

    private static string KullaniciMesaji(Exception ex) => ex is ArgumentException or InvalidOperationException or KeyNotFoundException
        ? ex.Message : "İşlem tamamlanamadı. Teknik ayrıntı kayıt altına alındı.";
}
