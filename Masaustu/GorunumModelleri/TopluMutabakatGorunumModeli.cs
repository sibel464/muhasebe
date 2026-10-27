using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Ortak;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;
using Uygulama.Dto.Rapor;

namespace Masaustu.GorunumModelleri;

public class TopluMutabakatSatiri : TemelGorunumModeli
{
    private bool _secili;
    private string _eposta;
    private string _durum = string.Empty;

    public TopluMutabakatSatiri(CariKart cari, decimal bakiye)
    {
        Cari = cari;
        Bakiye = bakiye;
        _eposta = cari.Eposta?.Trim() ?? string.Empty;
        _secili = _eposta.Length > 0;
    }

    public CariKart Cari { get; }
    public string Kod => Cari.Kod;
    public string Unvan => Cari.Unvan;
    public decimal Bakiye { get; }
    public decimal MutlakBakiye => Math.Abs(Bakiye);
    public string BakiyeTuru => Bakiye > 0 ? "Borçlu" : Bakiye < 0 ? "Alacaklı" : "-";
    public bool Secili { get => _secili; set => OzellikAyarla(ref _secili, value); }
    // Yalnız bu gönderim için değiştirilebilir; cari kartı güncellenmez.
    public string Eposta { get => _eposta; set => OzellikAyarla(ref _eposta, value); }
    public string Durum { get => _durum; set => OzellikAyarla(ref _durum, value); }
}

// Seçilen tarihteki bakiyeyle her cariye ayrı mutabakat mektubu (PDF) e-postalar. Gönderim sırayla yapılır,
// bir carideki hata diğerlerini durdurmaz; sonuç satırın Durum sütununda görünür.
public class TopluMutabakatGorunumModeli : TemelGorunumModeli
{
    public const string UnvanYeri = "[UNVAN]", TarihYeri = "[TARIH]";

    private readonly ICariServisi _cariServisi;
    private readonly ICariHareketServisi _hareketServisi;
    private readonly IBelgeYazdirmaServisi _yazdirmaServisi;
    private readonly IEpostaServisi _epostaServisi;
    private readonly IRaporMerkeziServisi? _raporServisi;
    private DateTime _tarih = DateTime.Today, _listeTarihi = DateTime.Today;
    private bool _sifirBakiyelerDahil, _baBsEkle = true, _calisiyor;
    private string _konu = string.Empty, _metin = string.Empty, _durum = string.Empty;
    private CancellationTokenSource? _iptal;

    public TopluMutabakatGorunumModeli(ICariServisi cariServisi, ICariHareketServisi hareketServisi, IBelgeYazdirmaServisi yazdirmaServisi,
        IEpostaServisi epostaServisi, IRaporMerkeziServisi? raporServisi = null)
    {
        _cariServisi = cariServisi;
        _hareketServisi = hareketServisi;
        _yazdirmaServisi = yazdirmaServisi;
        _epostaServisi = epostaServisi;
        _raporServisi = raporServisi;
        _metin = $"Sayın {UnvanYeri},\n\n[TARIH] tarihi itibarıyla firmamız nezdindeki cari hesabınıza ait mutabakat mektubu ektedir. Mektubu kontrol edip imzalayarak tarafımıza iletmenizi rica ederiz.\n\nSaygılarımızla";

        ListeleKomutu = new KomutSarici(async _ => await ListeleAsync(), _ => !_calisiyor);
        GonderKomutu = new KomutSarici(async _ => await GonderAsync(), _ => !_calisiyor && Satirlar.Any(s => s.Secili));
        DurdurKomutu = new KomutSarici(_ => _iptal?.Cancel(), _ => _calisiyor);
        TumunuSecKomutu = new KomutSarici(_ => { foreach (var s in Satirlar) s.Secili = s.Eposta.Length > 0; }, _ => !_calisiyor);
        HicbiriniSecmeKomutu = new KomutSarici(_ => { foreach (var s in Satirlar) s.Secili = false; }, _ => !_calisiyor);
    }

    public ObservableCollection<TopluMutabakatSatiri> Satirlar { get; } = new();
    public DateTime Tarih { get => _tarih; set => OzellikAyarla(ref _tarih, value); }
    public bool SifirBakiyelerDahil { get => _sifirBakiyelerDahil; set => OzellikAyarla(ref _sifirBakiyelerDahil, value); }
    public bool BaBsEkle { get => _baBsEkle; set => OzellikAyarla(ref _baBsEkle, value); }
    public string Konu { get => _konu; set => OzellikAyarla(ref _konu, value); }
    public string Metin { get => _metin; set => OzellikAyarla(ref _metin, value); }
    public string Durum { get => _durum; private set => OzellikAyarla(ref _durum, value); }
    public bool Calisiyor { get => _calisiyor; private set { OzellikAyarla(ref _calisiyor, value); CommandManager.InvalidateRequerySuggested(); } }

    public ICommand ListeleKomutu { get; }
    public ICommand GonderKomutu { get; }
    public ICommand DurdurKomutu { get; }
    public ICommand TumunuSecKomutu { get; }
    public ICommand HicbiriniSecmeKomutu { get; }

