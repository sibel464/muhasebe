using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Stok360;
using Masaustu.Yardimcilar;
using EtkiAlani.Modeller.Stok;

namespace Masaustu.GorunumModelleri;

public enum Stok360Sekme
{
    Genel = 0,
    LokasyonStoklari = 1,
    StokHareketleri = 2,
    SatisGecmisi = 3,
    AlisGecmisi = 4,
    SiparisRezervasyon = 5,
    Transferler = 6,
    SayimDuzeltme = 7,
    Fiyatlar = 8,
    MaliyetKarlilik = 9,
    BelgeGecmisi = 10,
    SistemBilgisi = 11
}

public record Stok360SekmeSecimi(Stok360Sekme Tur, string Ad);

public class UrunStok360GorunumModeli : TemelGorunumModeli
{
    private readonly IStok360Servisi _stok360Servisi;
    private readonly IUrunServisi _urunServisi;
    private readonly IAktifFirmaBaglami _firmaBaglami;
    private readonly IKullaniciServisi _kullaniciServisi;

    private int _urunId;
    private Stok360SekmeSecimi? _seciliSekme;
    private UrunStokOzetDto? _ozet;
    private UrunMaliyetKarlilikDto? _maliyetKarlilik;
    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;

    // Movement Filters
    private DateTime? _baslangic;
    private DateTime? _bitis;
    private int? _seciliDepoId;
    private StokHareketTuru? _seciliHareketTuru;
    private StokIslemTuru? _seciliIslemTuru;

    public StokHareketTuru? SeciliHareketTuru
    {
        get => _seciliHareketTuru;
        set => OzellikAyarla(ref _seciliHareketTuru, value);
    }

    public StokIslemTuru? SeciliIslemTuru
    {
        get => _seciliIslemTuru;
        set => OzellikAyarla(ref _seciliIslemTuru, value);
    }
    private string _belgeNo = string.Empty;
    private bool _iptalleriGoster;

    public ObservableCollection<Stok360SekmeSecimi> Sekmeler { get; } = new();
    public ObservableCollection<LokasyonStokOzetDto> LokasyonStoklari { get; } = new();
    public ObservableCollection<YuruyenStokSatirDto> Hareketler { get; } = new();
    public ObservableCollection<UrunSatisGecmisDto> Satislar { get; } = new();
    public ObservableCollection<UrunAlisGecmisDto> Alislar { get; } = new();
    public ObservableCollection<UrunSiparisRezervasyonDto> SiparisRezervasyonlar { get; } = new();
    public ObservableCollection<YuruyenStokSatirDto> Transferler { get; } = new();
    public ObservableCollection<YuruyenStokSatirDto> Sayimlar { get; } = new();
    public ObservableCollection<UrunSatisFiyatiDto> SatisFiyatlari { get; } = new();

    public UrunStokOzetDto? Ozet
    {
        get => _ozet;
        private set => OzellikAyarla(ref _ozet, value);
    }

    public UrunMaliyetKarlilikDto? MaliyetKarlilik
    {
        get => _maliyetKarlilik;
        private set => OzellikAyarla(ref _maliyetKarlilik, value);
    }

    public Stok360SekmeSecimi? SeciliSekme
    {
        get => _seciliSekme;
        set
        {
            if (OzellikAyarla(ref _seciliSekme, value))
            {
                BildirimGonder(nameof(GenelAcik));
                BildirimGonder(nameof(LokasyonAcik));
                BildirimGonder(nameof(HareketAcik));
                BildirimGonder(nameof(SatisAcik));
                BildirimGonder(nameof(AlisAcik));
                BildirimGonder(nameof(SiparisAcik));
                BildirimGonder(nameof(TransferAcik));
                BildirimGonder(nameof(SayimAcik));
                BildirimGonder(nameof(FiyatAcik));
                BildirimGonder(nameof(MaliyetAcik));
                BildirimGonder(nameof(BelgeAcik));
                BildirimGonder(nameof(SistemAcik));
                _ = SekmeYukleAsync();
            }
        }
    }

