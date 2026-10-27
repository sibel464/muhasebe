using System.Globalization;
using Altyapi.Veri;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;

namespace Altyapi.Servisler;

// Ay sonu KDV mahsubu. Ayın son günü 23:59 tarihli fişle 391 (hesaplanan KDV) ve 191 (indirilecek KDV) hesaplarının
// o ana kadarki bakiyeleri kapatılır; hesaplanan fazlaysa önce 190'daki devreden KDV kullanılır, kalan 360'a (ödenecek)
// yazılır; indirilecek fazlaysa fark 190'a devreder. Bakiyeler kümülatif alındığı için mahsup edilmemiş önceki aylar da
// bu fişe girer. Fiş "KDVMAHSUP_yyyy_MM" belge tipiyle işaretlenir; geri alma aynı tarihli ters fişle yapılır.
public class KdvMahsupServisi : IKdvMahsupServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;
    private readonly YevmiyeServisi _yevmiye;

    public KdvMahsupServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
        _yevmiye = new YevmiyeServisi(baglam, kullaniciServisi);
    }

    public static string BelgeTipi(int yil, int ay) => $"KDVMAHSUP_{yil:0000}_{ay:00}";
    public static DateTime FisTarihi(int yil, int ay) => new(yil, ay, DateTime.DaysInMonth(yil, ay), 23, 59, 0, DateTimeKind.Utc);
    private static bool Grup(string kod, string grup) => kod.Length >= 3 && kod[..3] == grup;

    private void Goruntule() => _kullaniciServisi?.YetkiKontrolEt(y => y.MizanGoruntule || y.MuhasebeFisGoruntule, "KDV mahsubu görüntüleme");

    private IQueryable<YevmiyeFis> GecerliMahsupFisleri(Guid sirketId) =>
        _baglam.YevmiyeFisleri.Where(f => f.SirketId == sirketId && f.BelgeTipi.StartsWith("KDVMAHSUP_") && f.TersFisId == null
            && f.IptalEdilenFisId == null && f.Durum == YevmiyeDurumu.Onaylandi);

    public async Task<List<KdvMahsupAyDto>> YilAsync(int yil, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        var onEk = $"KDVMAHSUP_{yil:0000}_";
        var fisler = await GecerliMahsupFisleri(sirketId).AsNoTracking().Where(f => f.BelgeTipi.StartsWith(onEk))
            .Select(f => new
            {
                f.BelgeTipi, f.FisNo,
                Odenecek = f.Satirlar.Where(s => s.Hesap!.HesapKodu.StartsWith("360")).Sum(s => s.Alacak - s.Borc),
                Devreden = f.Satirlar.Where(s => s.Hesap!.HesapKodu.StartsWith("190")).Sum(s => s.Borc)
            }).ToListAsync();
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        return Enumerable.Range(1, 12).Select(ay =>
        {
            var fis = fisler.FirstOrDefault(f => f.BelgeTipi == BelgeTipi(yil, ay));
            return new KdvMahsupAyDto
            {
                Ay = ay, AyAdi = tr.DateTimeFormat.GetMonthName(ay), Yapildi = fis != null, FisNo = fis?.FisNo,
                OdenecekKdv = fis?.Odenecek ?? 0m, SonrakiDonemeDevreden = fis?.Devreden ?? 0m
            };
        }).ToList();
    }

    public async Task<KdvMahsupOnizlemeDto> OnizleAsync(int yil, int ay, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        Goruntule();
        return await PlanlaAsync(yil, ay, sirketId);
    }

    private async Task<KdvMahsupOnizlemeDto> PlanlaAsync(int yil, int ay, Guid sirketId)
    {
        if (ay < 1 || ay > 12) throw new ArgumentOutOfRangeException(nameof(ay));
        var tarih = FisTarihi(yil, ay);
        var o = new KdvMahsupOnizlemeDto { Yil = yil, Ay = ay, FisTarihi = tarih };

        var mevcut = await GecerliMahsupFisleri(sirketId).AsNoTracking().Where(f => f.BelgeTipi == BelgeTipi(yil, ay)).Select(f => f.FisNo).FirstOrDefaultAsync();
        o.Yapildi = mevcut != null;
        o.FisNo = mevcut;

        if (DateTime.Today < tarih.Date)
            o.Engeller.Add($"{tarih:MMMM yyyy} henüz bitmedi. Mahsup {tarih:dd.MM.yyyy} tarihinde ya da sonra yapılabilir.");
        if (await GecerliMahsupFisleri(sirketId).AnyAsync(f => f.Tarih > tarih))
            o.Engeller.Add("Daha sonraki bir ayın KDV mahsubu yapılmış. Aylar sırayla mahsup edilir; önce sonraki ayların mahsubunu geri alın.");
        if (await _baglam.MaliDonemler.AnyAsync(m => m.SirketId == sirketId && m.Baslangic <= tarih && m.Bitis >= tarih && m.KilitliMi))
            o.Engeller.Add($"{tarih:dd.MM.yyyy} kilitli bir mali döneme düşüyor. Firma İşlemleri'nden kilidi açın.");
        var ayBasi = new DateTime(yil, ay, 1, 0, 0, 0, DateTimeKind.Utc);
        var taslak = await _baglam.YevmiyeFisleri.CountAsync(f => f.SirketId == sirketId && f.Durum == YevmiyeDurumu.Taslak && f.Tarih >= ayBasi && f.Tarih <= tarih);
        if (taslak > 0)
            o.Uyarilar.Add($"Bu ayda onaylanmamış {taslak} taslak fiş var; taslak fişlerdeki KDV mahsuba girmez.");
        var oncekiAy = ayBasi.AddMonths(-1);
        if (!o.Yapildi && !await GecerliMahsupFisleri(sirketId).AnyAsync(f => f.BelgeTipi == BelgeTipi(oncekiAy.Year, oncekiAy.Month))
            && await _baglam.YevmiyeSatirlari.AnyAsync(s => s.Fis!.SirketId == sirketId && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih < ayBasi
                && (s.Hesap!.HesapKodu.StartsWith("191") || s.Hesap.HesapKodu.StartsWith("391"))))
            o.Uyarilar.Add($"{oncekiAy:MMMM yyyy} ve öncesi mahsup edilmemiş; o aylardan kalan KDV bakiyeleri de bu fişe girer. Ayrı tutmak için önce önceki ayları mahsup edin.");

        // Fiş anına kadarki onaylı kayıtlar (yıl sonu kapanış fişi 23:59:59'da olduğu için aralık dışında kalır).
        var bakiyeler = await _baglam.YevmiyeSatirlari.AsNoTracking()
            .Where(s => s.Fis!.SirketId == sirketId && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih <= tarih
                && (s.Hesap!.HesapKodu.StartsWith("190") || s.Hesap.HesapKodu.StartsWith("191") || s.Hesap.HesapKodu.StartsWith("391")))
            .GroupBy(s => new { s.Hesap!.HesapKodu, s.Hesap.HesapAdi })
            .Select(g => new { g.Key.HesapKodu, g.Key.HesapAdi, Bakiye = g.Sum(x => x.Borc - x.Alacak) })
            .ToListAsync();
        bakiyeler = bakiyeler.Where(b => b.Bakiye != 0).OrderBy(b => b.HesapKodu, StringComparer.Ordinal).ToList();

        void Satir(string kod, string ad, decimal tutar)
        {
            if (tutar == 0) return;
            o.Satirlar.Add(new KdvMahsupSatiriDto { HesapKodu = kod, HesapAdi = ad, Borc = tutar > 0 ? tutar : 0, Alacak = tutar < 0 ? -tutar : 0 });
        }

        // 391 ve 191 hesapları bakiyelerinin tersiyle sıfırlanır.
        foreach (var b in bakiyeler.Where(b => Grup(b.HesapKodu, "391") || Grup(b.HesapKodu, "191")))
            Satir(b.HesapKodu, b.HesapAdi, -b.Bakiye);
        o.HesaplananKdv = -bakiyeler.Where(b => Grup(b.HesapKodu, "391")).Sum(b => b.Bakiye);
        o.IndirilecekKdv = bakiyeler.Where(b => Grup(b.HesapKodu, "191")).Sum(b => b.Bakiye);
        var devredenler = bakiyeler.Where(b => Grup(b.HesapKodu, "190")).ToList();
        o.OncekiDevreden = devredenler.Sum(b => b.Bakiye);
        if (devredenler.Any(b => b.Bakiye < 0))
            o.Uyarilar.Add("190 Devreden KDV hesabında alacak bakiyesi var; bu olağan dışıdır, hesabı kontrol edin.");

        var fark = o.HesaplananKdv - o.IndirilecekKdv;
        if (fark >= 0)
        {
            // Hesaplanan fazla: önce devreden KDV kullanılır (borç bakiyeli 190 hesaplarından sırayla).
            var kalan = Math.Min(Math.Max(o.OncekiDevreden, 0), fark);
            o.KullanilanDevreden = kalan;
            foreach (var d in devredenler.Where(d => d.Bakiye > 0))
            {
                if (kalan <= 0) break;
                var kullan = Math.Min(d.Bakiye, kalan);
                Satir(d.HesapKodu, d.HesapAdi, -kullan);
                kalan -= kullan;
            }
            o.OdenecekKdv = fark - o.KullanilanDevreden;
            Satir("360", "ÖDENECEK VERGİ VE FONLAR", -o.OdenecekKdv);
            o.SonrakiDonemeDevreden = o.OncekiDevreden - o.KullanilanDevreden;
        }
        else
        {
            Satir("190", "DEVREDEN KDV", -fark);
            o.SonrakiDonemeDevreden = o.OncekiDevreden - fark;
        }

        if (!o.Yapildi && o.HesaplananKdv == 0 && o.IndirilecekKdv == 0)
            o.Engeller.Add("Mahsup edilecek KDV yok: 391 ve 191 hesaplarının bakiyesi sıfır.");
        return o;
    }

    public async Task<string> MahsupEtAsync(int yil, int ay, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.MuhasebeFisEkle, "KDV mahsubu");

        await using var islem = await _baglam.Database.BeginTransactionAsync();
        await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", $"kdvmahsup:{sirketId}");
        var plan = await PlanlaAsync(yil, ay, sirketId);
        if (plan.Yapildi)
            throw new InvalidOperationException($"{plan.FisTarihi:MMMM yyyy} KDV mahsubu zaten yapılmış ({plan.FisNo}).");
        if (plan.Engeller.Count > 0)
            throw new InvalidOperationException("KDV mahsubu yapılamaz:\n- " + string.Join("\n- ", plan.Engeller));

        var satirlar = new List<YevmiyeSatiriDto>();
        foreach (var s in plan.Satirlar)
            satirlar.Add(new YevmiyeSatiriDto
            {
                HesapId = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(_baglam, sirketId, s.HesapKodu),
                Borc = s.Borc, Alacak = s.Alacak, Aciklama = $"{plan.FisTarihi:MM/yyyy} KDV mahsubu"
            });
        // Geri alınıp yeniden yapılan mahsup aynı belge tipini taşır; (firma, belge tipi, belge no) benzersiz olduğu için
        // her deneme sıra numarası alır.
        var deneme = await DenemeNoAsync(_baglam, sirketId, BelgeTipi(yil, ay));
        var fis = await _yevmiye.FisOlusturAsync(new YevmiyeFisDto
        {
            FisTuru = FisTuru.Mahsup,
            Tarih = plan.FisTarihi,
            BelgeTipi = BelgeTipi(yil, ay),
            BelgeId = deneme,
            Referans = $"KDV {ay:00}/{yil}",
            Aciklama = $"{plan.FisTarihi:MMMM yyyy} KDV mahsubu" + (plan.OdenecekKdv > 0 ? $" - ödenecek {plan.OdenecekKdv:N2} TL" : $" - devreden {plan.SonrakiDonemeDevreden:N2} TL"),
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = satirlar
        }, sirketId, 0);
        await islem.CommitAsync();
        return fis.FisNo;
    }

    internal static async Task<int> DenemeNoAsync(MuhasebeBaglami baglam, Guid sirketId, string belgeTipi) =>
        await baglam.YevmiyeFisleri.IgnoreQueryFilters().CountAsync(f => f.SirketId == sirketId && f.BelgeTipi == belgeTipi) + 1;

    public async Task GeriAlAsync(int yil, int ay, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.MuhasebeFisIptal, "KDV mahsubunu geri alma");

        await using var islem = await _baglam.Database.BeginTransactionAsync();
        await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", $"kdvmahsup:{sirketId}");
        var tarih = FisTarihi(yil, ay);
        if (await GecerliMahsupFisleri(sirketId).AnyAsync(f => f.Tarih > tarih))
            throw new InvalidOperationException("Daha sonraki bir ayın KDV mahsubu yapılmış. Önce sonraki ayların mahsubunu geri alın.");
        var fis = await GecerliMahsupFisleri(sirketId).Include(f => f.Satirlar).FirstOrDefaultAsync(f => f.BelgeTipi == BelgeTipi(yil, ay))
            ?? throw new InvalidOperationException($"{tarih:MMMM yyyy} için geri alınacak KDV mahsubu yok.");

        var ters = await _yevmiye.FisOlusturAsync(new YevmiyeFisDto
        {
            FisTuru = fis.FisTuru,
            Tarih = fis.Tarih,
            BelgeTipi = $"TERS_{fis.BelgeTipi}_{fis.Id}",
            Referans = $"Ters Fiş ({fis.FisNo})",
            Aciklama = $"KDV mahsubu geri alındı: {fis.FisNo}",
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = fis.Satirlar.Select(s => new YevmiyeSatiriDto { HesapId = s.HesapId, Borc = s.Alacak, Alacak = s.Borc, Aciklama = "Ters Kayıt: " + s.Aciklama }).ToList()
        }, sirketId, 0);
        var tersVarlik = await _baglam.YevmiyeFisleri.SingleAsync(f => f.Id == ters.Id);
        tersVarlik.IptalEdilenFisId = fis.Id;
        fis.TersFisId = ters.Id;
        await _baglam.SaveChangesAsync();
        await islem.CommitAsync();
    }
}
