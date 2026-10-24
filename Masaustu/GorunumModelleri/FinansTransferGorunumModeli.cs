using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Masaustu.Yardimcilar;
using Uygulama.Arayuzler;
using Uygulama.Dto.Finans360;

namespace Masaustu.GorunumModelleri;

// Transfer penceresinde seçilebilen kasa veya banka hesabı; bakiye hesabın kendi para birimindedir.
public sealed record TransferHesabi(bool KasaMi, int Id, string Ad, string ParaBirimi, decimal Bakiye)
{
    public string Gosterim => $"{(KasaMi ? "Kasa" : "Banka")} · {Ad} ({ParaBirimi}) — bakiye {Bakiye:N2}";
}

// Kasa ↔ banka ↔ kasa transferi. Kaynak ile hedefin para birimi farklıysa işlem döviz alım/satımıdır:
// çıkan tutar kaynağın, giren tutar hedefin para birimindedir; kur ikisinden hesaplanıp gösterilir.
public class FinansTransferGorunumModeli : TemelGorunumModeli
{
    private readonly IFinansMerkeziServisi _finansServisi;
    private readonly IAktifFirmaBaglami _firmaBaglami;
    private readonly IKullaniciServisi _kullaniciServisi;

    private TransferHesabi? _seciliKaynak;
    private TransferHesabi? _seciliHedef;
    private decimal _cikanTutar;
    private decimal _girenTutar;
    private decimal _masraf;
    private string _aciklama = string.Empty;
    private bool _kaydediliyor;

    public ObservableCollection<TransferHesabi> Hesaplar { get; } = new();

    public TransferHesabi? SeciliKaynak
    {
        get => _seciliKaynak;
        set { if (OzellikAyarla(ref _seciliKaynak, value)) SecimDegisti(); }
    }

    public TransferHesabi? SeciliHedef
    {
        get => _seciliHedef;
        set { if (OzellikAyarla(ref _seciliHedef, value)) SecimDegisti(); }
    }

    public decimal CikanTutar
    {
        get => _cikanTutar;
        set { if (OzellikAyarla(ref _cikanTutar, value)) BildirimGonder(nameof(KurMetni)); }
    }

    public decimal GirenTutar
    {
        get => _girenTutar;
        set { if (OzellikAyarla(ref _girenTutar, value)) BildirimGonder(nameof(KurMetni)); }
    }

    public decimal Masraf { get => _masraf; set => OzellikAyarla(ref _masraf, value); }
    public string Aciklama { get => _aciklama; set => OzellikAyarla(ref _aciklama, value); }
    public bool Kaydediliyor { get => _kaydediliyor; private set => OzellikAyarla(ref _kaydediliyor, value); }

    public string KaynakSimge => DovizKuruYardimcisi.Simge(SeciliKaynak?.ParaBirimi);
    public string HedefSimge => DovizKuruYardimcisi.Simge(SeciliHedef?.ParaBirimi);
    public bool DovizIslemi => SeciliKaynak != null && SeciliHedef != null &&
        !string.Equals(SeciliKaynak.ParaBirimi, SeciliHedef.ParaBirimi, StringComparison.OrdinalIgnoreCase);
    public bool MasrafGorunur => SeciliKaynak is { KasaMi: false } k && !DovizKuruYardimcisi.DovizliMi(k.ParaBirimi);
    public string IslemBasligi => DovizIslemi
        ? (DovizKuruYardimcisi.DovizliMi(SeciliHedef!.ParaBirimi) ? "Döviz alımı" : "Döviz satışı (bozdurma)")
        : "Hesaplar arası transfer (virman)";

    // Döviz tarafının 1 biriminin karşı para birimindeki değeri.
    public string KurMetni
    {
        get
        {
            if (!DovizIslemi || CikanTutar <= 0 || GirenTutar <= 0) return string.Empty;
            var kaynakDoviz = DovizKuruYardimcisi.DovizliMi(SeciliKaynak!.ParaBirimi);
            return kaynakDoviz
                ? $"Uygulanan kur: 1 {SeciliKaynak.ParaBirimi} = {GirenTutar / CikanTutar:N4} {HedefSimge}"
                : $"Uygulanan kur: 1 {SeciliHedef!.ParaBirimi} = {CikanTutar / GirenTutar:N4} {KaynakSimge}";
        }
    }

