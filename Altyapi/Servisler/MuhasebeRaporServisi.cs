using Altyapi.Veri;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;

namespace Altyapi.Servisler;

public class MuhasebeRaporServisi : IMuhasebeRaporServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public MuhasebeRaporServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<MizanRaporDto> MizanAlAsync(Guid sirketId, DateTime? baslangic, DateTime? bitis, MizanTuru tur = MizanTuru.Genel)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.MizanGoruntule, "Mizan Görüntüleme");

        // Varsayılan: yıl başından bugüne (gün sonu dahil).
        var aralik = RaporTarihAraligi.Olustur(baslangic ?? new DateTime(DateTime.Today.Year, 1, 1), bitis ?? DateTime.Today);
        var bas = aralik.Baslangic;
        var bitSonrasi = aralik.BitisSonrasi;

        var hesaplar = await _baglam.HesapPlanlari
            .AsNoTracking()
            .Where(h => h.SirketId == sirketId)
            .OrderBy(h => h.HesapKodu)
            .ToListAsync();

        // Calculate opening balances (Devir) from approved vouchers before start date
        var devirGrubu = await _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Where(s => s.Fis != null && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih < bas && (s.Fis.SirketId == sirketId))
            .GroupBy(s => s.HesapId)
            .Select(g => new { HesapId = g.Key, DevirBorc = g.Sum(x => x.Borc), DevirAlacak = g.Sum(x => x.Alacak) })
            .ToDictionaryAsync(x => x.HesapId);

        // Calculate period balances from approved vouchers between start date and end date
        var donemGrubu = await _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Where(s => s.Fis != null && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih >= bas && s.Fis.Tarih < bitSonrasi && (s.Fis.SirketId == sirketId))
            .GroupBy(s => s.HesapId)
            .Select(g => new { HesapId = g.Key, DonemBorc = g.Sum(x => x.Borc), DonemAlacak = g.Sum(x => x.Alacak) })
            .ToDictionaryAsync(x => x.HesapId);

        var satirlar = hesaplar.Select(h =>
        {
            devirGrubu.TryGetValue(h.Id, out var dev);
            donemGrubu.TryGetValue(h.Id, out var don);

            return new MizanSatirDto
            {
                HesapId = h.Id,
                HesapKodu = h.HesapKodu,
                HesapAdi = h.HesapAdi,
                Seviye = h.Seviye,
                DevirBorc = dev?.DevirBorc ?? 0m,
                DevirAlacak = dev?.DevirAlacak ?? 0m,
                DonemBorc = don?.DonemBorc ?? 0m,
                DonemAlacak = don?.DonemAlacak ?? 0m
            };
        })
        .Where(s => s.DevirBorc > 0 || s.DevirAlacak > 0 || s.DonemBorc > 0 || s.DonemAlacak > 0) // Hide zero lines
        .ToList();

        return new MizanRaporDto
        {
            Tur = tur,
            BaslangicTarihi = bas,
            BitisTarihi = aralik.BitisGunu,
            Satirlar = satirlar
        };
    }

    public async Task<MuavinRaporDto> MuavinAlAsync(int hesapId, Guid sirketId = default, DateTime? baslangic = null, DateTime? bitis = null)
    {
        var targetSirketId = _baglam.SirketIdCoz(sirketId);
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.MizanGoruntule, "Muavin Defter Görüntüleme");

        var aralik = RaporTarihAraligi.Olustur(baslangic, bitis);
        var bStart = aralik.Baslangic;
        var bEnd = aralik.BitisSonrasi;

        var hesap = await _baglam.HesapPlanlari
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == hesapId && (h.SirketId == targetSirketId));

        if (hesap == null)
            throw new KeyNotFoundException("Muavin dökümü için hesap bulunamadı.");

        // Devir
        var devir = await _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Where(s => s.HesapId == hesapId && s.Fis != null && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih < bStart)
            .GroupBy(s => s.HesapId)
            .Select(g => new { Borc = g.Sum(x => x.Borc), Alacak = g.Sum(x => x.Alacak) })
            .FirstOrDefaultAsync();

        var satirlar = await _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Include(s => s.Fis)
            .Where(s => s.HesapId == hesapId && s.Fis != null && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih >= bStart && s.Fis.Tarih < bEnd)
            .OrderBy(s => s.Fis!.Tarih)
            .ThenBy(s => s.Id)
            .ToListAsync();

        var running = (devir?.Borc ?? 0m) - (devir?.Alacak ?? 0m);

        var muavinSatirlar = satirlar.Select(s =>
        {
            running += (s.Borc - s.Alacak);
            return new MuavinSatirDto
            {
                Tarih = s.Fis?.Tarih ?? DateTime.MinValue,
                FisId = s.FisId,
                FisNo = s.Fis?.FisNo ?? "",
                BelgeTipi = s.Fis?.BelgeTipi ?? "",
                BelgeId = s.Fis?.BelgeId ?? 0,
                BelgeNo = s.BelgeNo,
                Aciklama = s.Aciklama,
                Borc = s.Borc,
                Alacak = s.Alacak,
                YuruyenBakiye = Math.Abs(running),
                BakiyeYonu = running >= 0 ? "Borç" : "Alacak"
            };
        }).ToList();

        return new MuavinRaporDto
        {
            HesapId = hesapId,
            HesapKodu = hesap.HesapKodu,
            HesapAdi = hesap.HesapAdi,
            BaslangicTarihi = bStart,
            BitisTarihi = aralik.BitisGunu,
            DevirBorc = devir?.Borc ?? 0m,
            DevirAlacak = devir?.Alacak ?? 0m,
            Satirlar = muavinSatirlar
        };
    }

    public async Task<List<BuyukDefterSatirDto>> BuyukDefterAlAsync(Guid sirketId = default, DateTime? baslangic = null, DateTime? bitis = null)
    {
        var targetSirketId = _baglam.SirketIdCoz(sirketId);
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.MizanGoruntule, "Büyük Defter Görüntüleme");

        var aralik = RaporTarihAraligi.Olustur(baslangic, bitis);
        var bStart = aralik.Baslangic;
        var bEnd = aralik.BitisSonrasi;

        var hesaplar = await _baglam.HesapPlanlari
            .AsNoTracking()
            .Where(h => h.SirketId == targetSirketId)
            .OrderBy(h => h.HesapKodu)
            .ToListAsync();

        var devir = await _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Where(s => s.Fis != null && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih < bStart && (s.Fis.SirketId == targetSirketId))
            .GroupBy(s => s.HesapId)
            .Select(g => new { HesapId = g.Key, Borc = g.Sum(x => x.Borc), Alacak = g.Sum(x => x.Alacak) })
            .ToDictionaryAsync(x => x.HesapId);

        var donem = await _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Where(s => s.Fis != null && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih >= bStart && s.Fis.Tarih < bEnd && (s.Fis.SirketId == targetSirketId))
            .GroupBy(s => s.HesapId)
            .Select(g => new { HesapId = g.Key, Borc = g.Sum(x => x.Borc), Alacak = g.Sum(x => x.Alacak), Count = g.Count() })
            .ToDictionaryAsync(x => x.HesapId);

        return hesaplar.Select(h =>
        {
            devir.TryGetValue(h.Id, out var d);
            donem.TryGetValue(h.Id, out var p);

            return new BuyukDefterSatirDto
            {
                HesapId = h.Id,
                HesapKodu = h.HesapKodu,
                HesapAdi = h.HesapAdi,
                DevirBorc = d?.Borc ?? 0m,
                DevirAlacak = d?.Alacak ?? 0m,
                DonemBorc = p?.Borc ?? 0m,
                DonemAlacak = p?.Alacak ?? 0m,
                HareketSayisi = p?.Count ?? 0
            };
        })
        .Where(b => b.DevirBorc > 0 || b.DevirAlacak > 0 || b.DonemBorc > 0 || b.DonemAlacak > 0)
        .ToList();
    }

    public async Task<MuhasebeSaglikDto> SaglikPaneliGetirAsync(Guid sirketId)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        var onayliFisler = await _baglam.YevmiyeFisleri
            .AsNoTracking()
            .Include(f => f.Satirlar)
            .Where(f => f.Durum == YevmiyeDurumu.Onaylandi && (f.SirketId == sirketId))
            .ToListAsync();

        var dengesizCount = onayliFisler.Count(f => Math.Abs(f.Satirlar.Sum(s => s.Borc) - f.Satirlar.Sum(s => s.Alacak)) >= 0.001m);

        var eksikBaglantiCount = await _baglam.MuhasebeBaglantiKodlari
            .AsNoTracking()
            .CountAsync(b => string.IsNullOrWhiteSpace(b.VarSayilanHesapKodu));

        var unposted = await GetirMuhasebelesmemisBelgelerAsync(sirketId);

        var kapaliDonemVar = await _baglam.MaliDonemler
            .AsNoTracking()
            .AnyAsync(m => m.KilitliMi && (m.SirketId == sirketId));

        var sonFis = await _baglam.YevmiyeFisleri
            .AsNoTracking()
            .Where(f => f.SirketId == sirketId)
            .OrderByDescending(f => f.Tarih)
            .FirstOrDefaultAsync();

        var toplamBorc = onayliFisler.Sum(f => f.Satirlar.Sum(s => s.Borc));
        var toplamAlacak = onayliFisler.Sum(f => f.Satirlar.Sum(s => s.Alacak));

        return new MuhasebeSaglikDto
        {
            DengesizFisSayisi = dengesizCount,
            EksikHesapEslameSayisi = eksikBaglantiCount,
            MuhasebelesmemisBelgeSayisi = unposted.Count,
            KapaliDonemVarMi = kapaliDonemVar,
            SonYevmiyeNo = sonFis?.FisNo ?? "-",
            SonFisTarihi = sonFis?.Tarih,
            ToplamYevmiyeBorc = toplamBorc,
            ToplamYevmiyeAlacak = toplamAlacak
        };
    }

    public async Task<List<MuhasebelesmemisBelgeDto>> GetirMuhasebelesmemisBelgelerAsync(Guid sirketId)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        var result = new List<MuhasebelesmemisBelgeDto>();

        // Approved Invoices without yevmiye fis
        var onayliFaturaIdler = await _baglam.Faturalar
            .AsNoTracking()
            .Where(f => f.Durum == BelgeDurumu.Onaylandi && (f.SirketId == sirketId))
            .Select(f => f.Id)
            .ToListAsync();

        var yevmiyeFaturaIdler = await _baglam.YevmiyeFisleri
            .AsNoTracking()
            .Where(y => y.BelgeTipi == "Fatura" && (y.SirketId == sirketId))
            .Select(y => y.BelgeId)
            .ToListAsync();

        var eksikFaturaIdler = onayliFaturaIdler.Except(yevmiyeFaturaIdler).ToList();

        if (eksikFaturaIdler.Count > 0)
        {
            var eksikFaturalar = await _baglam.Faturalar
                .AsNoTracking()
                .Include(f => f.CariKart)
                .Where(f => eksikFaturaIdler.Contains(f.Id))
                .ToListAsync();

            result.AddRange(eksikFaturalar.Select(f => new MuhasebelesmemisBelgeDto
            {
                BelgeTipi = "Fatura",
                BelgeId = f.Id,
                BelgeNo = f.FaturaNo,
                Tarih = f.Tarih,
                CariUnvan = f.CariKart?.Unvan ?? "",
                Tutar = f.GenelToplam,
                Aciklama = $"{f.Tur} Faturası Muhasebeleştirilmemiş"
            }));
        }

        return result;
    }
}
