using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Xml.Linq;
using EtkiAlani.Modeller.Fatura;
using Uygulama.Arayuzler;

namespace Altyapi.EBelge;

// Uyumsoft özel entegratörü (SOAP "Integration" servisi). Belge UBL-TR olarak gönderilir; mali mühürü entegratör basar.
// Test ortamı herkese açık "Uyumsoft"/"Uyumsoft" hesabıyla denenebilir.
public sealed class UyumsoftEBelgeEntegratoru : IEBelgeCevrimiciEntegrator
{
    public const string CanliAdres = "https://efatura.uyumsoft.com.tr/Services/Integration";
    public const string TestAdres = "https://efatura-test.uyumsoft.com.tr/Services/Integration";

    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private static readonly XNamespace Tns = "http://tempuri.org/";
    private static readonly XNamespace Cbc = UblTrFaturaUretici.Cbc;
    private static readonly XNamespace Cac = UblTrFaturaUretici.Cac;
    private static readonly HttpClient OrtakIstemci = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly string _kullanici;
    private readonly string _sifre;
    private readonly string _adres;
    private readonly HttpClient _http;

    public UyumsoftEBelgeEntegratoru(string kullanici, string sifre, bool testOrtami, HttpClient? http = null, string? adres = null)
    {
        _kullanici = kullanici?.Trim() ?? string.Empty;
        _sifre = sifre ?? string.Empty;
        TestOrtami = testOrtami;
        _adres = adres ?? (testOrtami ? TestAdres : CanliAdres);
        _http = http ?? OrtakIstemci;
    }

    public bool TestOrtami { get; }
    public string Ad => TestOrtami ? "Uyumsoft (TEST ortamı)" : "Uyumsoft";
    public bool DogrudanGonderir => true;

    public async Task<EBelgeGonderimSonucu> GonderAsync(EBelgePaketi paket, CancellationToken cancellationToken = default)
    {
        if (paket.Tur == EBelgeTuru.EIrsaliye)
            return await IrsaliyeGonderAsync(paket, cancellationToken);
        var ubl = XDocument.Parse(paket.Xml).Root ?? throw new InvalidOperationException("e-Belge XML'i boş.");
        var musteri = ubl.Element(Cac + "AccountingCustomerParty")?.Element(Cac + "Party");
        var unvan = musteri?.Element(Cac + "PartyName")?.Element(Cbc + "Name")?.Value;
        if (string.IsNullOrWhiteSpace(unvan))
        {
            var kisi = musteri?.Element(Cac + "Person");
            unvan = $"{kisi?.Element(Cbc + "FirstName")?.Value} {kisi?.Element(Cbc + "FamilyName")?.Value}".Trim();
        }

        var eposta = musteri?.Element(Cac + "Contact")?.Element(Cbc + "ElectronicMail")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(eposta) || !eposta.Contains('@')) eposta = null;

        string? etiket = null;
        if (paket.Tur == EBelgeTuru.EFatura)
        {
            etiket = string.IsNullOrWhiteSpace(paket.AliciEtiketi) ? await AliciEtiketiAsync(paket.AliciVknTckn, cancellationToken) : paket.AliciEtiketi.Trim();
            if (etiket == null)
                return new EBelgeGonderimSonucu(false, EBelgeDurumu.Hata,
                    $"Alıcı ({paket.AliciVknTckn}) GİB e-Fatura sistemine kayıtlı görünmüyor. Cari kartta 'e-Fatura mükellefi' işaretini kaldırıp e-Arşiv olarak gönderin.");
        }

