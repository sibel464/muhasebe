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
            // İlk kayıt varsayılan şirketin kartından doldurulur; örnek (uydurma) firma bilgisi belgeye basılmasın.
            var sirket = await _baglam.Sirketler.AsNoTracking().FirstOrDefaultAsync(s => s.Id == MuhasebeBaglami.VarsayilanSirketId);
            ayarlar = new SistemAyarlari
            {
                Id = 1,
                FirmaUnvani = sirket?.Unvan ?? string.Empty,
                VergiDairesi = sirket?.VergiDairesi,
                VergiNo = sirket?.VergiNo,
                Telefon = sirket?.Telefon,
                Eposta = sirket?.Email,
                Adres = sirket?.Adres,
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
            mevcut.MersisNo = guncelAyarlar.MersisNo?.Trim();
            mevcut.Telefon = guncelAyarlar.Telefon?.Trim();
            mevcut.Eposta = guncelAyarlar.Eposta?.Trim();
            mevcut.WebSitesi = guncelAyarlar.WebSitesi?.Trim();
            mevcut.Adres = guncelAyarlar.Adres?.Trim();
            mevcut.Il = guncelAyarlar.Il?.Trim();
            mevcut.Ilce = guncelAyarlar.Ilce?.Trim();
            mevcut.PostaKodu = guncelAyarlar.PostaKodu?.Trim();
            mevcut.EBelgeKlasoru = string.IsNullOrWhiteSpace(guncelAyarlar.EBelgeKlasoru) ? null : guncelAyarlar.EBelgeKlasoru.Trim();
            mevcut.EFaturaVarsayilanSenaryo = guncelAyarlar.EFaturaVarsayilanSenaryo;
            mevcut.SmtpSunucu = string.IsNullOrWhiteSpace(guncelAyarlar.SmtpSunucu) ? null : guncelAyarlar.SmtpSunucu.Trim();
            mevcut.SmtpPort = guncelAyarlar.SmtpPort is > 0 and < 65536 ? guncelAyarlar.SmtpPort : 587;
            mevcut.SmtpGuvenlik = guncelAyarlar.SmtpGuvenlik is >= 0 and <= 3 ? guncelAyarlar.SmtpGuvenlik : 0;
            mevcut.SmtpKullanici = string.IsNullOrWhiteSpace(guncelAyarlar.SmtpKullanici) ? null : guncelAyarlar.SmtpKullanici.Trim();
            mevcut.SmtpSifre = guncelAyarlar.SmtpSifre;
            mevcut.EBelgeEntegrator = guncelAyarlar.EBelgeEntegrator is >= 0 and <= 1 ? guncelAyarlar.EBelgeEntegrator : 0;
            mevcut.EBelgeKullanici = string.IsNullOrWhiteSpace(guncelAyarlar.EBelgeKullanici) ? null : guncelAyarlar.EBelgeKullanici.Trim();
            mevcut.EBelgeSifre = guncelAyarlar.EBelgeSifre;
            mevcut.EBelgeTestOrtami = guncelAyarlar.EBelgeTestOrtami;
            mevcut.EBelgeOtomatikGonder = guncelAyarlar.EBelgeOtomatikGonder;
            mevcut.EpostaGonderenAdi = string.IsNullOrWhiteSpace(guncelAyarlar.EpostaGonderenAdi) ? null : guncelAyarlar.EpostaGonderenAdi.Trim();
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
