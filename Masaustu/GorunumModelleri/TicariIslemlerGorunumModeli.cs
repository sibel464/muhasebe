using System.Collections.ObjectModel;
using System.Windows.Input;
using Uygulama.Arayuzler;
using Masaustu.Servisler;
using Masaustu.Yardimcilar;

namespace Masaustu.GorunumModelleri;

public sealed class TicariIslemlerGorunumModeli : TemelGorunumModeli
{
    private readonly IGlobalAramaServisi _aramaServisi;
    private readonly NavigasyonServisi _navigasyon;
    private CancellationTokenSource? _aramaIptal;
    private string _aramaMetni = string.Empty;
    private bool _aramaYapiliyor;
    private string _durumMesaji = "Aramak için en az 2 karakter girin.";

    public ObservableCollection<GlobalAramaSonucu> Sonuclar { get; } = new();
    public ICommand SonucaGitKomutu { get; }
    public ICommand CariKomutu { get; }
    public ICommand UrunKomutu { get; }
    public ICommand TahsilatKomutu { get; }
    public ICommand OdemeKomutu { get; }
    public ICommand KasaKomutu { get; }
    public ICommand BankaKomutu { get; }
    public ICommand FaturaKomutu { get; }
    public ICommand FaturaEkleKomutu { get; }
    public ICommand SiparisKomutu { get; }
    public ICommand IrsaliyeKomutu { get; }
    public ICommand SatisKomutu { get; }

    public string AramaMetni
    {
        get => _aramaMetni;
        set
        {
            if (OzellikAyarla(ref _aramaMetni, value))
                _ = AramayiBaslatAsync();
        }
    }

    public bool AramaYapiliyor
    {
        get => _aramaYapiliyor;
        private set => OzellikAyarla(ref _aramaYapiliyor, value);
    }

    public string DurumMesaji
    {
        get => _durumMesaji;
        private set => OzellikAyarla(ref _durumMesaji, value);
    }

    public TicariIslemlerGorunumModeli(IGlobalAramaServisi aramaServisi, NavigasyonServisi navigasyon)
    {
        _aramaServisi = aramaServisi;
        _navigasyon = navigasyon;
        SonucaGitKomutu = new KomutSarici(param =>
        {
            if (param is GlobalAramaSonucu sonuc)
                SonucaGit(sonuc);
        });
            CariKomutu = new KomutSarici(() => _navigasyon.Git("Musteri"));
            UrunKomutu = new KomutSarici(() => _navigasyon.Git("Urun"));
            TahsilatKomutu = new KomutSarici(() => _navigasyon.Git("Tahsilat"));
            OdemeKomutu = new KomutSarici(() => _navigasyon.Git("Odeme"));
            KasaKomutu = new KomutSarici(() => _navigasyon.Git("Kasa"));
            BankaKomutu = new KomutSarici(() => _navigasyon.Git("Banka"));
            FaturaKomutu = new KomutSarici(() => _navigasyon.Git("Fatura"));
            FaturaEkleKomutu = new KomutSarici(() => _navigasyon.Git("FaturaEkle"));
            SiparisKomutu = new KomutSarici(() => _navigasyon.Git("Siparis"));
            IrsaliyeKomutu = new KomutSarici(() => _navigasyon.Git("Irsaliye"));
            SatisKomutu = new KomutSarici(() => _navigasyon.Git("Satis"));
    }

    private async Task AramayiBaslatAsync()
    {
        _aramaIptal?.Cancel();
        _aramaIptal?.Dispose();
        _aramaIptal = new CancellationTokenSource();
        var token = _aramaIptal.Token;
        var metin = AramaMetni.Trim();

        Sonuclar.Clear();
        if (metin.Length < 2)
        {
            AramaYapiliyor = false;
            DurumMesaji = "Aramak için en az 2 karakter girin.";
            return;
        }

        AramaYapiliyor = true;
        DurumMesaji = "Aranıyor...";
        try
        {
            await Task.Delay(300, token);
            var sonuclar = await _aramaServisi.AraAsync(metin, token);
            foreach (var sonuc in sonuclar)
                Sonuclar.Add(sonuc);
            DurumMesaji = Sonuclar.Count == 0 ? "Sonuç bulunamadı." : $"{Sonuclar.Count} sonuç bulundu.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            DurumMesaji = "Arama yapılamadı. Lütfen tekrar deneyin.";
        }
        finally
        {
            if (!token.IsCancellationRequested)
                AramaYapiliyor = false;
        }
    }

    private void SonucaGit(GlobalAramaSonucu sonuc)
    {
        var sayfa = sonuc.Kategori switch
        {
            "CARİLER" => "Musteri",
            "ÜRÜNLER" => "Urun",
            "SİPARİŞLER" => "Siparis",
            "İRSALİYELER" => "Irsaliye",
            "FATURALAR" => "Fatura",
            _ => string.Empty
        };
        if (sayfa.Length > 0)
            _navigasyon.Git(sayfa);
    }
}
