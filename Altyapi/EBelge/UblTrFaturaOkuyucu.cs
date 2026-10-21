using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Altyapi.EBelge;

public sealed record GelenTaraf(string VknTckn, string Unvan, string? VergiDairesi, string? Adres, string? Ilce, string? Il, string? PostaKodu, string? Telefon, string? Eposta);

public sealed record GelenFaturaSatiri(
    int SiraNo, string UrunAdi, string? SaticiUrunKodu, decimal Miktar, string BirimKodu, decimal BirimFiyat,
    decimal Indirim, decimal SatirTutari, decimal KdvOrani, decimal KdvTutari);

public sealed record GelenFatura(
    string FaturaNo, Guid Ettn, DateTime Tarih, string Profil, string Tip, string ParaBirimi,
    GelenTaraf Satici, GelenTaraf Alici, IReadOnlyList<GelenFaturaSatiri> Satirlar,
    decimal MatrahToplam, decimal KdvToplam, decimal OdenecekTutar, string? IadeEdilenFaturaNo, DateTime? IadeEdilenFaturaTarihi);

// Tedarikçiden gelen UBL-TR e-Fatura/e-Arşiv XML'ini okur (imzalı belge de olur; imza bölümü yok sayılır).
public static class UblTrFaturaOkuyucu
{
    private static readonly XNamespace Cac = UblTrFaturaUretici.Cac;
    private static readonly XNamespace Cbc = UblTrFaturaUretici.Cbc;
    private static readonly CultureInfo Kultur = CultureInfo.InvariantCulture;

    public static GelenFatura Oku(string xml)
    {
        XDocument belge;
        try
        {
            // DTD kapalı: dışarıdan gelen dosyada varlık genişletme saldırısı olmasın.
            using var okuyucu = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            belge = XDocument.Load(okuyucu);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException("Dosya geçerli bir XML değil: " + ex.Message, ex);
        }

        var kok = belge.Root;
        if (kok == null || kok.Name != UblTrFaturaUretici.Inv + "Invoice")
            throw new InvalidOperationException("Dosya bir UBL-TR e-Fatura/e-Arşiv faturası değil (Invoice kök elementi bulunamadı).");

        string Zorunlu(XElement? e, string ad) => e?.Value.Trim() is { Length: > 0 } v ? v : throw new InvalidOperationException($"Faturada '{ad}' bilgisi yok.");

        var paraBirimi = kok.Element(Cbc + "DocumentCurrencyCode")?.Value.Trim() ?? "TRY";
        var faturaNo = Zorunlu(kok.Element(Cbc + "ID"), "fatura numarası");
        if (!Guid.TryParse(Zorunlu(kok.Element(Cbc + "UUID"), "ETTN"), out var ettn))
            throw new InvalidOperationException("Faturadaki ETTN geçersiz.");
        var tarih = DateTime.ParseExact(Zorunlu(kok.Element(Cbc + "IssueDate"), "düzenleme tarihi"), "yyyy-MM-dd", Kultur);
        if (kok.Element(Cbc + "IssueTime")?.Value is { Length: >= 5 } saat && TimeSpan.TryParse(saat[..Math.Min(8, saat.Length)], Kultur, out var zaman))
            tarih = tarih.Add(zaman);

        var satirlar = kok.Elements(Cac + "InvoiceLine").Select((s, i) =>
        {
            var miktarE = s.Element(Cbc + "InvoicedQuantity");
            var indirim = s.Elements(Cac + "AllowanceCharge")
                .Sum(a => (a.Element(Cbc + "ChargeIndicator")?.Value.Trim() == "true" ? -1 : 1) * Sayi(a.Element(Cbc + "Amount")));
            var vergi = s.Element(Cac + "TaxTotal")?.Elements(Cac + "TaxSubtotal")
                .FirstOrDefault(t => t.Descendants(Cbc + "TaxTypeCode").Any(c => c.Value.Trim() == "0015"));
            var item = s.Element(Cac + "Item");
            return new GelenFaturaSatiri(
                i + 1,
                item?.Element(Cbc + "Name")?.Value.Trim() ?? $"Satır {i + 1}",
                item?.Element(Cac + "SellersItemIdentification")?.Element(Cbc + "ID")?.Value.Trim(),
                Sayi(miktarE),
                miktarE?.Attribute("unitCode")?.Value ?? "C62",
                Sayi(s.Element(Cac + "Price")?.Element(Cbc + "PriceAmount")),
                indirim,
                Sayi(s.Element(Cbc + "LineExtensionAmount")),
                Sayi(vergi?.Element(Cbc + "Percent")),
                Sayi(vergi?.Element(Cbc + "TaxAmount")));
        }).ToList();
        if (satirlar.Count == 0)
            throw new InvalidOperationException("Faturada kalem yok.");

        var toplam = kok.Element(Cac + "LegalMonetaryTotal");
        var kdvToplam = kok.Elements(Cac + "TaxTotal").SelectMany(t => t.Elements(Cac + "TaxSubtotal"))
            .Where(t => t.Descendants(Cbc + "TaxTypeCode").Any(c => c.Value.Trim() == "0015"))
            .Sum(t => Sayi(t.Element(Cbc + "TaxAmount")));
        var iade = kok.Element(Cac + "BillingReference")?.Element(Cac + "InvoiceDocumentReference");

        return new GelenFatura(
            faturaNo, ettn, tarih,
            kok.Element(Cbc + "ProfileID")?.Value.Trim() ?? string.Empty,
            kok.Element(Cbc + "InvoiceTypeCode")?.Value.Trim().ToUpperInvariant() ?? "SATIS",
            paraBirimi,
            Taraf(kok.Element(Cac + "AccountingSupplierParty")?.Element(Cac + "Party"), "satıcı"),
            Taraf(kok.Element(Cac + "AccountingCustomerParty")?.Element(Cac + "Party"), "alıcı"),
            satirlar,
            Sayi(toplam?.Element(Cbc + "TaxExclusiveAmount")),
            kdvToplam,
            Sayi(toplam?.Element(Cbc + "PayableAmount")),
            iade?.Element(Cbc + "ID")?.Value.Trim(),
            iade?.Element(Cbc + "IssueDate")?.Value is { } t2 && DateTime.TryParseExact(t2.Trim(), "yyyy-MM-dd", Kultur, DateTimeStyles.None, out var it) ? it : null);
    }

