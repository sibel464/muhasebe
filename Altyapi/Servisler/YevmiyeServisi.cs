using Altyapi.Veri;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Modeller.Ortak;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;

namespace Altyapi.Servisler;

public class YevmiyeServisi : IYevmiyeServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;
    private readonly IMuhasebeBaglantiServisi? _baglantiServisi;

    public YevmiyeServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null, IMuhasebeBaglantiServisi? baglantiServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
        _baglantiServisi = baglantiServisi;
    }

    public async Task<YevmiyeFisDto> FisOlusturAsync(YevmiyeFisDto dto, Guid sirketId, int kullaniciId)
    {
        YetkiKontrolEt(y => y.MuhasebeFisEkle, "Muhasebe Fişi Ekleme");

        await MaliDonemAcikMiDogrulaAsync(dto.Tarih, sirketId);

        if (dto.Satirlar == null || dto.Satirlar.Count == 0)
            throw new InvalidOperationException("Muhasebe fişinde en az bir kalem satırı bulunmalıdır.");

        var borcToplam = dto.Satirlar.Sum(s => s.Borc);
        var alacakToplam = dto.Satirlar.Sum(s => s.Alacak);

        if (borcToplam != alacakToplam)
            throw new InvalidOperationException($"Yevmiye fişi dengelenemedi: Toplam Borç ({borcToplam:N2} TL) ile Toplam Alacak ({alacakToplam:N2} TL) eşit olmak zorundadır.");

        var hesapIds = dto.Satirlar.Select(s => s.HesapId).Distinct().ToArray();
        if (dto.Satirlar.Any(s => s.Borc < 0 || s.Alacak < 0 || (s.Borc > 0 && s.Alacak > 0)))
            throw new InvalidOperationException("Yevmiye satırında negatif tutar veya aynı anda borç ve alacak bulunamaz.");
        if (await _baglam.HesapPlanlari.CountAsync(h => hesapIds.Contains(h.Id) && h.SirketId == sirketId && h.AktifMi) != hesapIds.Length)
            throw new YetkisizErisimException("Yevmiye hesaplarının aktif firmaya ait olması");
        var cariIds = dto.Satirlar.Where(s => s.CariKartId.HasValue).Select(s => s.CariKartId!.Value).Distinct().ToArray();
        if (await _baglam.CariKartlar.CountAsync(c => cariIds.Contains(c.Id) && c.SirketId == sirketId) != cariIds.Length)
            throw new YetkisizErisimException("Yevmiye carilerinin aktif firmaya ait olması");

        var transactionSahibi = _baglam.Database.CurrentTransaction == null;
        await using var transaction = transactionSahibi ? await _baglam.Database.BeginTransactionAsync() : null;

        try
        {
            var fisNo = await FisNoUretAsync(dto.FisTuru, dto.Tarih, sirketId);

            var kullaniciAdi = _kullaniciServisi?.AktifKullanici?.KullaniciAdi ?? "Sistem";

            var entity = new YevmiyeFis
            {
                SirketId = sirketId,
                FisNo = fisNo,
                FisTuru = dto.FisTuru,
                Tarih = DateTime.SpecifyKind(dto.Tarih, DateTimeKind.Utc),
                MaliDonemId = dto.MaliDonemId,
                SubeId = dto.SubeId,
                BelgeTipi = dto.BelgeId > 0 ? (dto.BelgeTipi ?? "") : (string.IsNullOrWhiteSpace(dto.BelgeTipi) || dto.BelgeTipi == "MANUEL" ? $"MANUEL_{fisNo}" : dto.BelgeTipi),
                BelgeId = dto.BelgeId,
                Referans = dto.Referans ?? "",
                Aciklama = dto.Aciklama ?? "",
                Durum = dto.Durum,
                KullaniciId = kullaniciId > 0 ? kullaniciId : (_kullaniciServisi?.AktifKullanici?.Id ?? 1),
                OlusturanKullaniciAdi = kullaniciAdi,
                Satirlar = dto.Satirlar.Select(s => new YevmiyeSatiri
                {
                    HesapId = s.HesapId,
                    Borc = s.Borc,
                    Alacak = s.Alacak,
                    Aciklama = s.Aciklama ?? "",
                    BelgeNo = s.BelgeNo ?? "",
                    ParaBirimi = string.IsNullOrWhiteSpace(s.ParaBirimi) ? "TRY" : s.ParaBirimi,
                    Kur = s.Kur <= 0 ? 1.0m : s.Kur,
                    DovizBorc = s.DovizBorc,
                    DovizAlacak = s.DovizAlacak,
                    CariKartId = s.CariKartId
                }).ToList()
            };

            _baglam.YevmiyeFisleri.Add(entity);
            await _baglam.SaveChangesAsync();

            if (transactionSahibi)
                await transaction!.CommitAsync();

            return await GetirFisByIdAsync(entity.Id, sirketId)
                ?? throw new InvalidOperationException("Fiş oluşturuldu ancak okunamadı.");
        }
        catch
        {
            if (transactionSahibi)
                await transaction!.RollbackAsync();
            throw;
        }
    }

    public async Task<YevmiyeFisDto> FisOnaylaAsync(int fisId, Guid sirketId)
    {
        YetkiKontrolEt(y => y.MuhasebeFisOnayla, "Muhasebe Fişi Onaylama");

        var fis = await _baglam.YevmiyeFisleri
            .Include(f => f.Satirlar)
            .FirstOrDefaultAsync(f => f.Id == fisId && (f.SirketId == sirketId));

        if (fis == null)
            throw new KeyNotFoundException("Onaylanacak yevmiye fişi bulunamadı.");

        if (fis.Durum == YevmiyeDurumu.Onaylandi)
            throw new InvalidOperationException("Bu yevmiye fişi zaten onaylanmıştır.");

        if (fis.Durum == YevmiyeDurumu.Iptal)
            throw new InvalidOperationException("İptal edilmiş bir yevmiye fişi onaylanamaz.");

        await MaliDonemAcikMiDogrulaAsync(fis.Tarih, sirketId);

        var borcToplam = fis.Satirlar.Sum(s => s.Borc);
        var alacakToplam = fis.Satirlar.Sum(s => s.Alacak);
        if (borcToplam != alacakToplam)
            throw new InvalidOperationException($"Dengesiz fiş onaylanamaz: Toplam Borç ({borcToplam:N2}) != Toplam Alacak ({alacakToplam:N2}).");

        fis.Durum = YevmiyeDurumu.Onaylandi;
        await _baglam.SaveChangesAsync();

        return MapToDto(fis);
    }

    public async Task<YevmiyeFisDto> FisIptalEtAsync(int fisId, string iptalNedeni, Guid sirketId)
    {
        YetkiKontrolEt(y => y.MuhasebeFisIptal, "Muhasebe Fişi İptal Etme");

        var fis = await _baglam.YevmiyeFisleri
            .Include(f => f.Satirlar)
            .FirstOrDefaultAsync(f => f.Id == fisId && (f.SirketId == sirketId));

        if (fis == null)
            throw new KeyNotFoundException("İptal edilecek yevmiye fişi bulunamadı.");

        if (fis.Durum == YevmiyeDurumu.Iptal)
            throw new InvalidOperationException("Bu yevmiye fişi daha önce iptal edilmiştir. İkinci kez iptal edilemez.");

        await MaliDonemAcikMiDogrulaAsync(fis.Tarih, sirketId);

        var transactionSahibi = _baglam.Database.CurrentTransaction == null;
        await using var transaction = transactionSahibi ? await _baglam.Database.BeginTransactionAsync() : null;

        try
        {
            var kullaniciAdi = _kullaniciServisi?.AktifKullanici?.KullaniciAdi ?? "Sistem";

            // Mark original as cancelled
            fis.Durum = YevmiyeDurumu.Iptal;

            // Generate reversing entry (Ters Fiş)
            var tersFisNo = await FisNoUretAsync(fis.FisTuru, DateTime.UtcNow, sirketId);
            var tersFis = new YevmiyeFis
            {
                SirketId = sirketId,
                FisNo = tersFisNo,
                FisTuru = fis.FisTuru,
                Tarih = DateTime.UtcNow,
                MaliDonemId = fis.MaliDonemId,
                SubeId = fis.SubeId,
                BelgeTipi = !string.IsNullOrWhiteSpace(fis.BelgeTipi) ? $"TERS_{fis.BelgeTipi}_{fis.Id}" : $"TERS_{fis.FisNo}",
                BelgeId = fis.BelgeId,
                Referans = $"Ters Fiş ({fis.FisNo})",
                Aciklama = $"İptal Edilen Fiş: {fis.FisNo}. Neden: {iptalNedeni}",
                Durum = YevmiyeDurumu.Onaylandi,
                IptalEdilenFisId = fis.Id,
                KullaniciId = _kullaniciServisi?.AktifKullanici?.Id ?? 1,
                OlusturanKullaniciAdi = kullaniciAdi,
                Satirlar = fis.Satirlar.Select(s => new YevmiyeSatiri
                {
                    HesapId = s.HesapId,
                    Borc = s.Alacak, // Swap debit and credit
                    Alacak = s.Borc, // Swap debit and credit
                    Aciklama = $"Ters Kayıt: {s.Aciklama}",
                    BelgeNo = s.BelgeNo,
                    ParaBirimi = s.ParaBirimi,
                    Kur = s.Kur,
                    DovizBorc = s.DovizAlacak,
                    DovizAlacak = s.DovizBorc,
                    CariKartId = s.CariKartId
                }).ToList()
            };

            _baglam.YevmiyeFisleri.Add(tersFis);
            await _baglam.SaveChangesAsync();

            fis.TersFisId = tersFis.Id;
            await _baglam.SaveChangesAsync();

            if (transactionSahibi)
                await transaction!.CommitAsync();

            return MapToDto(fis);
        }
        catch
        {
            if (transactionSahibi)
                await transaction!.RollbackAsync();
            throw;
        }
    }

    public async Task<YevmiyeFisDto?> GetirFisByIdAsync(int fisId, Guid sirketId)
    {
        var fis = await _baglam.YevmiyeFisleri
            .AsNoTracking()
            .Include(f => f.Satirlar)
            .ThenInclude(s => s.Hesap)
            .FirstOrDefaultAsync(f => f.Id == fisId && (f.SirketId == sirketId));

        return fis == null ? null : MapToDto(fis);
    }

    public async Task<SayfaliSonuc<YevmiyeSatiriDto>> YevmiyeDefteriSorgulaAsync(YevmiyeFiltreDto filtre)
    {
        var sirketId = _baglam.SirketIdCoz(filtre.SirketId);
        var query = _baglam.YevmiyeSatirlari
            .AsNoTracking()
            .Include(s => s.Fis)
            .Include(s => s.Hesap)
            .Where(s => s.Fis != null && (s.Fis.SirketId == sirketId));

        if (filtre.BaslangicTarihi.HasValue)
            query = query.Where(s => s.Fis!.Tarih >= filtre.BaslangicTarihi.Value);

        if (filtre.BitisTarihi.HasValue)
            query = query.Where(s => s.Fis!.Tarih <= filtre.BitisTarihi.Value);

        if (!string.IsNullOrWhiteSpace(filtre.FisNo))
            query = query.Where(s => s.Fis!.FisNo.Contains(filtre.FisNo));

        if (filtre.FisTuru.HasValue)
            query = query.Where(s => s.Fis!.FisTuru == filtre.FisTuru.Value);

        if (filtre.Durum.HasValue)
            query = query.Where(s => s.Fis!.Durum == filtre.Durum.Value);

        if (!string.IsNullOrWhiteSpace(filtre.HesapKodu))
            query = query.Where(s => s.Hesap != null && s.Hesap.HesapKodu.StartsWith(filtre.HesapKodu));

        if (!string.IsNullOrWhiteSpace(filtre.AramaMetni))
        {
            var txt = filtre.AramaMetni.Trim().ToLower(System.Globalization.CultureInfo.InvariantCulture);
            query = query.Where(s => s.Fis!.FisNo.ToLower().Contains(txt) || s.Aciklama.ToLower().Contains(txt) || (s.Hesap != null && (s.Hesap.HesapKodu.ToLower().Contains(txt) || s.Hesap.HesapAdi.ToLower().Contains(txt))));
        }

        var toplamKayit = await query.CountAsync();
        var sayfa = Math.Max(1, filtre.Sayfa);
        var sayfaBoyutu = Math.Clamp(filtre.SayfaBoyutu, 10, 500);

        var list = await query
            .OrderByDescending(s => s.Fis!.Tarih)
            .ThenByDescending(s => s.FisId)
            .Skip((sayfa - 1) * sayfaBoyutu)
            .Take(sayfaBoyutu)
            .Select(s => new YevmiyeSatiriDto
            {
                Id = s.Id,
                FisId = s.FisId,
                FisNo = s.Fis!.FisNo,
                FisTuru = s.Fis.FisTuru.ToString(),
                Tarih = s.Fis.Tarih,
                HesapId = s.HesapId,
                HesapKodu = s.Hesap != null ? s.Hesap.HesapKodu : "",
                HesapAdi = s.Hesap != null ? s.Hesap.HesapAdi : "",
                Borc = s.Borc,
                Alacak = s.Alacak,
                Aciklama = s.Aciklama,
                BelgeNo = s.BelgeNo,
                KullaniciAdi = s.Fis.OlusturanKullaniciAdi
            })
            .ToListAsync();

        return new SayfaliSonuc<YevmiyeSatiriDto>
        {
            Veri = list,
            ToplamKayit = toplamKayit,
            MevcutSayfa = sayfa,
            SayfaBoyutu = sayfaBoyutu
        };
    }

    public async Task<YevmiyeFisDto?> GetirBelgeFisiAsync(string belgeTipi, int belgeId, Guid sirketId)
    {
        var fis = await _baglam.YevmiyeFisleri
            .AsNoTracking()
            .Include(f => f.Satirlar)
            .ThenInclude(s => s.Hesap)
            .FirstOrDefaultAsync(f => f.BelgeTipi == belgeTipi && f.BelgeId == belgeId && (f.SirketId == sirketId));

        return fis == null ? null : MapToDto(fis);
    }

    public async Task<YevmiyeFisDto> OtomatikMuhasebelestirAsync(string belgeTipi, int belgeId, Guid sirketId, int kullaniciId)
    {
        // Check if already posted (Idempotency)
        var mevcutFis = await GetirBelgeFisiAsync(belgeTipi, belgeId, sirketId);
        if (mevcutFis != null)
            return mevcutFis;

        if (belgeTipi == "Fatura")
        {
            var financialService = new FinancialTransactionService(_baglam, _kullaniciServisi);
            var resultFis = await financialService.CreateFromInvoiceAsync(belgeId);
            return MapToDto(resultFis);
        }

        if (belgeTipi is "Tahsilat" or "Odeme" or "Kasa" or "Banka")
        {
            var kasaHareketi = await _baglam.KasaHareketleri
                .Include(k => k.CariKart)
                .FirstOrDefaultAsync(k => k.Id == belgeId);

            if (kasaHareketi != null)
            {
                var kasaHesapKodu = await (_baglantiServisi?.GetirHesapKoduForKategoriAsync("KASA", sirketId) ?? Task.FromResult("100"));
                var cariHesapKodu = await (_baglantiServisi?.GetirHesapKoduForKategoriAsync(kasaHareketi.Tur == EtkiAlani.Modeller.Finans.HareketTuru.Gelir ? "ALICILAR" : "SATICILAR", sirketId) ?? Task.FromResult(kasaHareketi.Tur == EtkiAlani.Modeller.Finans.HareketTuru.Gelir ? "120" : "320"));

                var kasaHesap = await _baglam.HesapPlanlari.FirstOrDefaultAsync(h => h.HesapKodu == kasaHesapKodu && (h.SirketId == sirketId));
                var cariHesap = await _baglam.HesapPlanlari.FirstOrDefaultAsync(h => h.HesapKodu == cariHesapKodu && (h.SirketId == sirketId));

                if (kasaHesap != null && cariHesap != null)
                {
                    var isGelir = kasaHareketi.Tur == EtkiAlani.Modeller.Finans.HareketTuru.Gelir;
                    var dto = new YevmiyeFisDto
                    {
                        SirketId = sirketId,
                        FisTuru = isGelir ? FisTuru.Tahsil : FisTuru.Tediye,
                        Tarih = kasaHareketi.Tarih,
                        BelgeTipi = belgeTipi,
                        BelgeId = kasaHareketi.Id,
                        Referans = kasaHareketi.Belge ?? "",
                        Aciklama = kasaHareketi.Aciklama,
                        Durum = YevmiyeDurumu.Onaylandi,
                        Satirlar = new List<YevmiyeSatiriDto>
                        {
                            new YevmiyeSatiriDto { HesapId = isGelir ? kasaHesap.Id : cariHesap.Id, Borc = kasaHareketi.Tutar, Alacak = 0, Aciklama = kasaHareketi.Aciklama, CariKartId = kasaHareketi.CariKartId },
                            new YevmiyeSatiriDto { HesapId = isGelir ? cariHesap.Id : kasaHesap.Id, Borc = 0, Alacak = kasaHareketi.Tutar, Aciklama = kasaHareketi.Aciklama, CariKartId = kasaHareketi.CariKartId }
                        }
                    };
                    return await FisOlusturAsync(dto, sirketId, kullaniciId);
                }
            }
        }

        throw new InvalidOperationException($"'{belgeTipi}' türündeki belge için otomatik muhasebeleştirme kuralları çalıştırılamadı.");
    }

    private async Task MaliDonemAcikMiDogrulaAsync(DateTime tarih, Guid sirketId)
    {
        var donem = await _baglam.MaliDonemler
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Baslangic <= tarih && m.Bitis >= tarih && (m.SirketId == sirketId));

        if (donem != null && donem.Kapali)
            throw new InvalidOperationException($"Seçilen tarih ({tarih:dd.MM.yyyy}), kapalı bir mali döneme denk gelmektedir. Kapalı döneme fiş girilemez, düzenlenemez veya iptal edilemez.");
    }

    private async Task<string> FisNoUretAsync(FisTuru tur, DateTime tarih, Guid sirketId)
    {
        var prefix = tur switch
        {
            FisTuru.Mahsup => "MHS",
            FisTuru.Tahsil => "THS",
            FisTuru.Tediye => "TDY",
            FisTuru.Acilis => "ACL",
            FisTuru.Kapanis => "KPN",
            _ => "YF"
        };

        var yil = tarih.Year;
        var serikod = $"{prefix}-{yil}";

        var sonFis = await _baglam.YevmiyeFisleri
            .AsNoTracking()
            .Where(f => (f.SirketId == sirketId) && f.FisNo.StartsWith(serikod))
            .OrderByDescending(f => f.FisNo)
            .FirstOrDefaultAsync();

        int maxNo = 0;
        if (sonFis != null && sonFis.FisNo.Length > serikod.Length + 1)
        {
            var numPart = sonFis.FisNo.Substring(serikod.Length + 1);
            int.TryParse(numPart, out maxNo);
        }

        return $"{serikod}-{(maxNo + 1):D6}";
    }

    private void YetkiKontrolEt(Func<EtkiAlani.Modeller.Kullanici.KullaniciYetkileri, bool> sorgu, string islemAdi)
    {
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(sorgu, islemAdi);
    }

    private static YevmiyeFisDto MapToDto(YevmiyeFis f)
    {
        return new YevmiyeFisDto
        {
            Id = f.Id,
            SirketId = f.SirketId,
            FisNo = f.FisNo,
            FisTuru = f.FisTuru,
            Tarih = f.Tarih,
            MaliDonemId = f.MaliDonemId,
            SubeId = f.SubeId,
            BelgeTipi = f.BelgeTipi,
            BelgeId = f.BelgeId,
            Referans = f.Referans,
            Aciklama = f.Aciklama,
            Durum = f.Durum,
            TersFisId = f.TersFisId,
            IptalEdilenFisId = f.IptalEdilenFisId,
            KullaniciId = f.KullaniciId,
            OlusturanKullaniciAdi = f.OlusturanKullaniciAdi,
            ToplamBorc = f.Satirlar?.Sum(s => s.Borc) ?? 0m,
            ToplamAlacak = f.Satirlar?.Sum(s => s.Alacak) ?? 0m,
            Satirlar = f.Satirlar?.Select(s => new YevmiyeSatiriDto
            {
                Id = s.Id,
                FisId = s.FisId,
                FisNo = f.FisNo,
                Tarih = f.Tarih,
                HesapId = s.HesapId,
                HesapKodu = s.Hesap?.HesapKodu ?? "",
                HesapAdi = s.Hesap?.HesapAdi ?? "",
                Borc = s.Borc,
                Alacak = s.Alacak,
                Aciklama = s.Aciklama,
                BelgeNo = s.BelgeNo,
                ParaBirimi = s.ParaBirimi,
                Kur = s.Kur,
                DovizBorc = s.DovizBorc,
                DovizAlacak = s.DovizAlacak,
                CariKartId = s.CariKartId
            }).ToList() ?? new()
        };
    }

    public async Task<YevmiyeFisDto?> FisIptalEtTersKayitUretAsync(int fisId, string iptalNedeni, Guid sirketId = default)
    {
        var targetSirketId = _baglam.SirketIdCoz(sirketId);
        return await FisIptalEtAsync(fisId, iptalNedeni, targetSirketId);
    }

    public async Task<SayfaliSonuc<YevmiyeFisDto>> FisleriGetirAsync(YevmiyeFiltreDto filtre)
    {
        var sirketId = _baglam.SirketIdCoz(filtre.SirketId);
        var query = _baglam.YevmiyeFisleri
            .Include(f => f.Satirlar)
            .ThenInclude(s => s.Hesap)
            .AsNoTracking()
            .Where(f => f.SirketId == sirketId);

        if (!string.IsNullOrWhiteSpace(filtre.AramaMetni))
        {
            var q = filtre.AramaMetni.Trim().ToLower(System.Globalization.CultureInfo.InvariantCulture);
            query = query.Where(f => f.FisNo.ToLower().Contains(q) || f.Aciklama.ToLower().Contains(q) || f.Referans.ToLower().Contains(q));
        }

        var toplam = await query.CountAsync();
        var liste = await query
            .OrderByDescending(f => f.Tarih)
            .Skip((filtre.Sayfa - 1) * filtre.SayfaBoyutu)
            .Take(filtre.SayfaBoyutu)
            .ToListAsync();

        return new SayfaliSonuc<YevmiyeFisDto>
        {
            Veri = liste.Select(MapToDto).ToList(),
            ToplamKayit = toplam,
            MevcutSayfa = filtre.Sayfa,
            SayfaBoyutu = filtre.SayfaBoyutu
        };
    }
}