        // UBL belgesinin çocukları servisin <Invoice> öğesine olduğu gibi taşınır (ad alanları korunur).
        var fatura = new XElement(Tns + "Invoice", ubl.Attributes().Where(a => a.IsNamespaceDeclaration && a.Name.Namespace == XNamespace.Xmlns), ubl.Elements());
        var bilgi = new XElement(Tns + "InvoiceInfo",
            new XAttribute("LocalDocumentId", paket.FaturaNo),
            fatura,
            new XElement(Tns + "TargetCustomer",
                new XAttribute("VknTckn", paket.AliciVknTckn),
                etiket != null ? new XAttribute("Alias", etiket) : null,
                new XAttribute("Title", unvan ?? string.Empty)),
            paket.Tur == EBelgeTuru.EArsiv ? new XElement(Tns + "EArchiveInvoiceInfo", new XAttribute("DeliveryType", "Electronic")) : null,
            new XElement(Tns + "Scenario", paket.Tur == EBelgeTuru.EFatura ? "eInvoice" : "eArchive"),
            // e-Arşiv fatura alıcıya elektronik olarak teslim edilmelidir: alıcının e-postası varsa entegratör PDF'i gönderir.
            paket.Tur == EBelgeTuru.EArsiv && eposta != null
                ? new XElement(Tns + "Notification",
                    new XElement(Tns + "Mailing",
                        new XAttribute("EnableNotification", "true"), new XAttribute("To", eposta),
                        new XElement(Tns + "Subject", $"{paket.FaturaNo} numaralı e-Arşiv faturanız"),
                        new XElement(Tns + "Attachment", new XAttribute("Xml", "false"), new XAttribute("Pdf", "true"),
                            new XAttribute("Html", "false"), new XAttribute("AdditionalDocuments", "false"))))
                : null,
            new XElement(Tns + "CreateDateUtc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)));

        var sonuc = await CagirAsync("SendInvoice", new XElement(Tns + "SendInvoice", new XElement(Tns + "invoices", bilgi)), cancellationToken);
        if (!Basarili(sonuc, out var mesaj))
            return new EBelgeGonderimSonucu(false, EBelgeDurumu.Hata, "Entegratör belgeyi kabul etmedi: " + mesaj);

        var kimlik = sonuc.Elements(Tns + "Value").FirstOrDefault();
        var no = kimlik?.Attribute("Number")?.Value;
        return new EBelgeGonderimSonucu(true, EBelgeDurumu.Gonderildi,
            $"{Ad} üzerinden gönderildi" + (string.IsNullOrEmpty(no) ? "." : $" (belge no {no}).") +
            (paket.Tur == EBelgeTuru.EFatura ? $" Alıcı etiketi: {etiket}."
                : eposta != null ? $" Fatura PDF'i alıcıya e-postayla iletilecek ({eposta})."
                : " Alıcının e-posta adresi kayıtlı değil: faturayı (PDF) alıcıya siz iletin."));
    }

    // e-İrsaliye: alıcı e-İrsaliye sistemine kayıtlı olmalıdır (posta kutusu etiketi irsaliye servisinden sorgulanır).
    private async Task<EBelgeGonderimSonucu> IrsaliyeGonderAsync(EBelgePaketi paket, CancellationToken cancellationToken)
    {
        var belge = XDocument.Parse(paket.Xml).Root ?? throw new InvalidOperationException("e-İrsaliye XML'i boş.");
        var alici = belge.Element(Cac + "DeliveryCustomerParty")?.Element(Cac + "Party");
        var unvan = alici?.Element(Cac + "PartyName")?.Element(Cbc + "Name")?.Value;
        if (string.IsNullOrWhiteSpace(unvan))
        {
            var kisi = alici?.Element(Cac + "Person");
            unvan = $"{kisi?.Element(Cbc + "FirstName")?.Value} {kisi?.Element(Cbc + "FamilyName")?.Value}".Trim();
        }
        var etiket = await IrsaliyeEtiketiAsync(paket.AliciVknTckn, cancellationToken);
        if (etiket == null)
            return new EBelgeGonderimSonucu(false, EBelgeDurumu.Hata,
                $"Alıcı ({paket.AliciVknTckn}) GİB e-İrsaliye sistemine kayıtlı görünmüyor. Bu alıcıya kâğıt irsaliye düzenleyin.");

        var bilgi = new XElement(Tns + "DespatchInfo",
            new XAttribute("LocalDocumentId", paket.FaturaNo),
            new XElement(belge),
            new XElement(Tns + "TargetCustomer",
                new XAttribute("VknTckn", paket.AliciVknTckn), new XAttribute("Alias", etiket), new XAttribute("Title", unvan ?? string.Empty)));
        var sonuc = await CagirAsync("SendDespatch", new XElement(Tns + "SendDespatch", new XElement(Tns + "despatches", bilgi)), cancellationToken, "IDespatchIntegration");
        if (!Basarili(sonuc, out var mesaj))
            return new EBelgeGonderimSonucu(false, EBelgeDurumu.Hata, "Entegratör e-İrsaliyeyi kabul etmedi: " + mesaj);
        var no = sonuc.Elements(Tns + "Value").FirstOrDefault()?.Attribute("Number")?.Value;
        return new EBelgeGonderimSonucu(true, EBelgeDurumu.Gonderildi,
            $"e-İrsaliye {Ad} üzerinden gönderildi" + (string.IsNullOrEmpty(no) ? "." : $" (belge no {no}).") + $" Alıcı etiketi: {etiket}.");
    }

    private async Task<string?> IrsaliyeEtiketiAsync(string vknTckn, CancellationToken cancellationToken)
    {
        var sonuc = await CagirAsync("GetUserAliasses", new XElement(Tns + "GetUserAliasses", new XElement(Tns + "vknTckn", vknTckn.Trim())), cancellationToken, "IDespatchIntegration");
        if (!Basarili(sonuc, out var mesaj))
        {
            if (mesaj.Contains("bulun", StringComparison.OrdinalIgnoreCase) || mesaj.Contains("not found", StringComparison.OrdinalIgnoreCase) || mesaj.Contains("kayıtlı değil", StringComparison.OrdinalIgnoreCase))
                return null;
            throw new InvalidOperationException("Alıcı sorgulanamadı: " + mesaj);
        }
        var etiketler = sonuc.Element(Tns + "Value")?.Elements(Tns + "DespatchReceiverboxAliases")
            .Where(e => (string?)e.Attribute("Enabled") != "false")
            .Select(e => (string?)e.Attribute("Alias")).Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? [];
        return etiketler.FirstOrDefault(e => e!.StartsWith("urn:mail:", StringComparison.OrdinalIgnoreCase)) ?? etiketler.FirstOrDefault();
    }

    public async Task<EBelgeDurumBilgisi?> IrsaliyeDurumSorgulaAsync(Guid ettn, CancellationToken cancellationToken = default)
    {
        var sonuc = await CagirAsync("QueryOutboxDespatchStatus", new XElement(Tns + "QueryOutboxDespatchStatus",
            new XElement(Tns + "despatchIds", new XElement(Tns + "string", ettn.ToString().ToUpperInvariant()))), cancellationToken, "IDespatchIntegration");
        if (!Basarili(sonuc, out var mesaj))
        {
            if (mesaj.Contains("bulun", StringComparison.OrdinalIgnoreCase) || mesaj.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return null;
            throw new InvalidOperationException("Durum sorgulanamadı: " + mesaj);
        }
        var deger = sonuc.Elements(Tns + "Value").FirstOrDefault();
        if (deger == null) return null;
        var durum = (string?)deger.Attribute("StatusEnum") ?? string.Empty;
        var aciklama = (string?)deger.Attribute("Message");
        var d = durum switch
        {
            "Approved" or "PartialApproved" => EBelgeDurumu.KabulEdildi,
            "Canceled" or "Deleted" => EBelgeDurumu.IptalEdildi,
            "Error" or "Draft" => EBelgeDurumu.Hata,
            _ => EBelgeDurumu.Gonderildi
        };
        var metin = durum switch
        {
            "Approved" => "Entegratör: e-İrsaliye alıcıya ulaştı / onaylandı",
            "PartialApproved" => "Entegratör: alıcı irsaliyeyi kısmen kabul etti (irsaliye yanıtını kontrol edin)",
            _ => DurumAciklamasi(durum)
        };
        return new EBelgeDurumBilgisi(d, metin + (string.IsNullOrWhiteSpace(aciklama) ? "" : $" — {aciklama}"));
    }

    public async Task<byte[]> PdfAsync(Guid ettn, CancellationToken cancellationToken = default)
    {
        var sonuc = await CagirAsync("GetOutboxInvoicePdf", new XElement(Tns + "GetOutboxInvoicePdf", new XElement(Tns + "invoiceId", ettn.ToString().ToUpperInvariant())), cancellationToken);
        if (!Basarili(sonuc, out var mesaj))
            throw new InvalidOperationException("PDF alınamadı: " + mesaj);
        var veri = sonuc.Element(Tns + "Value")?.Element(Tns + "Data")?.Value;
        if (string.IsNullOrWhiteSpace(veri))
            throw new InvalidOperationException("Entegratör PDF göndermedi (belge henüz işleniyor olabilir).");
        var baytlar = Convert.FromBase64String(veri);
        if (baytlar.Length > 2 && baytlar[0] == (byte)'P' && baytlar[1] == (byte)'K')
        {
            using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(baytlar));
            var giris = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) ?? zip.Entries.First();
            using var akis = giris.Open();
            using var bellek = new MemoryStream();
            await akis.CopyToAsync(bellek, cancellationToken);
            baytlar = bellek.ToArray();
        }
        return baytlar;
    }

