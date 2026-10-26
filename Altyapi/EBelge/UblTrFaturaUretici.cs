using System.Globalization;
using System.Xml.Linq;
using EtkiAlani.Modeller.Fatura;

namespace Altyapi.EBelge;

// Faturadaki satıcı/alıcı bilgisi. VknTckn 10 haneli ise VKN (tüzel kişi), 11 haneli ise TCKN (gerçek kişi) kabul edilir.
public sealed record UblTaraf(
    string VknTckn,
    string Unvan,
    string? Ad = null,
    string? Soyad = null,
    string? VergiDairesi = null,
    string? Adres = null,
    string? Ilce = null,
    string? Il = null,
    string? PostaKodu = null,
    string? Telefon = null,
    string? Eposta = null,
    string? MersisNo = null,
    string? WebSitesi = null)
{
    public bool GercekKisi => VknTckn.Length == 11;
}

public sealed record UblFaturaGirdisi(
    Fatura Fatura,
    Guid Ettn,
    EBelgeTuru Tur,
    EFaturaSenaryosu Senaryo,
    UblTaraf Satici,
    UblTaraf Alici);

// GİB UBL-TR 1.2 fatura belgesi üretir (imzasız; imzayı entegratör/mali mühür atar).
// Element sırası UBL 2.1 şemasındaki sıradır; değiştirilirse şema doğrulaması bozulur.
//
// Tutarlar faturada kayıtlı rakamlardan alınır (FaturaHesaplayici ile hesaplanmış):
//   satır brüt = miktar × birim fiyat, satır matrahı = SatirToplami − KdvTutari,
//   satır indirimi = brüt − matrah (satır iskontosu + dağıtılmış fatura altı iskonto).
public static class UblTrFaturaUretici
{
    public static readonly XNamespace Inv = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    public static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

    // Nihai tüketiciye (kimlik numarası bilinmeyen) e-Arşiv faturada kullanılan GİB genel TCKN'si.
    public const string NihaiTuketiciTckn = "11111111111";

    private static readonly CultureInfo Kultur = CultureInfo.InvariantCulture;

    public static string Profil(EBelgeTuru tur, EFaturaSenaryosu senaryo) => tur switch
    {
        EBelgeTuru.EArsiv => "EARSIVFATURA",
        _ => senaryo == EFaturaSenaryosu.Ticari ? "TICARIFATURA" : "TEMELFATURA"
    };

    public static string FaturaTipi(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => "SATIS",
        FaturaTuru.AlisIade => "IADE",
        _ => throw new InvalidOperationException("Yalnız satış faturası ve alış iade faturası e-belge olarak düzenlenir; alış faturası ve satış iadesi karşı taraftan gelir.")
    };

