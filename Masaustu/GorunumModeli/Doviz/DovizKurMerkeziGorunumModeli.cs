using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using EtkiAlani.Modeller.Ortak;
using Uygulama.Arayuzler;
using Uygulama.Dto.Doviz;
using Masaustu.GorunumModelleri;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModeli.Doviz;

public class DovizKurMerkeziGorunumModeli : TemelGorunumModeli
{
    private readonly IAktifFirmaBaglami _aktifFirma;
    private RevaluasyonKaydiDto? _seciliRevaluasyon;
    public RevaluasyonKaydiDto? SeciliRevaluasyon
    {
        get => _seciliRevaluasyon;
        set { OzellikAyarla(ref _seciliRevaluasyon, value); CommandManager.InvalidateRequerySuggested(); }
    }
    private readonly IKurServisi _kurServisi;
    private readonly ITcmbKurServisi _tcmbServisi;
    private readonly IRevaluasyonServisi _revaluasyonServisi;
    private readonly IExcelExportServisi _excelServisi;
    private readonly IPdfRaporServisi _pdfServisi;

    private bool _yukleniyor;
    private string _durumMesaji = string.Empty;

    private DateTime _seciliTarih = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
    private DovizKuruDto? _seciliKur;

    // Manuel Kur Form
    private string _yeniParaBirimi = "USD";
    private decimal _yeniDovizAlis;
    private decimal _yeniDovizSatis;
    private string _yeniAciklama = string.Empty;

    // Revalüasyon Form
    private string _revaluasyonParaBirimi = "USD";
    private DateTime _revaluasyonTarihi = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
    private RevaluasyonOnizleDto? _revaluasyonOnizleme;

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

    public DateTime SeciliTarih
    {
        get => _seciliTarih;
        set { if (OzellikAyarla(ref _seciliTarih, value)) _ = KurlariYukleAsync(); }
    }

    public DovizKuruDto? SeciliKur
    {
        get => _seciliKur;
        set => OzellikAyarla(ref _seciliKur, value);
    }

    public string YeniParaBirimi
    {
        get => _yeniParaBirimi;
        set => OzellikAyarla(ref _yeniParaBirimi, value);
    }

    public decimal YeniDovizAlis
    {
        get => _yeniDovizAlis;
        set => OzellikAyarla(ref _yeniDovizAlis, value);
    }

    public decimal YeniDovizSatis
    {
        get => _yeniDovizSatis;
        set => OzellikAyarla(ref _yeniDovizSatis, value);
    }

    public string YeniAciklama
    {
        get => _yeniAciklama;
        set => OzellikAyarla(ref _yeniAciklama, value);
    }

    public string RevaluasyonParaBirimi
    {
        get => _revaluasyonParaBirimi;
        set => OzellikAyarla(ref _revaluasyonParaBirimi, value);
    }

    public DateTime RevaluasyonTarihi
    {
        get => _revaluasyonTarihi;
        set => OzellikAyarla(ref _revaluasyonTarihi, value);
    }

    public RevaluasyonOnizleDto? RevaluasyonOnizleme
    {
        get => _revaluasyonOnizleme;
        private set => OzellikAyarla(ref _revaluasyonOnizleme, value);
    }

    public ObservableCollection<DovizKuruDto> GunlukKurlar { get; } = new();
    public ObservableCollection<DovizKuruDto> KurGecmisi { get; } = new();
    public ObservableCollection<DovizliBakiyeOzetDto> DovizliBakiyeler { get; } = new();
    public ObservableCollection<RevaluasyonKaydiDto> RevaluasyonGecmisi { get; } = new();

    public ICommand TcmbGuncelleKomutu { get; }
    public ICommand ManuelKurKaydetKomutu { get; }
    public ICommand RevaluasyonOnizleKomutu { get; }
    public ICommand RevaluasyonUygulaKomutu { get; }
    public ICommand RevaluasyonIptalKomutu { get; }
    public ICommand YenileKomutu { get; }

    public DovizKurMerkeziGorunumModeli(
        IKurServisi kurServisi,
        ITcmbKurServisi tcmbServisi,
        IRevaluasyonServisi revaluasyonServisi,
        IExcelExportServisi excelServisi,
        IPdfRaporServisi pdfServisi,
        IAktifFirmaBaglami aktifFirma)
    {
        _aktifFirma = aktifFirma;
        _kurServisi = kurServisi;
        _tcmbServisi = tcmbServisi;
        _revaluasyonServisi = revaluasyonServisi;
        _excelServisi = excelServisi;
        _pdfServisi = pdfServisi;

        TcmbGuncelleKomutu = new KomutSarici(async () => await TcmbGuncelleAsync());
        ManuelKurKaydetKomutu = new KomutSarici(async () => await ManuelKurKaydetAsync());
        RevaluasyonOnizleKomutu = new KomutSarici(async () => await RevaluasyonOnizleAsync());
        RevaluasyonUygulaKomutu = new KomutSarici(async () => await RevaluasyonUygulaAsync(), () => !Yukleniyor && RevaluasyonOnizleme != null && RevaluasyonOnizleme.Kalemler.Count > 0);
        RevaluasyonIptalKomutu = new KomutSarici(async () => await RevaluasyonIptalAsync(), () => !Yukleniyor && SeciliRevaluasyon != null);
        YenileKomutu = new KomutSarici(async () => await KurlariYukleAsync());

        _ = KurlariYukleAsync();
    }

