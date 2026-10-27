using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Ortak;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;
using Uygulama.Dto.Rapor;

namespace Masaustu.GorunumModelleri;

public class VadeHatirlatmaSatiri : TemelGorunumModeli
{
    private bool _secili;
    private string _eposta;
    private string _durum = string.Empty;

    public VadeHatirlatmaSatiri(CariKart cari, List<VadesiGecmisAlacakDto> kalemler)
    {
        Cari = cari;
        Kalemler = kalemler;
        _eposta = cari.Eposta?.Trim() ?? string.Empty;
        _secili = _eposta.Length > 0;
    }

    public CariKart Cari { get; }
    public List<VadesiGecmisAlacakDto> Kalemler { get; }
    public string Kod => Cari.Kod;
    public string Unvan => Cari.Unvan;
    public int BelgeSayisi => Kalemler.Count;
    public decimal Toplam => Kalemler.Sum(k => k.AcikTutar);
    public int EnUzunGecikme => Kalemler.Max(k => k.GecikmeGun);
    public bool Secili { get => _secili; set => OzellikAyarla(ref _secili, value); }
    // Yalnız bu gönderim için değiştirilebilir; cari kartı güncellenmez.
    public string Eposta { get => _eposta; set => OzellikAyarla(ref _eposta, value); }
    public string Durum { get => _durum; set => OzellikAyarla(ref _durum, value); }
}

// Vadesi geçmiş açık borçları olan carilere, belgelerin listesini içeren hatırlatma e-postası gönderir.
// Açık borçlar yaşlandırma raporuyla aynı FIFO hesabından gelir; gönderim sırayla, satır bazında sonuçla yapılır.
public class VadeHatirlatmaGorunumModeli : TemelGorunumModeli
{
    public const string UnvanYeri = "[UNVAN]", ListeYeri = "[LISTE]", ToplamYeri = "[TOPLAM]";
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IRaporMerkeziServisi _raporServisi;
    private readonly ICariServisi _cariServisi;
    private readonly ICariHareketServisi _hareketServisi;
    private readonly IBelgeYazdirmaServisi _yazdirmaServisi;
    private readonly IEpostaServisi _epostaServisi;
    private int _enAzGecikmeGun = 1;
    private bool _ekstreEkle, _calisiyor;
    private string _konu = string.Empty, _metin, _durum = string.Empty;
    private CancellationTokenSource? _iptal;

    public VadeHatirlatmaGorunumModeli(IRaporMerkeziServisi raporServisi, ICariServisi cariServisi, ICariHareketServisi hareketServisi,
        IBelgeYazdirmaServisi yazdirmaServisi, IEpostaServisi epostaServisi)
    {
        _raporServisi = raporServisi;
        _cariServisi = cariServisi;
        _hareketServisi = hareketServisi;
        _yazdirmaServisi = yazdirmaServisi;
        _epostaServisi = epostaServisi;
        _metin = $"Sayın {UnvanYeri},\n\nKayıtlarımıza göre vadesi geçmiş aşağıdaki ödemeleriniz bulunmaktadır:\n\n{ListeYeri}\n\nToplam: {ToplamYeri}\n\nÖdemenizi en kısa sürede yapmanızı rica ederiz. Ödemeyi yaptıysanız bu iletiyi dikkate almayınız.\n\nSaygılarımızla";

        ListeleKomutu = new KomutSarici(async _ => await ListeleAsync(), _ => !_calisiyor);
        GonderKomutu = new KomutSarici(async _ => await GonderAsync(), _ => !_calisiyor && Satirlar.Any(s => s.Secili));
        DurdurKomutu = new KomutSarici(_ => _iptal?.Cancel(), _ => _calisiyor);
        TumunuSecKomutu = new KomutSarici(_ => { foreach (var s in Satirlar) s.Secili = s.Eposta.Length > 0; }, _ => !_calisiyor);
        HicbiriniSecmeKomutu = new KomutSarici(_ => { foreach (var s in Satirlar) s.Secili = false; }, _ => !_calisiyor);
    }