    // Belgenin gönderilmeden önce sağlaması gereken kurallar. Boş liste: belge hazırlanabilir.
    public static List<string> Dogrula(UblFaturaGirdisi g)
    {
        var hatalar = new List<string>();
        var f = g.Fatura;
        if (f.Tur is not (FaturaTuru.Satis or FaturaTuru.AlisIade))
            hatalar.Add("Yalnız satış faturası ve alış iade faturası e-belge olarak düzenlenir.");
        if (f.Durum != BelgeDurumu.Onaylandi)
            hatalar.Add("Yalnız onaylı fatura e-belge olarak hazırlanabilir.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(f.FaturaNo ?? "", "^[A-Z0-9]{3}20[0-9]{2}[0-9]{9}$"))
            hatalar.Add($"Fatura numarası GİB biçiminde değil (3 harf + yıl + 9 hane, ör. SAT2026000000001): '{f.FaturaNo}'.");
        else if (f.FaturaNo![3..7] != f.Tarih.Year.ToString(Kultur))
            hatalar.Add("Fatura numarasındaki yıl fatura tarihinin yılıyla aynı olmalıdır.");
        if (f.Kalemler.Count == 0)
            hatalar.Add("Faturada kalem yok.");
        if (f.Kalemler.Any(k => k.Miktar <= 0))
            hatalar.Add("Kalem miktarları sıfırdan büyük olmalıdır.");
        if (f.Tur == FaturaTuru.AlisIade && (string.IsNullOrWhiteSpace(f.IadeEdilenFaturaNo) || f.IadeEdilenFaturaTarihi == null))
            hatalar.Add("İade faturasında iade edilen faturanın numarası ve tarihi girilmelidir.");

        TarafDogrula("Firma (satıcı)", g.Satici, hatalar, adresZorunlu: true);
        if (g.Tur == EBelgeTuru.EFatura && g.Alici.VknTckn == NihaiTuketiciTckn)
            hatalar.Add("e-Fatura alıcısının gerçek vergi/T.C. kimlik numarası girilmelidir.");
        TarafDogrula("Alıcı (cari)", g.Alici, hatalar, adresZorunlu: g.Alici.VknTckn != NihaiTuketiciTckn);
        return hatalar;
    }

    private static void TarafDogrula(string ad, UblTaraf t, List<string> hatalar, bool adresZorunlu)
    {
        if (t.VknTckn.Length == 10)
        {
            if (!EtkiAlani.Yardimcilar.KimlikNoDogrulayici.VergiNoGecerliMi(t.VknTckn))
                hatalar.Add($"{ad}: vergi numarası geçersiz ({t.VknTckn}).");
            if (string.IsNullOrWhiteSpace(t.VergiDairesi))
                hatalar.Add($"{ad}: vergi dairesi girilmelidir.");
        }
        else if (t.VknTckn.Length == 11)
        {
            if (t.VknTckn != NihaiTuketiciTckn && !EtkiAlani.Yardimcilar.KimlikNoDogrulayici.TcKimlikNoGecerliMi(t.VknTckn))
                hatalar.Add($"{ad}: T.C. kimlik numarası geçersiz ({t.VknTckn}).");
        }
        else
            hatalar.Add($"{ad}: vergi numarası (10 hane) ya da T.C. kimlik numarası (11 hane) girilmelidir.");

        if (string.IsNullOrWhiteSpace(t.Unvan))
            hatalar.Add($"{ad}: unvan/ad soyad girilmelidir.");
        if (adresZorunlu && (string.IsNullOrWhiteSpace(t.Il) || string.IsNullOrWhiteSpace(t.Ilce)))
            hatalar.Add($"{ad}: il ve ilçe girilmelidir.");
    }

    public static XDocument Uret(UblFaturaGirdisi g)
    {
        var f = g.Fatura;
        var para = "TRY";
        var kalemler = f.Kalemler.OrderBy(k => k.Id).ToList();

        var satirlar = kalemler.Select(k =>
        {
            var brut = Kurus(k.Miktar * k.BirimFiyat);
            var matrah = k.SatirToplami - k.KdvTutari;
            return (Kalem: k, Brut: brut, Matrah: matrah, Indirim: brut - matrah);
        }).ToList();

        var matrahToplam = satirlar.Sum(s => s.Matrah);
        var kdvToplam = kalemler.Sum(k => k.KdvTutari);
        var indirimToplam = satirlar.Sum(s => s.Indirim);

        var kok = new XElement(Inv + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cac", Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc),
            new XAttribute(XNamespace.Xmlns + "ext", Ext),
            // UBLExtensions (mali mühür/e-imza) entegratör tarafından eklenir; boş bırakmak şemaya aykırıdır.
            B("UBLVersionID", "2.1"),
            B("CustomizationID", "TR1.2"),
            B("ProfileID", Profil(g.Tur, g.Senaryo)),
            B("ID", f.FaturaNo),
            B("CopyIndicator", "false"),
            B("UUID", g.Ettn.ToString().ToUpperInvariant()),
            B("IssueDate", f.Tarih.ToString("yyyy-MM-dd", Kultur)),
            B("IssueTime", f.Tarih.ToString("HH:mm:ss", Kultur)),
            B("InvoiceTypeCode", FaturaTipi(f.Tur)),
            string.IsNullOrWhiteSpace(f.Aciklama) ? null : B("Note", f.Aciklama.Trim()),
            B("Note", EtkiAlani.Yardimcilar.TutarYazici.Yaziyla(f.GenelToplam)),
            B("DocumentCurrencyCode", para),
            B("LineCountNumeric", kalemler.Count.ToString(Kultur)),
            f.Tur == FaturaTuru.AlisIade
                ? new XElement(Cac + "BillingReference",
                    new XElement(Cac + "InvoiceDocumentReference",
                        B("ID", f.IadeEdilenFaturaNo!.Trim()),
                        B("IssueDate", f.IadeEdilenFaturaTarihi!.Value.ToString("yyyy-MM-dd", Kultur)),
                        B("DocumentTypeCode", "IADE")))
                : null,
            Imza(g.Satici),
            new XElement(Cac + "AccountingSupplierParty", Taraf(g.Satici)),
            new XElement(Cac + "AccountingCustomerParty", Taraf(g.Alici)),
            VergiToplami(satirlar.Select(s => (s.Matrah, s.Kalem.KdvTutari, s.Kalem.KdvOrani)), para),
            new XElement(Cac + "LegalMonetaryTotal",
                T("LineExtensionAmount", matrahToplam, para),
                T("TaxExclusiveAmount", matrahToplam, para),
                T("TaxInclusiveAmount", matrahToplam + kdvToplam, para),
                T("AllowanceTotalAmount", indirimToplam, para),
                f.YuvarlamaTutari != 0 ? T("PayableRoundingAmount", f.YuvarlamaTutari, para) : null,
                T("PayableAmount", f.GenelToplam, para)),
            satirlar.Select((s, i) => new XElement(Cac + "InvoiceLine",
                B("ID", (i + 1).ToString(Kultur)),
                new XElement(Cbc + "InvoicedQuantity", new XAttribute("unitCode", BirimKodu(s.Kalem.Birim)), Sayi(s.Kalem.Miktar)),
                T("LineExtensionAmount", s.Matrah, para),
                s.Indirim != 0
                    ? new XElement(Cac + "AllowanceCharge",
                        B("ChargeIndicator", s.Indirim > 0 ? "false" : "true"),
                        s.Brut != 0 ? B("MultiplierFactorNumeric", Sayi(Math.Round(Math.Abs(s.Indirim) / s.Brut, 6))) : null,
                        T("Amount", Math.Abs(s.Indirim), para),
                        T("BaseAmount", s.Brut, para))
                    : null,
                VergiToplami([(s.Matrah, s.Kalem.KdvTutari, s.Kalem.KdvOrani)], para),
                new XElement(Cac + "Item",
                    B("Name", string.IsNullOrWhiteSpace(s.Kalem.UrunAd) ? s.Kalem.UrunKodu : s.Kalem.UrunAd),
                    string.IsNullOrWhiteSpace(s.Kalem.UrunKodu) ? null
                        : new XElement(Cac + "SellersItemIdentification", B("ID", s.Kalem.UrunKodu))),
                new XElement(Cac + "Price", T("PriceAmount", s.Kalem.BirimFiyat, para))))
        );

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), kok);
    }

    // UTF-8 bildirimli, girintili XML metni (dosyaya ve entegratöre giden biçim).
    public static string UretMetin(UblFaturaGirdisi g)
    {
        using var yazici = new Utf8StringWriter();
        Uret(g).Save(yazici);
        return yazici.ToString();
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override System.Text.Encoding Encoding => new System.Text.UTF8Encoding(false);
    }

    private static XElement Imza(UblTaraf satici) => new(Cac + "Signature",
        new XElement(Cbc + "ID", new XAttribute("schemeID", "VKN_TCKN"), satici.VknTckn),
        new XElement(Cac + "SignatoryParty",
            new XElement(Cac + "PartyIdentification", new XElement(Cbc + "ID", new XAttribute("schemeID", satici.GercekKisi ? "TCKN" : "VKN"), satici.VknTckn)),
            Adres(satici)),
        new XElement(Cac + "DigitalSignatureAttachment",
            new XElement(Cac + "ExternalReference", B("URI", "#Signature_" + satici.VknTckn))));

    private static XElement Taraf(UblTaraf t)
    {
        var (ad, soyad) = AdSoyad(t);
        return new XElement(Cac + "Party",
            string.IsNullOrWhiteSpace(t.WebSitesi) ? null : B("WebsiteURI", t.WebSitesi.Trim()),
            new XElement(Cac + "PartyIdentification", new XElement(Cbc + "ID", new XAttribute("schemeID", t.GercekKisi ? "TCKN" : "VKN"), t.VknTckn)),
            string.IsNullOrWhiteSpace(t.MersisNo) ? null
                : new XElement(Cac + "PartyIdentification", new XElement(Cbc + "ID", new XAttribute("schemeID", "MERSISNO"), t.MersisNo.Trim())),
            t.GercekKisi ? null : new XElement(Cac + "PartyName", B("Name", t.Unvan.Trim())),
            Adres(t),
            new XElement(Cac + "PartyTaxScheme", new XElement(Cac + "TaxScheme", B("Name", t.VergiDairesi?.Trim() ?? ""))),
            string.IsNullOrWhiteSpace(t.Telefon) && string.IsNullOrWhiteSpace(t.Eposta) ? null
                : new XElement(Cac + "Contact",
                    string.IsNullOrWhiteSpace(t.Telefon) ? null : B("Telephone", t.Telefon.Trim()),
                    string.IsNullOrWhiteSpace(t.Eposta) ? null : B("ElectronicMail", t.Eposta.Trim())),
            t.GercekKisi ? new XElement(Cac + "Person", B("FirstName", ad), B("FamilyName", soyad)) : null);
    }

    private static XElement Adres(UblTaraf t) => new(Cac + "PostalAddress",
        string.IsNullOrWhiteSpace(t.Adres) ? null : B("StreetName", t.Adres.Trim()),
        B("CitySubdivisionName", t.Ilce?.Trim() ?? ""),
        B("CityName", t.Il?.Trim() ?? ""),
        string.IsNullOrWhiteSpace(t.PostaKodu) ? null : B("PostalZone", t.PostaKodu.Trim()),
        new XElement(Cac + "Country", B("Name", "Türkiye")));

    // Gerçek kişide ad/soyad kartta yoksa unvanın son kelimesi soyad sayılır.
    private static (string Ad, string Soyad) AdSoyad(UblTaraf t)
    {
        if (!string.IsNullOrWhiteSpace(t.Ad) && !string.IsNullOrWhiteSpace(t.Soyad))
            return (t.Ad.Trim(), t.Soyad.Trim());
        var parcalar = t.Unvan.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parcalar.Length switch
        {
            0 => ("-", "-"),
            1 => (parcalar[0], "-"),
            _ => (string.Join(' ', parcalar[..^1]), parcalar[^1])
        };
    }

    private static XElement VergiToplami(IEnumerable<(decimal Matrah, decimal Kdv, decimal Oran)> satirlar, string para)
    {
        var gruplar = satirlar.GroupBy(s => s.Oran).OrderBy(x => x.Key).ToList();
        return new XElement(Cac + "TaxTotal",
            T("TaxAmount", gruplar.Sum(x => x.Sum(s => s.Kdv)), para),
            gruplar.Select(grp => new XElement(Cac + "TaxSubtotal",
                T("TaxableAmount", grp.Sum(s => s.Matrah), para),
                T("TaxAmount", grp.Sum(s => s.Kdv), para),
                B("Percent", Sayi(grp.Key)),
                new XElement(Cac + "TaxCategory",
                    // %0 KDV'de muafiyet sebebi zorunludur; özel istisna yoksa 351 (istisna olmayan diğer).
                    grp.Key == 0 ? B("TaxExemptionReasonCode", "351") : null,
                    grp.Key == 0 ? B("TaxExemptionReason", "KDV - İstisna Olmayan Diğer") : null,
                    new XElement(Cac + "TaxScheme", B("Name", "KDV"), B("TaxTypeCode", "0015"))))));
    }

    // Türkçe birim adlarını UN/ECE Rec 20 birim kodlarına çevirir; tanınmayan birim "adet" (C62) sayılır.
    public static string BirimKodu(string? birim)
    {
        var b = (birim ?? "").Trim().ToLower(new CultureInfo("tr-TR")).Replace(".", "");
        return b switch
        {
            "kg" or "kilogram" or "kilo" => "KGM",
            "g" or "gr" or "gram" => "GRM",
            "ton" => "TNE",
            "lt" or "l" or "litre" => "LTR",
            "ml" or "mililitre" => "MLT",
            "m" or "mt" or "metre" => "MTR",
            "cm" or "santimetre" => "CMT",
            "m2" or "m²" or "metrekare" => "MTK",
            "m3" or "m³" or "metreküp" => "MTQ",
            "paket" or "pk" => "PA",
            "kutu" => "BX",
            "koli" => "CT",
            "çift" => "PR",
            "düzine" => "DZN",
            "set" or "takım" => "SET",
            "saat" => "HUR",
            "gün" => "DAY",
            "ay" => "MON",
            "yıl" => "ANN",
            "kwh" => "KWH",
            _ => "C62"
        };
    }

    private static XElement B(string ad, string deger) => new(Cbc + ad, deger);

    private static XElement T(string ad, decimal tutar, string para) =>
        new(Cbc + ad, new XAttribute("currencyID", para), Kurus(tutar).ToString("0.00", Kultur));

    private static string Sayi(decimal d) => d.ToString("0.########", Kultur);

    private static decimal Kurus(decimal d) => decimal.Round(d, 2, MidpointRounding.AwayFromZero);
}