    private static GelenTaraf Taraf(XElement? p, string ad)
    {
        if (p == null)
            throw new InvalidOperationException($"Faturada {ad} bilgisi yok.");
        var kimlik = p.Elements(Cac + "PartyIdentification").Select(x => x.Element(Cbc + "ID"))
            .FirstOrDefault(x => x?.Attribute("schemeID")?.Value is "VKN" or "TCKN")?.Value.Trim()
            ?? throw new InvalidOperationException($"Faturada {ad} vergi/T.C. kimlik numarası yok.");
        var kisi = p.Element(Cac + "Person");
        var unvan = p.Element(Cac + "PartyName")?.Element(Cbc + "Name")?.Value.Trim();
        if (string.IsNullOrWhiteSpace(unvan) && kisi != null)
            unvan = $"{kisi.Element(Cbc + "FirstName")?.Value.Trim()} {kisi.Element(Cbc + "FamilyName")?.Value.Trim()}".Trim();
        var adres = p.Element(Cac + "PostalAddress");
        var sokak = string.Join(" ", new[] { "StreetName", "BuildingName", "BuildingNumber" }
            .Select(n => adres?.Element(Cbc + n)?.Value.Trim()).Where(v => !string.IsNullOrWhiteSpace(v)));
        var iletisim = p.Element(Cac + "Contact");
        return new GelenTaraf(kimlik, unvan ?? kimlik,
            p.Element(Cac + "PartyTaxScheme")?.Element(Cac + "TaxScheme")?.Element(Cbc + "Name")?.Value.Trim(),
            string.IsNullOrWhiteSpace(sokak) ? null : sokak,
            adres?.Element(Cbc + "CitySubdivisionName")?.Value.Trim(),
            adres?.Element(Cbc + "CityName")?.Value.Trim(),
            adres?.Element(Cbc + "PostalZone")?.Value.Trim(),
            iletisim?.Element(Cbc + "Telephone")?.Value.Trim(),
            iletisim?.Element(Cbc + "ElectronicMail")?.Value.Trim());
    }

    private static decimal Sayi(XElement? e) =>
        e != null && decimal.TryParse(e.Value.Trim(), NumberStyles.Number, Kultur, out var d) ? d : 0m;

    // UN/ECE birim kodunu programdaki birim adına çevirir (yeni ürün kartı açılırken).
    public static string BirimAdi(string kod) => kod switch
    {
        "KGM" => "Kg", "GRM" => "Gr", "TNE" => "Ton", "LTR" => "Lt", "MLT" => "Ml", "MTR" => "Mt", "CMT" => "Cm",
        "MTK" => "M2", "MTQ" => "M3", "PA" => "Paket", "BX" => "Kutu", "CT" => "Koli", "PR" => "Çift", "DZN" => "Düzine",
        "SET" => "Set", "HUR" => "Saat", "DAY" => "Gün", "MON" => "Ay", "ANN" => "Yıl", "KWH" => "kWh",
        _ => "Adet"
    };
}
