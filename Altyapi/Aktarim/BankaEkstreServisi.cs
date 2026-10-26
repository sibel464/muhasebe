using System.Text.RegularExpressions;
using Altyapi.Veri;
using EtkiAlani.Modeller.Finans;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Uygulama.Dto.Finans360;

namespace Altyapi.Aktarim;

// Ekstre satırlarını programdaki kayıtlarla eşleştirir ve seçilenleri banka hareketi olarak kaydeder.
//  - Aynı hesapta aynı gün, aynı yönde ve tutarda hareket varsa satır "zaten kayıtlı" sayılır (her kayıt bir kez eşleşir).
//  - Cari, açıklamadaki vergi / T.C. kimlik no, IBAN, fatura no ya da ünvan kelimelerinden tahmin edilir.
//  - Cari bulunamayan satırlarda masraf / faiz / kredi anahtar kelimeleri işlem türünü önerir; gerisi kullanıcıya kalır.
public class BankaEkstreServisi : IBankaEkstreServisi
{
    private static readonly HashSet<string> UnvanEkleri =
    [
        "LTD", "STI", "SIRKETI", "SIRKET", "AS", "A", "S", "SAN", "SANAYI", "TIC", "TICARET", "VE", "INS", "INSAAT", "LIMITED", "ANONIM",
        "PAZ", "PAZARLAMA", "ITH", "IHR", "ITHALAT", "IHRACAT", "DIS", "GIDA", "TUR", "OTO", "HIZ", "HIZMETLERI", "ORG", "LTDSTI", "KOLL", "KOLLEKTIF"
    ];
    private static readonly string[] MasrafKelimeleri = ["MASRAF", "KOMISYON", "BSMV", "UCRET", "UCRETI", "KESINTI", "AIDAT", "SIGORTA PRIMI", "KART AIDATI"];
    private static readonly string[] FaizKelimeleri = ["FAIZ", "NEMA", "GETIRI"];
    private static readonly string[] KrediKelimeleri = ["KREDI TAKSIT", "KREDI ODEME", "TAKSIT TAHSILATI", "KREDI ANAPARA"];

    private readonly MuhasebeBaglami _baglam;
    private readonly IBankaServisi _banka;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public BankaEkstreServisi(MuhasebeBaglami baglam, IBankaServisi banka, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _banka = banka;
        _kullaniciServisi = kullaniciServisi;
    }

    public EkstreOkumaSonucuDto Oku(string dosyaYolu) => BankaEkstreOkuyucu.Oku(dosyaYolu);