    public async Task KurlariYukleAsync()
    {
        try
        {
            Yukleniyor = true;
            DurumMesaji = $"{SeciliTarih:dd.MM.yyyy} kurları yükleniyor...";
            GunlukKurlar.Clear();
            DovizliBakiyeler.Clear();

            var list = await _kurServisi.GetirGunlukKurlarAsync(SeciliTarih);
            foreach (var k in list)
            {
                GunlukKurlar.Add(k);
            }

            var bakiyeler = await _revaluasyonServisi.GetirDovizliBakiyeOzetleriAsync(_aktifFirma.SirketId);
            foreach (var b in bakiyeler)
            {
                DovizliBakiyeler.Add(b);
            }

            var gecmis = await _revaluasyonServisi.GetirRevaluasyonGecmisiAsync(_aktifFirma.SirketId, 1, 50);
            RevaluasyonGecmisi.Clear();
            foreach (var r in gecmis.Liste)
            {
                RevaluasyonGecmisi.Add(r);
            }

            DurumMesaji = $"{GunlukKurlar.Count} adet döviz kuru listelendi.";
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

    private async Task TcmbGuncelleAsync()
    {
        try
        {
            Yukleniyor = true;
            DurumMesaji = "TCMB resmi güncel kurları çekiliyor...";
            await _tcmbServisi.GunlukKurlariGuncelleVeyaGetirAsync(SeciliTarih);
            await KurlariYukleAsync();
            DurumMesaji = "TCMB kurları başarıyla güncellendi.";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"TCMB Güncelleme Uyarı: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task ManuelKurKaydetAsync()
    {
        try
        {
            if (YeniDovizAlis <= 0 || YeniDovizSatis <= 0)
            {
                MessageBox.Show("Lütfen geçerli alış ve satış kurları giriniz.", "Manuel Kur Girişi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Yukleniyor = true;
            DurumMesaji = "Manuel kur kaydediliyor ve audit kaydı üretiliyor...";

            await _kurServisi.KaydetManuelKurAsync(new ManuelKurGirisDto
            {
                ParaBirimiKodu = YeniParaBirimi,
                Tarih = SeciliTarih,
                DovizAlis = YeniDovizAlis,
                DovizSatis = YeniDovizSatis,
                Aciklama = YeniAciklama
            });

            MessageBox.Show($"[{YeniParaBirimi}] için manuel kur başarıyla kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            await KurlariYukleAsync();
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Manuel Kur Hatası: {ex.Message}";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task RevaluasyonOnizleAsync()
    {
        try
        {
            Yukleniyor = true;
            DurumMesaji = $"Dönem sonu [{RevaluasyonParaBirimi}] revalüasyon önizlemesi hesaplanıyor...";
            RevaluasyonOnizleme = await _revaluasyonServisi.RevaluasyonOnizleAsync(_aktifFirma.SirketId, RevaluasyonParaBirimi, RevaluasyonTarihi);
            DurumMesaji = $"Önizleme Tamamlandı: {RevaluasyonOnizleme.Kalemler.Count} bakiye kalemi. Toplam Kur Farkı: {RevaluasyonOnizleme.ToplamKurFarki:N2} TL";
        }
        catch (Exception ex)
        {
            DurumMesaji = $"Revalüasyon Önizleme Hatası: {ex.Message}";
            MessageBox.Show(ex.Message, "Önizleme Uyarısı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task RevaluasyonUygulaAsync()
    {
        if (RevaluasyonOnizleme == null || RevaluasyonOnizleme.Kalemler.Count == 0) return;

        var res = MessageBox.Show($"Bu işlem [{RevaluasyonParaBirimi}] için [{RevaluasyonTarihi:dd.MM.yyyy}] tarihli dönem sonu kur farkı yevmiye kaydı üretecektir.\nToplam Kur Farkı Etkisi: {RevaluasyonOnizleme.ToplamKurFarki:N2} TL\n\nDevam etmek istiyor musunuz?", "Revalüasyon Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            try
            {
                Yukleniyor = true;
                DurumMesaji = "Revalüasyon fişi oluşturuluyor...";
                var kayit = await _revaluasyonServisi.RevaluasyonUygulaAsync(RevaluasyonOnizleme);
                MessageBox.Show($"Revalüasyon başarıyla uygulandı.\nFiş No: {kayit.FisNo}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                RevaluasyonOnizleme = null;
                await KurlariYukleAsync();
            }
            catch (Exception ex)
            {
                DurumMesaji = $"Revalüasyon Uygulama Hatası: {ex.Message}";
                MessageBox.Show(ex.Message, "Revalüasyon Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Yukleniyor = false;
            }
        }
    }

    private async Task RevaluasyonIptalAsync()
    {
        var secili = SeciliRevaluasyon;
        if (secili == null) return;

        var res = MessageBox.Show($"Revalüasyon kaydını ({secili.DegerlemeTarihi:dd.MM.yyyy}) İPTAL etmek (storno ters fiş üretmek) istiyor musunuz?", "Revalüasyon İptali", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res == MessageBoxResult.Yes)
        {
            try
            {
                Yukleniyor = true;
                DurumMesaji = "Revalüasyon kaydı iptal ediliyor...";
                await _revaluasyonServisi.RevaluasyonIptalEtAsync(secili.Id, "Kullanıcı İptali", _aktifFirma.SirketId);
                MessageBox.Show("Revalüasyon kaydı ve ilgili yevmiye fişi storno edilerek iptal edildi.", "İptal Edildi", MessageBoxButton.OK, MessageBoxImage.Information);
                await KurlariYukleAsync();
            }
            catch (Exception ex)
            {
                DurumMesaji = $"İptal Hatası: {ex.Message}";
                MessageBox.Show(ex.Message, "İptal Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Yukleniyor = false;
            }
        }
    }
}
