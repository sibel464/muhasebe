using System.Globalization;
using System.Text;
using System.Text.Json;
using EtkiAlani.Modeller.Guvenlik;

namespace Uygulama.Servisler;

// Denetim kaydını (kim, ne zaman, ne yaptı) teknik olmayan kullanıcının okuyacağı Türkçe metne çevirir:
// "CariKart / Guncelleme / {"Unvan":"A"}" yerine "Cari Hesap / Değiştirdi / Ünvan: A → B".
public static class DenetimMetinleri
{
    public sealed record Degisiklik(string Alan, string Eski, string Yeni);

    // Gerçek UTC tutulan sistem zamanları (belge tarihlerinden farklı olarak) yerel saate çevrilir.
    private static readonly HashSet<string> SistemZamanlari = new() { "SonGirisTarihi", "KilitBitisTarihi", "SilinmeTarihi" };

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static readonly IReadOnlyDictionary<string, string> Moduller = new Dictionary<string, string>
    {
        ["AmortismanKaydi"] = "Amortisman", ["BankaHareketi"] = "Banka Hareketi", ["BankaHesabi"] = "Banka Hesabı",
        ["BankaMutabakat"] = "Banka Mutabakatı", ["BelgeKapama"] = "Belge Kapama", ["BelgeSerisi"] = "Belge Serisi",
        ["CariHareket"] = "Cari Hareket", ["CariKart"] = "Cari Hesap", ["CariNotu"] = "Cari Notu",
        ["CariTipiTanimi"] = "Cari Tipi", ["CekSenet"] = "Çek / Senet", ["Depo"] = "Mağaza / Depo",
        ["DepoTransferFisi"] = "Depo Transferi", ["DepoTransferKalemi"] = "Depo Transfer Kalemi", ["DovizKuru"] = "Döviz Kuru",
        ["EBelgeKaydi"] = "e-Belge", ["Fatura"] = "Fatura", ["FaturaKalemi"] = "Fatura Kalemi",
        ["FaturaKalemiStokDagilimi"] = "Fatura Stok Dağılımı", ["GiderPusulasi"] = "Gider Pusulası",
        ["GiderPusulasiKalemi"] = "Gider Pusulası Kalemi", ["HesapPlani"] = "Hesap Planı", ["Irsaliye"] = "İrsaliye",
        ["IrsaliyeKalemi"] = "İrsaliye Kalemi", ["IrsaliyeKalemiStokDagilimi"] = "İrsaliye Stok Dağılımı", ["Kasa"] = "Kasa",
        ["KasaHareketi"] = "Kasa Hareketi", ["KasaSayimMutabakat"] = "Kasa Sayımı", ["KdvOrani"] = "KDV Oranı",
        ["Kullanici"] = "Kullanıcı", ["KullaniciYetkileri"] = "Kullanıcı Yetkileri", ["MaliDonem"] = "Mali Dönem",
        ["MerkeziTanim"] = "Tanım", ["MuhasebeBaglantiKodu"] = "Muhasebe Bağlantısı", ["ParaBirimi"] = "Para Birimi",
        ["Personel"] = "Personel", ["PersonelHareketi"] = "Personel Hareketi (maaş/avans)", ["PosIslemi"] = "POS İşlemi",
        ["RevaluasyonKalemi"] = "Değerleme Kalemi", ["RevaluasyonKaydi"] = "Kur Değerlemesi", ["SabitKiymet"] = "Demirbaş",
        ["Siparis"] = "Sipariş", ["SiparisKalemi"] = "Sipariş Kalemi", ["Sirket"] = "Firma", ["SistemAyarlari"] = "Sistem Ayarları",
        ["StokHareketi"] = "Stok Hareketi", ["StokSayimKalemi"] = "Stok Sayım Kalemi", ["StokSayimi"] = "Stok Sayımı",
        ["Sube"] = "Şube", ["TekrarlayanFatura"] = "Tekrarlayan Fatura", ["Urun"] = "Ürün / Hizmet",
        ["UrunLokasyonStogu"] = "Depo Stoğu", ["UrunSatisFiyati"] = "Ürün Fiyatı", ["YevmiyeFis"] = "Yevmiye Fişi",
        ["YevmiyeSatiri"] = "Yevmiye Satırı", ["Oturum"] = "Oturum (giriş/çıkış)", ["RaporMerkezi"] = "Rapor"
    };

