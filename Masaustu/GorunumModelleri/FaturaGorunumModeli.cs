using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;
using Masaustu.Servisler;
using System.Windows;
using Masaustu.Gorunumler.Sayfa;

namespace Masaustu.GorunumModelleri;

public sealed class FaturaKalemiGorunumModeli : TemelGorunumModeli
{
	private Urun? _urun;
	private decimal _miktar = 1;
	private decimal _birimFiyat;
	private decimal _iskontoOrani;
	private decimal _kdvOrani;
	private int _siraNo;

	public Urun? Urun { get => _urun; set { if (OzellikAyarla(ref _urun, value)) { UrunBilgileriniUygula(); } } }
	public decimal Miktar 
	{ 
		get => _miktar; 
		set 
		{ 
			if (OzellikAyarla(ref _miktar, value)) 
			{ 
				StokDagiliminiGuncelle(); 
				StokDagilimiOzetiGuncelle(); 
				Hesapla(); 
				Degisti?.Invoke();
				BildirimGonder(nameof(MiktarMetni));
			} 
		} 
	}
	public decimal BirimFiyat { get => _birimFiyat; set { if (OzellikAyarla(ref _birimFiyat, value)) { Hesapla(); Degisti?.Invoke(); BildirimGonder(nameof(BirimFiyatMetni)); } } }
	public decimal IskontoOrani { get => _iskontoOrani; set { if (OzellikAyarla(ref _iskontoOrani, value)) { Hesapla(); Degisti?.Invoke(); BildirimGonder(nameof(IskontoOraniMetni)); } } }
	public decimal KdvOrani { get => _kdvOrani; set { if (OzellikAyarla(ref _kdvOrani, value)) { Hesapla(); Degisti?.Invoke(); BildirimGonder(nameof(KdvOraniMetni)); } } }

	public string MiktarMetni { get => _miktar == 0 ? string.Empty : _miktar.ToString("0.####"); set => Miktar = ParseDecimalSafe(value); }
	public string BirimFiyatMetni { get => _birimFiyat == 0 ? string.Empty : _birimFiyat.ToString("0.####"); set => BirimFiyat = ParseDecimalSafe(value); }
	public string IskontoOraniMetni { get => _iskontoOrani == 0 ? string.Empty : _iskontoOrani.ToString("0.####"); set => IskontoOrani = ParseDecimalSafe(value); }
	public string KdvOraniMetni { get => _kdvOrani == 0 ? string.Empty : _kdvOrani.ToString("0.####"); set => KdvOrani = ParseDecimalSafe(value); }

	// "1.250" bin iki yüz elli okunur (eskiden 1,25 okunuyordu); bkz. TurkceSayi.
	private static decimal ParseDecimalSafe(string val) => TurkceSayi.PozitifVeyaSifir(val);
	public string Kod => Urun?.Kod ?? string.Empty;
	public string Barkod => Urun?.Barkod ?? string.Empty;
	public string Ad => Urun?.Ad ?? string.Empty;
	public string Birim => Urun?.Birim ?? string.Empty;
	public int SiraNo { get => _siraNo; set => OzellikAyarla(ref _siraNo, value); }
	public decimal IskontoTutari { get; private set; }
	public decimal KdvTutari { get; private set; }
	public decimal SatirToplami { get; private set; }
	public ObservableCollection<FaturaKalemiStokDagilimi> StokDagilimlari { get; } = new();
	public string StokDagilimiOzeti => StokDagilimlari.Count > 0
		? string.Join(" | ", StokDagilimlari.Select(d => $"{DepoAdi(d.DepoId)}: {d.Miktar:0.##}"))
		: "Varsayılan mağaza";
	public Action? Degisti { get; set; }
	
	public void StokDagilimiOzetiGuncelle() => BildirimGonder(nameof(StokDagilimiOzeti));
	private static string DepoAdi(int depoId) => StokYerleri.Ad(depoId);

