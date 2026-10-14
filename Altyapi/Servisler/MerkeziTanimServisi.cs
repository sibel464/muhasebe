using Altyapi.Veri;
using EtkiAlani.Modeller.Tanimlar;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

public class MerkeziTanimServisi : IMerkeziTanimServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi _kullaniciServisi;
    public MerkeziTanimServisi(MuhasebeBaglami baglam, IKullaniciServisi kullaniciServisi)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public Task<List<MerkeziTanim>> ListeleAsync(string grup, bool sadeceAktif = true, int? dahilEdilecekId = null) =>
        _baglam.MerkeziTanimlar.Where(x => x.Grup == grup && (!sadeceAktif || x.AktifMi || x.Id == dahilEdilecekId))
            .OrderBy(x => x.SiraNo).ThenBy(x => x.Ad).AsNoTracking().ToListAsync();

    public async Task<MerkeziTanim> KaydetAsync(MerkeziTanim tanim)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.AyarlariYonet, "Merkezi Tanım Kaydetme");
        tanim.Grup = tanim.Grup.Trim().ToUpperInvariant();
        tanim.Kod = tanim.Kod.Trim().ToUpperInvariant();
        tanim.Ad = tanim.Ad.Trim();
        tanim.KisaAd = tanim.KisaAd?.Trim();
        tanim.Aciklama = tanim.Aciklama?.Trim();
        if (string.IsNullOrWhiteSpace(tanim.Grup)) throw new ArgumentException("Tanım grubu boş olamaz.");
        if (string.IsNullOrWhiteSpace(tanim.Kod)) throw new ArgumentException("Kod boş olamaz.");
        if (string.IsNullOrWhiteSpace(tanim.Ad)) throw new ArgumentException("Ad boş olamaz.");
        if (await _baglam.MerkeziTanimlar.IgnoreQueryFilters().AnyAsync(x => x.Id != tanim.Id && x.Grup == tanim.Grup && (x.Kod.ToUpper() == tanim.Kod || x.Ad.ToUpper() == tanim.Ad.ToUpper())))
            throw new InvalidOperationException("Bu grupta aynı kod veya ada sahip bir tanım zaten var.");

        MerkeziTanim kayit;
        if (tanim.Id == 0)
        {
            tanim.SiraNo = tanim.SiraNo > 0 ? tanim.SiraNo : (await _baglam.MerkeziTanimlar.Where(x => x.Grup == tanim.Grup).MaxAsync(x => (int?)x.SiraNo) ?? 0) + 10;
            tanim.OlusturmaTarihi = DateTime.UtcNow;
            kayit = tanim;
            _baglam.MerkeziTanimlar.Add(kayit);
        }
        else
        {
            kayit = await _baglam.MerkeziTanimlar.FirstOrDefaultAsync(x => x.Id == tanim.Id) ?? throw new KeyNotFoundException("Tanım bulunamadı.");
            if (kayit.Grup != tanim.Grup) throw new InvalidOperationException("Tanım grubu değiştirilemez.");
            kayit.Kod = tanim.Kod; kayit.Ad = tanim.Ad; kayit.KisaAd = tanim.KisaAd;
            kayit.Aciklama = tanim.Aciklama; kayit.AktifMi = tanim.AktifMi; kayit.SiraNo = tanim.SiraNo;
            kayit.GuncellemeTarihi = DateTime.UtcNow;
        }
        await _baglam.SaveChangesAsync();
        return kayit;
    }

    public async Task AktiflikDegistirAsync(int id, bool aktifMi)
    {
        _kullaniciServisi.YetkiKontrolEt(y => y.AyarlariYonet, "Merkezi Tanım Durumu Değiştirme");
        var kayit = await _baglam.MerkeziTanimlar.FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Tanım bulunamadı.");
        kayit.AktifMi = aktifMi; kayit.GuncellemeTarihi = DateTime.UtcNow;
        await _baglam.SaveChangesAsync();
    }

    public Task<bool> KullanimdaMiAsync(int id) => _baglam.Urunler.AnyAsync(x => x.KategoriTanimiId == id || x.MarkaTanimiId == id || x.BirimTanimiId == id);
}