    // Ana belgeyle birlikte kendiliğinden yazılan alt satırlar. Varsayılan listede gizlenir, istenirse gösterilir.
    public static readonly IReadOnlySet<string> AyrintiTablolari = new HashSet<string>
    {
        "FaturaKalemi", "FaturaKalemiStokDagilimi", "IrsaliyeKalemi", "IrsaliyeKalemiStokDagilimi", "SiparisKalemi",
        "DepoTransferKalemi", "StokSayimKalemi", "GiderPusulasiKalemi", "RevaluasyonKalemi", "YevmiyeSatiri",
        "StokHareketi", "UrunLokasyonStogu", "UrunSatisFiyati"
    };

    // Ekranda gösterilmeyen teknik alanlar.
    private static readonly HashSet<string> TeknikAlanlar = new() { "Id", "OlusturmaTarihi", "GuncellemeTarihi", "Versiyon", "SirketId", "xmin", "RowVersion" };

    private static readonly Dictionary<string, string> Alanlar = new()
    {
        ["Unvan"] = "Ünvan", ["Ad"] = "Ad", ["Soyad"] = "Soyad", ["Kod"] = "Kod", ["Aciklama"] = "Açıklama", ["Tutar"] = "Tutar",
        ["Tarih"] = "Tarih", ["Silindi"] = "Silindi", ["SilinmeNedeni"] = "Silme nedeni", ["SilinmeTarihi"] = "Silinme tarihi",
        ["Telefon"] = "Telefon", ["Eposta"] = "E-posta", ["Adres"] = "Adres", ["Il"] = "İl", ["Ilce"] = "İlçe",
        ["VergiNo"] = "Vergi no", ["VergiDairesi"] = "Vergi dairesi", ["TcNo"] = "TC kimlik no", ["Iban"] = "IBAN",
        ["FaturaNo"] = "Fatura no", ["IrsaliyeNo"] = "İrsaliye no", ["SiparisNo"] = "Sipariş no", ["BelgeNo"] = "Belge no", ["FisNo"] = "Fiş no",
        ["GenelToplam"] = "Genel toplam", ["AraToplam"] = "Ara toplam", ["KdvToplam"] = "KDV toplamı", ["KdvOrani"] = "KDV oranı",
        ["KdvTutari"] = "KDV tutarı", ["IskontoTutari"] = "İskonto", ["Miktar"] = "Miktar", ["BirimFiyat"] = "Birim fiyat",
        ["AlisFiyati"] = "Alış fiyatı", ["PerakendeSatisFiyati"] = "Satış fiyatı", ["ToptanSatisFiyati"] = "Toptan fiyat",
        ["StokMiktari"] = "Stok miktarı", ["Barkod"] = "Barkod", ["Borc"] = "Borç", ["Alacak"] = "Alacak", ["Bakiye"] = "Bakiye",
        ["Durum"] = "Durum", ["Aktif"] = "Aktif", ["AktifMi"] = "Aktif", ["Rol"] = "Rol", ["KullaniciAdi"] = "Kullanıcı adı",
        ["SifreDegistirmeli"] = "Şifre değiştirmeli", ["HataliGirisSayisi"] = "Hatalı giriş sayısı", ["KilitBitisTarihi"] = "Kilit bitişi",
        ["SonGirisTarihi"] = "Son giriş", ["VadeTarihi"] = "Vade tarihi", ["Fiyat"] = "Fiyat", ["IptalNedeni"] = "İptal nedeni",
        ["CariKartId"] = "Cari no", ["UrunId"] = "Ürün no", ["KasaId"] = "Kasa no", ["BankaHesabiId"] = "Banka hesabı no",
        ["DepoId"] = "Depo no", ["FaturaId"] = "Fatura no (iç)", ["KullaniciId"] = "Kullanıcı no", ["ParaBirimiId"] = "Para birimi no",
        ["HesapKodu"] = "Hesap kodu", ["TaksitSayisi"] = "Taksit", ["Ettn"] = "ETTN", ["EBelgeDurumu"] = "e-Belge durumu",
        ["OdenecekTutar"] = "Ödenecek tutar", ["KapaliTutar"] = "Kapatılan tutar", ["DovizTutar"] = "Döviz tutarı", ["Kur"] = "Kur",
        ["Net"] = "Net", ["Brut"] = "Brüt", ["Gorev"] = "Görev", ["IseGirisTarihi"] = "İşe giriş", ["IstenCikisTarihi"] = "İşten çıkış"
    };