    public ICommand KaydetKomutu { get; }
    public Action? BasariliKayitAction { get; set; }

    public FinansTransferGorunumModeli(
        IFinansMerkeziServisi finansServisi,
        IAktifFirmaBaglami firmaBaglami,
        IKullaniciServisi kullaniciServisi)
    {
        _finansServisi = finansServisi;
        _firmaBaglami = firmaBaglami;
        _kullaniciServisi = kullaniciServisi;

        KaydetKomutu = new KomutSarici(async _ => await KaydetAsync(), _ => !Kaydediliyor && _kullaniciServisi.YetkiliMi(x => x.VirmanYap));
    }

    private void SecimDegisti()
    {
        BildirimGonder(nameof(KaynakSimge));
        BildirimGonder(nameof(HedefSimge));
        BildirimGonder(nameof(DovizIslemi));
        BildirimGonder(nameof(MasrafGorunur));
        BildirimGonder(nameof(IslemBasligi));
        BildirimGonder(nameof(KurMetni));
    }

    // transferTuru: KasaVirman, BankaVirman, KasaBanka, BankaKasa — yalnız başlangıç seçimini belirler.
    public async Task InitAsync(string transferTuru)
    {
        Hesaplar.Clear();
        foreach (var k in (await _finansServisi.GetKasalarAsync(_firmaBaglami.SirketId)).Where(k => k.Aktif))
            Hesaplar.Add(new TransferHesabi(true, k.Id, k.KasaAdi, k.ParaBirimi, k.MevcutBakiye));
        foreach (var b in (await _finansServisi.GetBankaHesaplariAsync(_firmaBaglami.SirketId)).Where(b => b.Aktif))
            Hesaplar.Add(new TransferHesabi(false, b.Id, b.HesapAdi, b.ParaBirimi, b.Bakiye));

        var kasalar = Hesaplar.Where(h => h.KasaMi).ToList();
        var bankalar = Hesaplar.Where(h => !h.KasaMi).ToList();
        (SeciliKaynak, SeciliHedef) = transferTuru switch
        {
            "BankaVirman" => (bankalar.ElementAtOrDefault(0), bankalar.ElementAtOrDefault(1)),
            "KasaBanka" => (kasalar.FirstOrDefault(), bankalar.FirstOrDefault()),
            "BankaKasa" => (bankalar.FirstOrDefault(), kasalar.FirstOrDefault()),
            _ => (kasalar.ElementAtOrDefault(0), kasalar.ElementAtOrDefault(1))
        };
    }

    public async Task KaydetAsync()
    {
        try
        {
            if (SeciliKaynak == null || SeciliHedef == null)
                throw new InvalidOperationException("Kaynak ve hedef hesabı seçiniz.");
            if (SeciliKaynak.KasaMi == SeciliHedef.KasaMi && SeciliKaynak.Id == SeciliHedef.Id)
                throw new InvalidOperationException("Kaynak ve hedef hesap aynı olamaz.");
            if (CikanTutar <= 0)
                throw new InvalidOperationException("Çıkan tutar sıfırdan büyük olmalıdır.");
            if (DovizIslemi && GirenTutar <= 0)
                throw new InvalidOperationException($"Hedefe giren {HedefSimge} tutarını giriniz.");

            Kaydediliyor = true;
            await _finansServisi.HesapTransferiYapAsync(new HesapTransferIstegiDto(
                SeciliKaynak.KasaMi, SeciliKaynak.Id, SeciliHedef.KasaMi, SeciliHedef.Id,
                CikanTutar, DovizIslemi ? GirenTutar : 0m, Aciklama, MasrafGorunur ? Masraf : 0m),
                _firmaBaglami.SirketId, _kullaniciServisi.AktifKullanici?.Id ?? 0);

            MessageBox.Show($"{IslemBasligi} kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            BasariliKayitAction?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Transfer yapılamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            Kaydediliyor = false;
        }
    }
}