	// Varsayılan stok yeri ilk aktif mağazadır (sabit 1 numaralı depo değil). Liste yüklenemediyse dağılım boş
	// kalır; sunucu da aynı kuralla ilk aktif mağazayı kullanır.
	public void VarsayilanStokDagilimiOlustur()
	{
		StokDagilimlari.Clear();
		if (_miktar > 0 && StokYerleri.VarsayilanId > 0)
		{
			StokDagilimlari.Add(new FaturaKalemiStokDagilimi { DepoId = StokYerleri.VarsayilanId, Miktar = _miktar });
		}
		StokDagilimiOzetiGuncelle();
	}

	public void StokDagiliminiGuncelle()
	{
		if (StokDagilimlari.Count == 0)
		{
			VarsayilanStokDagilimiOlustur();
			return;
		}

		if (StokDagilimlari.Count == 1)
		{
			StokDagilimlari[0].Miktar = _miktar;
		}
		else
		{
			var toplam = StokDagilimlari.Sum(d => d.Miktar);
			if (toplam != _miktar)
			{
				var ilk = StokDagilimlari[0];
				var digerToplam = StokDagilimlari.Skip(1).Sum(d => d.Miktar);
				if (_miktar >= digerToplam)
				{
					ilk.Miktar = _miktar - digerToplam;
				}
				else
				{
					VarsayilanStokDagilimiOlustur();
				}
			}
		}
		StokDagilimiOzetiGuncelle();
	}

	public void UrunBilgileriniUygula()
	{
		if (Urun == null) return;
		if (_miktar == 0) _miktar = 1;
		if (_birimFiyat == 0) _birimFiyat = Urun.PerakendeSatisFiyati;
		if (_kdvOrani == 0) _kdvOrani = Urun.KdvOrani;
		if (StokDagilimlari.Count == 0) VarsayilanStokDagilimiOlustur();
		BildirimGonder(nameof(BirimFiyat));
		BildirimGonder(nameof(KdvOrani));
		BildirimGonder(nameof(Kod));
		BildirimGonder(nameof(Ad));
		BildirimGonder(nameof(Birim));
		Hesapla();
		Degisti?.Invoke();
	}

	public void Hesapla()
	{
		// Satır tutarı kayıttaki hesaplayıcıyla aynı (kuruşa yuvarlı); fatura altı iskonto toplamda dağıtılır.
		var satir = EtkiAlani.Yardimcilar.FaturaHesaplayici.Hesapla([new(Miktar, BirimFiyat, IskontoOrani, KdvOrani)], 0).Satirlar[0];
		IskontoTutari = satir.SatirIskontosu;
		KdvTutari = satir.Kdv;
		SatirToplami = satir.Toplam;
		BildirimGonder(nameof(IskontoTutari));
		BildirimGonder(nameof(KdvTutari));
		BildirimGonder(nameof(SatirToplami));
		// Üst toplamı burada tetiklemeyiz: ToplamlariGuncelle her satırın Hesapla'sını çağırır; burada Degisti çağrılınca
		// ikisi birbirini sonsuza dek çağırıp programı (yığın taşmasıyla) kapatıyordu — faturaya tıklamak yetiyordu.
		// Değeri değişen ayarlayıcılar Degisti'yi ayrıca çağırır.
	}

	public FaturaKalemi ToEntity() 
	{
		if (StokDagilimlari.Count == 0) VarsayilanStokDagilimiOlustur();
		return new()
		{
			UrunId = Urun?.Id ?? 0,
			Miktar = Miktar,
			BirimFiyat = BirimFiyat,
			IskontoOrani = IskontoOrani,
			KdvOrani = KdvOrani,
			UrunKodu = Kod,
			UrunAd = Ad,
			Birim = Birim,
			StokDagilimlari = StokDagilimlari.ToList()
		};
	}
}

