using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using EtkiAlani.Modeller.Ortak;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Muhasebe;

public class YevmiyeFisGorunumModeli : TemelGorunumModeli
{
    private readonly IYevmiyeServisi _yevmiyeServisi;
    private readonly IExcelExportServisi _excelServisi;
    private readonly IPdfRaporServisi _pdfServisi;

    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;

    private YevmiyeFiltreDto _filtre = new();
    private SayfaliSonuc<YevmiyeFisDto>? _sayfaSonucu;
    private YevmiyeFisDto? _seciliFis;

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

    public YevmiyeFiltreDto Filtre
    {
        get => _filtre;
        set => OzellikAyarla(ref _filtre, value);
    }

    public SayfaliSonuc<YevmiyeFisDto>? SayfaSonucu
    {
        get => _sayfaSonucu;
        private set => OzellikAyarla(ref _sayfaSonucu, value);
    }

    public YevmiyeFisDto? SeciliFis
    {
        get => _seciliFis;
        set => OzellikAyarla(ref _seciliFis, value);
    }

    public ObservableCollection<YevmiyeFisDto> Fisler { get; } = new();

    public ICommand YenileKomutu { get; }
    public ICommand YeniFisKomutu { get; }
    public ICommand FisOnaylaKomutu { get; }
    public ICommand FisIptalKomutu { get; }
    public ICommand KaynakBelgeyeGitKomutu { get; }
    public ICommand SonrakiSayfaKomutu { get; }
    public ICommand ÖncekiSayfaKomutu { get; }

    public YevmiyeFisGorunumModeli(
        IYevmiyeServisi yevmiyeServisi,
        IExcelExportServisi excelServisi,
        IPdfRaporServisi pdfServisi)
    {
        _yevmiyeServisi = yevmiyeServisi;
        _excelServisi = excelServisi;
        _pdfServisi = pdfServisi;

        YenileKomutu = new KomutSarici(async () => await SorgulaAsync());
        YeniFisKomutu = new KomutSarici(YeniFisAc);
        FisOnaylaKomutu = new KomutSarici(async () => await FisOnaylaAsync(), () => SeciliFis != null && SeciliFis.Durum == EtkiAlani.Modeller.Muhasebe.YevmiyeDurumu.Taslak);
        FisIptalKomutu = new KomutSarici(async () => await FisIptalAsync(), () => SeciliFis != null && SeciliFis.Durum != EtkiAlani.Modeller.Muhasebe.YevmiyeDurumu.Iptal);
        KaynakBelgeyeGitKomutu = new KomutSarici(KaynakBelgeyeGit, () => SeciliFis != null && SeciliFis.BelgeId > 0);
        SonrakiSayfaKomutu = new KomutSarici(async () => { Filtre.Sayfa++; await SorgulaAsync(); }, () => SayfaSonucu != null && SayfaSonucu.SonrakiSayfaVar);
        ÖncekiSayfaKomutu = new KomutSarici(async () => { Filtre.Sayfa--; await SorgulaAsync(); }, () => SayfaSonucu != null && SayfaSonucu.ÖncekiSayfaVar);

        _ = SorgulaAsync();
    }

    public async Task SorgulaAsync()
    {
        try
        {
            Yukleniyor = true;
            DurumMesaji = "Muhasebe fişleri sorgulanıyor...";
            Fisler.Clear();

            SayfaSonucu = await _yevmiyeServisi.FisleriGetirAsync(Filtre);
            foreach (var f in SayfaSonucu.Liste)
            {
                Fisler.Add(f);
            }

            DurumMesaji = $"Sayfa {SayfaSonucu.Sayfa}/{SayfaSonucu.ToplamSayfa} - Toplam {SayfaSonucu.ToplamKayit} fiş bulundu.";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Sorgulama hatası: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private void YeniFisAc()
    {
        DurumMesaji = "Yeni manuel mahsup fişi penceresi açılıyor...";
    }

    private async Task FisOnaylaAsync()
    {
        if (SeciliFis == null) return;
        var sonuc = MessageBox.Show($"[{SeciliFis.FisNo}] numaralı fişi onaylamak istiyor musunuz?", "Fiş Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (sonuc == MessageBoxResult.Yes)
        {
            var res = await _yevmiyeServisi.FisOnaylaAsync(SeciliFis.Id);
            if (res != null)
            {
                await SorgulaAsync();
            }
        }
    }

    private async Task FisIptalAsync()
    {
        if (SeciliFis == null) return;
        var sonuc = MessageBox.Show($"[{SeciliFis.FisNo}] numaralı fişi İPTAL etmek (ters fiş üretmek) istiyor musunuz?", "Fiş İptali", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (sonuc == MessageBoxResult.Yes)
        {
            var res = await _yevmiyeServisi.FisIptalEtTersKayitUretAsync(SeciliFis.Id, "Kullanıcı İptali");
            if (res != null)
            {
                await SorgulaAsync();
            }
        }
    }

    private void KaynakBelgeyeGit()
    {
        if (SeciliFis == null || SeciliFis.BelgeId <= 0) return;
        MessageBox.Show($"Kaynak Belge Tipi: {SeciliFis.BelgeTipi}, Belge ID: {SeciliFis.BelgeId}", "Kaynak Belge Drilldown", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
