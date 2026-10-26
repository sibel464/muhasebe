using Altyapi.Veri;
using EtkiAlani.Modeller.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Muhasebe;

namespace Altyapi.Servisler;

// Tek Düzen Hesap Planı'na göre bilanço ve gelir tablosu (önceki dönemle karşılaştırmalı).
// - Raporun yılına ait yıl sonu kapanış fişleri (açılış hariç) sayılmaz; böylece 31.12 bilançosu kapanış öncesi
//   bakiyeleri, gelir tablosu da kapatılmadan önceki 6'lı hesapları gösterir.
// - 7/A maliyet hesapları (74-78 grupları) yansıtılmamışsa ilgili 62/63/66 satırına yansıtılmış gibi eklenir.
// - Bilançoda henüz 590/591'e aktarılmamış dönem sonucu "Dönem Net Kârı (Zararı)" olarak hesaplanıp gösterilir.
public class MaliTabloServisi : IMaliTabloServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public MaliTabloServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    private static readonly Dictionary<string, string> Yansitma = new() { ["74"] = "622", ["75"] = "630", ["76"] = "631", ["77"] = "632", ["78"] = "660" };

    private sealed record Grup(string Kod, string Ad, string[] Onekler);
    private sealed record Bolum(string Kod, string Ad, Grup[] Gruplar);

    private static readonly Bolum[] Aktif =
    [
        new("I", "DÖNEN VARLIKLAR",
        [
            new("A", "Hazır Değerler", ["10"]), new("B", "Menkul Kıymetler", ["11"]), new("C", "Ticari Alacaklar", ["12"]),
            new("D", "Diğer Alacaklar", ["13"]), new("E", "Stoklar", ["15"]), new("F", "Yıllara Yaygın İnşaat ve Onarım Maliyetleri", ["17"]),
            new("G", "Gelecek Aylara Ait Giderler ve Gelir Tahakkukları", ["18"]), new("H", "Diğer Dönen Varlıklar", ["19", "14", "16"])
        ]),
        new("II", "DURAN VARLIKLAR",
        [
            new("A", "Ticari Alacaklar", ["22"]), new("B", "Diğer Alacaklar", ["23"]), new("C", "Mali Duran Varlıklar", ["24"]),
            new("D", "Maddi Duran Varlıklar", ["25"]), new("E", "Maddi Olmayan Duran Varlıklar", ["26"]), new("F", "Özel Tükenmeye Tabi Varlıklar", ["27"]),
            new("G", "Gelecek Yıllara Ait Giderler ve Gelir Tahakkukları", ["28"]), new("H", "Diğer Duran Varlıklar", ["29", "20", "21"])
        ])
    ];

    private static readonly Bolum[] Pasif =
    [
        new("III", "KISA VADELİ YABANCI KAYNAKLAR",
        [
            new("A", "Mali Borçlar", ["30"]), new("B", "Ticari Borçlar", ["32"]), new("C", "Diğer Borçlar", ["33"]), new("D", "Alınan Avanslar", ["34"]),
            new("E", "Yıllara Yaygın İnşaat ve Onarım Hakedişleri", ["35"]), new("F", "Ödenecek Vergi ve Diğer Yükümlülükler", ["36"]),
            new("G", "Borç ve Gider Karşılıkları", ["37"]), new("H", "Gelecek Aylara Ait Gelirler ve Gider Tahakkukları", ["38"]),
            new("I", "Diğer Kısa Vadeli Yabancı Kaynaklar", ["39", "31"])
        ]),
        new("IV", "UZUN VADELİ YABANCI KAYNAKLAR",
        [
            new("A", "Mali Borçlar", ["40"]), new("B", "Ticari Borçlar", ["42"]), new("C", "Diğer Borçlar", ["43"]), new("D", "Alınan Avanslar", ["44"]),
            new("E", "Borç ve Gider Karşılıkları", ["47"]), new("F", "Gelecek Yıllara Ait Gelirler ve Gider Tahakkukları", ["48"]),
            new("G", "Diğer Uzun Vadeli Yabancı Kaynaklar", ["49", "41", "45", "46"])
        ]),
        new("V", "ÖZKAYNAKLAR",
        [
            new("A", "Ödenmiş Sermaye", ["50"]), new("B", "Sermaye Yedekleri", ["52"]), new("C", "Kâr Yedekleri", ["54"]),
            new("D", "Geçmiş Yıllar Kârları", ["57"]), new("E", "Geçmiş Yıllar Zararları (-)", ["58"]), new("F", "Dönem Net Kârı (Zararı)", ["59"])
        ])
    ];

    public async Task<MaliTabloDto> BilancoAsync(DateTime tarih, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        _kullaniciServisi?.YetkiKontrolEt(y => y.MizanGoruntule, "Bilanço görüntüleme");
        var onceki = new DateTime(tarih.Year - 1, 12, 31);
        var adlar = await HesapAdlariAsync(sirketId);
        var cari = await BilancoBakiyeleriAsync(sirketId, tarih);
        var once = await BilancoBakiyeleriAsync(sirketId, onceki);

        var dto = new MaliTabloDto { Baslik = "BİLANÇO", DonemMetni = $"{tarih:dd.MM.yyyy}", OncekiDonemMetni = $"{onceki:dd.MM.yyyy}" };
        var aktifToplam = Bolumler(dto, Aktif, cari.Bakiye, once.Bakiye, adlar, 1m, null);
        dto.Satirlar.Add(new MaliTabloSatiriDto { Ad = "AKTİF (VARLIKLAR) TOPLAMI", Seviye = 3, Tutar = aktifToplam.Cari, OncekiTutar = aktifToplam.Onceki });
        var pasifToplam = Bolumler(dto, Pasif, cari.Bakiye, once.Bakiye, adlar, -1m, (cari.DonemSonucu, once.DonemSonucu, cari.GecmisSonuc, once.GecmisSonuc));
        dto.Satirlar.Add(new MaliTabloSatiriDto { Ad = "PASİF (KAYNAKLAR) TOPLAMI", Seviye = 3, Tutar = pasifToplam.Cari, OncekiTutar = pasifToplam.Onceki });

        if (Math.Abs(aktifToplam.Cari - pasifToplam.Cari) >= 0.01m)
            dto.Uyarilar.Add($"Aktif ve pasif toplamı {aktifToplam.Cari - pasifToplam.Cari:N2} TL farklı. Nazım / 8'li hesaplar ya da dengesiz kayıt olabilir; mizanı kontrol edin.");
        if (cari.GecmisSonuc != 0)
            dto.Uyarilar.Add("Önceki yıllar kapatılmamış; o yılların kâr / zararı \"Geçmiş Yıllar\" satırında hesaplanarak gösterildi. Muhasebe → Yıl Sonu Kapanışı ile kapatın.");
        return dto;
    }

    public async Task<MaliTabloDto> GelirTablosuAsync(DateTime baslangic, DateTime bitis, Guid sirketId = default)
    {
        sirketId = _baglam.SirketIdCoz(sirketId);
        _kullaniciServisi?.YetkiKontrolEt(y => y.MizanGoruntule, "Gelir tablosu görüntüleme");
        var adlar = await HesapAdlariAsync(sirketId);
        var cari = GelirTablosuBakiyeleri(await HareketlerAsync(sirketId, baslangic, bitis, bitis.Year));
        var once = GelirTablosuBakiyeleri(await HareketlerAsync(sirketId, baslangic.AddYears(-1), bitis.AddYears(-1), bitis.Year - 1));
        var dto = new MaliTabloDto
        {
            Baslik = "GELİR TABLOSU",
            DonemMetni = $"{baslangic:dd.MM.yyyy} - {bitis:dd.MM.yyyy}",
            OncekiDonemMetni = $"{baslangic.AddYears(-1):dd.MM.yyyy} - {bitis.AddYears(-1):dd.MM.yyyy}"
        };

        // Gelir grupları alacak bakiyesiyle, gider grupları (-) borç bakiyesiyle pozitif gösterilir.
        (decimal Cari, decimal Onceki) Grup(string kod, string ad, string onek, bool gelir)
        {
            var isaret = gelir ? -1m : 1m;
            var hesaplar = cari.Keys.Union(once.Keys).Where(k => k.StartsWith(onek, StringComparison.Ordinal)).OrderBy(k => k).ToList();
            var c = hesaplar.Sum(k => isaret * cari.GetValueOrDefault(k));
            var o = hesaplar.Sum(k => isaret * once.GetValueOrDefault(k));
            dto.Satirlar.Add(new MaliTabloSatiriDto { Kod = kod, Ad = ad, Seviye = 1, Tutar = c, OncekiTutar = o });
            foreach (var k in hesaplar)
            {
                var hc = isaret * cari.GetValueOrDefault(k);
                var ho = isaret * once.GetValueOrDefault(k);
                if (hc != 0 || ho != 0)
                    dto.Satirlar.Add(new MaliTabloSatiriDto { Kod = k, Ad = adlar.GetValueOrDefault(k, ""), Seviye = 2, Tutar = hc, OncekiTutar = ho });
            }
            return (c, o);
        }
        void Sonuc(string ad, (decimal Cari, decimal Onceki) t) => dto.Satirlar.Add(new MaliTabloSatiriDto { Ad = ad, Seviye = 3, Tutar = t.Cari, OncekiTutar = t.Onceki });
        static (decimal, decimal) Topla((decimal C, decimal O) a, (decimal C, decimal O) b, decimal k = 1) => (a.C + k * b.C, a.O + k * b.O);

        var brut = Grup("A", "Brüt Satışlar", "60", true);
        var indirim = Grup("B", "Satış İndirimleri (-)", "61", false);
        var net = Topla(brut, indirim, -1); Sonuc("C. NET SATIŞLAR", net);
        var maliyet = Grup("D", "Satışların Maliyeti (-)", "62", false);
        var brutKar = Topla(net, maliyet, -1); Sonuc("BRÜT SATIŞ KÂRI VEYA ZARARI", brutKar);
        var faaliyet = Grup("E", "Faaliyet Giderleri (-)", "63", false);
        var faaliyetKari = Topla(brutKar, faaliyet, -1); Sonuc("FAALİYET KÂRI VEYA ZARARI", faaliyetKari);
        var olaganGelir = Grup("F", "Diğer Faaliyetlerden Olağan Gelir ve Kârlar", "64", true);
        var olaganGider = Grup("G", "Diğer Faaliyetlerden Olağan Gider ve Zararlar (-)", "65", false);
        var finansman = Grup("H", "Finansman Giderleri (-)", "66", false);
        var olaganKar = Topla(Topla(Topla(faaliyetKari, olaganGelir), olaganGider, -1), finansman, -1); Sonuc("OLAĞAN KÂR VEYA ZARAR", olaganKar);
        var disiGelir = Grup("I", "Olağandışı Gelir ve Kârlar", "67", true);
        var disiGider = Grup("J", "Olağandışı Gider ve Zararlar (-)", "68", false);
        var donemKari = Topla(Topla(olaganKar, disiGelir), disiGider, -1); Sonuc("DÖNEM KÂRI VEYA ZARARI", donemKari);
        var vergi = Grup("K", "Dönem Kârı Vergi ve Diğer Yasal Yükümlülük Karşılıkları (-)", "691", false);
        Sonuc("DÖNEM NET KÂRI VEYA ZARARI", Topla(donemKari, vergi, -1));

        var digerYedi = cari.Where(x => x.Key.StartsWith('7') && x.Value != 0).Select(x => x.Key).ToList();
        if (digerYedi.Count > 0)
            dto.Uyarilar.Add($"Şu maliyet hesapları gelir tablosuna yansıtılamadı: {string.Join(", ", digerYedi)} (üretim maliyeti hesapları).");
        return dto;
    }

    // Hesap kodunun ilk üç hanesi → net (borç - alacak). Raporun yılına ait yıl sonu kapanış fişleri (açılış hariç) sayılmaz.
    private async Task<Dictionary<string, decimal>> HareketlerAsync(Guid sirketId, DateTime? baslangic, DateTime bitis, int haricYil)
    {
        var bitisSonrasi = DateTime.SpecifyKind(bitis.Date.AddDays(1), DateTimeKind.Utc);
        var onek = $"YILSONU_{haricYil}_";
        var acilis = $"YILSONU_{haricYil}_ACILIS";
        var tersOnek = $"TERS_YILSONU_{haricYil}_";
        var sorgu = _baglam.YevmiyeSatirlari.AsNoTracking()
            .Where(s => s.Fis!.SirketId == sirketId && s.Fis.Durum == YevmiyeDurumu.Onaylandi && s.Fis.Tarih < bitisSonrasi
                && !((s.Fis.BelgeTipi.StartsWith(onek) && s.Fis.BelgeTipi != acilis) || s.Fis.BelgeTipi.StartsWith(tersOnek)));
        if (baslangic.HasValue)
        {
            var bas = DateTime.SpecifyKind(baslangic.Value.Date, DateTimeKind.Utc);
            sorgu = sorgu.Where(s => s.Fis!.Tarih >= bas);
        }
        var ham = await sorgu.GroupBy(s => s.Hesap!.HesapKodu).Select(g => new { Kod = g.Key, Net = g.Sum(x => x.Borc - x.Alacak) }).ToListAsync();
        return ham.GroupBy(x => x.Kod.Length >= 3 ? x.Kod[..3] : x.Kod).ToDictionary(g => g.Key, g => g.Sum(x => x.Net));
    }

    // 7/A gruplarının net bakiyesi ilgili 62/63/66 hesabına eklenir (yansıtma yapılmamış gibi); 7x0 ve 7x1 ayrıca gösterilmez.
    private static Dictionary<string, decimal> GelirTablosuBakiyeleri(Dictionary<string, decimal> hareketler)
    {
        var sonuc = hareketler.Where(x => x.Key.StartsWith('6') && x.Value != 0).ToDictionary(x => x.Key, x => x.Value);
        foreach (var grup in hareketler.Where(x => x.Key.StartsWith('7')).GroupBy(x => x.Key[..Math.Min(2, x.Key.Length)]))
        {
            var net = grup.Sum(x => x.Value);
            if (net == 0) continue;
            if (Yansitma.TryGetValue(grup.Key, out var hedef)) sonuc[hedef] = sonuc.GetValueOrDefault(hedef) + net;
            else foreach (var x in grup.Where(x => x.Value != 0)) sonuc[x.Key] = x.Value;
        }
        return sonuc;
    }

    private sealed record BilancoBakiyesi(Dictionary<string, decimal> Bakiye, decimal DonemSonucu, decimal GecmisSonuc);

    private async Task<BilancoBakiyesi> BilancoBakiyeleriAsync(Guid sirketId, DateTime tarih)
    {
        var tumu = await HareketlerAsync(sirketId, null, tarih, tarih.Year);
        var yilIci = await HareketlerAsync(sirketId, new DateTime(tarih.Year, 1, 1), tarih, tarih.Year);
        decimal Sonuc(Dictionary<string, decimal> d) => -d.Where(x => x.Key.StartsWith('6') || x.Key.StartsWith('7')).Sum(x => x.Value);
        var donem = Sonuc(yilIci);
        var gecmis = Sonuc(tumu) - donem;
        return new BilancoBakiyesi(tumu.Where(x => x.Key[0] is >= '1' and <= '5').ToDictionary(x => x.Key, x => x.Value), donem, gecmis);
    }

    private static (decimal Cari, decimal Onceki) Bolumler(MaliTabloDto dto, Bolum[] bolumler, Dictionary<string, decimal> cari, Dictionary<string, decimal> once,
        Dictionary<string, string> adlar, decimal isaret, (decimal Donem, decimal OncekiDonem, decimal Gecmis, decimal OncekiGecmis)? sonuc)
    {
        decimal toplamC = 0, toplamO = 0;
        foreach (var b in bolumler)
        {
            var bolumSatiri = new MaliTabloSatiriDto { Kod = b.Kod, Ad = b.Ad, Seviye = 0 };
            dto.Satirlar.Add(bolumSatiri);
            foreach (var g in b.Gruplar)
            {
                var hesaplar = cari.Keys.Union(once.Keys).Where(k => g.Onekler.Any(o => k.StartsWith(o, StringComparison.Ordinal))).OrderBy(k => k).ToList();
                var satirlar = hesaplar.Select(k => new MaliTabloSatiriDto { Kod = k, Ad = adlar.GetValueOrDefault(k, ""), Seviye = 2, Tutar = isaret * cari.GetValueOrDefault(k), OncekiTutar = isaret * once.GetValueOrDefault(k) })
                    .Where(s => s.Tutar != 0 || s.OncekiTutar != 0).ToList();
                // Henüz 590/591'e aktarılmamış dönem sonucu ile kapatılmamış önceki yılların sonucu hesaplanarak eklenir.
                if (sonuc is { } s && g.Onekler.Contains("59") && (s.Donem != 0 || s.OncekiDonem != 0))
                    satirlar.Add(new MaliTabloSatiriDto { Ad = "Dönem sonucu (henüz aktarılmamış)", Seviye = 2, Tutar = s.Donem, OncekiTutar = s.OncekiDonem });
                if (sonuc is { } s2 && g.Onekler.Contains("57") && (s2.Gecmis != 0 || s2.OncekiGecmis != 0))
                    satirlar.Add(new MaliTabloSatiriDto { Ad = "Kapatılmamış önceki yıllar sonucu", Seviye = 2, Tutar = s2.Gecmis, OncekiTutar = s2.OncekiGecmis });
                if (satirlar.Count == 0) continue;
                var grupSatiri = new MaliTabloSatiriDto { Kod = g.Kod, Ad = g.Ad, Seviye = 1, Tutar = satirlar.Sum(x => x.Tutar), OncekiTutar = satirlar.Sum(x => x.OncekiTutar) };
                dto.Satirlar.Add(grupSatiri);
                dto.Satirlar.AddRange(satirlar);
                bolumSatiri.Tutar += grupSatiri.Tutar;
                bolumSatiri.OncekiTutar += grupSatiri.OncekiTutar;
            }
            toplamC += bolumSatiri.Tutar;
            toplamO += bolumSatiri.OncekiTutar;
        }
        return (toplamC, toplamO);
    }

    // Üç haneli ana hesap adları; ana hesap tanımlı değilse o gruptaki ilk alt hesabın adı kullanılır.
    private async Task<Dictionary<string, string>> HesapAdlariAsync(Guid sirketId)
    {
        var hesaplar = await _baglam.HesapPlanlari.IgnoreQueryFilters().AsNoTracking().Where(h => h.SirketId == sirketId)
            .Select(h => new { h.HesapKodu, h.HesapAdi }).ToListAsync();
        return hesaplar.GroupBy(h => h.HesapKodu.Length >= 3 ? h.HesapKodu[..3] : h.HesapKodu)
            .ToDictionary(g => g.Key, g => (g.FirstOrDefault(x => x.HesapKodu == g.Key) ?? g.OrderBy(x => x.HesapKodu).First()).HesapAdi);
    }
}