public class FaturaGorunumModeli : TemelGorunumModeli
{
	private readonly IFaturaServisi _faturaServisi;
	private readonly ICariServisi _cariServisi;
	private readonly IUrunServisi _urunServisi;
	private readonly IBelgeYazdirmaServisi? _yazdirmaServisi;
	private readonly IEBelgeServisi? _eBelgeServisi;
	private ObservableCollection<Fatura> _faturalar = new();
	private ObservableCollection<CariKart> _cariler = new();
	private ObservableCollection<Urun> _urunler = new();
	private List<CariKart> _tumCariler = new();
	private List<Urun> _tumUrunler = new();
	private ObservableCollection<FaturaKalemiGorunumModeli> _kalemler = new();
	private Fatura? _seciliFatura;
	private CariKart? _seciliCari;
	private Urun? _seciliUrun;
	private FaturaKalemiGorunumModeli? _seciliKalem;
	private FaturaTuru _tur = FaturaTuru.Satis;
	private BelgeDurumu _durum = BelgeDurumu.Taslak;
	private string _faturaNo = string.Empty;
	private DateTime _tarih = DateTime.Today;
	private string _aramaMetni = string.Empty;
	private const string Tumu = "Tümü";
	private string _turFiltresi = Tumu;
	private string _durumFiltresi = Tumu;
	private List<Fatura> _tumFaturalar = new();
	private static readonly Masaustu.Yardimcilar.FaturaMetinDonusturucu Metin = new();
	private string _cariAramaMetni = string.Empty;
	private string _urunAramaMetni = string.Empty;
	private decimal _genelIskontoOrani;
	private string _aciklama = string.Empty;
	private string _durumMesaji = string.Empty;
	private bool _yukleniyor;
	private bool _kaydediliyor;

	public ObservableCollection<Fatura> Faturalar { get => _faturalar; private set => OzellikAyarla(ref _faturalar, value); }
	public ObservableCollection<CariKart> Cariler { get => _cariler; private set => OzellikAyarla(ref _cariler, value); }
	public ObservableCollection<Urun> Urunler { get => _urunler; private set => OzellikAyarla(ref _urunler, value); }
	public ObservableCollection<FaturaKalemiGorunumModeli> Kalemler { get => _kalemler; private set => OzellikAyarla(ref _kalemler, value); }
	public Fatura? SeciliFatura { get => _seciliFatura; set { if (OzellikAyarla(ref _seciliFatura, value)) { if (value != null) _ = DuzenleAsync(value); BildirimGonder(nameof(Duzenlenebilir)); KomutlariGuncelle(); } } }
	public CariKart? SeciliCari { get => _seciliCari; set => OzellikAyarla(ref _seciliCari, value); }
	public Urun? SeciliUrun { get => _seciliUrun; set { if (OzellikAyarla(ref _seciliUrun, value)) (KalemEkleKomutu as KomutSarici)?.BildirimGonder(); } }
	public FaturaKalemiGorunumModeli? SeciliKalem { get => _seciliKalem; set { if (OzellikAyarla(ref _seciliKalem, value)) (KalemSilKomutu as KomutSarici)?.BildirimGonder(); } }
	public FaturaTuru Tur { get => _tur; set => OzellikAyarla(ref _tur, value); }
	public BelgeDurumu Durum { get => _durum; private set { if (OzellikAyarla(ref _durum, value)) { BildirimGonder(nameof(Duzenlenebilir)); KomutlariGuncelle(); } } }
	public string FaturaNo { get => _faturaNo; set => OzellikAyarla(ref _faturaNo, value); }
	public DateTime Tarih { get => _tarih; set => OzellikAyarla(ref _tarih, value); }
	public string AramaMetni { get => _aramaMetni; set { if (OzellikAyarla(ref _aramaMetni, value)) Filtrele(); } }
	public IReadOnlyList<string> TurSecenekleri { get; } = new[] { Tumu, "Satış", "Alış", "Satış İade", "Alış İade" };
	public IReadOnlyList<string> DurumSecenekleri { get; } = new[] { Tumu, "Taslak", "Onaylandı", "İptal", "Beklemede" };
	public string TurFiltresi { get => _turFiltresi; set { if (OzellikAyarla(ref _turFiltresi, value)) Filtrele(); } }
	public string DurumFiltresi { get => _durumFiltresi; set { if (OzellikAyarla(ref _durumFiltresi, value)) Filtrele(); } }
	public string CariAramaMetni { get => _cariAramaMetni; set { OzellikAyarla(ref _cariAramaMetni, value); CariFiltrele(); } }
	public string UrunAramaMetni { get => _urunAramaMetni; set { OzellikAyarla(ref _urunAramaMetni, value); UrunFiltrele(); } }
	public decimal GenelIskontoOrani { get => _genelIskontoOrani; set { if (OzellikAyarla(ref _genelIskontoOrani, value)) ToplamlariGuncelle(); } }
	public string Aciklama { get => _aciklama; set => OzellikAyarla(ref _aciklama, value); }
	public string DurumMesaji { get => _durumMesaji; private set => OzellikAyarla(ref _durumMesaji, value); }
	public bool Yukleniyor { get => _yukleniyor; private set => OzellikAyarla(ref _yukleniyor, value); }
	public bool Kaydediliyor { get => _kaydediliyor; private set => OzellikAyarla(ref _kaydediliyor, value); }
	// Toplamlar kayıtla aynı hesaplayıcıdan gelir; genel iskonto KDV matrahını düşürür.
	private EtkiAlani.Yardimcilar.FaturaHesaplayici.Sonuc Hesap => EtkiAlani.Yardimcilar.FaturaHesaplayici.Hesapla(
		Kalemler.Select(k => new EtkiAlani.Yardimcilar.FaturaHesaplayici.Girdi(k.Miktar, k.BirimFiyat, k.IskontoOrani, k.KdvOrani)).ToList(),
		GenelIskontoOrani);
	public decimal AraToplam => Hesap.AraToplam;
	public decimal SatirIskontosuToplam => Hesap.SatirIskontosuToplam;
	public decimal GenelIskontoTutari => Hesap.GenelIskontoTutari;
	public decimal KdvToplam => Hesap.KdvToplam;
	public decimal GenelToplam => Hesap.GenelToplam;
	public bool Duzenlenebilir => SeciliFatura == null || Durum == BelgeDurumu.Taslak;
	private readonly NavigasyonServisi? _navigasyon;