    public async Task EArsivIptalAsync(Guid ettn, DateTime iptalTarihi, CancellationToken cancellationToken = default)
    {
        var sonuc = await CagirAsync("CancelEArchiveInvoice", new XElement(Tns + "CancelEArchiveInvoice",
            new XElement(Tns + "request",
                new XAttribute("InvoiceId", ettn.ToString().ToUpperInvariant()),
                new XAttribute("CancelDate", iptalTarihi.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)))), cancellationToken);
        if (!Basarili(sonuc, out var mesaj) || (string?)sonuc.Attribute("Value") == "false")
            throw new InvalidOperationException("e-Arşiv fatura iptal edilemedi: " + mesaj +
                (mesaj.Contains("Kuyruk", StringComparison.OrdinalIgnoreCase) || mesaj.Contains("işleniyor", StringComparison.OrdinalIgnoreCase)
                    ? "\nBelge entegratörde henüz işleniyor; birkaç dakika sonra tekrar deneyin." : ""));
    }

    public async Task<FirmaSorguSonucu?> FirmaSorgulaAsync(string vknTckn, CancellationToken cancellationToken = default)
    {
        var kimlik = new string((vknTckn ?? "").Where(char.IsDigit).ToArray());
        if (kimlik.Length is not (10 or 11)) return null;
        var sonuc = await CagirAsync("TryToGetAddressFromVknTckn", new XElement(Tns + "TryToGetAddressFromVknTckn",
            new XElement(Tns + "vknTckn", kimlik), new XElement(Tns + "queryType", "Normal")), cancellationToken, "ICustomerServiceBase");
        if (!Basarili(sonuc, out _)) return null;
        var d = sonuc.Element(Tns + "Value");
        string? D(XElement? e, string ad) => e?.Element(Tns + ad)?.Value is { Length: > 0 } v ? v.Trim() : null;
        var unvan = D(d, "Unvan");
        var ad = D(d, "Adi");
        var soyad = D(d, "Soyadi");
        if (d == null || (unvan == null && ad == null)) return null;
        var adres = d.Element(Tns + "IsAdresi") is { HasElements: true } i && D(i, "IlAdi") != null ? i : d.Element(Tns + "IkametgahAdresi");
        var acik = string.Join(" ", new[] { D(adres, "MahalleSemt"), D(adres, "CaddeSokak"),
                D(adres, "KapiNO") is { } k ? "No:" + k : null, D(adres, "DaireNO") is { } dn ? "D:" + dn : null }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        return new FirmaSorguSonucu(kimlik, unvan ?? $"{ad} {soyad}".Trim(), ad, soyad,
            D(d, "VergiDairesiAdi"), acik.Length > 0 ? acik : null, Baslik(D(adres, "IlAdi")), Baslik(D(adres, "IlceAdi")),
            D(d, "FaalTerkBilgisi") is null or "Faal");
    }

    // "İSTANBUL" → "İstanbul" (Türkçe büyük/küçük harf kurallarıyla).
    private static string? Baslik(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        return string.Join(' ', s.Trim().ToLower(tr).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => char.ToUpper(p[0], tr) + p[1..]));
    }

    public async Task<EntegratorHesapBilgisi> BaglantiDeneAsync(CancellationToken cancellationToken = default)
    {
        var sonuc = await CagirAsync("WhoAmI", new XElement(Tns + "WhoAmI"), cancellationToken);
        if (!Basarili(sonuc, out var mesaj))
            throw new InvalidOperationException("Entegratöre bağlanılamadı: " + mesaj);
        var deger = sonuc.Element(Tns + "Value");
        var musteri = deger?.Element(Tns + "Customer");
        var hizmet = deger?.Element(Tns + "Services");
        return new EntegratorHesapBilgisi(
            musteri?.Element(Tns + "Name")?.Value ?? string.Empty,
            musteri?.Element(Tns + "VkTckNo")?.Value ?? string.Empty,
            (string?)hizmet?.Attribute("HasEInvoice") == "true",
            (string?)hizmet?.Attribute("HasEarchive") == "true");
    }

    public async Task<string?> AliciEtiketiAsync(string vknTckn, CancellationToken cancellationToken = default)
    {
        var sonuc = await CagirAsync("GetUserAliasses", new XElement(Tns + "GetUserAliasses", new XElement(Tns + "vknTckn", vknTckn.Trim())), cancellationToken);
        if (!Basarili(sonuc, out var mesaj))
        {
            // Kayıtlı olmayan VKN için servis "bulunamadı" der; bu bir bağlantı hatası değil.
            if (mesaj.Contains("bulun", StringComparison.OrdinalIgnoreCase) || mesaj.Contains("not found", StringComparison.OrdinalIgnoreCase) || mesaj.Contains("kayıtlı değil", StringComparison.OrdinalIgnoreCase))
                return null;
            throw new InvalidOperationException("Alıcı sorgulanamadı: " + mesaj);
        }
        var etiketler = sonuc.Element(Tns + "Value")?.Elements(Tns + "ReceiverboxAliases")
            .Where(e => (string?)e.Attribute("Enabled") != "false")
            .Select(e => (string?)e.Attribute("Alias"))
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .ToList() ?? [];
        // urn:mail: biçimli etiket GİB'in resmi biçimidir; yoksa ilk etiket.
        return etiketler.FirstOrDefault(e => e!.StartsWith("urn:mail:", StringComparison.OrdinalIgnoreCase)) ?? etiketler.FirstOrDefault();
    }

    public async Task<EBelgeDurumBilgisi?> DurumSorgulaAsync(Guid ettn, EBelgeTuru tur, CancellationToken cancellationToken = default)
    {
        var sonuc = await CagirAsync("QueryOutboxInvoiceStatus", new XElement(Tns + "QueryOutboxInvoiceStatus",
            new XElement(Tns + "invoiceIds", new XElement(Tns + "string", ettn.ToString().ToUpperInvariant()))), cancellationToken);
        if (!Basarili(sonuc, out var mesaj))
        {
            if (mesaj.Contains("bulun", StringComparison.OrdinalIgnoreCase) || mesaj.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return null;
            throw new InvalidOperationException("Durum sorgulanamadı: " + mesaj);
        }
        var deger = sonuc.Elements(Tns + "Value").FirstOrDefault();
        if (deger == null) return null;
        var durum = (string?)deger.Attribute("Status") ?? string.Empty;
        var aciklama = (string?)deger.Attribute("Message");
        return new EBelgeDurumBilgisi(DurumCevir(durum, tur), DurumAciklamasi(durum) + (string.IsNullOrWhiteSpace(aciklama) ? "" : $" — {aciklama}"));
    }

    public static EBelgeDurumu DurumCevir(string durum, EBelgeTuru tur) => durum switch
    {
        "Approved" => EBelgeDurumu.KabulEdildi,
        "Declined" or "Return" => EBelgeDurumu.Reddedildi,
        "Canceled" or "EArchivedCanceled" => EBelgeDurumu.IptalEdildi,
        "Error" => EBelgeDurumu.Hata,
        "NotPrepared" or "NotSend" or "Draft" => EBelgeDurumu.Hata,
        _ => EBelgeDurumu.Gonderildi // Queued, Processing, SentToGib, WaitingForAprovement
    };

    private static string DurumAciklamasi(string durum) => durum switch
    {
        "Approved" => "Entegratör: belge GİB'e ulaştı / onaylandı",
        "WaitingForAprovement" => "Entegratör: alıcının kabul/red yanıtı bekleniyor",
        "Declined" => "Entegratör: alıcı reddetti",
        "Return" => "Entegratör: alıcı iade etti",
        "Canceled" or "EArchivedCanceled" => "Entegratör: belge iptal edildi",
        "Error" => "Entegratör: belge hatalı, gönderilemedi",
        "Queued" or "Processing" => "Entegratör: belge işleniyor",
        "SentToGib" => "Entegratör: GİB'e iletildi, yanıt bekleniyor",
        "NotPrepared" or "NotSend" or "Draft" => "Entegratör: belge gönderilmemiş (taslak)",
        _ => "Entegratör durumu: " + durum
    };

    public async Task<IReadOnlyList<GelenEBelgeOzeti>> GelenListeAsync(DateTime baslangic, DateTime bitis, CancellationToken cancellationToken = default)
    {
        var liste = new List<GelenEBelgeOzeti>();
        for (var sayfa = 0; sayfa < 50; sayfa++)
        {
            var sorgu = new XElement(Tns + "query",
                new XAttribute("PageIndex", sayfa), new XAttribute("PageSize", 100), new XAttribute("OnlyNewestInvoices", "false"),
                new XElement(Tns + "ExecutionStartDate", baslangic.Date.ToString("yyyy-MM-ddT00:00:00", CultureInfo.InvariantCulture)),
                new XElement(Tns + "ExecutionEndDate", bitis.Date.ToString("yyyy-MM-ddT23:59:59", CultureInfo.InvariantCulture)));
            var sonuc = await CagirAsync("GetInboxInvoiceList", new XElement(Tns + "GetInboxInvoiceList", sorgu), cancellationToken);
            if (!Basarili(sonuc, out var mesaj))
                throw new InvalidOperationException("Gelen faturalar alınamadı: " + mesaj);
            var deger = sonuc.Element(Tns + "Value");
            foreach (var x in deger?.Elements(Tns + "Items") ?? [])
            {
                string D(string ad) => x.Element(Tns + ad)?.Value ?? string.Empty;
                DateTime.TryParse(D("ExecutionDate") is { Length: > 0 } t ? t : D("CreateDateUtc"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var tarih);
                decimal.TryParse(D("PayableAmount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var tutar);
                liste.Add(new GelenEBelgeOzeti(D("DocumentId"), D("InvoiceId"), tarih, D("TargetTitle"), D("TargetTcknVkn"), tutar,
                    D("DocumentCurrencyCode") is { Length: > 0 } p ? p : "TRY", DurumAciklamasi(D("Status")).Replace("Entegratör: ", "")));
            }
            var toplamSayfa = int.TryParse((string?)deger?.Attribute("TotalPages"), out var ts) ? ts : 0;
            if (sayfa + 1 >= toplamSayfa) break;
        }
        return liste;
    }

    public async Task<string> GelenXmlAsync(string ettn, CancellationToken cancellationToken = default)
    {
        var sonuc = await CagirAsync("GetInboxInvoiceData", new XElement(Tns + "GetInboxInvoiceData", new XElement(Tns + "invoiceId", ettn)), cancellationToken);
        if (!Basarili(sonuc, out var mesaj))
            throw new InvalidOperationException("Fatura indirilemedi: " + mesaj);
        var veri = sonuc.Element(Tns + "Value")?.Element(Tns + "Data")?.Value;
        if (string.IsNullOrWhiteSpace(veri))
            throw new InvalidOperationException("Entegratör fatura içeriğini göndermedi.");
        var baytlar = Convert.FromBase64String(veri);
        if (baytlar.Length > 2 && baytlar[0] == (byte)'P' && baytlar[1] == (byte)'K')
        {
            using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(baytlar));
            var giris = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) ?? zip.Entries.First();
            using var akis = giris.Open();
            using var okuyucu = new StreamReader(akis, Encoding.UTF8);
            return await okuyucu.ReadToEndAsync(cancellationToken);
        }
        return Encoding.UTF8.GetString(baytlar).TrimStart('﻿');
    }

    // ---- SOAP ----

    private static bool Basarili(XElement sonuc, out string mesaj)
    {
        mesaj = (string?)sonuc.Attribute("Message") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(mesaj)) mesaj = "bilinmeyen hata";
        return (string?)sonuc.Attribute("IsSucceded") == "true";
    }

    // e-İrsaliye ayrı serviste: .../Services/DespatchIntegration
    private string IrsaliyeAdresi => _adres.EndsWith("/Integration", StringComparison.OrdinalIgnoreCase)
        ? _adres[..^"Integration".Length] + "DespatchIntegration" : _adres;

    private async Task<XElement> CagirAsync(string islem, XElement govde, CancellationToken cancellationToken, string sozlesme = "IIntegration")
    {
        if (string.IsNullOrWhiteSpace(_kullanici) || string.IsNullOrEmpty(_sifre))
            throw new InvalidOperationException("Entegratör kullanıcı adı ve şifresi Ayarlar > Firma & Parametreler > e-Belge bölümünde girilmemiş.");

        var zarf = new XDocument(new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", Soap),
            new XElement(Soap + "Header",
                new XElement(Wsse + "Security", new XAttribute(Soap + "mustUnderstand", "1"),
                    new XElement(Wsse + "UsernameToken",
                        new XElement(Wsse + "Username", _kullanici),
                        new XElement(Wsse + "Password",
                            new XAttribute("Type", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText"),
                            _sifre)))),
            new XElement(Soap + "Body", govde)));

        using var istek = new HttpRequestMessage(HttpMethod.Post, sozlesme == "IDespatchIntegration" ? IrsaliyeAdresi : _adres)
        {
            Content = new StringContent(zarf.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml")
        };
        istek.Headers.Add("SOAPAction", $"\"http://tempuri.org/{sozlesme}/{islem}\"");

        HttpResponseMessage yanit;
        try
        {
            yanit = await _http.SendAsync(istek, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Entegratöre ulaşılamadı (internet bağlantısını kontrol edin): {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Entegratör zamanında yanıt vermedi. Belge gitmiş olabilir: birkaç dakika sonra 'Durumu Sorgula' ile kontrol edin.", ex);
        }

        using (yanit)
        {
            var metin = await yanit.Content.ReadAsStringAsync(cancellationToken);
            XDocument belge;
            try { belge = XDocument.Parse(metin); }
            catch (System.Xml.XmlException)
            {
                throw new InvalidOperationException($"Entegratörden beklenmeyen yanıt (HTTP {(int)yanit.StatusCode}).");
            }
            var hata = belge.Descendants(Soap + "Fault").FirstOrDefault();
            if (hata != null)
            {
                var neden = hata.Descendants().FirstOrDefault(e => e.Name.LocalName is "faultstring" or "Text")?.Value ?? "bilinmeyen hata";
                if (neden.Contains("authenticat", StringComparison.OrdinalIgnoreCase) || neden.Contains("security", StringComparison.OrdinalIgnoreCase) ||
                    neden.Contains("kullanıcı", StringComparison.OrdinalIgnoreCase) || neden.Contains("şifre", StringComparison.OrdinalIgnoreCase))
                    neden = "Kullanıcı adı ya da şifre hatalı (" + neden + ")";
                throw new InvalidOperationException("Entegratör hatası: " + neden);
            }
            return belge.Descendants(Tns + islem + "Result").FirstOrDefault()
                   ?? throw new InvalidOperationException($"Entegratörden beklenmeyen yanıt (HTTP {(int)yanit.StatusCode}).");
        }
    }
}