    // Kimlik niteliğindeki alanlar: eklenen/silinen kaydın özetinde önce bunlar gösterilir.
    private static readonly string[] KimlikAlanlari =
    [
        "FaturaNo", "IrsaliyeNo", "SiparisNo", "BelgeNo", "FisNo", "Kod", "KullaniciAdi", "Unvan", "Ad", "Soyad", "Barkod",
        "Tarih", "GenelToplam", "Tutar", "Borc", "Alacak", "Miktar", "Fiyat", "Aciklama"
    ];

    public static string ModulAdi(string? tablo) =>
        string.IsNullOrEmpty(tablo) ? "" : Moduller.TryGetValue(tablo, out var ad) ? ad : Bolu(tablo);

    public static string IslemAdi(AuditIslemTuru tur, string? tablo) => tablo == "Oturum" ? "Oturum" : tur switch
    {
        AuditIslemTuru.Ekleme => "Ekledi",
        AuditIslemTuru.Guncelleme => "Değiştirdi",
        AuditIslemTuru.Silme => "Sildi (kalıcı)",
        AuditIslemTuru.SoftDelete => "Sildi",
        _ => tur.ToString()
    };

    public static string AlanAdi(string alan) => Alanlar.TryGetValue(alan, out var ad) ? ad : Bolu(alan);

    // Denetim saati gerçek UTC'dir; ekranda bilgisayarın yerel saatine çevrilir.
    public static DateTime YerelZaman(DateTime tarih) =>
        (tarih.Kind == DateTimeKind.Local ? tarih : DateTime.SpecifyKind(tarih, DateTimeKind.Utc).ToLocalTime());

    public static IReadOnlyList<Degisiklik> Degisiklikler(AuditLog log)
    {
        var eski = Oku(log.EskiDegerJson);
        var yeni = Oku(log.YeniDegerJson);
        var alanlar = eski.Keys.Union(yeni.Keys).Where(a => !TeknikAlanlar.Contains(a)).ToList();
        // Eklenen/silinen kayıtta kimlik alanları başa alınır; boş değerler gösterilmez.
        alanlar = alanlar.OrderBy(a => Array.IndexOf(KimlikAlanlari, a) is var i && i >= 0 ? i : 1000).ThenBy(a => a, StringComparer.Ordinal).ToList();
        var sonuc = new List<Degisiklik>();
        foreach (var a in alanlar)
        {
            var e = eski.TryGetValue(a, out var ev) ? Bicimle(a, ev) : "";
            var y = yeni.TryGetValue(a, out var yv) ? Bicimle(a, yv) : "";
            if (DegisiklikKaydi(log) && e == y) continue;
            if (!DegisiklikKaydi(log) && e == "" && y == "") continue;
            sonuc.Add(new Degisiklik(AlanAdi(a), e, y));
        }
        return sonuc;
    }