	private decimal _buAySatisToplam;
	private decimal _buAyAlisToplam;
	private int _taslakSayisi;

	public decimal BuAySatisToplam { get => _buAySatisToplam; private set => OzellikAyarla(ref _buAySatisToplam, value); }
	public decimal BuAyAlisToplam { get => _buAyAlisToplam; private set => OzellikAyarla(ref _buAyAlisToplam, value); }
	public int TaslakSayisi { get => _taslakSayisi; private set => OzellikAyarla(ref _taslakSayisi, value); }

	public ICommand FaturaEkleSayfasinaGitKomutu { get; }
	public ICommand YeniKomutu { get; }
	public ICommand YenileKomutu { get; }
	public ICommand KaydetKomutu { get; }
	public ICommand SilKomutu { get; }
	public ICommand OnaylaKomutu { get; }
	public ICommand IptalKomutu { get; }
	public ICommand KalemEkleKomutu { get; }
	public ICommand KalemSilKomutu { get; }
	public ICommand FaturaYazdirKomutu { get; }
	public ICommand DetayKomutu { get; }
	public ICommand ExcelAktarKomutu { get; }
	public ICommand EBelgeKomutu { get; }
	public ICommand TopluEBelgeKomutu { get; }
	public ICommand GelenEFaturaKomutu { get; }
	public bool EBelgeKullanilabilir => _eBelgeServisi != null;

