using Altyapi.Veri;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;

namespace Altyapi.Servisler;

public class HesapPlaniServisi : IHesapPlaniServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public HesapPlaniServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    public async Task<List<HesapPlaniDto>> GetirTumHesaplarAsync(Guid sirketId)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        var hesaplar = await _baglam.HesapPlanlari
            .AsNoTracking()
            .Where(h => h.SirketId == sirketId)
            .OrderBy(h => h.HesapKodu)
            .ToListAsync();

        var satirlar = await _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Where(s => s.Fis != null && s.Fis.Durum == YevmiyeDurumu.Onaylandi && (s.Fis.SirketId == sirketId))
            .GroupBy(s => s.HesapId)
            .Select(g => new
            {
                HesapId = g.Key,
                Borc = g.Sum(x => x.Borc),
                Alacak = g.Sum(x => x.Alacak)
            })
            .ToDictionaryAsync(x => x.HesapId);

        return hesaplar.Select(h =>
        {
            satirlar.TryGetValue(h.Id, out var b);
            return MapToDto(h, b?.Borc ?? 0m, b?.Alacak ?? 0m);
        }).ToList();
    }

    public async Task<List<HesapPlaniDto>> GetirHiyerarşikHesaplarAsync(Guid sirketId)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        var tumu = await GetirTumHesaplarAsync(sirketId);
        var lookup = tumu.ToDictionary(h => h.Id);
        var kokHesaplar = new List<HesapPlaniDto>();

        foreach (var h in tumu)
        {
            if (h.UstHesapId.HasValue && lookup.TryGetValue(h.UstHesapId.Value, out var ust))
            {
                ust.AltHesaplar.Add(h);
            }
            else
            {
                kokHesaplar.Add(h);
            }
        }

        return kokHesaplar;
    }

    public async Task<HesapPlaniDto?> GetirHesapByIdAsync(int id, Guid sirketId)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        var hesap = await _baglam.HesapPlanlari
            .AsNoTracking()
            .Include(h => h.UstHesap)
            .FirstOrDefaultAsync(h => h.Id == id && (h.SirketId == sirketId));

        if (hesap == null) return null;

        var borcAlacak = await _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Where(s => s.HesapId == id && s.Fis != null && s.Fis.Durum == YevmiyeDurumu.Onaylandi)
            .GroupBy(s => s.HesapId)
            .Select(g => new { Borc = g.Sum(x => x.Borc), Alacak = g.Sum(x => x.Alacak) })
            .FirstOrDefaultAsync();

        return MapToDto(hesap, borcAlacak?.Borc ?? 0m, borcAlacak?.Alacak ?? 0m);
    }

    public async Task<HesapPlaniDto?> GetirHesapByKodAsync(string hesapKodu, Guid sirketId)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        var hesap = await _baglam.HesapPlanlari
            .AsNoTracking()
            .Include(h => h.UstHesap)
            .FirstOrDefaultAsync(h => h.HesapKodu == hesapKodu && (h.SirketId == sirketId));

        if (hesap == null) return null;

        var borcAlacak = await _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Where(s => s.HesapId == hesap.Id && s.Fis != null && s.Fis.Durum == YevmiyeDurumu.Onaylandi)
            .GroupBy(s => s.HesapId)
            .Select(g => new { Borc = g.Sum(x => x.Borc), Alacak = g.Sum(x => x.Alacak) })
            .FirstOrDefaultAsync();

        return MapToDto(hesap, borcAlacak?.Borc ?? 0m, borcAlacak?.Alacak ?? 0m);
    }

    public async Task<Hesap360Dto> GetirHesap360Async(int hesapId, Guid sirketId, DateTime? baslangic = null, DateTime? bitis = null)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        var hesapDto = await GetirHesapByIdAsync(hesapId, sirketId);
        if (hesapDto == null)
            throw new KeyNotFoundException("Hesap bulunamadı.");

        // Gün bazında, varsayılan yıl başından bugüne (bitiş günü dahil).
        var aralik = RaporTarihAraligi.Olustur(baslangic ?? new DateTime(DateTime.Today.Year, 1, 1), bitis ?? DateTime.Today);
        var basTarih = aralik.Baslangic;
        var bitTarih = aralik.BitisSonrasi;

        // Devir
        var devir = await _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Where(s => s.HesapId == hesapId && s.Fis != null && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih < basTarih)
            .GroupBy(s => s.HesapId)
            .Select(g => new { Borc = g.Sum(x => x.Borc), Alacak = g.Sum(x => x.Alacak) })
            .FirstOrDefaultAsync();

        // Dönem
        var donemSatirlar = await _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Include(s => s.Fis)
            .Where(s => s.HesapId == hesapId && s.Fis != null && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih >= basTarih && s.Fis.Tarih < bitTarih)
            .OrderBy(s => s.Fis!.Tarih)
            .ThenBy(s => s.Id)
            .ToListAsync();

        var donemBorc = donemSatirlar.Sum(s => s.Borc);
        var donemAlacak = donemSatirlar.Sum(s => s.Alacak);

        var hareketlerDto = donemSatirlar.Select(s => new YevmiyeSatiriDto
        {
            Id = s.Id,
            FisId = s.FisId,
            FisNo = s.Fis?.FisNo ?? "",
            Tarih = s.Fis?.Tarih ?? DateTime.MinValue,
            HesapId = s.HesapId,
            HesapKodu = hesapDto.HesapKodu,
            HesapAdi = hesapDto.HesapAdi,
            Borc = s.Borc,
            Alacak = s.Alacak,
            Aciklama = s.Aciklama,
            BelgeNo = s.BelgeNo,
            ParaBirimi = s.ParaBirimi,
            Kur = s.Kur,
            DovizBorc = s.DovizBorc,
            DovizAlacak = s.DovizAlacak,
            CariKartId = s.CariKartId
        }).ToList();

        // Unique Yevmiye Fişleri
        var fisIdleri = donemSatirlar.Select(s => s.FisId).Distinct().ToList();
        var fisler = await _baglam.YevmiyeFisleri
            .AsNoTracking()
            .Include(f => f.Satirlar)
            .Where(f => fisIdleri.Contains(f.Id))
            .OrderByDescending(f => f.Tarih)
            .ToListAsync();

        var fislerDto = fisler.Select(f => new YevmiyeFisDto
        {
            Id = f.Id,
            SirketId = f.SirketId,
            FisNo = f.FisNo,
            FisTuru = f.FisTuru,
            Tarih = f.Tarih,
            BelgeTipi = f.BelgeTipi,
            BelgeId = f.BelgeId,
            Referans = f.Referans,
            Aciklama = f.Aciklama,
            Durum = f.Durum,
            ToplamBorc = f.Satirlar.Sum(s => s.Borc),
            ToplamAlacak = f.Satirlar.Sum(s => s.Alacak)
        }).ToList();

        // Monthly breakdown
        var tumSatirlar = await _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Where(s => s.HesapId == hesapId && s.Fis != null && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih.Year == aralik.BitisGunu.Year)
            .Select(s => new { Tarih = s.Fis!.Tarih, s.Borc, s.Alacak })
            .ToListAsync();

        var aylikOzet = Enumerable.Range(1, 12).Select(ay =>
        {
            var aySatirlari = tumSatirlar.Where(s => s.Tarih.Month == ay).ToList();
            var b = aySatirlari.Sum(x => x.Borc);
            var a = aySatirlari.Sum(x => x.Alacak);
            return new HesapAylikOzetDto
            {
                Yil = aralik.BitisGunu.Year,
                Ay = ay,
                AyAdi = new DateTime(aralik.BitisGunu.Year, ay, 1).ToString("MMMM"),
                Borc = b,
                Alacak = a
            };
        }).ToList();

        // Muavin calculations
        var running = (devir?.Borc ?? 0m) - (devir?.Alacak ?? 0m);
        var muavinSatirlar = donemSatirlar.Select(s =>
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

        var muavin = new MuavinRaporDto
        {
            HesapId = hesapId,
            HesapKodu = hesapDto.HesapKodu,
            HesapAdi = hesapDto.HesapAdi,
            BaslangicTarihi = basTarih,
            BitisTarihi = aralik.BitisGunu,
            DevirBorc = devir?.Borc ?? 0m,
            DevirAlacak = devir?.Alacak ?? 0m,
            Satirlar = muavinSatirlar
        };

        // Linked documents
        var bagliBelgeler = fisler.Select(f => new BagliBelgeDto
        {
            BelgeTipi = f.BelgeTipi,
            BelgeId = f.BelgeId,
            BelgeNo = f.FisNo,
            Tarih = f.Tarih,
            Aciklama = f.Aciklama,
            Tutar = f.Satirlar.Sum(s => s.Borc)
        }).ToList();

        // Audit Logs
        var auditLogs = await _baglam.AuditLoglar
            .AsNoTracking()
            .Where(a => a.TabloAdi == "HesapPlanlari" && a.KayitId == hesapId.ToString())
            .OrderByDescending(a => a.Tarih)
            .Take(20)
            .Select(a => new HesapAuditDto
            {
                Tarih = a.Tarih,
                KullaniciAdi = a.KullaniciAdi ?? "Sistem",
                IslemTuru = a.IslemTuru.ToString(),
                Aciklama = $"{a.IslemTuru} işlemi yapıldı."
            })
            .ToListAsync();

        return new Hesap360Dto
        {
            Hesap = hesapDto,
            DevirBorc = devir?.Borc ?? 0m,
            DevirAlacak = devir?.Alacak ?? 0m,
            DonemBorc = donemBorc,
            DonemAlacak = donemAlacak,
            SonHareketTarihi = donemSatirlar.LastOrDefault()?.Fis?.Tarih,
            Hareketler = hareketlerDto,
            YevmiyeFisleri = fislerDto,
            AylikOzet = aylikOzet,
            Muavin = muavin,
            BagliBelgeler = bagliBelgeler,
            AuditLogs = auditLogs
        };
    }

    public async Task<HesapPlaniDto> EkleHesapAsync(HesapPlaniDto dto, Guid sirketId)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        YetkiKontrolEt(y => y.HesapPlaniYonet, "Hesap Planı Ekleme");

        if (string.IsNullOrWhiteSpace(dto.HesapKodu))
            throw new InvalidOperationException("Hesap kodu boş olamaz.");

        if (string.IsNullOrWhiteSpace(dto.HesapAdi))
            throw new InvalidOperationException("Hesap adı boş olamaz.");

        dto.HesapKodu = dto.HesapKodu.Trim();
        dto.HesapAdi = dto.HesapAdi.Trim();

        var kodExists = await _baglam.HesapPlanlari
            .AnyAsync(h => h.HesapKodu == dto.HesapKodu && (h.SirketId == sirketId));
        if (kodExists)
            throw new InvalidOperationException($"'{dto.HesapKodu}' hesap kodu zaten kullanılıyor.");

        int? ustId = dto.UstHesapId;
        if (!ustId.HasValue && dto.HesapKodu.Contains('.'))
        {
            var ustKod = dto.HesapKodu.Substring(0, dto.HesapKodu.LastIndexOf('.'));
            var ust = await _baglam.HesapPlanlari.FirstOrDefaultAsync(h => h.HesapKodu == ustKod && (h.SirketId == sirketId));
            if (ust != null) ustId = ust.Id;
        }

        if (ustId.HasValue)
        {
            var ustHesap = await _baglam.HesapPlanlari.FirstOrDefaultAsync(h => h.Id == ustId.Value);
            if (ustHesap == null)
                throw new KeyNotFoundException("Üst hesap bulunamadı.");
            if (ustHesap.SirketId != sirketId)
                throw new InvalidOperationException("Farklı bir firmaya ait hesap, üst hesap olarak seçilemez.");
            if (!ustHesap.AktifMi)
                throw new InvalidOperationException("Pasif durumdaki bir hesap üst hesap olarak seçilemez.");
            if (!dto.HesapKodu.StartsWith(ustHesap.HesapKodu))
                throw new InvalidOperationException($"Alt hesap kodu ('{dto.HesapKodu}'), üst hesap kodu ('{ustHesap.HesapKodu}') ile başlamalıdır.");
        }

        var seviye = DetermineLevel(dto.HesapKodu);

        var entity = new HesapPlani
        {
            SirketId = sirketId,
            HesapKodu = dto.HesapKodu,
            HesapAdi = dto.HesapAdi,
            Tip = dto.Tip,
            Seviye = seviye,
            BakiyeYonu = dto.BakiyeYonu,
            ParaBirimiKodu = string.IsNullOrWhiteSpace(dto.ParaBirimiKodu) ? "TRY" : dto.ParaBirimiKodu,
            SistemHesabi = false,
            Aciklama = dto.Aciklama ?? "",
            UstHesapId = ustId,
            AktifMi = dto.AktifMi
        };

        _baglam.HesapPlanlari.Add(entity);
        await _baglam.SaveChangesAsync();

        dto.Id = entity.Id;
        dto.Seviye = seviye;
        return dto;
    }

    public async Task GuncelleHesapAsync(HesapPlaniDto dto, Guid sirketId)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        YetkiKontrolEt(y => y.HesapPlaniYonet, "Hesap Planı Güncelleme");

        var entity = await _baglam.HesapPlanlari.FirstOrDefaultAsync(h => h.Id == dto.Id && (h.SirketId == sirketId));
        if (entity == null)
            throw new KeyNotFoundException("Güncellenecek hesap bulunamadı.");

        if (entity.SistemHesabi && entity.HesapKodu != dto.HesapKodu)
            throw new InvalidOperationException("Sistem hesaplarının hesap kodu değiştirilemez.");

        if (dto.HesapKodu != entity.HesapKodu)
        {
            var kodExists = await _baglam.HesapPlanlari.AnyAsync(h => h.HesapKodu == dto.HesapKodu && h.SirketId == sirketId && h.Id != entity.Id);
            if (kodExists) throw new InvalidOperationException($"'{dto.HesapKodu}' hesap kodu zaten kullanılıyor.");
        }

        var hasKullanim = await _baglam.YevmiyeSatirlari.AnyAsync(s => s.HesapId == entity.Id);

        if (hasKullanim)
        {
            if (entity.HesapKodu != dto.HesapKodu)
                throw new InvalidOperationException("Hareket görmüş hesabın hesap kodu değiştirilemez.");
            if (entity.Tip != dto.Tip)
                throw new InvalidOperationException("Hareket görmüş hesabın hesap tipi değiştirilemez.");
            if (entity.BakiyeYonu != dto.BakiyeYonu)
                throw new InvalidOperationException("Hareket görmüş hesabın bakiye yönü değiştirilemez.");
            if (entity.ParaBirimiKodu != dto.ParaBirimiKodu)
                throw new InvalidOperationException("Hareket görmüş hesabın para birimi değiştirilemez.");
            if (entity.UstHesapId != dto.UstHesapId)
                throw new InvalidOperationException("Hareket görmüş hesabın üst hesabı değiştirilemez.");
        }
        else
        {
            if (dto.UstHesapId.HasValue)
            {
                if (dto.UstHesapId.Value == entity.Id)
                    throw new InvalidOperationException("Hesap kendisinin üst hesabı olamaz.");

                var ustHesap = await _baglam.HesapPlanlari.FirstOrDefaultAsync(h => h.Id == dto.UstHesapId.Value && h.SirketId == sirketId);
                if (ustHesap == null)
                    throw new KeyNotFoundException("Seçilen üst hesap bulunamadı.");
                if (!ustHesap.AktifMi)
                    throw new InvalidOperationException("Pasif durumdaki bir hesap üst hesap olarak seçilemez.");
                
                var currentUst = ustHesap.UstHesapId;
                while (currentUst.HasValue)
                {
                    if (currentUst.Value == entity.Id) throw new InvalidOperationException("Dairesel hiyerarşi oluşturulamaz.");
                    var u = await _baglam.HesapPlanlari.AsNoTracking().FirstOrDefaultAsync(x => x.Id == currentUst.Value);
                    currentUst = u?.UstHesapId;
                }
            }

            entity.HesapKodu = dto.HesapKodu.Trim();
            entity.Tip = dto.Tip;
            entity.BakiyeYonu = dto.BakiyeYonu;
            entity.ParaBirimiKodu = dto.ParaBirimiKodu ?? "TRY";
            entity.UstHesapId = dto.UstHesapId;
            entity.Seviye = DetermineLevel(entity.HesapKodu);
        }

        entity.HesapAdi = dto.HesapAdi.Trim();
        entity.Aciklama = dto.Aciklama ?? "";
        
        if (!dto.AktifMi && entity.AktifMi)
        {
            await ValidatePasifeAlma(entity, sirketId);
        }
        
        entity.AktifMi = dto.AktifMi;

        await _baglam.SaveChangesAsync();
    }

    public async Task SilVeyaPasifeAlAsync(int hesapId, Guid sirketId)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        YetkiKontrolEt(y => y.HesapPlaniYonet, "Hesap Planı Silme/Pasife Alma");

        var entity = await _baglam.HesapPlanlari.FirstOrDefaultAsync(h => h.Id == hesapId && (h.SirketId == sirketId));
        if (entity == null)
            throw new KeyNotFoundException("İşlem yapılacak hesap bulunamadı.");

        await ValidatePasifeAlma(entity, sirketId);

        var hasKullanim = await _baglam.YevmiyeSatirlari.AnyAsync(s => s.HesapId == hesapId);
        if (hasKullanim)
        {
            entity.AktifMi = false;
        }
        else
        {
            _baglam.HesapPlanlari.Remove(entity);
        }
        await _baglam.SaveChangesAsync();
    }
    
    private async Task ValidatePasifeAlma(HesapPlani entity, Guid sirketId)
    {
        if (entity.SistemHesabi)
            throw new InvalidOperationException($"'{entity.HesapKodu}' kodlu hesap bir sistem hesabıdır ve pasife alınamaz/silinemez.");

        var baglantiKullanim = await _baglam.MuhasebeBaglantiKodlari.FirstOrDefaultAsync(b => b.SirketId == sirketId && b.VarSayilanHesapKodu == entity.HesapKodu);
        if (baglantiKullanim != null)
            throw new InvalidOperationException($"Bu hesap '{baglantiKullanim.KategoriAdi}' muhasebe bağlantısında kullanılmaktadır. Önce bağlantı kodunu başka bir hesaba yönlendirin.");

        var hasAktifAltHesap = await _baglam.HesapPlanlari.AnyAsync(h => h.UstHesapId == entity.Id && h.AktifMi);
        if (hasAktifAltHesap)
            throw new InvalidOperationException("Bu hesabın aktif alt hesapları bulunduğu için pasife alınamaz veya silinemez.");
    }

    private void YetkiKontrolEt(Func<EtkiAlani.Modeller.Kullanici.KullaniciYetkileri, bool> sorgu, string islemAdi)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(sorgu, islemAdi);
    }

    private static int DetermineLevel(string kod)
    {
        if (string.IsNullOrWhiteSpace(kod)) return 1;
        if (kod.Contains('.'))
        {
            var parts = kod.Split('.');
            return parts.Length + 2;
        }
        return kod.Length <= 1 ? 1 : (kod.Length <= 2 ? 2 : 3);
    }

    private static HesapPlaniDto MapToDto(HesapPlani h, decimal borc, decimal alacak)
    {
        return new HesapPlaniDto
        {
            Id = h.Id,
            SirketId = h.SirketId,
            HesapKodu = h.HesapKodu,
            HesapAdi = h.HesapAdi,
            Tip = h.Tip,
            Seviye = h.Seviye,
            BakiyeYonu = h.BakiyeYonu,
            ParaBirimiKodu = h.ParaBirimiKodu,
            SistemHesabi = h.SistemHesabi,
            Aciklama = h.Aciklama,
            UstHesapId = h.UstHesapId,
            UstHesapKodu = h.UstHesap?.HesapKodu,
            UstHesapAdi = h.UstHesap?.HesapAdi,
            AktifMi = h.AktifMi,
            ToplamBorc = borc,
            ToplamAlacak = alacak
        };
    }

    public async Task<List<HesapPlaniDto>> HesapPlaniGetirAsync(string aramaMetni = "", bool sadeceAktifler = true, Guid sirketId = default)
    {
        var targetSirketId = _baglam.SirketIdCoz(sirketId);
        var tumu = await GetirTumHesaplarAsync(targetSirketId);
        if (sadeceAktifler) tumu = tumu.Where(h => h.AktifMi).ToList();
        if (!string.IsNullOrWhiteSpace(aramaMetni))
        {
            var q = aramaMetni.Trim().ToLower(System.Globalization.CultureInfo.InvariantCulture);
            tumu = tumu.Where(h => h.HesapKodu.ToLower().Contains(q) || h.HesapAdi.ToLower().Contains(q)).ToList();
        }
        return tumu;
    }

    public async Task<Hesap360Dto> Hesap360GetirAsync(int hesapId, DateTime? baslangic = null, DateTime? bitis = null, Guid sirketId = default)
    {
        var targetSirketId = _baglam.SirketIdCoz(sirketId);
        return await GetirHesap360Async(hesapId, targetSirketId, baslangic, bitis);
    }

    public async Task<bool> HesapDurumDegistirAsync(int hesapId, bool aktifMi, Guid sirketId = default)
    {
        YetkiKontrolEt(y => y.HesapPlaniYonet, "Hesap Durumu Değiştirme");
        var targetSirketId = _baglam.SirketIdCoz(sirketId);
        var hesap = await _baglam.HesapPlanlari.FirstOrDefaultAsync(h => h.Id == hesapId && (h.SirketId == targetSirketId));
        if (hesap == null) return false;
        
        if (!aktifMi)
        {
            await ValidatePasifeAlma(hesap, targetSirketId);
        }
        
        hesap.AktifMi = aktifMi;
        await _baglam.SaveChangesAsync();
        return true;
    }
}
