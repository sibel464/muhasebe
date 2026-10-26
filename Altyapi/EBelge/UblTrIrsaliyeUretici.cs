using System.Globalization;
using System.Xml.Linq;
using EtkiAlani.Modeller.Irsaliye;

namespace Altyapi.EBelge;

public sealed record UblIrsaliyeGirdisi(Irsaliye Irsaliye, string BelgeNo, Guid Ettn, UblTaraf Gonderici, UblTaraf Alici);

// GİB UBL-TR e-İrsaliye (DespatchAdvice, TEMELIRSALIYE) belgesi üretir; imzayı entegratör/mali mühür atar.
// Element sırası UBL 2.1 DespatchAdvice şemasındaki sıradır; değiştirilirse şema doğrulaması bozulur.
public static class UblTrIrsaliyeUretici
{
    public static readonly XNamespace Da = "urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2";
    private static readonly XNamespace Cac = UblTrFaturaUretici.Cac;
    private static readonly XNamespace Cbc = UblTrFaturaUretici.Cbc;
    private static readonly CultureInfo Kultur = CultureInfo.InvariantCulture;

    // Belgenin gönderilmeden önce sağlaması gereken kurallar. Boş liste: belge hazırlanabilir.
    public static List<string> Dogrula(UblIrsaliyeGirdisi g)
    {
        var hatalar = new List<string>();
        var i = g.Irsaliye;
        if (i.Tur != IrsaliyeTuru.Satis)
            hatalar.Add("Yalnız sevk (satış) irsaliyesi e-İrsaliye olarak düzenlenir; alış irsaliyesi karşı taraftan gelir.");
        if (i.Durum is not (IrsaliyeDurumu.Onaylandi or IrsaliyeDurumu.Faturalandi))
            hatalar.Add("Yalnız onaylanmış (sevk edilmiş) irsaliye e-İrsaliye olarak hazırlanabilir.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(g.BelgeNo, "^[A-Z0-9]{3}20[0-9]{2}[0-9]{9}$"))
            hatalar.Add($"e-İrsaliye numarası GİB biçiminde değil: '{g.BelgeNo}'.");
        if (i.Kalemler.Count(k => !k.Silindi) == 0)
            hatalar.Add("İrsaliyede kalem yok.");
        if (string.IsNullOrWhiteSpace(i.AracPlaka))
            hatalar.Add("Araç plakası girilmelidir.");
        if (string.IsNullOrWhiteSpace(i.SoforAdi) || i.SoforAdi.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
            hatalar.Add("Şoförün adı ve soyadı girilmelidir.");
        if (!EtkiAlani.Yardimcilar.KimlikNoDogrulayici.TcKimlikNoGecerliMi(i.SoforTckn))
            hatalar.Add("Şoförün geçerli T.C. kimlik numarası girilmelidir.");
        UblTrFaturaUretici.TarafDogrula("Firma (gönderici)", g.Gonderici, hatalar, adresZorunlu: true);
        UblTrFaturaUretici.TarafDogrula("Alıcı (cari)", g.Alici, hatalar, adresZorunlu: true);
        return hatalar;
    }

    public static XDocument Uret(UblIrsaliyeGirdisi g)
    {
        var i = g.Irsaliye;
        var kalemler = i.Kalemler.Where(k => !k.Silindi).OrderBy(k => k.Id).ToList();
        var sevk = i.SevkTarihi == default ? i.Tarih : i.SevkTarihi;
        var sofor = i.SoforAdi.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Teslimat adresi irsaliyede ayrıca girilmişse o, yoksa alıcının adresi.
        var teslimat = string.IsNullOrWhiteSpace(i.TeslimatAdres) ? g.Alici
            : g.Alici with { Adres = i.TeslimatAdres, Il = string.IsNullOrWhiteSpace(i.TeslimatIl) ? g.Alici.Il : i.TeslimatIl,
                Ilce = string.IsNullOrWhiteSpace(i.TeslimatIlce) ? g.Alici.Ilce : i.TeslimatIlce, PostaKodu = null };
        var adres = UblTrFaturaUretici.Adres(teslimat);

        var kok = new XElement(Da + "DespatchAdvice",
            new XAttribute(XNamespace.Xmlns + "cac", Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc),
            B("UBLVersionID", "2.1"),
            B("CustomizationID", "TR1.2.1"),
            B("ProfileID", "TEMELIRSALIYE"),
            B("ID", g.BelgeNo),
            B("CopyIndicator", "false"),
            B("UUID", g.Ettn.ToString().ToUpperInvariant()),
            B("IssueDate", i.Tarih.ToString("yyyy-MM-dd", Kultur)),
            B("IssueTime", i.Tarih.ToString("HH:mm:ss", Kultur)),
            B("DespatchAdviceTypeCode", "SEVK"),
            string.IsNullOrWhiteSpace(i.Aciklama) ? null : B("Note", i.Aciklama.Trim()),
            B("Note", "İrsaliye No: " + i.IrsaliyeNo),
            B("LineCountNumeric", kalemler.Count.ToString(Kultur)),
            UblTrFaturaUretici.Imza(g.Gonderici),
            new XElement(Cac + "DespatchSupplierParty", UblTrFaturaUretici.Taraf(g.Gonderici)),
            new XElement(Cac + "DeliveryCustomerParty", UblTrFaturaUretici.Taraf(g.Alici)),
            new XElement(Cac + "Shipment",
                B("ID", "1"),
                new XElement(Cac + "ShipmentStage",
                    new XElement(Cac + "TransportMeans",
                        new XElement(Cac + "RoadTransport",
                            new XElement(Cbc + "LicensePlateID", new XAttribute("schemeID", "PLAKA"), Plaka(i.AracPlaka)))),
                    new XElement(Cac + "DriverPerson",
                        B("FirstName", string.Join(' ', sofor[..^1])),
                        B("FamilyName", sofor[^1]),
                        B("Title", "Şoför"),
                        B("NationalityID", i.SoforTckn.Trim()))),
                new XElement(Cac + "Delivery",
                    new XElement(Cac + "DeliveryAddress", adres.Elements()),
                    new XElement(Cac + "Despatch",
                        B("ActualDespatchDate", sevk.ToString("yyyy-MM-dd", Kultur)),
                        B("ActualDespatchTime", sevk.ToString("HH:mm:ss", Kultur))))),
            kalemler.Select((k, n) => new XElement(Cac + "DespatchLine",
                B("ID", (n + 1).ToString(Kultur)),
                new XElement(Cbc + "DeliveredQuantity", new XAttribute("unitCode", UblTrFaturaUretici.BirimKodu(k.Birim)), UblTrFaturaUretici.Sayi(k.Miktar)),
                new XElement(Cac + "OrderLineReference", B("LineID", (n + 1).ToString(Kultur))),
                new XElement(Cac + "Item",
                    B("Name", string.IsNullOrWhiteSpace(k.UrunAd) ? k.UrunKodu : k.UrunAd),
                    string.IsNullOrWhiteSpace(k.UrunKodu) ? null
                        : new XElement(Cac + "SellersItemIdentification", B("ID", k.UrunKodu)))))
        );
        return new XDocument(new XDeclaration("1.0", "UTF-8", null), kok);
    }

    public static string UretMetin(UblIrsaliyeGirdisi g)
    {
        using var yazici = new Utf8StringWriter();
        Uret(g).Save(yazici);
        return yazici.ToString();
    }

    // "34 abc 123" → "34ABC123" (GİB plakayı boşluksuz büyük harfle bekler).
    public static string Plaka(string plaka) => new string(plaka.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpper(new CultureInfo("tr-TR"));

    private sealed class Utf8StringWriter : StringWriter
    {
        public override System.Text.Encoding Encoding => new System.Text.UTF8Encoding(false);
    }

    private static XElement B(string ad, string deger) => new(Cbc + ad, deger);
}
