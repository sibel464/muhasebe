using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Ayar;
using Uygulama.Arayuzler;
using Altyapi.Veri;

namespace Altyapi.Servisler;

public class SistemAyarlariServisi : ISistemAyarlariServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi _kullaniciServisi;
    private static SistemAyarlari? _onbellek;

    public SistemAyarlariServisi(MuhasebeBaglami baglam, IKullaniciServisi kullaniciServisi)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<SistemAyarlari> AyarlariGetirAsync()
    {
        var ayarlar = await _baglam.SistemAyarlari.FirstOrDefaultAsync(a => a.Id == 1);
        if (ayarlar == null)
        {
            ayarlar = new SistemAyarlari
            {
                Id = 1,
                FirmaUnvani = "Örnek Ticaret Ltd. Şti.",
                VergiDairesi = "Kadıköy V.D.",
                VergiNo = "1234567890",
                Telefon = "0216 555 00 00",
                Eposta = "info@ornekticaret.com",
                WebSitesi = "www.ornekticaret.com",
                Adres = "Atatürk Cad. No: 15/A Kadıköy / İstanbul",
                FisHeader = "Örnek Ticaret Ltd. Şti.",
                FisFooter = "Bizi tercih ettiğiniz için teşekkür ederiz!",
                VarsayilanKdv = 20.00m,
                ParaBirimiSembolu = "₺",
                ParaBirimiKodu = "TRY",
                VarsayilanVadeGunu = 30
            };
            _baglam.SistemAyarlari.Add(ayarlar);
            await _baglam.SaveChangesAsync();
        }

        _onbellek = ayarlar;
        return ayarlar;
    }

    public async Task<SistemAyarlari> AyarlariKaydetAsync(SistemAyarlari guncelAyarlar)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.AyarlariYonet, "Sistem Ayarlarını Güncelleme");
        if (string.IsNullOrWhiteSpace(guncelAyarlar.FirmaUnvani))
            throw new ArgumentException("Firma ünvanı boş olamaz.");

        var mevcut = await _baglam.SistemAyarlari.FirstOrDefaultAsync(a => a.Id == 1);
        if (mevcut == null)
        {
            guncelAyarlar.Id = 1;
            _baglam.SistemAyarlari.Add(guncelAyarlar);
            mevcut = guncelAyarlar;
        }
        else
        {
            mevcut.FirmaUnvani = guncelAyarlar.FirmaUnvani.Trim();
            mevcut.VergiDairesi = guncelAyarlar.VergiDairesi?.Trim();
            mevcut.VergiNo = guncelAyarlar.VergiNo?.Trim();
            mevcut.Telefon = guncelAyarlar.Telefon?.Trim();
            mevcut.Eposta = guncelAyarlar.Eposta?.Trim();
            mevcut.WebSitesi = guncelAyarlar.WebSitesi?.Trim();
            mevcut.Adres = guncelAyarlar.Adres?.Trim();
            mevcut.FisHeader = guncelAyarlar.FisHeader?.Trim();
            mevcut.FisFooter = guncelAyarlar.FisFooter?.Trim();
            mevcut.VarsayilanKdv = guncelAyarlar.VarsayilanKdv;
            mevcut.ParaBirimiSembolu = guncelAyarlar.ParaBirimiSembolu;
            mevcut.ParaBirimiKodu = guncelAyarlar.ParaBirimiKodu;
            mevcut.VarsayilanVadeGunu = guncelAyarlar.VarsayilanVadeGunu;
            mevcut.FaturaNotu = guncelAyarlar.FaturaNotu?.Trim();
            mevcut.IrsaliyeNotu = guncelAyarlar.IrsaliyeNotu?.Trim();
            mevcut.GuncellemeTarihi = DateTime.UtcNow;
        }

        await _baglam.SaveChangesAsync();
        _onbellek = mevcut;
        return mevcut;
    }
}
