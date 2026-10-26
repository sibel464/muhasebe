using Altyapi.Veri;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Yardimcilar;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;

namespace Altyapi.Servisler;

// Demirbaş kartları ve amortisman. Yıllık amortisman 31.12 tarihli tek fişle ayrılır (gider hesabı borç / birikmiş
// amortisman alacak); fiş "AMORTISMAN" belge tipi ve yıl*100+sıra numarasıyla işaretlenir, geri alma aynı tarihli ters fişle.
public class SabitKiymetServisi : ISabitKiymetServisi
{
    public const string BelgeTipi = "AMORTISMAN";
    public const string CikisBelgeTipi = "SABITKIYMET_CIKIS";

    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;
    private readonly YevmiyeServisi _yevmiye;

    public SabitKiymetServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
        _yevmiye = new YevmiyeServisi(baglam, kullaniciServisi);
    }

    private void Goruntule() => _kullaniciServisi?.YetkiKontrolEt(y => y.MuhasebeFisGoruntule || y.HesapPlaniYonet || y.MizanGoruntule, "Demirbaş görüntüleme");
    private void Duzenle(string islem) => (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.MuhasebeFisEkle, islem);
    private static DateTime AmortismanTarihi(int yil) => new(yil, 12, 31, 23, 0, 0, DateTimeKind.Utc);

    public async Task<List<SabitKiymetListeDto>> ListeAsync(bool eldenCikanlarDahil = false, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        var buYil = DateTime.Today.Year;
        var kiymetler = await _baglam.SabitKiymetler.AsNoTracking().Include(k => k.Kayitlar)
            .Where(k => k.SirketId == sirketId && (eldenCikanlarDahil || k.EldenCikarmaTarihi == null))
            .OrderBy(k => k.Kod).ToListAsync();
        return kiymetler.Select(k =>
        {
            var birikmis = AmortismanHesaplayici.Birikmis(k);
            return new SabitKiymetListeDto
            {
                Id = k.Id, Kod = k.Kod, Ad = k.Ad, HesapKodu = k.HesapKodu, AlisTarihi = k.AlisTarihi, Maliyet = k.Maliyet,
                FaydaliOmurYil = k.FaydaliOmurYil, Yontem = YontemAdi(k), Birikmis = birikmis,
                BuYilAyrilacak = AmortismanHesaplayici.AyrilacakTutar(k, buYil),
                Durum = k.EldenCikarmaTarihi != null ? $"Elden çıkarıldı ({k.EldenCikarmaTarihi:dd.MM.yyyy})" : birikmis >= k.Maliyet ? "Tamamen amorti edildi" : "Kullanımda",
                Konum = k.Konum, SeriNo = k.SeriNo
            };
        }).ToList();
    }

    private static string YontemAdi(SabitKiymet k) =>
        (k.Yontem == AmortismanYontemi.Normal ? "Normal" : "Azalan bakiyeler") + $" %{AmortismanHesaplayici.Oran(k) * 100:0.##}" + (k.KistAmortisman ? ", kıst" : "");

    public async Task<SabitKiymet?> GetirAsync(int id, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        return await _baglam.SabitKiymetler.AsNoTracking().Include(k => k.Kayitlar).FirstOrDefaultAsync(k => k.Id == id && k.SirketId == sirketId);
    }

    public async Task<SabitKiymet> KaydetAsync(SabitKiymet kiymet, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Duzenle("Demirbaş kaydetme");
        kiymet.Kod = (kiymet.Kod ?? "").Trim();
        kiymet.Ad = (kiymet.Ad ?? "").Trim();
        if (kiymet.Ad.Length == 0) throw new InvalidOperationException("Demirbaş adı zorunludur.");
        if (kiymet.Maliyet <= 0) throw new InvalidOperationException("Maliyet sıfırdan büyük olmalıdır.");
        if (kiymet.FaydaliOmurYil is < 1 or > 50) throw new InvalidOperationException("Faydalı ömür 1 ile 50 yıl arasında olmalıdır (Maliye Bakanlığı amortisman listesindeki süre).");
        if (kiymet.KistAmortisman && kiymet.Yontem != AmortismanYontemi.Normal) throw new InvalidOperationException("Kıst amortisman yalnız normal (eşit tutarlı) yöntemde uygulanır.");
        if (kiymet.DevirBirikmisAmortisman < 0 || kiymet.DevirBirikmisAmortisman > kiymet.Maliyet) throw new InvalidOperationException("Önceden ayrılmış amortisman sıfır ile maliyet arasında olmalıdır.");
        if (kiymet.DevirBirikmisAmortisman > 0 && kiymet.DevirSonYil == null) throw new InvalidOperationException("Önceden ayrılmış amortisman için hangi yıla kadar ayrıldığını yazın.");
        if (kiymet.DevirSonYil is int dy && dy < kiymet.AlisTarihi.Year) throw new InvalidOperationException("Önceki amortismanın son yılı alış yılından önce olamaz.");
        foreach (var (kod, ad) in new[] { (kiymet.HesapKodu, "Varlık"), (kiymet.BirikmisAmortismanHesapKodu, "Birikmiş amortisman"), (kiymet.GiderHesapKodu, "Gider") })
            if (string.IsNullOrWhiteSpace(kod)) throw new InvalidOperationException($"{ad} hesap kodu zorunludur.");
        kiymet.AlisTarihi = DateTime.SpecifyKind(kiymet.AlisTarihi.Date, DateTimeKind.Utc);

        if (kiymet.Kod.Length == 0)
        {
            var sayi = await _baglam.SabitKiymetler.IgnoreQueryFilters().CountAsync(k => k.SirketId == sirketId) + 1;
            kiymet.Kod = $"DMR-{sayi:0000}";
            while (await _baglam.SabitKiymetler.AnyAsync(k => k.SirketId == sirketId && k.Kod == kiymet.Kod)) kiymet.Kod = $"DMR-{++sayi:0000}";
        }
        if (await _baglam.SabitKiymetler.AnyAsync(k => k.SirketId == sirketId && k.Kod == kiymet.Kod && k.Id != kiymet.Id))
            throw new InvalidOperationException($"{kiymet.Kod} kodlu başka bir demirbaş var.");

        if (kiymet.Id == 0)
        {
            kiymet.SirketId = sirketId;
            kiymet.Kayitlar = new();
            _baglam.SabitKiymetler.Add(kiymet);
        }
        else
        {
            var mevcut = await _baglam.SabitKiymetler.Include(k => k.Kayitlar).FirstOrDefaultAsync(k => k.Id == kiymet.Id && k.SirketId == sirketId)
                ?? throw new KeyNotFoundException("Demirbaş bulunamadı.");
            // Amortisman ayrıldıktan sonra hesaplamayı değiştiren alanlar kilitlenir; geçmiş fişlerle tutarsızlık olmasın.
            if (mevcut.Kayitlar.Any(x => !x.Silindi) && (mevcut.Maliyet != kiymet.Maliyet || mevcut.FaydaliOmurYil != kiymet.FaydaliOmurYil || mevcut.Yontem != kiymet.Yontem
                    || mevcut.KistAmortisman != kiymet.KistAmortisman || mevcut.AlisTarihi.Date != kiymet.AlisTarihi.Date || mevcut.DevirBirikmisAmortisman != kiymet.DevirBirikmisAmortisman
                    || mevcut.DevirSonYil != kiymet.DevirSonYil || mevcut.BirikmisAmortismanHesapKodu != kiymet.BirikmisAmortismanHesapKodu || mevcut.HesapKodu != kiymet.HesapKodu))
                throw new InvalidOperationException("Bu demirbaş için amortisman ayrılmış. Maliyet, tarih, ömür, yöntem ve hesaplar değiştirilemez; önce ilgili yılın amortismanını geri alın.");
            mevcut.Kod = kiymet.Kod; mevcut.Ad = kiymet.Ad; mevcut.HesapKodu = kiymet.HesapKodu; mevcut.BirikmisAmortismanHesapKodu = kiymet.BirikmisAmortismanHesapKodu;
            mevcut.GiderHesapKodu = kiymet.GiderHesapKodu; mevcut.AlisTarihi = kiymet.AlisTarihi; mevcut.Maliyet = kiymet.Maliyet; mevcut.FaydaliOmurYil = kiymet.FaydaliOmurYil;
            mevcut.Yontem = kiymet.Yontem; mevcut.KistAmortisman = kiymet.KistAmortisman; mevcut.DevirBirikmisAmortisman = kiymet.DevirBirikmisAmortisman;
            mevcut.DevirSonYil = kiymet.DevirSonYil; mevcut.SeriNo = kiymet.SeriNo; mevcut.Konum = kiymet.Konum; mevcut.Aciklama = kiymet.Aciklama ?? "";
            mevcut.GuncellemeTarihi = DateTime.UtcNow;
            kiymet = mevcut;
        }
        await _baglam.SaveChangesAsync();
        return kiymet;
    }

    public async Task SilAsync(int id, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Duzenle("Demirbaş silme");
        var k = await _baglam.SabitKiymetler.Include(x => x.Kayitlar).FirstOrDefaultAsync(x => x.Id == id && x.SirketId == sirketId)
            ?? throw new KeyNotFoundException("Demirbaş bulunamadı.");
        if (k.Kayitlar.Any(x => !x.Silindi) || k.EldenCikarmaFisId != null)
            throw new InvalidOperationException("Amortisman ayrılmış ya da elden çıkarılmış demirbaş silinemez.");
        k.Silindi = true;
        k.SilinmeTarihi = DateTime.UtcNow;
        k.SilinmeNedeni = "Kullanıcı sildi";
        await _baglam.SaveChangesAsync();
    }

    public async Task<List<AmortismanSatiri>> PlanAsync(int id, Guid sirketId = default)
    {
        var k = await GetirAsync(id, sirketId) ?? throw new KeyNotFoundException("Demirbaş bulunamadı.");
        return AmortismanHesaplayici.Plan(k);
    }

    public async Task<AmortismanYilOnizlemeDto> YilOnizleAsync(int yil, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        return await OnizleAsync(yil, sirketId);
    }

    private async Task<AmortismanYilOnizlemeDto> OnizleAsync(int yil, Guid sirketId)
    {
        var o = new AmortismanYilOnizlemeDto { Yil = yil };
        var kiymetler = await _baglam.SabitKiymetler.AsNoTracking().Include(k => k.Kayitlar).Where(k => k.SirketId == sirketId).OrderBy(k => k.Kod).ToListAsync();
        foreach (var k in kiymetler)
        {
            o.AyrilmisToplam += k.Kayitlar.Where(x => !x.Silindi && x.Yil == yil).Sum(x => x.Tutar);
            var tutar = AmortismanHesaplayici.AyrilacakTutar(k, yil);
            if (tutar > 0)
                o.Kalemler.Add(new AmortismanKalemiDto { SabitKiymetId = k.Id, Kod = k.Kod, Ad = k.Ad, GiderHesabi = k.GiderHesapKodu, BirikmisHesabi = k.BirikmisAmortismanHesapKodu, Tutar = tutar });
        }
        if (DateTime.Today < new DateTime(yil, 12, 1))
            o.Engeller.Add($"{yil} yılı amortismanı yıl sonunda ayrılır (Aralık ayından itibaren).");
        if (await _baglam.YevmiyeFisleri.AnyAsync(f => f.SirketId == sirketId && f.BelgeTipi == $"YILSONU_{yil}_KAPANIS" && f.TersFisId == null))
            o.Engeller.Add($"{yil} yılı kapatılmış. Önce Yıl Sonu Kapanışı ekranından kapanışı geri alın.");
        var tarih = AmortismanTarihi(yil);
        if (await _baglam.MaliDonemler.AnyAsync(m => m.SirketId == sirketId && m.Baslangic <= tarih && m.Bitis >= tarih && m.KilitliMi))
            o.Engeller.Add($"31.12.{yil} kilitli bir mali döneme düşüyor.");
        return o;
    }

    public async Task<string> AmortismanAyirAsync(int yil, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Duzenle("Amortisman ayırma");
        await using var islem = await _baglam.Database.BeginTransactionAsync();
        await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", $"amortisman:{sirketId}");
        var o = await OnizleAsync(yil, sirketId);
        if (o.Engeller.Count > 0) throw new InvalidOperationException(string.Join("\n", o.Engeller));
        if (o.Kalemler.Count == 0) throw new InvalidOperationException($"{yil} yılı için ayrılacak amortisman yok.");

        var satirlar = new List<YevmiyeSatiriDto>();
        foreach (var g in o.Kalemler.GroupBy(k => k.GiderHesabi))
            satirlar.Add(new YevmiyeSatiriDto { HesapId = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(_baglam, sirketId, g.Key), Borc = g.Sum(x => x.Tutar), Aciklama = $"{yil} yılı amortisman gideri" });
        foreach (var k in o.Kalemler)
            satirlar.Add(new YevmiyeSatiriDto { HesapId = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(_baglam, sirketId, k.BirikmisHesabi), Alacak = k.Tutar, Aciklama = $"{k.Kod} {k.Ad} - {yil} amortismanı" });

        var sira = await _baglam.YevmiyeFisleri.CountAsync(f => f.SirketId == sirketId && f.BelgeTipi == BelgeTipi && f.BelgeId > yil * 100 && f.BelgeId < (yil + 1) * 100) + 1;
        var fis = await _yevmiye.FisOlusturAsync(new YevmiyeFisDto
        {
            FisTuru = FisTuru.Mahsup, Tarih = AmortismanTarihi(yil), BelgeTipi = BelgeTipi, BelgeId = yil * 100 + sira,
            Referans = $"{yil} amortisman", Aciklama = $"{yil} yılı amortisman ({o.Kalemler.Count} demirbaş)", Durum = YevmiyeDurumu.Onaylandi, Satirlar = satirlar
        }, sirketId, 0);
        foreach (var k in o.Kalemler)
            _baglam.AmortismanKayitlari.Add(new AmortismanKaydi { SabitKiymetId = k.SabitKiymetId, Yil = yil, Tutar = k.Tutar, YevmiyeFisId = fis.Id });
        await _baglam.SaveChangesAsync();
        await islem.CommitAsync();
        return fis.FisNo;
    }

    public async Task AmortismanGeriAlAsync(int yil, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.MuhasebeFisIptal, "Amortisman geri alma");
        await using var islem = await _baglam.Database.BeginTransactionAsync();
        await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", $"amortisman:{sirketId}");
        if (await _baglam.YevmiyeFisleri.AnyAsync(f => f.SirketId == sirketId && f.BelgeTipi == $"YILSONU_{yil}_KAPANIS" && f.TersFisId == null))
            throw new InvalidOperationException($"{yil} yılı kapatılmış. Önce yıl sonu kapanışını geri alın.");
        var fisler = await _baglam.YevmiyeFisleri.Include(f => f.Satirlar)
            .Where(f => f.SirketId == sirketId && f.BelgeTipi == BelgeTipi && f.BelgeId > yil * 100 && f.BelgeId < (yil + 1) * 100 && f.TersFisId == null && f.Durum == YevmiyeDurumu.Onaylandi)
            .ToListAsync();
        if (fisler.Count == 0) throw new InvalidOperationException($"{yil} yılı için geri alınacak amortisman yok.");
        var cikanlar = await _baglam.SabitKiymetler.Where(k => k.SirketId == sirketId && k.EldenCikarmaTarihi != null && k.EldenCikarmaTarihi.Value.Year > yil
            && k.Kayitlar.Any(x => !x.Silindi && x.Yil == yil)).Select(k => k.Kod).ToListAsync();
        if (cikanlar.Count > 0)
            throw new InvalidOperationException($"Şu demirbaşlar daha sonra elden çıkarılmış, o yılın amortismanı geri alınamaz: {string.Join(", ", cikanlar)}.");

        foreach (var fis in fisler)
        {
            var ters = await _yevmiye.FisOlusturAsync(new YevmiyeFisDto
            {
                FisTuru = fis.FisTuru, Tarih = fis.Tarih, BelgeTipi = $"TERS_{fis.BelgeTipi}_{fis.Id}", Referans = $"Ters Fiş ({fis.FisNo})",
                Aciklama = $"Amortisman geri alındı: {fis.FisNo}", Durum = YevmiyeDurumu.Onaylandi,
                Satirlar = fis.Satirlar.Select(s => new YevmiyeSatiriDto { HesapId = s.HesapId, Borc = s.Alacak, Alacak = s.Borc, Aciklama = "Ters Kayıt: " + s.Aciklama }).ToList()
            }, sirketId, 0);
            (await _baglam.YevmiyeFisleri.SingleAsync(f => f.Id == ters.Id)).IptalEdilenFisId = fis.Id;
            fis.TersFisId = ters.Id;
            var fisIds = fis.Id;
            foreach (var kayit in await _baglam.AmortismanKayitlari.Where(x => x.YevmiyeFisId == fisIds).ToListAsync())
            {
                kayit.Silindi = true;
                kayit.SilinmeTarihi = DateTime.UtcNow;
                kayit.SilinmeNedeni = "Amortisman geri alındı";
            }
        }
        await _baglam.SaveChangesAsync();
        await islem.CommitAsync();
    }

    // Elden çıkarma: birikmiş amortisman ve net defter değeri (689) karşılığında varlık hesabı kapatılır. Satış geliri
    // ayrıca faturayla (679 / 100-102-120) kaydedilir. VUK gereği çıkış yılında amortisman ayrılmaz.
    public async Task<string> EldenCikarAsync(int id, DateTime tarih, string aciklama, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Duzenle("Demirbaş elden çıkarma");
        await using var islem = await _baglam.Database.BeginTransactionAsync();
        var k = await _baglam.SabitKiymetler.Include(x => x.Kayitlar).FirstOrDefaultAsync(x => x.Id == id && x.SirketId == sirketId)
            ?? throw new KeyNotFoundException("Demirbaş bulunamadı.");
        if (k.EldenCikarmaTarihi != null) throw new InvalidOperationException("Bu demirbaş zaten elden çıkarılmış.");
        tarih = DateTime.SpecifyKind(tarih.Date, DateTimeKind.Utc);
        if (tarih.Date < k.AlisTarihi.Date) throw new InvalidOperationException("Çıkış tarihi alış tarihinden önce olamaz.");
        if (k.Kayitlar.Any(x => !x.Silindi && x.Yil >= tarih.Year))
            throw new InvalidOperationException($"{tarih.Year} yılı (ve sonrası) için bu demirbaşa amortisman ayrılmış. Çıkış yılında amortisman ayrılmaz; önce o yılın amortismanını geri alın.");

        var birikmis = AmortismanHesaplayici.Birikmis(k);
        var net = k.Maliyet - birikmis;
        var satirlar = new List<YevmiyeSatiriDto>();
        if (birikmis > 0)
            satirlar.Add(new YevmiyeSatiriDto { HesapId = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(_baglam, sirketId, k.BirikmisAmortismanHesapKodu), Borc = birikmis, Aciklama = $"{k.Kod} birikmiş amortisman" });
        if (net > 0)
            satirlar.Add(new YevmiyeSatiriDto { HesapId = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(_baglam, sirketId, "689"), Borc = net, Aciklama = $"{k.Kod} net defter değeri" });
        satirlar.Add(new YevmiyeSatiriDto { HesapId = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(_baglam, sirketId, k.HesapKodu), Alacak = k.Maliyet, Aciklama = $"{k.Kod} {k.Ad} elden çıkarıldı" });

        var fis = await _yevmiye.FisOlusturAsync(new YevmiyeFisDto
        {
            FisTuru = FisTuru.Mahsup, Tarih = tarih, BelgeTipi = CikisBelgeTipi, BelgeId = k.Id, Referans = k.Kod,
            Aciklama = $"Demirbaş elden çıkarma: {k.Kod} {k.Ad}" + (string.IsNullOrWhiteSpace(aciklama) ? "" : $" - {aciklama.Trim()}"),
            Durum = YevmiyeDurumu.Onaylandi, Satirlar = satirlar
        }, sirketId, 0);
        k.EldenCikarmaTarihi = tarih;
        k.EldenCikarmaFisId = fis.Id;
        k.GuncellemeTarihi = DateTime.UtcNow;
        await _baglam.SaveChangesAsync();
        await islem.CommitAsync();
        return fis.FisNo;
    }

    // Yıl sonu kapanışı uyarısı için: verilen yılda amortismanı ayrılmamış demirbaş sayısı.
    public static async Task<int> AyrilmamisSayisiAsync(MuhasebeBaglami baglam, Guid sirketId, int yil)
    {
        var kiymetler = await baglam.SabitKiymetler.AsNoTracking().Include(k => k.Kayitlar).Where(k => k.SirketId == sirketId).ToListAsync();
        return kiymetler.Count(k => AmortismanHesaplayici.AyrilacakTutar(k, yil) > 0);
    }
}
