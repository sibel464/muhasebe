using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

public class CariTipiTanimiServisi : ICariTipiTanimiServisi
{
    private readonly MuhasebeBaglami _baglam;

    public CariTipiTanimiServisi(MuhasebeBaglami baglam) => _baglam = baglam;

    public Task<List<CariTipiTanimi>> ListeleAsync(bool sadeceAktif = true, int? dahilEdilecekId = null) =>
        _baglam.CariTipiTanimlari
            .Where(x => !sadeceAktif || x.AktifMi || (dahilEdilecekId.HasValue && x.Id == dahilEdilecekId))
            .OrderBy(x => x.SiraNo).ThenBy(x => x.Ad)
            .AsNoTracking().ToListAsync();

    public async Task<CariTipiTanimi> KaydetAsync(CariTipiTanimi tanim)
    {
        tanim.Kod = tanim.Kod.Trim().ToUpperInvariant();
        tanim.Ad = tanim.Ad.Trim();
        tanim.Aciklama = tanim.Aciklama?.Trim();
        if (string.IsNullOrWhiteSpace(tanim.Kod)) throw new ArgumentException("Kod boş olamaz.");
        if (string.IsNullOrWhiteSpace(tanim.Ad)) throw new ArgumentException("Cari tipi adı boş olamaz.");

        var ayni = await _baglam.CariTipiTanimlari.IgnoreQueryFilters()
            .AnyAsync(x => x.Id != tanim.Id && (x.Kod.ToUpper() == tanim.Kod || x.Ad.ToUpper() == tanim.Ad.ToUpper()));
        if (ayni) throw new InvalidOperationException("Aynı kod veya ada sahip bir cari tipi zaten mevcut.");

        CariTipiTanimi kayit;
        if (tanim.Id == 0)
        {
            tanim.OlusturmaTarihi = DateTime.UtcNow;
            tanim.Silindi = false;
            if (tanim.SiraNo <= 0)
                tanim.SiraNo = (await _baglam.CariTipiTanimlari.MaxAsync(x => (int?)x.SiraNo) ?? 0) + 10;
            kayit = tanim;
            _baglam.CariTipiTanimlari.Add(kayit);
        }
        else
        {
            kayit = await _baglam.CariTipiTanimlari.FirstOrDefaultAsync(x => x.Id == tanim.Id)
                ?? throw new KeyNotFoundException("Cari tipi bulunamadı.");
            kayit.Kod = tanim.Kod;
            kayit.Ad = tanim.Ad;
            kayit.Aciklama = tanim.Aciklama;
            kayit.AktifMi = tanim.AktifMi;
            kayit.SiraNo = tanim.SiraNo;
            kayit.GuncellemeTarihi = DateTime.UtcNow;
        }
        await _baglam.SaveChangesAsync();
        return kayit;
    }

    public async Task AktiflikDegistirAsync(int id, bool aktifMi)
    {
        var kayit = await _baglam.CariTipiTanimlari.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Cari tipi bulunamadı.");
        kayit.AktifMi = aktifMi;
        kayit.GuncellemeTarihi = DateTime.UtcNow;
        await _baglam.SaveChangesAsync();
    }

    public Task<bool> KullanimdaMiAsync(int id) => _baglam.CariKartlar.AnyAsync(x => x.CariTipiId == id);
}
