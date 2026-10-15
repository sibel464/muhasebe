using System.Collections.ObjectModel;
using System.Windows.Input;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Stok;
using Uygulama.Arayuzler;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public class StokDagilimiGorunumModeli : TemelGorunumModeli
{
    private readonly IUrunServisi _urunServisi;
    private FaturaKalemiGorunumModeli _kalem;

    public Urun Urun => _kalem.Urun!;
    public decimal ToplamMiktar => _kalem.Miktar;
    public decimal DagitilanMiktar => Dagilimlar.Sum(d => d.Miktar);
    public decimal KalanMiktar => ToplamMiktar - DagitilanMiktar;
    
    public ObservableCollection<DagilimSatiri> Dagilimlar { get; } = new();

    public ICommand UygulaKomutu { get; }
    public ICommand IptalKomutu { get; }
    public ICommand TemizleKomutu { get; }
    public ICommand MagazayaEsiteKomutu { get; }

    public Action? Kapat { get; set; }

    // stokCikisi: satış / alış iadesi (stok düşer). Alış ve satış iadesinde stok girdiği için yeterlilik aranmaz.
    private readonly bool _stokCikisi;

    public StokDagilimiGorunumModeli(FaturaKalemiGorunumModeli kalem, IUrunServisi urunServisi, bool stokCikisi = true)
    {
        _kalem = kalem;
        _urunServisi = urunServisi;
        _stokCikisi = stokCikisi;

        UygulaKomutu = new KomutSarici(Uygula);
        IptalKomutu = new KomutSarici(_ => Kapat?.Invoke());
        TemizleKomutu = new KomutSarici(_ => Temizle());
        MagazayaEsiteKomutu = new KomutSarici(_ => MagazayaEsite());
        
        _ = YukleAsync();
    }

    private async Task YukleAsync()
    {
        if (Urun == null) return;
        List<EtkiAlani.Modeller.Stok.UrunLokasyonStogu> lokasyonStoklari = new();
        try
        {
            lokasyonStoklari = await _urunServisi.LokasyonStoklariniGetirAsync(Urun.Id) ?? new();
        }
        catch { }
        
        // Aktif mağaza ve depolar veritabanından okunur (sabit 1-2-3 numaralı depo varsayılmaz).
        await StokYerleri.YukleAsync(_urunServisi);

        foreach (var depo in StokYerleri.Liste)
        {
            var lokasyonStogu = lokasyonStoklari.FirstOrDefault(ls => ls.DepoId == depo.Id);
            var mevcutStok = lokasyonStogu == null ? 0 : lokasyonStogu.MevcutMiktar - lokasyonStogu.RezerveMiktar;
            var eskiDagilim = _kalem.StokDagilimlari.FirstOrDefault(d => d.DepoId == depo.Id);

            var satir = new DagilimSatiri
            {
                DepoId = depo.Id,
                DepoAdi = depo.Ad,
                MevcutStok = mevcutStok,
                Miktar = eskiDagilim?.Miktar ?? 0
            };
            
            satir.Degisti = HesaplaVeGuncelle;
            Dagilimlar.Add(satir);
        }

        // Eğer toplam dağıtılan miktar satış miktarına eşit değilse (eski uyumsuz veriler dahil), Mağazaya otomatik eşitle
        if (Dagilimlar.Sum(d => d.Miktar) != ToplamMiktar)
        {
            MagazayaEsite();
        }
        else
        {
            HesaplaVeGuncelle();
        }
    }

    private void HesaplaVeGuncelle()
    {
        BildirimGonder(nameof(DagitilanMiktar));
        BildirimGonder(nameof(KalanMiktar));
    }

    private void Temizle()
    {
        foreach (var d in Dagilimlar) d.Miktar = 0;
        HesaplaVeGuncelle();
    }

    private void MagazayaEsite()
    {
        foreach (var d in Dagilimlar) d.Miktar = 0;
        var magaza = Dagilimlar.FirstOrDefault(d => d.DepoId == StokYerleri.VarsayilanId) ?? Dagilimlar.FirstOrDefault();
        if (magaza != null) magaza.Miktar = ToplamMiktar;
        HesaplaVeGuncelle();
    }

    private void Uygula()
    {
        if (Dagilimlar.Any(d => d.Miktar < 0))
        {
            System.Windows.MessageBox.Show("Çıkış miktarı negatif olamaz.", "Geçersiz Miktar", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        if (KalanMiktar != 0)
        {
            System.Windows.MessageBox.Show($"Satış miktarı ({ToplamMiktar:N2}) ile dağıtılan stok miktarı ({DagitilanMiktar:N2}) eşit olmalıdır.", "Geçersiz Dağılım", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        // Satışta stok eksiye düşemez (kural sunucuda). Önceden "devam edilsin mi" sorulup sonra
        // sunucuda reddediliyordu; artık burada açıkça söylenir.
        var yetersiz = _stokCikisi ? Dagilimlar.FirstOrDefault(d => d.Miktar > d.MevcutStok) : null;
        if (yetersiz != null)
        {
            System.Windows.MessageBox.Show(
                $"{yetersiz.DepoAdi} için girilen miktar ({yetersiz.Miktar:N2}), kullanılabilir stoktan ({yetersiz.MevcutStok:N2}) fazla.\n\n" +
                "Başka bir stok yerinden düşün veya önce stok girişi/transfer yapın.",
                "Stok Yetersiz", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        _kalem.StokDagilimlari.Clear();
        foreach (var d in Dagilimlar.Where(d => d.Miktar > 0))
        {
            _kalem.StokDagilimlari.Add(new FaturaKalemiStokDagilimi
            {
                DepoId = d.DepoId,
                Miktar = d.Miktar
            });
        }
        
        _kalem.Degisti?.Invoke();
        _kalem.StokDagilimiOzetiGuncelle();

        Kapat?.Invoke();
    }
}

public class DagilimSatiri : TemelGorunumModeli
{
    private decimal _miktar;

    public int DepoId { get; set; }
    public string DepoAdi { get; set; } = string.Empty;
    public decimal MevcutStok { get; set; }

    public decimal Miktar
    {
        get => _miktar;
        set
        {
            if (OzellikAyarla(ref _miktar, value))
            {
                BildirimGonder(nameof(MiktarMetni));
                Degisti?.Invoke();
            }
        }
    }

    public string MiktarMetni
    {
        get => _miktar == 0 ? string.Empty : _miktar.ToString("0.####");
        set
        {
            Miktar = TurkceSayi.PozitifVeyaSifir(value);
        }
    }

    public Action? Degisti { get; set; }
}