    public bool GenelAcik => SeciliSekme?.Tur == Stok360Sekme.Genel;
    public bool LokasyonAcik => SeciliSekme?.Tur == Stok360Sekme.LokasyonStoklari;
    public bool HareketAcik => SeciliSekme?.Tur == Stok360Sekme.StokHareketleri;
    public bool SatisAcik => SeciliSekme?.Tur == Stok360Sekme.SatisGecmisi;
    public bool AlisAcik => SeciliSekme?.Tur == Stok360Sekme.AlisGecmisi;
    public bool SiparisAcik => SeciliSekme?.Tur == Stok360Sekme.SiparisRezervasyon;
    public bool TransferAcik => SeciliSekme?.Tur == Stok360Sekme.Transferler;
    public bool SayimAcik => SeciliSekme?.Tur == Stok360Sekme.SayimDuzeltme;
    public bool FiyatAcik => SeciliSekme?.Tur == Stok360Sekme.Fiyatlar;
    public bool MaliyetAcik => SeciliSekme?.Tur == Stok360Sekme.MaliyetKarlilik;
    public bool BelgeAcik => SeciliSekme?.Tur == Stok360Sekme.BelgeGecmisi;
    public bool SistemAcik => SeciliSekme?.Tur == Stok360Sekme.SistemBilgisi;

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

    // Filter properties
    public DateTime? Baslangic { get => _baslangic; set => OzellikAyarla(ref _baslangic, value); }
    public DateTime? Bitis { get => _bitis; set => OzellikAyarla(ref _bitis, value); }
    public int? SeciliDepoId { get => _seciliDepoId; set => OzellikAyarla(ref _seciliDepoId, value); }
    public string BelgeNo { get => _belgeNo; set => OzellikAyarla(ref _belgeNo, value); }
    public bool IptalleriGoster { get => _iptalleriGoster; set { if (OzellikAyarla(ref _iptalleriGoster, value)) _ = SekmeYukleAsync(); } }

    public ICommand YenileKomutu { get; }
    public ICommand FiltreleKomutu { get; }
    public ICommand TemizleKomutu { get; }
    public ICommand KapatKomutu { get; }

    public Action? KapatAction { get; set; }

    public UrunStok360GorunumModeli(
        IStok360Servisi stok360Servisi,
        IUrunServisi urunServisi,
        IAktifFirmaBaglami firmaBaglami,
        IKullaniciServisi kullaniciServisi)
    {
        _stok360Servisi = stok360Servisi;
        _urunServisi = urunServisi;
        _firmaBaglami = firmaBaglami;
        _kullaniciServisi = kullaniciServisi;

        string[] sekmeAdlari = new[] {
            "Genel", "Lokasyon Stokları", "Stok Hareketleri", "Satış Geçmişi",
            "Alış Geçmişi", "Sipariş / Rezervasyon", "Transferler", "Sayım / Düzeltme",
            "Fiyatlar", "Maliyet & Kârlılık", "Belge Geçmişi", "Sistem Bilgisi"
        };

        foreach (var tur in Enum.GetValues<Stok360Sekme>())
            Sekmeler.Add(new Stok360SekmeSecimi(tur, sekmeAdlari[(int)tur]));

        _seciliSekme = Sekmeler.FirstOrDefault();

        YenileKomutu = new KomutSarici(async _ => await InitAsync(_urunId));
        FiltreleKomutu = new KomutSarici(async _ => await SekmeYukleAsync());
        TemizleKomutu = new KomutSarici(async _ => { Baslangic = null; Bitis = null; SeciliDepoId = null; BelgeNo = string.Empty; IptalleriGoster = false; await SekmeYukleAsync(); });
        KapatKomutu = new KomutSarici(() => KapatAction?.Invoke());
    }

