using Altyapi.Veri;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;

namespace Altyapi.Servisler;

// Yıl sonu kapanışı (Tek Düzen Hesap Planı, 7/A):
//  1) Yansıtma: 74-78 gider grupları net bakiyesiyle 622/630/631/632/660'a aktarılır, 7xx ve 7x1 karşılıklı kapanır.
//  2) Gelir tablosu: 60-68 hesapları 690'a kapatılır.
//  3) Kâr aktarımı: 690 ve 691 (vergi karşılığı) 692'ye, 692 kârsa 590'a, zararsa 591'e aktarılır.
//  4) Kapanış fişi (31.12) bilanço hesaplarını sıfırlar, açılış fişi (1 Ocak) aynı bakiyeleri yeni yıla devreder.
// Fişler "YILSONU_{yıl}_..." belge tipiyle işaretlenir; geri alma aynı tarihli ters fişlerle yapılır.
public class YilSonuServisi : IYilSonuServisi
{
    private static readonly Dictionary<string, string> YansitmaHedefi = new()
    {
        ["74"] = "622", ["75"] = "630", ["76"] = "631", ["77"] = "632", ["78"] = "660"
    };

    private static readonly (string Ek, FisTuru Tur, string Aciklama)[] FisSirasi =
    [
        ("YANSITMA", FisTuru.Mahsup, "Yıl sonu gider yansıtma kayıtları"),
        ("GELIRTABLOSU", FisTuru.Mahsup, "Gelir tablosu hesaplarının 690'a kapatılması"),
        ("KARAKTARIM", FisTuru.Mahsup, "Dönem kâr / zararının bilançoya aktarılması"),
        ("KAPANIS", FisTuru.Kapanis, "Yıl sonu kapanış fişi"),
        ("ACILIS", FisTuru.Acilis, "Açılış fişi")
    ];

    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;
    private readonly YevmiyeServisi _yevmiye;

