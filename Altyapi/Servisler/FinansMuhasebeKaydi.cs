using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Muhasebe;
using Altyapi.Veri;

namespace Altyapi.Servisler;

// Kasa ve banka hareketlerinin yevmiye (muhasebe) fişini, hareketle aynı transaction içinde oluşturur.
// Böylece kasa/banka bakiyesi ile mizandaki 100/102 hesap bakiyeleri birbirini tutar.
internal static class FinansMuhasebeKaydi
{
    public const string KasaBelgeTipi = "KasaHareketi";
    public const string BankaBelgeTipi = "BankaHareketi";

    // Tek Düzen Hesap Planı'ndaki standart hesaplar; şirket planında yoksa otomatik açılır.
    private static readonly Dictionary<string, (string Ad, HesapTipi Tip, BorcAlacakYonu Yon)> StandartHesaplar = new()
    {
        ["100"] = ("KASA", HesapTipi.Aktif, BorcAlacakYonu.Borc),
        ["102"] = ("BANKALAR", HesapTipi.Aktif, BorcAlacakYonu.Borc),
        ["108"] = ("DİĞER HAZIR DEĞERLER (POS)", HesapTipi.Aktif, BorcAlacakYonu.Borc),
        ["120"] = ("ALICILAR", HesapTipi.Aktif, BorcAlacakYonu.Borc),
        ["197"] = ("SAYIM VE TESELLÜM NOKSANLARI", HesapTipi.Aktif, BorcAlacakYonu.Borc),
        ["300"] = ("BANKA KREDİLERİ", HesapTipi.Pasif, BorcAlacakYonu.Alacak),
        ["320"] = ("SATICILAR", HesapTipi.Pasif, BorcAlacakYonu.Alacak),
        ["397"] = ("SAYIM VE TESELLÜM FAZLALARI", HesapTipi.Pasif, BorcAlacakYonu.Alacak),
        ["500"] = ("SERMAYE", HesapTipi.Pasif, BorcAlacakYonu.Alacak),
        ["642"] = ("FAİZ GELİRLERİ", HesapTipi.Gelir, BorcAlacakYonu.Alacak),
        ["649"] = ("DİĞER OLAĞAN GELİR VE KARLAR", HesapTipi.Gelir, BorcAlacakYonu.Alacak),
        ["653"] = ("KOMİSYON GİDERLERİ (-)", HesapTipi.Gider, BorcAlacakYonu.Borc),
        ["770"] = ("GENEL YÖNETİM GİDERLERİ", HesapTipi.Gider, BorcAlacakYonu.Borc),
        ["780"] = ("FİNANSMAN GİDERLERİ", HesapTipi.Gider, BorcAlacakYonu.Borc),
    };

    public sealed record Satir(string HesapKodu, decimal Borc, decimal Alacak, int? CariKartId = null);

    // Kasa/banka tanımındaki alt hesap (örn. 100.01.001) planda varsa o, yoksa ana hesap kullanılır.
    public static async Task<string> HesapKoduSecAsync(MuhasebeBaglami baglam, Guid sirketId, string? tanimliKod, string anaKod)
    {
        if (!string.IsNullOrWhiteSpace(tanimliKod) && tanimliKod != anaKod && tanimliKod.StartsWith(anaKod, StringComparison.Ordinal) &&
            await baglam.HesapPlanlari.AnyAsync(h => h.SirketId == sirketId && h.HesapKodu == tanimliKod && h.AktifMi))
            return tanimliKod;
        return anaKod;
    }

    public static Task<string> KasaKoduAsync(MuhasebeBaglami baglam, EtkiAlani.Modeller.Finans.Kasa kasa) =>
        HesapKoduSecAsync(baglam, kasa.SirketId ?? MuhasebeBaglami.VarsayilanSirketId, kasa.MuhasebeKodu, "100");

    public static Task<string> BankaKoduAsync(MuhasebeBaglami baglam, EtkiAlani.Modeller.Finans.BankaHesabi hesap) =>
        HesapKoduSecAsync(baglam, hesap.SirketId ?? MuhasebeBaglami.VarsayilanSirketId, hesap.MuhasebeKodu, hesap.HesapTuru switch
        {
            EtkiAlani.Modeller.Finans.BankaHesapTuru.POSHesabi => "108",
            EtkiAlani.Modeller.Finans.BankaHesapTuru.KrediHesabi => "300",
            _ => "102"
        });