    public async Task InitAsync(int urunId)
    {
        _urunId = urunId;
        Yukleniyor = true;
        DurumMesaji = "Ürün Stok 360° yükleniyor...";

        try
        {
            Ozet = await _stok360Servisi.GetUrunOzetAsync(_urunId, _firmaBaglami.SirketId);
            MaliyetKarlilik = await _stok360Servisi.GetMaliyetKarlilikAsync(_urunId, _firmaBaglami.SirketId);

            // Installment prices setup
            SatisFiyatlari.Clear();
            if (Ozet != null)
            {
                SatisFiyatlari.Add(new UrunSatisFiyatiDto("Tek Çekim (Peşin)", Ozet.PerakendeSatisFiyati));
                for (int i = 2; i <= 9; i++)
                {
                    decimal taksitliFiyat = Math.Round(Ozet.PerakendeSatisFiyati * (1 + (i * 0.015m)), 2);
                    SatisFiyatlari.Add(new UrunSatisFiyatiDto($"{i} Taksit", taksitliFiyat));
                }
            }

            await SekmeYukleAsync();
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "UrunStok360");
            DurumMesaji = "Stok 360° verileri yüklenirken bir hata oluştu.";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task SekmeYukleAsync()
    {
        if (_urunId == 0 || SeciliSekme == null) return;

        Yukleniyor = true;
        DurumMesaji = $"{SeciliSekme.Ad} yükleniyor...";

        try
        {
            var sirket = _firmaBaglami.SirketId;
            var sorgu = new UrunStok360Sorgu(Baslangic, Bitis, SeciliDepoId, _seciliHareketTuru, _seciliIslemTuru, BelgeNo, null, IptalleriGoster, 1, 100);

            switch (SeciliSekme.Tur)
            {
                case Stok360Sekme.LokasyonStoklari:
                    LokasyonStoklari.Clear();
                    foreach (var item in await _stok360Servisi.GetLokasyonStoklariAsync(_urunId, sirket))
                        LokasyonStoklari.Add(item);
                    break;

                case Stok360Sekme.StokHareketleri:
                    Hareketler.Clear();
                    foreach (var item in await _stok360Servisi.GetYuruyenStokHareketleriAsync(_urunId, sirket, sorgu))
                        Hareketler.Add(item);
                    break;

                case Stok360Sekme.SatisGecmisi:
                    Satislar.Clear();
                    foreach (var item in await _stok360Servisi.GetSatisGecmisiAsync(_urunId, sirket, sorgu))
                        Satislar.Add(item);
                    break;

                case Stok360Sekme.AlisGecmisi:
                    Alislar.Clear();
                    foreach (var item in await _stok360Servisi.GetAlisGecmisiAsync(_urunId, sirket, sorgu))
                        Alislar.Add(item);
                    break;

                case Stok360Sekme.SiparisRezervasyon:
                    SiparisRezervasyonlar.Clear();
                    foreach (var item in await _stok360Servisi.GetSiparisRezervasyonlariAsync(_urunId, sirket))
                        SiparisRezervasyonlar.Add(item);
                    break;

                case Stok360Sekme.Transferler:
                    Transferler.Clear();
                    foreach (var item in await _stok360Servisi.GetTransferGecmisiAsync(_urunId, sirket))
                        Transferler.Add(item);
                    break;

                case Stok360Sekme.SayimDuzeltme:
                    Sayimlar.Clear();
                    foreach (var item in await _stok360Servisi.GetSayimGecmisiAsync(_urunId, sirket))
                        Sayimlar.Add(item);
                    break;
            }

            DurumMesaji = "Kayıtlar yüklendi.";
        }
        catch (Exception ex)
        {
            await App.HatayiLoglaAsync(ex, "UrunStok360Sekme");
            DurumMesaji = "Veriler yüklenirken hata oluştu.";
        }
        finally
        {
            Yukleniyor = false;
        }
    }
}

public record UrunSatisFiyatiDto(string FiyatTuru, decimal Fiyat);