    public async Task ListeleAsync()
    {
        try
        {
            Calisiyor = true;
            Durum = "Bakiyeler hesaplanıyor...";
            var tarih = Tarih.Date;
            var bakiyeler = await _hareketServisi.TarihtekiBakiyelerAsync(tarih);
            var cariler = await _cariServisi.TumunuGetirAsync();
            Satirlar.Clear();
            foreach (var c in cariler.Where(c => c.Aktif).OrderBy(c => c.Kod))
            {
                var bakiye = bakiyeler.TryGetValue(c.Id, out var b) ? b : 0m;
                if (bakiye == 0m && !SifirBakiyelerDahil) continue;
                Satirlar.Add(new TopluMutabakatSatiri(c, bakiye));
            }
            _listeTarihi = tarih;
            if (string.IsNullOrWhiteSpace(Konu))
                Konu = $"{(await _yazdirmaServisi.FirmaBilgisiGetirAsync()).FirmaUnvani} - Cari hesap mutabakatı";
            var epostasiz = Satirlar.Count(s => s.Eposta.Length == 0);
            Durum = Satirlar.Count == 0
                ? "Bu tarihte bakiyesi olan cari yok."
                : $"{Satirlar.Count} cari listelendi" + (epostasiz > 0 ? $"; {epostasiz} carinin e-posta adresi yok (adres sütununa yazabilirsiniz)." : ".");
        }
        catch (Exception ex)
        {
            Durum = "Liste alınamadı: " + ex.Message;
        }
        finally
        {
            Calisiyor = false;
        }
    }

    public async Task GonderAsync()
    {
        var secilenler = Satirlar.Where(s => s.Secili).ToList();
        if (secilenler.Count == 0) return;
        _iptal = new CancellationTokenSource();
        var iptal = _iptal.Token;
        int basarili = 0, hatali = 0;
        try
        {
            Calisiyor = true;
            if (!await _epostaServisi.YapilandirildiMiAsync())
            {
                Durum = "E-posta ayarları yapılmamış. Ayarlar → E-posta sekmesinden SMTP bilgilerini girip kaydedin.";
                return;
            }
            // Bakiyeler listelendiği tarihe aittir; tarih sonradan değiştirildiyse önce yeniden listelenmeli.
            if (Tarih.Date != _listeTarihi)
            {
                Durum = "Mutabakat tarihi değişti. Bakiyelerin o tarihe göre hesaplanması için önce 'Listele'ye basın.";
                return;
            }
            var tarih = _listeTarihi;
            var baBs = await BaBsGetirAsync(tarih);
            var firmalar = new Dictionary<Guid, FirmaBilgisi>();
            for (var i = 0; i < secilenler.Count; i++)
            {
                if (iptal.IsCancellationRequested) break;
                var satir = secilenler[i];
                Durum = $"Gönderiliyor {i + 1} / {secilenler.Count}: {satir.Unvan}";
                try
                {
                    if (satir.Eposta.Length == 0) throw new InvalidOperationException("E-posta adresi yok.");
                    var sirketAnahtari = satir.Cari.SirketId ?? Guid.Empty;
                    if (!firmalar.TryGetValue(sirketAnahtari, out var firma))
                        firmalar[sirketAnahtari] = firma = await _yazdirmaServisi.FirmaBilgisiGetirAsync(satir.Cari.SirketId);
                    var cariBaBs = baBs?.Where(k => k.CariKod == satir.Kod).ToList();
                    satir.Durum = "PDF hazırlanıyor...";
                    var pdf = await _epostaServisi.HtmldenPdfAsync(
                        _yazdirmaServisi.MutabakatMektubuHtmlUret(satir.Cari, tarih, satir.Bakiye, cariBaBs, firma), iptal);
                    satir.Durum = "Gönderiliyor...";
                    var guvenliKod = string.Concat(satir.Kod.Where(char.IsLetterOrDigit));
                    await _epostaServisi.GonderAsync(new EpostaMesaji
                    {
                        Alicilar = satir.Eposta,
                        Konu = Konu,
                        Metin = MetniHazirla(Metin, satir.Unvan, tarih, firma.FirmaUnvani),
                        Ekler = { new EpostaEki($"Mutabakat_{guvenliKod}_{tarih:yyyyMMdd}.pdf", pdf) }
                    }, iptal);
                    satir.Durum = $"Gönderildi {DateTime.Now:HH:mm}";
                    satir.Secili = false;
                    basarili++;
                }
                catch (OperationCanceledException) when (iptal.IsCancellationRequested)
                {
                    satir.Durum = "Durduruldu";
                    break;
                }
                catch (Exception ex)
                {
                    satir.Durum = "Hata: " + ex.Message;
                    hatali++;
                }
            }
            Durum = $"{basarili} cariye gönderildi" + (hatali > 0 ? $", {hatali} cariye gönderilemedi (Durum sütununa bakın)" : "")
                + (iptal.IsCancellationRequested ? "; gönderim durduruldu." : ".");
        }
        catch (Exception ex)
        {
            Durum = "Gönderim yapılamadı: " + ex.Message;
        }
        finally
        {
            Calisiyor = false;
            _iptal.Dispose();
            _iptal = null;
        }
    }

    public static string MetniHazirla(string sablon, string unvan, DateTime tarih, string firmaUnvani)
    {
        var metin = (sablon ?? string.Empty).Replace(UnvanYeri, unvan).Replace(TarihYeri, tarih.ToString("dd.MM.yyyy"));
        return string.IsNullOrWhiteSpace(firmaUnvani) ? metin : metin.TrimEnd() + "\n" + firmaUnvani;
    }

    // Mektuba, mutabakat tarihinin ayına ait Ba-Bs tutarları eklenir (tek sorgu, carilere kodla dağıtılır).
    private async Task<List<BaBsKalemiDto>?> BaBsGetirAsync(DateTime tarih)
    {
        if (!BaBsEkle || _raporServisi == null) return null;
        try
        {
            var ayBasi = new DateTime(tarih.Year, tarih.Month, 1);
            return await _raporServisi.GetBaBsRaporuAsync(new SatisRaporFiltreDto { BaslangicTarihi = ayBasi, BitisTarihi = ayBasi.AddMonths(1).AddTicks(-1) }, 0m);
        }
        catch (YetkisizErisimException)
        {
            return null;
        }
    }
}