	public FaturaGorunumModeli(
		IFaturaServisi faturaServisi, 
		ICariServisi cariServisi, 
		IUrunServisi urunServisi, 
		NavigasyonServisi? navigasyon = null,
		IBelgeYazdirmaServisi? yazdirmaServisi = null,
		IEBelgeServisi? eBelgeServisi = null)
	{
		_eBelgeServisi = eBelgeServisi;
		_faturaServisi = faturaServisi;
		_cariServisi = cariServisi;
		_urunServisi = urunServisi;
		_navigasyon = navigasyon;
		_yazdirmaServisi = yazdirmaServisi;

		ExcelAktarKomutu = new KomutSarici(_ => ExcelDisaAktarYardimcisi.AktarCsv(Faturalar.Select(f => new { f.FaturaNo, f.Tarih, f.Tur, f.GenelToplam, f.Durum }), "Fatura_Listesi"));
		FaturaEkleSayfasinaGitKomutu = new KomutSarici(_ => YeniFaturaPenceresiAc());
		YeniKomutu = new KomutSarici(_ => Yeni());
		YenileKomutu = new KomutSarici(async _ => await YukleAsync());
		KaydetKomutu = new KomutSarici(async _ => await KaydetAsync(), _ => Duzenlenebilir && !Kaydediliyor);
		// Listedeki satır düğmeleri kendi satırının faturasını parametre olarak gönderir; düğme o faturanın durumuna göre açılır.
		SilKomutu = new KomutSarici(async param => { if (param is Fatura f) SeciliFatura = f; await SilAsync(); }, param => (param as Fatura ?? SeciliFatura)?.Durum == BelgeDurumu.Taslak);
		OnaylaKomutu = new KomutSarici(async param => { if (param is Fatura f) SeciliFatura = f; await OnaylaAsync(); }, param => (param as Fatura ?? SeciliFatura)?.Durum == BelgeDurumu.Taslak);
		IptalKomutu = new KomutSarici(async param => { if (param is Fatura f) SeciliFatura = f; await IptalAsync(); }, param => (param as Fatura ?? SeciliFatura)?.Durum == BelgeDurumu.Onaylandi);
		// e-Belge yalnız onaylı satış faturası ve alış iadesi için (diğerleri karşı taraftan gelir).
		EBelgeKomutu = new KomutSarici(async param =>
		{
			if ((param as Fatura ?? SeciliFatura) is not Fatura f || _eBelgeServisi == null) return;
			if (await EBelgePenceresi.GosterAsync(_eBelgeServisi, f))
				await YukleAsync();
		}, param => (param as Fatura ?? SeciliFatura) is { Durum: BelgeDurumu.Onaylandi, Tur: FaturaTuru.Satis or FaturaTuru.AlisIade } && _eBelgeServisi != null);
		TopluEBelgeKomutu = new KomutSarici(async _ =>
		{
			if (_eBelgeServisi != null && await EBelgePenceresi.TopluGosterAsync(_eBelgeServisi))
				await YukleAsync();
		}, _ => _eBelgeServisi != null);
		GelenEFaturaKomutu = new KomutSarici(async _ =>
		{
			if (_eBelgeServisi != null && await EBelgePenceresi.GelenAktarAsync(_eBelgeServisi) > 0)
				await YukleAsync();
		}, _ => _eBelgeServisi != null);
		DetayKomutu = new KomutSarici(param => { if ((param as Fatura ?? SeciliFatura) is Fatura f) FaturaDetayPenceresi.Goster(f); }, param => param is Fatura || SeciliFatura != null);
		KalemEkleKomutu = new KomutSarici(_ => KalemEkle(), _ => Duzenlenebilir && SeciliUrun != null);
		KalemSilKomutu = new KomutSarici(_ => { if (SeciliKalem != null) Kalemler.Remove(SeciliKalem); ToplamlariGuncelle(); }, _ => Duzenlenebilir && SeciliKalem != null);
		FaturaYazdirKomutu = new KomutSarici(async param => 
		{
			var hedefFatura = (param as Fatura) ?? SeciliFatura;
			if (hedefFatura is { Id: > 0, Kalemler.Count: 0 })
				hedefFatura = await _faturaServisi.DetayGetirAsync(hedefFatura.Id) ?? hedefFatura;
			if (hedefFatura != null && _yazdirmaServisi != null)
			{
				var firma = await _yazdirmaServisi.FirmaBilgisiGetirAsync(hedefFatura.SirketId);
				if ((string.IsNullOrWhiteSpace(firma.VergiNo) || string.IsNullOrWhiteSpace(firma.Adres)) &&
				    System.Windows.MessageBox.Show("Firmanızın vergi numarası veya adresi girilmemiş; fatura bu bilgiler olmadan basılacak.\n\nBilgileri Ayarlar > Firma & Parametreler sekmesinden girebilirsiniz. Yine de yazdırılsın mı?",
				        "Firma bilgileri eksik", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
					return;
				await _yazdirmaServisi.FaturaYazdirAsync(hedefFatura, firma: firma);
			}
		});
		Yeni();
		_ = YukleAsync();
	}

	private void YeniFaturaPenceresiAc()
	{
		if (_navigasyon == null) return;
		var vm = new FaturaEkleGorunumModeli(_faturaServisi, _cariServisi, _urunServisi, _navigasyon);
		var pencere = new Window { Title = "Yeni Fatura", Width = 1240, Height = 720, MinWidth = 1050, MinHeight = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Background = System.Windows.Media.Brushes.White, Content = new FaturaEkleSayfasi { DataContext = vm } };
		pencere.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
		vm.Kapat = pencere.Close;
		pencere.ShowDialog();
		_ = YukleAsync();
	}

	public async Task YukleAsync()
	{
		Yukleniyor = true;
		try
		{
			_tumFaturalar = await _faturaServisi.ListeGetirAsync();

			var ayBaslangic = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
			BuAySatisToplam = _tumFaturalar.Where(f => f.Tarih >= ayBaslangic && f.Tur == FaturaTuru.Satis && f.Durum == BelgeDurumu.Onaylandi).Sum(f => f.GenelToplam);
			BuAyAlisToplam = _tumFaturalar.Where(f => f.Tarih >= ayBaslangic && f.Tur == FaturaTuru.Alis && f.Durum == BelgeDurumu.Onaylandi).Sum(f => f.GenelToplam);
			TaslakSayisi = _tumFaturalar.Count(f => f.Durum == BelgeDurumu.Taslak);

			_tumCariler = await _cariServisi.TumunuGetirAsync();
			_tumUrunler = (await _urunServisi.TumunuGetirAsync()).Where(x => x.Aktif).ToList();
			Cariler = new ObservableCollection<CariKart>(_tumCariler);
			Urunler = new ObservableCollection<Urun>(_tumUrunler);
			Filtrele();
		}
		catch (Exception ex) { DurumMesaji = $"Veriler yüklenemedi: {ex.Message}"; HatayiLogla(ex, "FaturaGorunumModeli.YukleAsync"); }
		finally { Yukleniyor = false; }
	}

	// Liste bir kez yüklenir; arama ve filtreler bellekte uygulanır (yazarken veritabanına art arda sorgu gitmez).
	private void Filtrele()
	{
		var arama = AramaMetni.Trim();
		var sonuc = _tumFaturalar.Where(f =>
			(arama.Length == 0 || f.FaturaNo.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ||
			 (f.CariKart?.Unvan?.Contains(arama, StringComparison.CurrentCultureIgnoreCase) ?? false)) &&
			(TurFiltresi == Tumu || (string)Metin.Convert(f.Tur, typeof(string), null!, CultureInfo.CurrentCulture) == TurFiltresi) &&
			(DurumFiltresi == Tumu || (string)Metin.Convert(f.Durum, typeof(string), null!, CultureInfo.CurrentCulture) == DurumFiltresi)).ToList();
		Faturalar = new ObservableCollection<Fatura>(sonuc);
		DurumMesaji = sonuc.Count == _tumFaturalar.Count ? $"{sonuc.Count} fatura listeleniyor." : $"{sonuc.Count} / {_tumFaturalar.Count} fatura listeleniyor.";
	}
	private void CariFiltrele() => Cariler = new ObservableCollection<CariKart>(_tumCariler.Where(x => string.IsNullOrWhiteSpace(CariAramaMetni) || x.Kod.Contains(CariAramaMetni, StringComparison.OrdinalIgnoreCase) || x.Unvan.Contains(CariAramaMetni, StringComparison.OrdinalIgnoreCase)));
	private void UrunFiltrele() => Urunler = new ObservableCollection<Urun>(_tumUrunler.Where(x => string.IsNullOrWhiteSpace(UrunAramaMetni) || x.Kod.Contains(UrunAramaMetni, StringComparison.OrdinalIgnoreCase) || x.Ad.Contains(UrunAramaMetni, StringComparison.OrdinalIgnoreCase)));

	private void Yeni()
	{
		_tevkifat = (null, 0);
		SeciliFatura = null; Tur = FaturaTuru.Satis; Durum = BelgeDurumu.Taslak; FaturaNo = string.Empty; Tarih = DateTime.Today; SeciliCari = null; Kalemler = new(); GenelIskontoOrani = 0; Aciklama = string.Empty;
		BildirimGonder(nameof(Duzenlenebilir));
		KomutlariGuncelle();
	}

	// Liste kalemsiz yüklenir; seçilen faturanın kalemleri o an okunur.
	private async Task DuzenleAsync(Fatura fatura)
	{
		if (fatura.Kalemler.Count == 0 && fatura.Id > 0)
		{
			try
			{
				var detay = await _faturaServisi.DetayGetirAsync(fatura.Id);
				if (detay == null || !ReferenceEquals(SeciliFatura, fatura)) return;
				fatura.Kalemler = detay.Kalemler;
			}
			catch (Exception ex) { DurumMesaji = $"Fatura kalemleri yüklenemedi: {ex.Message}"; HatayiLogla(ex, "FaturaGorunumModeli.DuzenleAsync"); return; }
		}
		Duzenle(fatura);
	}

	// Bu ekranda tevkifat seçimi yok; düzenlenen taslağın tevkifatı kaydederken korunur.
	private (string? Kod, decimal Oran) _tevkifat;

	private void Duzenle(Fatura fatura)
	{
		_tevkifat = (fatura.TevkifatKodu, fatura.TevkifatOrani);
		Tur = fatura.Tur; Durum = fatura.Durum; FaturaNo = fatura.FaturaNo; Tarih = fatura.Tarih; SeciliCari = _tumCariler.FirstOrDefault(x => x.Id == fatura.CariKartId); GenelIskontoOrani = fatura.GenelIskontoOrani; Aciklama = fatura.Aciklama;
		var kalemList = fatura.Kalemler.Select(k =>
		{
			var vm = new FaturaKalemiGorunumModeli
			{
				Urun = _tumUrunler.FirstOrDefault(x => x.Id == k.UrunId),
				Miktar = k.Miktar,
				BirimFiyat = k.BirimFiyat,
				IskontoOrani = k.IskontoOrani,
				KdvOrani = k.KdvOrani,
				Degisti = ToplamlariGuncelle
			};
			vm.Hesapla();
			return vm;
		});
		Kalemler = new ObservableCollection<FaturaKalemiGorunumModeli>(kalemList);
		ToplamlariGuncelle(); KomutlariGuncelle();
	}

	private void KalemEkle()
	{
		if (SeciliUrun == null) return;
		var row = new FaturaKalemiGorunumModeli { Urun = SeciliUrun };
		row.Degisti = ToplamlariGuncelle;
		row.UrunBilgileriniUygula();
		row.BirimFiyat = Tur == FaturaTuru.Alis || Tur == FaturaTuru.AlisIade ? SeciliUrun.AlisFiyati : SeciliUrun.PerakendeSatisFiyati;
		row.Miktar = 1; SeciliKalem = row; SeciliUrun = null; ToplamlariGuncelle();
	}

	private async Task KaydetAsync()
	{
		if (SeciliCari == null) { DurumMesaji = "Cari seçilmelidir."; return; }
		if (Kalemler.Count == 0) { DurumMesaji = "En az bir fatura kalemi eklenmelidir."; return; }
		Kaydediliyor = true;
		try
		{
			var entity = new Fatura { Id = SeciliFatura?.Id ?? 0, CariKartId = SeciliCari.Id, Tur = Tur, Tarih = Tarih, FaturaNo = FaturaNo, Aciklama = Aciklama, GenelIskontoOrani = GenelIskontoOrani, TevkifatKodu = SeciliFatura == null ? null : _tevkifat.Kod, TevkifatOrani = SeciliFatura == null ? 0 : _tevkifat.Oran, Kalemler = Kalemler.Select(x => x.ToEntity()).ToList() };
			SeciliFatura = SeciliFatura == null ? await _faturaServisi.EkleAsync(entity) : await _faturaServisi.GuncelleAsync(entity);
			Durum = SeciliFatura.Durum; DurumMesaji = "Taslak fatura kaydedildi."; await YukleAsync();
		}
		catch (Exception ex) { DurumMesaji = $"Fatura kaydedilemedi: {ex.Message}"; HatayiLogla(ex, "FaturaGorunumModeli.KaydetAsync"); }
		finally { Kaydediliyor = false; }
	}

	private async Task SilAsync() { if (SeciliFatura == null) return; try { await _faturaServisi.SilAsync(SeciliFatura.Id); DurumMesaji = "Taslak fatura silindi."; Yeni(); await YukleAsync(); } catch (Exception ex) { DurumMesaji = ex.Message; HatayiLogla(ex, "FaturaGorunumModeli.SilAsync"); } }
	private async Task OnaylaAsync() { if (SeciliFatura == null) return; try { var f = await _faturaServisi.OnaylaAsync(SeciliFatura.Id); SeciliFatura = f; Durum = f.Durum; DurumMesaji = "Fatura onaylandı; cari ve stok hareketleri oluşturuldu."; await YukleAsync(); } catch (Exception ex) { DurumMesaji = $"Fatura onaylanamadı: {ex.Message}"; HatayiLogla(ex, "FaturaGorunumModeli.OnaylaAsync"); } }
	private async Task IptalAsync()
	{
		if (SeciliFatura == null) return;
		var fatura = SeciliFatura;
		var pencere = new Masaustu.Gorunumler.Ortak.MetinGirisPenceresi(
			"Faturayı İptal Et",
			$"{fatura.FaturaNo} - {fatura.CariKart?.Unvan} - {fatura.GenelToplam:N2} ₺\n\n" +
			"İptal edilince carinin borcu/alacağı, stok ve muhasebe kaydı ters kayıtla geri alınır. " +
			"Fatura peşin ödendiyse kasadan/bankadan iade kaydı oluşur. Bu işlem geri alınamaz.\n\nİptal nedenini yazın:");
		pencere.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
		if (pencere.ShowDialog() != true) return;
		try
		{
			var f = await _faturaServisi.IptalEtAsync(fatura.Id, pencere.GirilenMetin);
			await YukleAsync();
			SeciliFatura = _tumFaturalar.FirstOrDefault(x => x.Id == f.Id);
			DurumMesaji = $"{f.FaturaNo} iptal edildi; cari, stok ve muhasebe kayıtları ters kayıtla geri alındı.";
		}
		catch (Exception ex) { DurumMesaji = $"Fatura iptal edilemedi: {ex.Message}"; HatayiLogla(ex, "FaturaGorunumModeli.IptalAsync"); }
	}
	private void ToplamlariGuncelle() { foreach (var row in Kalemler) row.Hesapla(); BildirimGonder(nameof(AraToplam)); BildirimGonder(nameof(SatirIskontosuToplam)); BildirimGonder(nameof(GenelIskontoTutari)); BildirimGonder(nameof(KdvToplam)); BildirimGonder(nameof(GenelToplam)); }
	private void KomutlariGuncelle() { (KaydetKomutu as KomutSarici)?.BildirimGonder(); (SilKomutu as KomutSarici)?.BildirimGonder(); (OnaylaKomutu as KomutSarici)?.BildirimGonder(); (IptalKomutu as KomutSarici)?.BildirimGonder(); }

	private static void HatayiLogla(Exception exception, string kaynak)
	{
		try
		{
			var log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {kaynak}\nMessage: {exception.Message}\nInnerException: {exception.InnerException?.Message}\nStackTrace: {exception.StackTrace}\n\n";
			File.AppendAllText(Altyapi.Veri.UygulamaKlasorleri.LogYolu("uygulama-hatalari.log"), log);
		}
		catch
		{
			System.Diagnostics.Debug.WriteLine($"{kaynak}: {exception}");
		}
	}
}
