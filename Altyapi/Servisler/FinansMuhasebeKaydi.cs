using Microsoft.EntityFrameworkCore;
using EtkiAlani.Modeller.Muhasebe;
using EtkiAlani.Modeller.Finans;
using Altyapi.Veri;

namespace Altyapi.Servisler;

// Kasa ve banka hareketlerinin yevmiye (muhasebe) fişini, hareketle aynı transaction içinde oluşturur.
// Böylece kasa/banka bakiyesi ile mizandaki 100/102 hesap bakiyeleri birbirini tutar.
public static class FinansMuhasebeKaydi
{
    public const string KasaBelgeTipi = "KasaHareketi";
    public const string BankaBelgeTipi = "BankaHareketi";

    // Tek Düzen Hesap Planı'ndaki standart hesaplar; şirket planında yoksa otomatik açılır.
    private static readonly Dictionary<string, (string Ad, HesapTipi Tip, BorcAlacakYonu Yon)> StandartHesaplar = new()
    {
        ["100"] = ("KASA", HesapTipi.Aktif, BorcAlacakYonu.Borc),
        ["101"] = ("ALINAN ÇEKLER", HesapTipi.Aktif, BorcAlacakYonu.Borc),
        ["102"] = ("BANKALAR", HesapTipi.Aktif, BorcAlacakYonu.Borc),
        ["103"] = ("VERİLEN ÇEKLER VE ÖDEME EMİRLERİ (-)", HesapTipi.Aktif, BorcAlacakYonu.Alacak),
        ["108"] = ("DİĞER HAZIR DEĞERLER (POS)", HesapTipi.Aktif, BorcAlacakYonu.Borc),
        ["120"] = ("ALICILAR", HesapTipi.Aktif, BorcAlacakYonu.Borc),
        ["121"] = ("ALACAK SENETLERİ", HesapTipi.Aktif, BorcAlacakYonu.Borc),
        ["197"] = ("SAYIM VE TESELLÜM NOKSANLARI", HesapTipi.Aktif, BorcAlacakYonu.Borc),
        ["300"] = ("BANKA KREDİLERİ", HesapTipi.Pasif, BorcAlacakYonu.Alacak),
        ["320"] = ("SATICILAR", HesapTipi.Pasif, BorcAlacakYonu.Alacak),
        ["321"] = ("BORÇ SENETLERİ", HesapTipi.Pasif, BorcAlacakYonu.Alacak),
        ["397"] = ("SAYIM VE TESELLÜM FAZLALARI", HesapTipi.Pasif, BorcAlacakYonu.Alacak),
        ["500"] = ("SERMAYE", HesapTipi.Pasif, BorcAlacakYonu.Alacak),
        ["602"] = ("DİĞER GELİRLER", HesapTipi.Gelir, BorcAlacakYonu.Alacak),
        ["642"] = ("FAİZ GELİRLERİ", HesapTipi.Gelir, BorcAlacakYonu.Alacak),
        ["649"] = ("DİĞER OLAĞAN GELİR VE KARLAR", HesapTipi.Gelir, BorcAlacakYonu.Alacak),
        ["653"] = ("KOMİSYON GİDERLERİ (-)", HesapTipi.Gider, BorcAlacakYonu.Borc),
        ["659"] = ("DİĞER OLAĞAN GİDER VE ZARARLAR (-)", HesapTipi.Gider, BorcAlacakYonu.Borc),
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

    // Tek bacaklı kasa hareketi: giriş için kasa borç / karşı hesap alacak, çıkış için tersi.
    public static async Task KasaHareketiFisiEkleAsync(MuhasebeBaglami baglam, KasaHareketi h, string karsiKod, int? cariKartId = null)
    {
        var kasa = h.KasaId.HasValue ? await baglam.Kasalar.AsNoTracking().FirstOrDefaultAsync(k => k.Id == h.KasaId.Value) : null;
        var kasaKodu = kasa != null ? await KasaKoduAsync(baglam, kasa) : "100";
        var giris = h.Tur == HareketTuru.Gelir;
        await FisEkleAsync(baglam, h.SirketId ?? kasa?.SirketId, KasaBelgeTipi, h.Id, h.Tarih, h.Aciklama, h.MakbuzNo ?? h.Belge ?? h.FisNo,
            giris ? new Satir(kasaKodu, h.Tutar, 0) : new Satir(karsiKod, h.Tutar, 0, cariKartId),
            giris ? new Satir(karsiKod, 0, h.Tutar, cariKartId) : new Satir(kasaKodu, 0, h.Tutar));
    }

    public static async Task BankaHareketiFisiEkleAsync(MuhasebeBaglami baglam, BankaHareketi h, string karsiKod, int? cariKartId = null)
    {
        var hesap = h.BankaHesabiId.HasValue ? await baglam.BankaHesaplari.AsNoTracking().FirstOrDefaultAsync(b => b.Id == h.BankaHesabiId.Value) : null;
        var bankaKodu = hesap != null ? await BankaKoduAsync(baglam, hesap) : "102";
        var giris = h.Tur == HareketTuru.Gelir;
        await FisEkleAsync(baglam, h.SirketId ?? hesap?.SirketId, BankaBelgeTipi, h.Id, h.Tarih, h.Aciklama, h.DekontNo ?? h.FisNo,
            giris ? new Satir(bankaKodu, h.Tutar, 0) : new Satir(karsiKod, h.Tutar, 0, cariKartId),
            giris ? new Satir(karsiKod, 0, h.Tutar, cariKartId) : new Satir(bankaKodu, 0, h.Tutar));
    }

    // İptal edilen hareketin fişini borç/alacak yer değiştirerek ters kayda yansıtır.
    // Asıl hareketin fişi yoksa (otomatik fişten önceki eski kayıt) ters kayıt da fişlenmez; ikisi birlikte mizan dışında kalır.
    public static async Task TersFisEkleAsync(MuhasebeBaglami baglam, bool kasaMi, int asilHareketId, KasaHareketi? kasaTers, BankaHareketi? bankaTers)
    {
        var belgeTipleri = kasaMi ? new[] { KasaBelgeTipi, "TahsilatOdeme" } : new[] { BankaBelgeTipi, "TahsilatOdeme" };
        var adaylar = await baglam.YevmiyeFisleri.AsNoTracking().Include(f => f.Satirlar).ThenInclude(s => s.Hesap)
            .Where(f => belgeTipleri.Contains(f.BelgeTipi) && f.BelgeId == asilHareketId && f.Durum == YevmiyeDurumu.Onaylandi)
            .ToListAsync();
        var asilFis = adaylar.FirstOrDefault(f => f.Satirlar.Any(s => s.Hesap!.HesapKodu.StartsWith(kasaMi ? "100" : "10", StringComparison.Ordinal)));

        if (asilFis == null)
            return;

        var tersId = kasaTers?.Id ?? bankaTers!.Id;
        var tarih = kasaTers?.Tarih ?? bankaTers!.Tarih;
        var aciklama = kasaTers?.Aciklama ?? bankaTers!.Aciklama;
        await FisEkleAsync(baglam, asilFis.SirketId, kasaMi ? KasaBelgeTipi : BankaBelgeTipi, tersId, tarih, aciklama, asilFis.Referans,
            asilFis.Satirlar.Select(s => new Satir(s.Hesap!.HesapKodu, s.Alacak, s.Borc, s.CariKartId)).ToArray());
    }

    // Otomatik fiş özelliğinden önce girilmiş kasa/banka hareketlerinin eksik fişlerini oluşturur.
    // Başka bir modülün (tahsilat/ödeme, transfer) zaten fişlediği hareketlere dokunmaz; tekrar çalıştırmak güvenlidir.
    public static async Task<int> EksikFisleriTamamlaAsync(MuhasebeBaglami baglam, CancellationToken cancellationToken = default)
    {
        var fisler = await baglam.YevmiyeFisleri.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.BelgeTipi == KasaBelgeTipi || f.BelgeTipi == BankaBelgeTipi || f.BelgeTipi == "TahsilatOdeme" ||
                        f.BelgeTipi == "KasaBankaTransfer" || f.BelgeTipi == "BankaKasaTransfer")
            .Select(f => new { f.BelgeTipi, f.BelgeId, KasaSatiriVar = f.Satirlar.Any(s => s.Hesap!.HesapKodu.StartsWith("100")) })
            .ToListAsync(cancellationToken);

        var kasaKapsanan = new HashSet<int>();
        var bankaKapsanan = new HashSet<int>();
        foreach (var f in fisler)
        {
            switch (f.BelgeTipi)
            {
                case KasaBelgeTipi or "KasaBankaTransfer": kasaKapsanan.Add(f.BelgeId); break;
                case BankaBelgeTipi or "BankaKasaTransfer": bankaKapsanan.Add(f.BelgeId); break;
                default: (f.KasaSatiriVar ? kasaKapsanan : bankaKapsanan).Add(f.BelgeId); break;
            }
        }

        var kasaHareketleri = await baglam.KasaHareketleri.Where(h => h.IslemDurumu == FinansIslemDurumu.Tamamlandi).OrderBy(h => h.Id).ToListAsync(cancellationToken);
        var bankaHareketleri = await baglam.BankaHareketleri.Where(h => h.IslemDurumu == FinansIslemDurumu.Tamamlandi).OrderBy(h => h.Id).ToListAsync(cancellationToken);

        // Eski Finans Merkezi transferlerinde iki bacak birbirine bağlı değildi; aynı an ve tutardaki karşı bacak da fişlidir.
        var transferAnlari = kasaHareketleri.Where(h => kasaKapsanan.Contains(h.Id)).Select(h => (h.Tarih, h.Tutar))
            .Concat(bankaHareketleri.Where(h => bankaKapsanan.Contains(h.Id)).Select(h => (h.Tarih, h.Tutar)))
            .ToHashSet();

        var olusturulan = 0;
        await using var tx = baglam.Database.CurrentTransaction == null ? await baglam.Database.BeginTransactionAsync(cancellationToken) : null;

        foreach (var h in kasaHareketleri.Where(h => !kasaKapsanan.Contains(h.Id)))
        {
            if (h.CariKartId == null && h.FisTuru is KasaFisTuru.Tahsilat or KasaFisTuru.Odeme && transferAnlari.Contains((h.Tarih, h.Tutar)))
                continue;
            if (await KilitliDonemdeMiAsync(baglam, h.Tarih, h.SirketId))
                continue;

            switch (h.FisTuru)
            {
                case KasaFisTuru.KasaVirman:
                    if (h.Tur != HareketTuru.Gider || h.TersKayitId == null) continue;
                    var hedef = kasaHareketleri.FirstOrDefault(x => x.Id == h.TersKayitId);
                    if (hedef?.KasaId == null || h.KasaId == null) continue;
                    await FisEkleAsync(baglam, h.SirketId, KasaBelgeTipi, h.Id, h.Tarih, h.Aciklama, h.FisNo,
                        new Satir(await KasaKoduAsync(baglam, await baglam.Kasalar.AsNoTracking().FirstAsync(k => k.Id == hedef.KasaId)), h.Tutar, 0),
                        new Satir(await KasaKoduAsync(baglam, await baglam.Kasalar.AsNoTracking().FirstAsync(k => k.Id == h.KasaId)), 0, h.Tutar));
                    break;
                case KasaFisTuru.BankayaYatirma:
                    var bankaBacagi = bankaHareketleri.FirstOrDefault(x => x.Id == h.TersKayitId);
                    if (bankaBacagi == null) continue;
                    await KasaHareketiFisiEkleAsync(baglam, h, await BankaHareketKoduAsync(baglam, bankaBacagi));
                    break;
                case KasaFisTuru.BankadanCekme:
                    continue; // banka bacağından fişlenir
                default:
                    var karsiKod = h.CariKartId != null
                        ? (h.Tur == HareketTuru.Gelir ? "120" : "320")
                        : h.FisTuru switch
                        {
                            KasaFisTuru.AcilisDevir => "500",
                            KasaFisTuru.SayimFarki => h.Tur == HareketTuru.Gelir ? "397" : "197",
                            _ => h.Tur == HareketTuru.Gelir ? "649" : "770"
                        };
                    await KasaHareketiFisiEkleAsync(baglam, h, karsiKod, h.CariKartId);
                    break;
            }
            olusturulan++;
        }

        foreach (var h in bankaHareketleri.Where(h => !bankaKapsanan.Contains(h.Id)))
        {
            if (h.CariKartId == null && h.FisTuru is BankaFisTuru.GelenHavale or BankaFisTuru.GidenHavale &&
                (transferAnlari.Contains((h.Tarih, h.Tutar)) || h.OdemeYontemi == OdemeYontemi.Virman))
                continue;
            if (await KilitliDonemdeMiAsync(baglam, h.Tarih, h.SirketId))
                continue;

            switch (h.FisTuru)
            {
                case BankaFisTuru.BankaVirman:
                    if (h.Tur != HareketTuru.Gider || h.TersKayitId == null) continue;
                    var hedef = bankaHareketleri.FirstOrDefault(x => x.Id == h.TersKayitId);
                    if (hedef == null) continue;
                    await BankaHareketiFisiEkleAsync(baglam, h, await BankaHareketKoduAsync(baglam, hedef));
                    break;
                case BankaFisTuru.KasadanCekme:
                    var kasaBacagi = kasaHareketleri.FirstOrDefault(x => x.Id == h.TersKayitId);
                    if (kasaBacagi?.KasaId == null) continue;
                    await BankaHareketiFisiEkleAsync(baglam, h, await KasaKoduAsync(baglam, await baglam.Kasalar.AsNoTracking().FirstAsync(k => k.Id == kasaBacagi.KasaId)));
                    break;
                case BankaFisTuru.BankayaYatirma:
                    continue; // kasa bacağından fişlenir
                case BankaFisTuru.PosHesabaGecis:
                    if (h.Tur != HareketTuru.Gider || h.TersKayitId == null) continue;
                    var vadesiz = bankaHareketleri.FirstOrDefault(x => x.Id == h.TersKayitId);
                    if (vadesiz == null) continue;
                    var satirlar = new List<Satir> { new(await BankaHareketKoduAsync(baglam, vadesiz), vadesiz.Tutar, 0) };
                    if (h.Tutar > vadesiz.Tutar) satirlar.Add(new Satir("653", h.Tutar - vadesiz.Tutar, 0));
                    satirlar.Add(new Satir(await BankaHareketKoduAsync(baglam, h), 0, h.Tutar));
                    await FisEkleAsync(baglam, h.SirketId, BankaBelgeTipi, h.Id, h.Tarih, h.Aciklama, h.FisNo, satirlar.ToArray());
                    break;
                default:
                    var karsiKod = h.CariKartId != null
                        ? (h.Tur == HareketTuru.Gelir ? "120" : "320")
                        : h.FisTuru switch
                        {
                            BankaFisTuru.BankaGelir => "642",
                            BankaFisTuru.BankaGiderMasraf => "780",
                            BankaFisTuru.KrediOdemesi => "300",
                            _ => h.Tur == HareketTuru.Gelir ? "649" : "770"
                        };
                    await BankaHareketiFisiEkleAsync(baglam, h, karsiKod, h.CariKartId);
                    break;
            }
            olusturulan++;
        }

        if (tx != null)
            await tx.CommitAsync(cancellationToken);
        return olusturulan;
    }

    private static async Task<string> BankaHareketKoduAsync(MuhasebeBaglami baglam, BankaHareketi h)
    {
        var hesap = h.BankaHesabiId.HasValue ? await baglam.BankaHesaplari.AsNoTracking().FirstOrDefaultAsync(b => b.Id == h.BankaHesabiId.Value) : null;
        return hesap != null ? await BankaKoduAsync(baglam, hesap) : "102";
    }

    private static Task<bool> KilitliDonemdeMiAsync(MuhasebeBaglami baglam, DateTime tarih, Guid? sirketId) =>
        baglam.MaliDonemler.AnyAsync(d => d.KilitliMi && d.Baslangic <= tarih && d.Bitis >= tarih && (sirketId == null || d.SirketId == sirketId));

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
            FisNo = FinansKurallari.FisNoUret(belgeTipi switch
            {
                KasaBelgeTipi => "MF-KSA",
                CariHareketServisi.CariFisBelgeTipi => "MF-CRI",
                _ => "MF-BNK"
            }),
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

    public static async Task<int> HesapGetirVeyaOlusturAsync(MuhasebeBaglami baglam, Guid sirketId, string hesapKodu)
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