    public ObservableCollection<VadeHatirlatmaSatiri> Satirlar { get; } = new();
    public int EnAzGecikmeGun { get => _enAzGecikmeGun; set => OzellikAyarla(ref _enAzGecikmeGun, Math.Max(1, value)); }
    public bool EkstreEkle { get => _ekstreEkle; set => OzellikAyarla(ref _ekstreEkle, value); }
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
            Durum = "Vadesi geçmiş alacaklar hesaplanıyor...";
            var kalemler = await _raporServisi.GetVadesiGecmisAlacaklarAsync(new SatisRaporFiltreDto(), EnAzGecikmeGun);
            var cariler = (await _cariServisi.TumunuGetirAsync()).ToDictionary(c => c.Id);
            Satirlar.Clear();
            foreach (var g in kalemler.GroupBy(k => k.CariId).OrderByDescending(g => g.Max(k => k.GecikmeGun)))
                if (cariler.TryGetValue(g.Key, out var cari) && cari.Aktif)
                    Satirlar.Add(new VadeHatirlatmaSatiri(cari, g.OrderBy(k => k.VadeTarihi).ToList()));
            if (string.IsNullOrWhiteSpace(Konu))
                Konu = $"{(await _yazdirmaServisi.FirmaBilgisiGetirAsync()).FirmaUnvani} - Vadesi geçmiş ödeme hatırlatması";
            var epostasiz = Satirlar.Count(s => s.Eposta.Length == 0);
            Durum = Satirlar.Count == 0
                ? "Vadesi geçmiş açık alacak yok."
                : $"{Satirlar.Count} cari, toplam {Satirlar.Sum(s => s.Toplam).ToString("N2", Tr)} TL vadesi geçmiş alacak"
                  + (epostasiz > 0 ? $"; {epostasiz} carinin e-posta adresi yok (adres sütununa yazabilirsiniz)." : ".");
        }
        catch (YetkisizErisimException)
        {
            Durum = "Bu listeyi görmek için rapor yetkiniz yok.";
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
                    var mesaj = new EpostaMesaji
                    {
                        Alicilar = satir.Eposta,
                        Konu = Konu,
                        Metin = MetniHazirla(Metin, satir.Unvan, satir.Kalemler, firma.FirmaUnvani)
                    };
                    if (EkstreEkle)
                    {
                        satir.Durum = "Ekstre hazırlanıyor...";
                        var hareketler = await _hareketServisi.CariKartinHareketleriniGetirAsync(satir.Cari.Id);
                        var guvenliKod = string.Concat(satir.Kod.Where(char.IsLetterOrDigit));
                        mesaj.Ekler.Add(new EpostaEki($"Ekstre_{guvenliKod}_{DateTime.Today:yyyyMMdd}.pdf",
                            await _epostaServisi.HtmldenPdfAsync(_yazdirmaServisi.CariEkstreHtmlUret(satir.Cari, hareketler, firma: firma), iptal)));
                    }
                    satir.Durum = "Gönderiliyor...";
                    await _epostaServisi.GonderAsync(mesaj, iptal);
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

    public static string MetniHazirla(string sablon, string unvan, IReadOnlyList<VadesiGecmisAlacakDto> kalemler, string firmaUnvani)
    {
        var liste = new StringBuilder();
        foreach (var k in kalemler)
        {
            var belge = string.IsNullOrWhiteSpace(k.BelgeNo) ? k.BelgeTuru : $"{k.BelgeTuru} {k.BelgeNo}";
            var kismi = k.AcikTutar < k.Tutar ? $" ({k.Tutar.ToString("N2", Tr)} TL'lik belgenin kalanı)" : "";
            liste.Append($"• {belge}, {k.BelgeTarihi:dd.MM.yyyy} tarihli, vade {k.VadeTarihi:dd.MM.yyyy}: {k.AcikTutar.ToString("N2", Tr)} TL{kismi}, {k.GecikmeGun} gün gecikmiş\n");
        }
        var metin = (sablon ?? string.Empty)
            .Replace(UnvanYeri, unvan)
            .Replace(ListeYeri, liste.ToString().TrimEnd())
            .Replace(ToplamYeri, kalemler.Sum(k => k.AcikTutar).ToString("N2", Tr) + " TL");
        return string.IsNullOrWhiteSpace(firmaUnvani) ? metin : metin.TrimEnd() + "\n" + firmaUnvani;
    }
}