    public async Task<List<EkstreEslesmeDto>> EslestirAsync(int bankaHesabiId, IReadOnlyList<EkstreSatiriDto> satirlar)
    {
        _kullaniciServisi?.YetkiKontrolEt(y => y.BankaGoruntule, "Banka ekstresi eşleştirme");
        var hesap = await _baglam.BankaHesaplari.AsNoTracking().FirstOrDefaultAsync(h => h.Id == bankaHesabiId)
            ?? throw new KeyNotFoundException("Banka hesabı bulunamadı.");
        if (!string.Equals(hesap.ParaBirimi, "TRY", StringComparison.OrdinalIgnoreCase) && !string.Equals(hesap.ParaBirimi, "TL", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Döviz hesabının ekstresi aktarılamaz; yalnız TL hesaplar desteklenir.");
        var sirketId = hesap.SirketId;
        if (satirlar.Count == 0) return new();

        var bas = DateTime.SpecifyKind(satirlar.Min(s => s.Tarih).Date.AddDays(-1), DateTimeKind.Utc);
        var bit = DateTime.SpecifyKind(satirlar.Max(s => s.Tarih).Date.AddDays(2), DateTimeKind.Utc);
        var mevcut = await _baglam.BankaHareketleri.AsNoTracking()
            .Where(h => h.BankaHesabiId == bankaHesabiId && h.Tarih >= bas && h.Tarih < bit && h.IslemDurumu == FinansIslemDurumu.Tamamlandi && h.TersKayitId == null)
            .Select(h => new { h.Id, h.Tarih, h.Tutar, h.Tur, h.DekontNo })
            .ToListAsync();
        var kullanilan = new HashSet<int>();

        var cariler = await _baglam.CariKartlar.AsNoTracking().Where(c => c.SirketId == sirketId && c.Aktif)
            .Select(c => new { c.Id, c.Unvan, c.VergiNo, c.TcNo, c.Iban }).ToListAsync();
        var cariAnahtar = cariler.Select(c => new
        {
            c.Id, c.Unvan,
            Kimlik = new[] { c.VergiNo, c.TcNo }.Select(x => Regex.Replace(x ?? "", @"\D", "")).Where(x => x.Length is 10 or 11).ToArray(),
            Iban = BankaEkstreOkuyucu.Sadelestir(c.Iban).Replace(" ", ""),
            Kelimeler = BankaEkstreOkuyucu.Sadelestir(c.Unvan).Split(' ').Where(k => k.Length >= 3 && !UnvanEkleri.Contains(k)).Take(3).ToArray()
        }).ToList();
        var faturalar = await _baglam.Faturalar.AsNoTracking().Where(f => f.SirketId == sirketId && f.FaturaNo.Length >= 5)
            .OrderByDescending(f => f.Tarih).Take(5000).Select(f => new { f.FaturaNo, f.CariKartId }).ToListAsync();

        var sonuc = new List<EkstreEslesmeDto>();
        foreach (var s in satirlar)
        {
            var e = new EkstreEslesmeDto { Satir = s };
            var giris = s.Tutar > 0;
            var tutar = Math.Abs(s.Tutar);
            var kayitli = mevcut.Where(m => !kullanilan.Contains(m.Id) && m.Tutar == tutar && (m.Tur == HareketTuru.Gelir) == giris && Math.Abs((m.Tarih.Date - s.Tarih.Date).TotalDays) <= 1)
                .OrderBy(m => string.Equals(m.DekontNo, s.DekontNo, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(m => Math.Abs((m.Tarih.Date - s.Tarih.Date).TotalDays)).FirstOrDefault();
            if (kayitli != null)
            {
                kullanilan.Add(kayitli.Id);
                e.ZatenKayitli = true;
                e.Durum = "Programda zaten kayıtlı";
                sonuc.Add(e);
                continue;
            }

            var metin = BankaEkstreOkuyucu.Sadelestir(s.Aciklama);
            var bitisik = metin.Replace(" ", "");
            var kelimeler = metin.Split(' ').ToHashSet();
            var rakamlar = Regex.Replace(s.Aciklama, @"\D", " ");

            int? cariId = null;
            string neden = "";
            var kimlikle = cariAnahtar.Where(c => c.Kimlik.Any(k => Regex.IsMatch(rakamlar, $@"(^|\s){k}(\s|$)")) || (c.Iban.Length >= 20 && bitisik.Contains(c.Iban))).ToList();
            if (kimlikle.Count == 1) { cariId = kimlikle[0].Id; neden = "vergi no / IBAN"; }
            if (cariId == null)
            {
                var faturaCarisi = faturalar.Where(f => bitisik.Contains(BankaEkstreOkuyucu.Sadelestir(f.FaturaNo).Replace(" ", ""))).Select(f => f.CariKartId).Distinct().ToList();
                if (faturaCarisi.Count == 1) { cariId = faturaCarisi[0]; neden = "fatura no"; }
            }
            if (cariId == null)
            {
                var adayList = cariAnahtar.Where(c => c.Kelimeler.Length > 0)
                    .Select(c => new { c.Id, Eslesen = c.Kelimeler.Count(kelimeler.Contains), Toplam = c.Kelimeler.Length })
                    .Where(x => x.Eslesen >= Math.Min(2, x.Toplam) && (x.Eslesen > 1 || x.Toplam == 1))
                    .OrderByDescending(x => x.Eslesen).ToList();
                if (adayList.Count == 1 || (adayList.Count > 1 && adayList[0].Eslesen > adayList[1].Eslesen)) { cariId = adayList[0].Id; neden = "ünvan"; }
                else if (adayList.Count > 1) e.Durum = "Birden çok cari uyuyor; seçin";
            }

            if (cariId != null)
            {
                e.CariId = cariId;
                e.CariUnvan = cariler.First(c => c.Id == cariId).Unvan;
                e.Islem = giris ? EkstreIslemTuru.Tahsilat : EkstreIslemTuru.Odeme;
                e.Durum = $"Cari bulundu ({neden})";
                e.Secili = true;
            }
            else if (!giris && KrediKelimeleri.Any(metin.Contains)) { e.Islem = EkstreIslemTuru.KrediOdemesi; e.Durum = "Kredi ödemesi (300)"; e.Secili = true; }
            else if (!giris && MasrafKelimeleri.Any(metin.Contains)) { e.Islem = EkstreIslemTuru.BankaMasrafi; e.Durum = "Banka masrafı (780)"; e.Secili = true; }
            else if (giris && FaizKelimeleri.Any(metin.Contains)) { e.Islem = EkstreIslemTuru.FaizGeliri; e.Durum = "Faiz geliri (642)"; e.Secili = true; }
            else if (e.Durum.Length == 0) e.Durum = "Cari ya da işlem türü seçin";
            sonuc.Add(e);
        }
        return sonuc;
    }

    public async Task<EkstreKayitSonucuDto> KaydetAsync(int bankaHesabiId, IReadOnlyList<EkstreEslesmeDto> secimler)
    {
        var sonuc = new EkstreKayitSonucuDto();
        // Tarih sırasıyla kaydedilir; aynı gündeki girişler çıkışlardan önce (bakiye yetersiz uyarısı çıkmasın).
        foreach (var e in secimler.Where(x => x.Secili && !x.ZatenKayitli && x.Islem != EkstreIslemTuru.Atla)
                     .OrderBy(x => x.Satir.Tarih.Date).ThenByDescending(x => x.Satir.Tutar > 0).ThenBy(x => x.Satir.SatirNo))
        {
            var s = e.Satir;
            var tutar = Math.Abs(s.Tutar);
            var tarih = DateTime.SpecifyKind(s.Tarih.Date.AddHours(12), DateTimeKind.Utc);
            var aciklama = string.IsNullOrWhiteSpace(s.Aciklama) ? "Banka ekstresinden aktarıldı" : s.Aciklama.Trim();
            try
            {
                switch (e.Islem)
                {
                    case EkstreIslemTuru.Tahsilat or EkstreIslemTuru.Odeme:
                        if (e.CariId is not > 0) throw new InvalidOperationException("Cari seçilmedi.");
                        if ((e.Islem == EkstreIslemTuru.Tahsilat) != (s.Tutar > 0))
                            throw new InvalidOperationException(s.Tutar > 0 ? "Hesaba giren tutar ödeme olarak kaydedilemez." : "Hesaptan çıkan tutar tahsilat olarak kaydedilemez.");
                        if (e.Islem == EkstreIslemTuru.Tahsilat) await _banka.HesabaTahsilatEkleAsync(bankaHesabiId, e.CariId.Value, tutar, aciklama, s.DekontNo, tarih);
                        else await _banka.HesaptanOdemeEkleAsync(bankaHesabiId, e.CariId.Value, tutar, aciklama, s.DekontNo, tarih);
                        break;
                    default:
                        var fisTuru = e.Islem switch
                        {
                            EkstreIslemTuru.FaizGeliri => BankaFisTuru.BankaGelir,
                            EkstreIslemTuru.KrediOdemesi => BankaFisTuru.KrediOdemesi,
                            _ => BankaFisTuru.BankaGiderMasraf
                        };
                        if ((fisTuru == BankaFisTuru.BankaGelir) != (s.Tutar > 0))
                            throw new InvalidOperationException("İşlem türü tutarın yönüyle uyuşmuyor.");
                        await _banka.EkleAsync(new BankaHareketi { BankaHesabiId = bankaHesabiId, FisTuru = fisTuru, Tutar = tutar, Tarih = tarih, Aciklama = aciklama, DekontNo = s.DekontNo });
                        break;
                }
                e.Sonuc = "Kaydedildi";
                sonuc.Kaydedilen++;
            }
            catch (Exception ex)
            {
                e.Sonuc = "Hata: " + ex.GetBaseException().Message;
                sonuc.Hatali++;
                sonuc.Hatalar.Add($"{s.Tarih:dd.MM.yyyy} {s.Tutar:N2} TL: {ex.GetBaseException().Message}");
            }
        }
        return sonuc;
    }
}