    // Güncelleme ve çöp kutusuna silme, yalnız değişen alanları taşır (eski → yeni).
    private static bool DegisiklikKaydi(AuditLog log) => log.IslemTuru is AuditIslemTuru.Guncelleme or AuditIslemTuru.SoftDelete;

    // Listede tek satırlık özet.
    public static string Ozet(AuditLog log)
    {
        if (log.TabloAdi == "Oturum" || (string.IsNullOrEmpty(log.EskiDegerJson) && string.IsNullOrEmpty(log.YeniDegerJson)))
            return log.IslemDetayi ?? "";
        var degisiklikler = Degisiklikler(log);
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(log.IslemDetayi)) sb.Append(log.IslemDetayi).Append(" — ");
        var parcalar = log.IslemTuru == AuditIslemTuru.SoftDelete
            ? degisiklikler.Where(d => d.Alan is not ("Silindi" or "Silinme tarihi") && d.Yeni != "").Select(d => $"{d.Alan}: {Kisalt(d.Yeni)}")
            : log.IslemTuru == AuditIslemTuru.Guncelleme
            ? degisiklikler.Select(d => $"{d.Alan}: {Kisalt(d.Eski)} → {Kisalt(d.Yeni)}")
            : degisiklikler.Take(5).Select(d => $"{d.Alan}: {Kisalt(log.IslemTuru == AuditIslemTuru.Ekleme ? d.Yeni : d.Eski)}");
        sb.Append(string.Join(" · ", parcalar));
        return sb.ToString();
    }

    // Arama metni Türkçe modül adıyla da eşleşsin ("fatura" → Fatura, FaturaKalemi...).
    public static IReadOnlyList<string> ModulAdiylaEslesenTablolar(string arama) =>
        Moduller.Where(m => m.Value.Contains(arama, StringComparison.CurrentCultureIgnoreCase)).Select(m => m.Key).ToList();

    private static Dictionary<string, JsonElement> Oku(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static string Bicimle(string alan, JsonElement d) => d.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        JsonValueKind.True => "Evet",
        JsonValueKind.False => "Hayır",
        JsonValueKind.Number when alan.EndsWith("Id", StringComparison.Ordinal) => d.GetRawText(),
        JsonValueKind.Number => d.TryGetDecimal(out var sayi) ? sayi.ToString(sayi == decimal.Truncate(sayi) ? "#,0" : "#,0.00##", Tr) : d.GetRawText(),
        JsonValueKind.String => MetinBicimle(alan, d.GetString()),
        _ => d.GetRawText()
    };

    private static string MetinBicimle(string alan, string? metin)
    {
        if (!TarihMi(metin, out var t)) return metin ?? "";
        if (SistemZamanlari.Contains(alan)) t = DateTime.SpecifyKind(t, DateTimeKind.Utc).ToLocalTime();
        return t.TimeOfDay == TimeSpan.Zero ? t.ToString("dd.MM.yyyy", Tr) : t.ToString("dd.MM.yyyy HH:mm", Tr);
    }

    // Belge tarihleri yerel saat olarak saklanır (UTC etiketiyle); saat olduğu gibi gösterilir, çevrilmez.
    private static bool TarihMi(string? metin, out DateTime tarih)
    {
        tarih = default;
        return metin is { Length: >= 19 } && metin[4] == '-' && metin[10] == 'T' &&
               DateTime.TryParse(metin, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out tarih);
    }

    private static string Kisalt(string metin) => metin.Length > 60 ? metin[..57] + "…" : metin == "" ? "(boş)" : metin;

    // "PerakendeSatisFiyati" → "Perakende satis fiyati"
    private static string Bolu(string ad)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < ad.Length; i++)
        {
            if (i > 0 && char.IsUpper(ad[i]) && !char.IsUpper(ad[i - 1])) sb.Append(' ').Append(char.ToLower(ad[i], Tr));
            else sb.Append(ad[i]);
        }
        return sb.ToString();
    }
}