    public static async Task FisEkleAsync(MuhasebeBaglami baglam, Guid? sirketId, string belgeTipi, int belgeId,
        DateTime tarih, string aciklama, string? belgeNo, params Satir[] satirlar)
    {
        var sirket = sirketId ?? MuhasebeBaglami.VarsayilanSirketId;

        // Aynı hesaba borç ve alacak (örn. iki kasa da 100'e bağlıysa virman) muhasebede etkisizdir.
        var gruplar = satirlar.GroupBy(s => (s.HesapKodu, s.CariKartId)).ToList();
        if (gruplar.All(g => g.Sum(s => s.Borc) == g.Sum(s => s.Alacak)))
            return;

        if (satirlar.Sum(s => s.Borc) != satirlar.Sum(s => s.Alacak))
            throw new InvalidOperationException("Muhasebe fişi dengesiz: borç ve alacak toplamı eşit değil.");

        var hesapIdleri = new Dictionary<string, int>();
        foreach (var kod in satirlar.Select(s => s.HesapKodu).Distinct())
            hesapIdleri[kod] = await HesapGetirVeyaOlusturAsync(baglam, sirket, kod);

        var temizAciklama = aciklama.Length > 500 ? aciklama[..500] : aciklama;
        baglam.YevmiyeFisleri.Add(new YevmiyeFis
        {
            SirketId = sirket,
            FisNo = FinansKurallari.FisNoUret(belgeTipi == KasaBelgeTipi ? "MF-KSA" : "MF-BNK"),
            FisTuru = FisTuru.Mahsup,
            Tarih = tarih,
            BelgeTipi = belgeTipi,
            BelgeId = belgeId,
            Referans = belgeNo ?? string.Empty,
            Aciklama = temizAciklama,
            Durum = YevmiyeDurumu.Onaylandi,
            Satirlar = satirlar.Select(s => new YevmiyeSatiri
            {
                HesapId = hesapIdleri[s.HesapKodu],
                Borc = s.Borc,
                Alacak = s.Alacak,
                Aciklama = temizAciklama,
                BelgeNo = belgeNo ?? string.Empty,
                CariKartId = s.CariKartId
            }).ToList()
        });
        await baglam.SaveChangesAsync();
    }

    // Silinen kasa/banka hareketlerinin fişleri de silinir; mizan hareketle birlikte geri döner.
    public static async Task FisleriSilAsync(MuhasebeBaglami baglam, IEnumerable<int> kasaHareketIdleri, IEnumerable<int> bankaHareketIdleri, DateTime simdi, string neden)
    {
        var kasaIdleri = kasaHareketIdleri.ToList();
        var bankaIdleri = bankaHareketIdleri.ToList();
        var fisler = await baglam.YevmiyeFisleri.Include(f => f.Satirlar)
            .Where(f => (f.BelgeTipi == KasaBelgeTipi && kasaIdleri.Contains(f.BelgeId)) ||
                        (f.BelgeTipi == BankaBelgeTipi && bankaIdleri.Contains(f.BelgeId)))
            .ToListAsync();
        foreach (var fis in fisler)
        {
            foreach (var kayit in fis.Satirlar.Cast<EtkiAlani.Modeller.Ortak.TemelVarlik>().Append(fis))
            {
                kayit.Silindi = true;
                kayit.SilinmeTarihi = simdi;
                kayit.SilinmeNedeni = neden;
                kayit.GuncellemeTarihi = simdi;
            }
        }
    }

    private static async Task<int> HesapGetirVeyaOlusturAsync(MuhasebeBaglami baglam, Guid sirketId, string hesapKodu)
    {
        var hesap = await baglam.HesapPlanlari.IgnoreQueryFilters()
            .FirstOrDefaultAsync(h => h.SirketId == sirketId && h.HesapKodu == hesapKodu);
        if (hesap != null)
        {
            if (hesap.Silindi || !hesap.AktifMi)
                throw new InvalidOperationException($"{hesapKodu} muhasebe hesabı pasif veya silinmiş. Hesap planından aktif hale getirin.");
            return hesap.Id;
        }

        if (!StandartHesaplar.TryGetValue(hesapKodu, out var standart))
            throw new InvalidOperationException($"{hesapKodu} muhasebe hesabı hesap planında bulunamadı.");

        var yeni = new HesapPlani
        {
            SirketId = sirketId,
            HesapKodu = hesapKodu,
            HesapAdi = standart.Ad,
            Tip = standart.Tip,
            BakiyeYonu = standart.Yon,
            Seviye = 1,
            ParaBirimiKodu = "TRY",
            SistemHesabi = true,
            Aciklama = "Kasa/banka işlemi için otomatik açıldı",
            AktifMi = true,
            OlusturmaTarihi = DateTime.UtcNow
        };
        baglam.HesapPlanlari.Add(yeni);
        await baglam.SaveChangesAsync();
        return yeni.Id;
    }
}
