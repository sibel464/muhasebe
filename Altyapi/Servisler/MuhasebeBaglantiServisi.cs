using Altyapi.Veri;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;

namespace Altyapi.Servisler;

public class MuhasebeBaglantiServisi : IMuhasebeBaglantiServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public MuhasebeBaglantiServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<List<MuhasebeBaglantiKoduDto>> GetirTumBaglantilarAsync(Guid sirketId)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        var baglantilar = await _baglam.MuhasebeBaglantiKodlari
            .AsNoTracking()
            .Where(b => b.SirketId == sirketId)
            .OrderBy(b => b.Id)
            .ToListAsync();

        var hesapKodlari = baglantilar.Select(b => b.VarSayilanHesapKodu).Distinct().ToList();
        var hesaplar = await _baglam.HesapPlanlari
            .AsNoTracking()
            .Where(h => hesapKodlari.Contains(h.HesapKodu) && (h.SirketId == sirketId))
            .ToDictionaryAsync(h => h.HesapKodu, h => h.HesapAdi);

        return baglantilar.Select(b => new MuhasebeBaglantiKoduDto
        {
            Id = b.Id,
            SirketId = b.SirketId,
            KategoriKodu = b.KategoriKodu,
            KategoriAdi = b.KategoriAdi,
            VarSayilanHesapKodu = b.VarSayilanHesapKodu,
            Aciklama = b.Aciklama,
            AktifMi = b.AktifMi,
            MevcutHesapAdi = hesaplar.TryGetValue(b.VarSayilanHesapKodu, out var ad) ? ad : null
        }).ToList();
    }

    public async Task GuncelleBaglantiAsync(MuhasebeBaglantiKoduDto dto, Guid sirketId)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.AyarlariYonet, "Muhasebe Bağlantı Kodları Güncelleme");

        var entity = await _baglam.MuhasebeBaglantiKodlari
            .FirstOrDefaultAsync(b => b.Id == dto.Id && (b.SirketId == sirketId));

        if (entity == null)
            throw new KeyNotFoundException("Güncellenecek bağlantı kodu bulunamadı.");

        // Validate account code exists in HesapPlanlari
        var hesapExists = await _baglam.HesapPlanlari
            .AnyAsync(h => h.HesapKodu == dto.VarSayilanHesapKodu && (h.SirketId == sirketId));

        if (!hesapExists)
            throw new InvalidOperationException($"'{dto.VarSayilanHesapKodu}' kodlu hesap planı kaydı sistemde bulunamadı. Lütfen önce bu hesabı hesap planına ekleyin.");

        entity.VarSayilanHesapKodu = dto.VarSayilanHesapKodu.Trim();
        entity.Aciklama = dto.Aciklama ?? "";
        entity.AktifMi = dto.AktifMi;

        await _baglam.SaveChangesAsync();
    }

    public async Task<string> GetirHesapKoduForKategoriAsync(string kategoriKodu, Guid sirketId)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        var item = await _baglam.MuhasebeBaglantiKodlari
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.KategoriKodu == kategoriKodu && (b.SirketId == sirketId) && b.AktifMi);

        if (item != null && !string.IsNullOrWhiteSpace(item.VarSayilanHesapKodu))
            return item.VarSayilanHesapKodu;

        // Fallback defaults
        return kategoriKodu switch
        {
            "KASA" => "100",
            "BANKA" => "102",
            "POS" => "108",
            "ALICILAR" => "120",
            "SATICILAR" => "320",
            "YURTICI_SATIS" => "600",
            "SATIS_KDV" => "391",
            "INDIRILECEK_KDV" => "191",
            "STOK" => "153",
            "SATIS_IADE" => "610",
            "STMM" => "621",
            "KOMISYON" => "653",
            _ => "100"
        };
    }

    public async Task<List<MuhasebeBaglantiKoduDto>> GetirBaglantiKodlariAsync(Guid sirketId = default)
    {
        return await GetirTumBaglantilarAsync(_baglam.SirketIdCoz(sirketId));
    }

    public async Task<bool> GuncelleBaglantiKoduAsync(MuhasebeBaglantiKoduDto dto, Guid sirketId = default)
    {
        await GuncelleBaglantiAsync(dto, _baglam.SirketIdCoz(sirketId));
        return true;
    }
}
