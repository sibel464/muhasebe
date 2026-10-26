using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Uygulama.Dto.Finans360;

namespace Altyapi.Aktarim;

// Bankaların internet şubesinden indirilen Excel / CSV ekstreyi okur. Bankalar başlık satırının üstüne hesap bilgisi
// yazdığı için başlık satırı ilk 40 satır içinde aranır; sütunlar adlarından tanınır:
//  tarih ("İşlem Tarihi", "Tarih"), açıklama, tek işaretli "Tutar" ya da ayrı "Borç/Alacak", "Giriş/Çıkış",
//  "Gelen/Giden", "Yatan/Çekilen"; isteğe bağlı "Bakiye" ve "Dekont / Fiş / Referans No".
// Banka ekstresinde "Borç" hesaptan çıkan, "Alacak" hesaba giren paradır.
public static class BankaEkstreOkuyucu
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static EkstreOkumaSonucuDto Oku(string dosyaYolu)
    {
        var uzanti = Path.GetExtension(dosyaYolu).ToLowerInvariant();
        var tablo = uzanti switch
        {
            ".xlsx" => TabloOkuyucu.XlsxOku(File.ReadAllBytes(dosyaYolu)),
            ".csv" or ".txt" => TabloOkuyucu.CsvOku(File.ReadAllBytes(dosyaYolu)),
            ".xls" => throw new InvalidOperationException("Eski Excel biçimi (.xls) okunamıyor. Dosyayı Excel'de açıp 'Farklı Kaydet → Excel Çalışma Kitabı (.xlsx)' ile kaydedin."),
            _ => throw new InvalidOperationException("Yalnız Excel (.xlsx) ya da CSV (.csv) ekstre aktarılabilir.")
        };
        return Coz(tablo);
    }

    public static string Sadelestir(string? metin)
    {
        if (string.IsNullOrEmpty(metin)) return "";
        var sb = new StringBuilder(metin.Length);
        foreach (var c in metin.ToUpper(Tr))
        {
            sb.Append(c switch
            {
                'İ' or 'I' or 'ı' => 'I', 'Ş' => 'S', 'Ğ' => 'G', 'Ü' => 'U', 'Ö' => 'O', 'Ç' => 'C', 'Â' => 'A', 'Î' => 'I', 'Û' => 'U',
                _ => char.IsLetterOrDigit(c) ? c : ' '
            });
        }
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private sealed class Sutunlar
    {
        public int Tarih = -1, Aciklama = -1, Tutar = -1, Giris = -1, Cikis = -1, Bakiye = -1, Dekont = -1, Yon = -1;
        public bool Gecerli => Tarih >= 0 && (Tutar >= 0 || (Giris >= 0 && Cikis >= 0));
    }

    private static Sutunlar? BaslikCoz(List<string> hucreler)
    {
        var s = new Sutunlar();
        var adlar = hucreler.Select(Sadelestir).ToList();
        int Bul(Func<string, bool> kosul) => adlar.FindIndex(a => a.Length > 0 && kosul(a));

        s.Tarih = Bul(a => a is "ISLEM TARIHI" or "TARIH" or "ISLEM TARIH" or "TARIH SAAT" or "ISLEM TARIHI SAATI");
        if (s.Tarih < 0) s.Tarih = Bul(a => a.Contains("TARIH") && !a.Contains("VALOR"));
        s.Aciklama = Bul(a => a.Contains("ACIKLAMA"));
        if (s.Aciklama < 0) s.Aciklama = Bul(a => a.Contains("DETAY") || a == "ISLEM" || a.Contains("ISLEM TIPI") || a.Contains("ISLEM ADI"));
        s.Tutar = Bul(a => a is "TUTAR" or "ISLEM TUTARI" or "TUTAR TL" or "TUTAR TRY" or "MIKTAR");
        if (s.Tutar < 0) s.Tutar = Bul(a => a.StartsWith("TUTAR") || a.EndsWith(" TUTAR") || a.EndsWith(" TUTARI"));
        s.Giris = Bul(a => a is "ALACAK" or "GIRIS" or "GELEN" or "YATAN" or "ALACAK TUTARI" or "GIRIS TUTARI" or "YATIRILAN" or "ALACAK TL");
        s.Cikis = Bul(a => a is "BORC" or "CIKIS" or "GIDEN" or "CEKILEN" or "BORC TUTARI" or "CIKIS TUTARI" or "BORC TL");
        s.Bakiye = Bul(a => a.Contains("BAKIYE"));
        s.Dekont = Bul(a => a.Contains("DEKONT") || a.Contains("FIS NO") || a.Contains("REFERANS") || a == "SIRA NO" || a.Contains("ISLEM NO"));
        s.Yon = Bul(a => a is "B A" or "BA" or "BORC ALACAK" or "YON" or "ISLEM YONU" or "B A IND");
        if (s.Tutar == s.Bakiye) s.Tutar = -1;
        return s.Gecerli ? s : null;
    }

    public static EkstreOkumaSonucuDto Coz(List<(int SatirNo, List<string> Hucreler)> tablo)
    {
        var sonuc = new EkstreOkumaSonucuDto();
        Sutunlar? sutun = null;
        List<string>? basliklar = null;
        var basla = 0;
        for (var i = 0; i < Math.Min(40, tablo.Count); i++)
        {
            sutun = BaslikCoz(tablo[i].Hucreler);
            if (sutun != null) { basliklar = tablo[i].Hucreler; basla = i + 1; break; }
        }
        if (sutun == null || basliklar == null)
            throw new InvalidOperationException("Ekstrede tarih ve tutar sütunları bulunamadı. Sütun başlıklarında 'Tarih' ile 'Tutar' (ya da 'Borç' ve 'Alacak') olmalı.");

        string Ad(int i) => i >= 0 && i < basliklar.Count ? basliklar[i].Trim() : "-";
        sonuc.SutunBilgisi = $"Tarih: {Ad(sutun.Tarih)} · Açıklama: {Ad(sutun.Aciklama)} · " +
            (sutun.Tutar >= 0 ? $"Tutar: {Ad(sutun.Tutar)}" : $"Giriş: {Ad(sutun.Giris)} / Çıkış: {Ad(sutun.Cikis)}") +
            (sutun.Bakiye >= 0 ? $" · Bakiye: {Ad(sutun.Bakiye)}" : "");

        string Hucre(List<string> h, int i) => i >= 0 && i < h.Count ? h[i].Trim() : "";
        var atlanan = 0;
        foreach (var (no, h) in tablo.Skip(basla))
        {
            if (h.All(string.IsNullOrWhiteSpace)) continue;
            if (!TarihCoz(Hucre(h, sutun.Tarih), out var tarih)) { atlanan++; continue; }
            decimal tutar;
            if (sutun.Tutar >= 0)
            {
                if (!TutarCoz(Hucre(h, sutun.Tutar), out tutar)) { atlanan++; continue; }
                // İşaretsiz tutar + ayrı yön sütunu: B (borç) hesaptan çıkış, A (alacak) hesaba giriş.
                var yon = Sadelestir(Hucre(h, sutun.Yon));
                if (yon.StartsWith('B')) tutar = -Math.Abs(tutar);
                else if (yon.StartsWith('A')) tutar = Math.Abs(tutar);
            }
            else
            {
                TutarCoz(Hucre(h, sutun.Giris), out var giris);
                TutarCoz(Hucre(h, sutun.Cikis), out var cikis);
                tutar = Math.Abs(giris) - Math.Abs(cikis);
            }
            if (tutar == 0) continue;
            decimal? bakiye = sutun.Bakiye >= 0 && TutarCoz(Hucre(h, sutun.Bakiye), out var b) ? b : null;
            var aciklama = Hucre(h, sutun.Aciklama);
            sonuc.Satirlar.Add(new EkstreSatiriDto
            {
                SatirNo = no, Tarih = tarih, Tutar = tutar, Bakiye = bakiye,
                Aciklama = aciklama.Length == 0 ? (tutar > 0 ? "Banka girişi" : "Banka çıkışı") : aciklama.Length > 400 ? aciklama[..400] : aciklama,
                DekontNo = Hucre(h, sutun.Dekont) is { Length: > 0 } d ? (d.Length > 50 ? d[..50] : d) : null
            });
        }
        if (atlanan > 0) sonuc.Uyarilar.Add($"{atlanan} satır tarih ya da tutar okunamadığı için atlandı (ara toplam / açıklama satırları olabilir).");
        if (sonuc.Satirlar.Count == 0) throw new InvalidOperationException("Ekstrede aktarılacak hareket bulunamadı.");
        sonuc.Satirlar = sonuc.Satirlar.OrderBy(s => s.Tarih).ThenBy(s => s.SatirNo).ToList();
        return sonuc;
    }

    private static readonly string[] TarihBicimleri =
    [
        "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "d.M.yyyy HH:mm", "d.M.yyyy H:mm:ss",
        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss", "dd-MM-yyyy", "dd-MM-yyyy HH:mm:ss",
        "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "dd.MM.yy", "dd/MM/yy"
    ];

    public static bool TarihCoz(string metin, out DateTime tarih)
    {
        tarih = default;
        metin = metin.Trim();
        if (metin.Length < 6) return false;
        if (DateTime.TryParseExact(metin, TarihBicimleri, Tr, DateTimeStyles.AllowWhiteSpaces, out var t)) { tarih = t.Date; return t.Year > 1990; }
        // Excel tarih seri numarası (biçimsiz hücre)
        if (double.TryParse(metin.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var seri) && seri is > 30000 and < 80000)
        {
            tarih = DateTime.FromOADate(Math.Floor(seri));
            return true;
        }
        return false;
    }

    // "1.234,56", "-1.234,56", "1234.56", "1,234.56", "(1.234,56)", "1.234,56 TL", "1.234,56-" biçimlerini okur.
    public static bool TutarCoz(string metin, out decimal tutar)
    {
        tutar = 0;
        var m = metin.Trim();
        if (m.Length == 0) return false;
        var eksi = false;
        if (m.StartsWith('(') && m.EndsWith(')')) { eksi = true; m = m[1..^1]; }
        m = Regex.Replace(m, @"(TL|TRY|₺|USD|EUR|\s)", "", RegexOptions.IgnoreCase);
        if (m.EndsWith('-')) { eksi = true; m = m[..^1]; }
        if (m.StartsWith('-')) { eksi = !eksi; m = m[1..]; }
        else if (m.StartsWith('+')) m = m[1..];
        if (m.Length == 0 || !Regex.IsMatch(m, @"^[0-9.,]+$")) return false;

        var sonNokta = m.LastIndexOf('.');
        var sonVirgul = m.LastIndexOf(',');
        string duz;
        if (sonNokta >= 0 && sonVirgul >= 0)
            duz = sonVirgul > sonNokta ? m.Replace(".", "").Replace(',', '.') : m.Replace(",", "");
        else if (sonVirgul >= 0)
            duz = m.Count(c => c == ',') == 1 ? m.Replace(',', '.') : m.Replace(",", "");
        else if (sonNokta >= 0)
            duz = Regex.IsMatch(m, @"^\d{1,3}(\.\d{3})+$") ? m.Replace(".", "") : m;
        else duz = m;

        if (!decimal.TryParse(duz, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d)) return false;
        tutar = Math.Round(eksi ? -d : d, 2);
        return true;
    }
}
