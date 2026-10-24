using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Uygulama.Dto.Finans360;
using Masaustu.Yardimcilar;
using System.Windows;

namespace Masaustu.GorunumModelleri;

public class MutabakatGorunumModeli : TemelGorunumModeli
{
    private readonly IFinansMerkeziServisi _finansServisi;
    private readonly IAktifFirmaBaglami _firmaBaglami;
    private readonly IKullaniciServisi _kullaniciServisi;

    private int? _seciliKasaId;
    private decimal _sistemBakiyesi;
    private decimal _fiiliSayim;
    private string _aciklama = string.Empty;
    private bool _duzeltmeHareketOlustur;
    private bool _kaydediliyor;

    public ObservableCollection<KasaOzetDto> Kasalar { get; } = new();

    public int? SeciliKasaId
    {
        get => _seciliKasaId;
        set
        {
            if (OzellikAyarla(ref _seciliKasaId, value))
            {
                var k = Kasalar.FirstOrDefault(x => x.Id == value);
                SistemBakiyesi = k?.MevcutBakiye ?? 0m;
            }
        }
    }

    public decimal SistemBakiyesi
    {
        get => _sistemBakiyesi;
        private set
        {
            if (OzellikAyarla(ref _sistemBakiyesi, value))
                BildirimGonder(nameof(Fark));
        }
    }

    public decimal FiiliSayim
    {
        get => _fiiliSayim;
        set
        {
            if (OzellikAyarla(ref _fiiliSayim, value))
                BildirimGonder(nameof(Fark));
        }
    }

    public decimal Fark => FiiliSayim - SistemBakiyesi;

    public string Aciklama
    {
        get => _aciklama;
        set => OzellikAyarla(ref _aciklama, value);
    }

    public bool DuzeltmeHareketOlustur
    {
        get => _duzeltmeHareketOlustur;
        set => OzellikAyarla(ref _duzeltmeHareketOlustur, value);
    }

    public bool Kaydediliyor
    {
        get => _kaydediliyor;
        private set => OzellikAyarla(ref _kaydediliyor, value);
    }

    public ICommand KaydetKomutu { get; }

    public Action? BasariliKayitAction { get; set; }

    public MutabakatGorunumModeli(
        IFinansMerkeziServisi finansServisi,
        IAktifFirmaBaglami firmaBaglami,
        IKullaniciServisi kullaniciServisi)
    {
        _finansServisi = finansServisi;
        _firmaBaglami = firmaBaglami;
        _kullaniciServisi = kullaniciServisi;

        KaydetKomutu = new KomutSarici(async _ => await KaydetAsync(), _ => _kullaniciServisi.YetkiliMi(x => x.Mutabakat));
    }

    public async Task InitAsync()
    {
        Kasalar.Clear();
        var kasalar = await _finansServisi.GetKasalarAsync(_firmaBaglami.SirketId);
        // Banknot sayımı TL kasalar içindir; dövizli kasada fark gelir/gider fişiyle girilir.
        foreach (var k in kasalar.Where(k => k.Aktif && !DovizKuruYardimcisi.DovizliMi(k.ParaBirimi))) Kasalar.Add(k);

        if (Kasalar.Count > 0) SeciliKasaId = Kasalar[0].Id;
    }

    public async Task KaydetAsync()
    {
        if (!SeciliKasaId.HasValue)
        {
            MessageBox.Show("Lütfen mutabakat yapılacak kasayı seçiniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Aciklama))
        {
            MessageBox.Show("Mutabakat açıklaması girmek zorunludur.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Kaydediliyor = true;
        try
        {
            await _finansServisi.KasaMutabakatKaydetAsync(new KasaMutabakatKayitDto(
                SeciliKasaId.Value,
                FiiliSayim,
                Aciklama,
                DuzeltmeHareketOlustur
            ), _firmaBaglami.SirketId, _kullaniciServisi.AktifKullanici?.Id ?? 1);

            MessageBox.Show("Kasa mutabakat kaydı başarıyla kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            BasariliKayitAction?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Mutabakat kaydı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Kaydediliyor = false;
        }
    }
}
