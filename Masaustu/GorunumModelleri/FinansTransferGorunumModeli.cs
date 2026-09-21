using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Finans360;
using Masaustu.Yardimcilar;
using System.Windows;

namespace Masaustu.GorunumModelleri;

public class FinansTransferGorunumModeli : TemelGorunumModeli
{
    private readonly IFinansMerkeziServisi _finansServisi;
    private readonly IAktifFirmaBaglami _firmaBaglami;
    private readonly IKullaniciServisi _kullaniciServisi;

    private string _transferTuru = "KasaVirman"; // KasaVirman, BankaVirman, KasaBanka, BankaKasa
    private int? _seciliKaynakKasaId;
    private int? _seciliHedefKasaId;
    private int? _seciliKaynakBankaHesapId;
    private int? _seciliHedefBankaHesapId;
    private decimal _tutar;
    private decimal _masraf;
    private string _aciklama = string.Empty;
    private bool _kaydediliyor;

    public ObservableCollection<KasaOzetDto> Kasalar { get; } = new();
    public ObservableCollection<BankaHesapOzetDto> BankaHesaplari { get; } = new();

    public string TransferTuru
    {
        get => _transferTuru;
        set => OzellikAyarla(ref _transferTuru, value);
    }

    public int? SeciliKaynakKasaId
    {
        get => _seciliKaynakKasaId;
        set => OzellikAyarla(ref _seciliKaynakKasaId, value);
    }

    public int? SeciliHedefKasaId
    {
        get => _seciliHedefKasaId;
        set => OzellikAyarla(ref _seciliHedefKasaId, value);
    }

    public int? SeciliKaynakBankaHesapId
    {
        get => _seciliKaynakBankaHesapId;
        set => OzellikAyarla(ref _seciliKaynakBankaHesapId, value);
    }

    public int? SeciliHedefBankaHesapId
    {
        get => _seciliHedefBankaHesapId;
        set => OzellikAyarla(ref _seciliHedefBankaHesapId, value);
    }

    public decimal Tutar
    {
        get => _tutar;
        set => OzellikAyarla(ref _tutar, value);
    }

    public decimal Masraf
    {
        get => _masraf;
        set => OzellikAyarla(ref _masraf, value);
    }

    public string Aciklama
    {
        get => _aciklama;
        set => OzellikAyarla(ref _aciklama, value);
    }

    public bool Kaydediliyor
    {
        get => _kaydediliyor;
        private set => OzellikAyarla(ref _kaydediliyor, value);
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

        KaydetKomutu = new KomutSarici(async _ => await KaydetAsync(), _ => _kullaniciServisi.YetkiliMi(x => x.VirmanYap));
    }

    public async Task InitAsync(string transferTuru)
    {
        TransferTuru = transferTuru;
        Kasalar.Clear();
        var kasalar = await _finansServisi.GetKasalarAsync(_firmaBaglami.SirketId);
        foreach (var k in kasalar) Kasalar.Add(k);

        BankaHesaplari.Clear();
        var bankalar = await _finansServisi.GetBankaHesaplariAsync(_firmaBaglami.SirketId);
        foreach (var b in bankalar) BankaHesaplari.Add(b);

        if (Kasalar.Count > 0)
        {
            SeciliKaynakKasaId = Kasalar[0].Id;
            if (Kasalar.Count > 1) SeciliHedefKasaId = Kasalar[1].Id;
        }

        if (BankaHesaplari.Count > 0)
        {
            SeciliKaynakBankaHesapId = BankaHesaplari[0].Id;
            if (BankaHesaplari.Count > 1) SeciliHedefBankaHesapId = BankaHesaplari[1].Id;
        }
    }

    public async Task KaydetAsync()
    {
        if (Tutar <= 0)
        {
            MessageBox.Show("Transfer tutarı 0'dan büyük olmalıdır.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Kaydediliyor = true;
        try
        {
            var sirketId = _firmaBaglami.SirketId;
            var kullaniciId = _kullaniciServisi.AktifKullanici?.Id ?? 1;

            if (TransferTuru == "KasaVirman")
            {
                if (!SeciliKaynakKasaId.HasValue || !SeciliHedefKasaId.HasValue)
                    throw new InvalidOperationException("Kaynak ve hedef kasayı seçiniz.");
                if (SeciliKaynakKasaId == SeciliHedefKasaId)
                    throw new InvalidOperationException("Aynı kasa için virman yapılamaz.");

                await _finansServisi.KasaBankaTransferiYapAsync(new KasaBankaTransferIstegiDto(
                    SeciliKaynakKasaId.Value,
                    SeciliHedefKasaId.Value, // Note: Virman service handles transfer cleanly
                    Tutar,
                    Aciklama
                ), sirketId, kullaniciId);
            }
            else if (TransferTuru == "BankaVirman")
            {
                if (!SeciliKaynakBankaHesapId.HasValue || !SeciliHedefBankaHesapId.HasValue)
                    throw new InvalidOperationException("Kaynak ve hedef banka hesabını seçiniz.");

                await _finansServisi.BankaVirmaniYapAsync(new BankaVirmanIstegiDto(
                    SeciliKaynakBankaHesapId.Value,
                    SeciliHedefBankaHesapId.Value,
                    Tutar,
                    Masraf,
                    Aciklama
                ), sirketId, kullaniciId);
            }
            else if (TransferTuru == "KasaBanka")
            {
                if (!SeciliKaynakKasaId.HasValue || !SeciliHedefBankaHesapId.HasValue)
                    throw new InvalidOperationException("Kaynak kasayı ve hedef banka hesabını seçiniz.");

                await _finansServisi.KasaBankaTransferiYapAsync(new KasaBankaTransferIstegiDto(
                    SeciliKaynakKasaId.Value,
                    SeciliHedefBankaHesapId.Value,
                    Tutar,
                    Aciklama
                ), sirketId, kullaniciId);
            }
            else if (TransferTuru == "BankaKasa")
            {
                if (!SeciliKaynakBankaHesapId.HasValue || !SeciliHedefKasaId.HasValue)
                    throw new InvalidOperationException("Kaynak banka hesabını ve hedef kasayı seçiniz.");

                await _finansServisi.BankaKasaTransferiYapAsync(new BankaKasaTransferIstegiDto(
                    SeciliKaynakBankaHesapId.Value,
                    SeciliHedefKasaId.Value,
                    Tutar,
                    Aciklama
                ), sirketId, kullaniciId);
            }

            MessageBox.Show("Transfer işlemi başarıyla tamamlandı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            BasariliKayitAction?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Transfer hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Kaydediliyor = false;
        }
    }
}