    public YilSonuServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
        _yevmiye = new YevmiyeServisi(baglam, kullaniciServisi);
    }

    private sealed record PlanSatiri(string HesapKodu, decimal Borc, decimal Alacak, string Aciklama);

    private sealed class Plan
    {
        public YilSonuOnizlemeDto Onizleme { get; } = new();
        public List<PlanSatiri>[] Fisler { get; } = Enumerable.Range(0, FisSirasi.Length).Select(_ => new List<PlanSatiri>()).ToArray();
    }

    private static string BelgeTipi(int yil, string ek) => $"YILSONU_{yil}_{ek}";
    private static DateTime KapanisTarihi(int yil) => new(yil, 12, 31, 23, 59, 59, DateTimeKind.Utc);
    private static DateTime AcilisTarihi(int yil) => new(yil + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task<YilSonuOnizlemeDto> OnizleAsync(int yil, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        _kullaniciServisi?.YetkiKontrolEt(y => y.MizanGoruntule, "Yıl sonu kapanışı önizleme");
        return (await PlanlaAsync(yil, sirketId)).Onizleme;
    }

    public async Task<YilSonuSonucDto> KapatAsync(int yil, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.MuhasebeFisEkle, "Yıl sonu kapanışı");

        await using var islem = await _baglam.Database.BeginTransactionAsync();
        // Aynı firmada iki kapanışın aynı anda çalışmasını engeller.
        await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", $"yilsonu:{sirketId}");
        var plan = await PlanlaAsync(yil, sirketId);
        if (plan.Onizleme.Kapatildi)
            throw new InvalidOperationException($"{yil} yılı zaten kapatılmış.");
        if (plan.Onizleme.Engeller.Count > 0)
            throw new InvalidOperationException("Yıl sonu kapanışı yapılamaz:\n- " + string.Join("\n- ", plan.Onizleme.Engeller));

        var sonuc = new YilSonuSonucDto { Yil = yil, NetKar = plan.Onizleme.NetKar };
        for (int i = 0; i < FisSirasi.Length; i++)
        {
            if (plan.Fisler[i].Count == 0) continue;
            var (ek, tur, aciklama) = FisSirasi[i];
            var satirlar = new List<YevmiyeSatiriDto>();
            foreach (var s in plan.Fisler[i])
                satirlar.Add(new YevmiyeSatiriDto
                {
                    HesapId = await FinansMuhasebeKaydi.HesapGetirVeyaOlusturAsync(_baglam, sirketId, s.HesapKodu),
                    Borc = s.Borc, Alacak = s.Alacak, Aciklama = s.Aciklama
                });
            var fis = await _yevmiye.FisOlusturAsync(new YevmiyeFisDto
            {
                FisTuru = tur,
                Tarih = ek == "ACILIS" ? AcilisTarihi(yil) : KapanisTarihi(yil),
                BelgeTipi = BelgeTipi(yil, ek),
                Referans = $"{yil} yıl sonu",
                Aciklama = ek == "ACILIS" ? $"{yil + 1} açılış fişi ({yil} kapanışından devir)" : $"{yil} - {aciklama}",
                Durum = YevmiyeDurumu.Onaylandi,
                Satirlar = satirlar
            }, sirketId, 0);
            sonuc.FisNolari.Add(fis.FisNo);
        }
        await islem.CommitAsync();
        return sonuc;
    }

    public async Task GeriAlAsync(int yil, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(y => y.MuhasebeFisIptal, "Yıl sonu kapanışını geri alma");

        await using var islem = await _baglam.Database.BeginTransactionAsync();
        await _baglam.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", $"yilsonu:{sirketId}");
        if (await _baglam.YevmiyeFisleri.AnyAsync(f => f.SirketId == sirketId && f.BelgeTipi == BelgeTipi(yil + 1, "KAPANIS") && f.TersFisId == null))
            throw new InvalidOperationException($"Önce {yil + 1} yılının kapanışını geri alın.");

        var tipler = FisSirasi.Select(f => BelgeTipi(yil, f.Ek)).ToArray();
        var fisler = await _baglam.YevmiyeFisleri.Include(f => f.Satirlar)
            .Where(f => f.SirketId == sirketId && tipler.Contains(f.BelgeTipi) && f.TersFisId == null && f.IptalEdilenFisId == null && f.Durum == YevmiyeDurumu.Onaylandi)
            .OrderByDescending(f => f.Id).ToListAsync();
        if (fisler.Count == 0)
            throw new InvalidOperationException($"{yil} yılı için geri alınacak kapanış kaydı yok.");

        // Ters fişler asıl fişin tarihini taşır; bugünün tarihiyle kesilseler kapanış bir yıl, geri alması başka yıl görünürdü.
        foreach (var fis in fisler)
        {
            var ters = await _yevmiye.FisOlusturAsync(new YevmiyeFisDto
            {
                FisTuru = fis.FisTuru,
                Tarih = fis.Tarih,
                BelgeTipi = $"TERS_{fis.BelgeTipi}_{fis.Id}",
                Referans = $"Ters Fiş ({fis.FisNo})",
                Aciklama = $"Yıl sonu kapanışı geri alındı: {fis.FisNo}",
                Durum = YevmiyeDurumu.Onaylandi,
                Satirlar = fis.Satirlar.Select(s => new YevmiyeSatiriDto { HesapId = s.HesapId, Borc = s.Alacak, Alacak = s.Borc, Aciklama = "Ters Kayıt: " + s.Aciklama }).ToList()
            }, sirketId, 0);
            var tersVarlik = await _baglam.YevmiyeFisleri.SingleAsync(f => f.Id == ters.Id);
            tersVarlik.IptalEdilenFisId = fis.Id;
            fis.TersFisId = ters.Id;
            await _baglam.SaveChangesAsync();
        }
        await islem.CommitAsync();
    }

    private async Task<Plan> PlanlaAsync(int yil, Guid sirketId)
    {
        var plan = new Plan();
        var o = plan.Onizleme;
        o.Yil = yil;
        var yilBasi = new DateTime(yil, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yilSonrasi = AcilisTarihi(yil);

        o.Kapatildi = await _baglam.YevmiyeFisleri.AnyAsync(f => f.SirketId == sirketId && f.BelgeTipi == BelgeTipi(yil, "KAPANIS") && f.TersFisId == null);

        if (DateTime.Today < new DateTime(yil, 12, 31))
            o.Engeller.Add($"{yil} yılı henüz bitmedi. Kapanış 31.12.{yil} tarihinden sonra yapılabilir.");
        var taslak = await _baglam.YevmiyeFisleri.CountAsync(f => f.SirketId == sirketId && f.Durum == YevmiyeDurumu.Taslak && f.Tarih >= yilBasi && f.Tarih < yilSonrasi);
        if (taslak > 0)
            o.Engeller.Add($"{yil} yılında onaylanmamış {taslak} taslak fiş var. Kapanıştan önce onaylayın ya da iptal edin.");
        foreach (var tarih in new[] { KapanisTarihi(yil), AcilisTarihi(yil) })
            if (await _baglam.MaliDonemler.AnyAsync(m => m.SirketId == sirketId && m.Baslangic <= tarih && m.Bitis >= tarih && m.KilitliMi))
                o.Engeller.Add($"{tarih:dd.MM.yyyy} tarihi kilitli bir mali döneme düşüyor. Firma İşlemleri'nden kilidi açın.");
        if (yil > 2000 && !await _baglam.YevmiyeFisleri.AnyAsync(f => f.SirketId == sirketId && f.BelgeTipi == BelgeTipi(yil - 1, "KAPANIS") && f.TersFisId == null))
        {
            var oncekiGelirGider = await _baglam.YevmiyeSatirlari.AnyAsync(s => s.Fis!.SirketId == sirketId && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih < yilBasi
                && (s.Hesap!.HesapKodu.StartsWith("6") || s.Hesap.HesapKodu.StartsWith("7")));
            if (oncekiGelirGider)
                o.Uyarilar.Add($"{yil - 1} ve önceki yıllar kapatılmamış. O yıllara ait gelir-gider bakiyeleri de bu kapanışa dahil edilecek; ayrı tutmak için önce önceki yılları kapatın.");
        }

        var hesaplar = await _baglam.HesapPlanlari.IgnoreQueryFilters().AsNoTracking()
            .Where(h => h.SirketId == sirketId)
            .Select(h => new { h.Id, h.HesapKodu, h.HesapAdi, h.AktifMi, h.Silindi })
            .ToDictionaryAsync(h => h.Id);
        var hamBakiyeler = await _baglam.YevmiyeSatirlari.AsNoTracking()
            .Where(s => s.Fis!.SirketId == sirketId && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih < yilSonrasi)
            .GroupBy(s => s.HesapId)
            .Select(g => new { HesapId = g.Key, Bakiye = g.Sum(x => x.Borc - x.Alacak) })
            .ToListAsync();

        // Bakiye: borç - alacak, hesap koduna göre (kod firmada benzersiz).
        var bakiye = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var b in hamBakiyeler.Where(b => b.Bakiye != 0))
        {
            if (!hesaplar.TryGetValue(b.HesapId, out var h)) continue;
            if (h.Silindi || !h.AktifMi)
                o.Engeller.Add($"{h.HesapKodu} {h.HesapAdi} hesabı pasif veya silinmiş ama {Math.Abs(b.Bakiye):N2} TL bakiyesi var. Hesap planından aktif hale getirin.");
            bakiye[h.HesapKodu] = b.Bakiye;
        }
        decimal Bakiye(string kod) => bakiye.TryGetValue(kod, out var v) ? v : 0m;

        void Ekle(List<PlanSatiri> fis, string kod, decimal tutar, string aciklama)
        {
            if (tutar == 0) return;
            fis.Add(tutar > 0 ? new PlanSatiri(kod, tutar, 0, aciklama) : new PlanSatiri(kod, 0, -tutar, aciklama));
            bakiye[kod] = Bakiye(kod) + tutar;
        }

        // 1) Yansıtma
        var desteksiz = bakiye.Where(x => x.Key.StartsWith('7') && x.Value != 0 && !YansitmaHedefi.ContainsKey(x.Key.Length >= 2 ? x.Key[..2] : x.Key)).Select(x => x.Key).ToList();
        if (desteksiz.Count > 0)
            o.Engeller.Add($"Şu maliyet hesaplarının yansıtması otomatik yapılamıyor: {string.Join(", ", desteksiz)}. Bu hesapları 31.12 tarihli fişle elle kapatın, sonra tekrar deneyin.");
        foreach (var (grup, hedef) in YansitmaHedefi)
        {
            var grupHesaplari = bakiye.Where(x => x.Key.StartsWith(grup, StringComparison.Ordinal) && x.Value != 0).ToList();
            if (grupHesaplari.Count == 0) continue;
            var net = grupHesaplari.Sum(x => x.Value);
            var yansitma = grup + "1";
            if (net != 0)
            {
                o.Yansitmalar.Add(new YansitmaKalemiDto { GiderHesabi = grup + "0", YansitmaHesabi = yansitma, HedefHesap = hedef, Tutar = net });
                Ekle(plan.Fisler[0], hedef, net, $"{grup}0 grubunun yansıtılması");
                Ekle(plan.Fisler[0], yansitma, -net, $"{grup}0 grubunun yansıtılması");
            }
            foreach (var (kod, _) in bakiye.Where(x => x.Key.StartsWith(grup, StringComparison.Ordinal) && x.Value != 0).ToList())
                Ekle(plan.Fisler[0], kod, -Bakiye(kod), "Yansıtma hesabıyla karşılıklı kapatma");
        }

        // 2) Gelir tablosu → 690
        var gelirTablosu = bakiye.Where(x => x.Key.StartsWith('6') && !x.Key.StartsWith("69", StringComparison.Ordinal) && x.Value != 0).ToList();
        o.GelirToplami = -gelirTablosu.Where(x => x.Value < 0).Sum(x => x.Value);
        o.GiderToplami = gelirTablosu.Where(x => x.Value > 0).Sum(x => x.Value);
        foreach (var (kod, b) in gelirTablosu)
            Ekle(plan.Fisler[1], kod, -b, "690 Dönem kâr / zararına aktarım");
        Ekle(plan.Fisler[1], "690", gelirTablosu.Sum(x => x.Value), "Gelir tablosu hesaplarından");

        // 3) 690 + 691 → 692 → 590 / 591 (692 iki yönde de hareket görür; satırlar birleştirilmez)
        o.VergiKarsiligi = Bakiye("691");
        var b690 = Bakiye("690");
        var b691 = Bakiye("691");
        Ekle(plan.Fisler[2], "690", -b690, "692'ye aktarım");
        Ekle(plan.Fisler[2], "691", -b691, "692'ye aktarım");
        Ekle(plan.Fisler[2], "692", b690 + b691, "690 / 691'den");
        var b692 = Bakiye("692");
        if (b692 != 0)
        {
            Ekle(plan.Fisler[2], "692", -b692, b692 < 0 ? "590 Dönem net kârına aktarım" : "591 Dönem net zararına aktarım");
            Ekle(plan.Fisler[2], b692 < 0 ? "590" : "591", b692, "692 Dönem net kâr / zararından");
        }

        // 4) Kapanış (bilanço hesapları) ve açılış
        var bilanco = bakiye.Where(x => !x.Key.StartsWith('6') && !x.Key.StartsWith('7') && x.Value != 0).ToList();
        if (Math.Abs(bilanco.Sum(x => x.Value)) >= 0.005m)
            o.Engeller.Add($"Defter dengesiz görünüyor (fark {bilanco.Sum(x => x.Value):N2} TL). Mizanı kontrol edin.");
        o.BilancoHesapSayisi = bilanco.Count;
        foreach (var (kod, b) in bilanco)
        {
            Ekle(plan.Fisler[3], kod, -b, "Kapanış");
            plan.Fisler[4].Add(b > 0 ? new PlanSatiri(kod, b, 0, "Açılış (devir)") : new PlanSatiri(kod, 0, -b, "Açılış (devir)"));
        }

        if (o.NetKar > 0 && o.VergiKarsiligi == 0)
            o.Uyarilar.Add("Vergi karşılığı (691) girilmemiş. Kâr üzerinden kurumlar / gelir vergisi karşılığı ayrılacaksa kapanıştan önce 31.12 tarihli fişle girin (691 borç / 370 alacak).");
        if (bakiye.Count == 0 || (gelirTablosu.Count == 0 && bilanco.Count == 0))
            o.Engeller.Add($"{yil} yılı sonuna kadar onaylı muhasebe kaydı yok; kapatılacak bakiye bulunmuyor.");
        return plan;
    }
}
