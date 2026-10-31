using System.Net;
using System.Text;
using Altyapi.EBelge;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Yardimcilar;
using Uygulama.Arayuzler;
using Xunit;
using Xunit.Sdk;

namespace Altyapi.EntegrasyonTestleri;

// Uyumsoft entegratörü: SOAP isteğinin biçimi ve yanıtların yorumu sahte sunucuyla, gerçek gönderim herkese açık
// TEST ortamıyla (kullanıcı/şifre "Uyumsoft") denenir. Test ortamına ulaşılamazsa çevrimiçi testler atlanır.
public sealed class UyumsoftEntegratorTests
{
    private const string TestFirmaVkn = "9000068418";

    private static UyumsoftEBelgeEntegratoru TestOrtami() => new("Uyumsoft", "Uyumsoft", testOrtami: true);

    internal static async Task CevrimiciGerekirAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var yanit = await http.GetAsync(UyumsoftEBelgeEntegratoru.TestAdres + "?wsdl");
            if (!yanit.IsSuccessStatusCode) throw SkipException.ForSkip($"Uyumsoft test ortamı yanıt vermiyor (HTTP {(int)yanit.StatusCode}).");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw SkipException.ForSkip("Uyumsoft test ortamına ulaşılamıyor: " + ex.Message);
        }
    }

    private static Fatura DenemeFaturasi()
    {
        var sonuc = FaturaHesaplayici.Hesapla([new FaturaHesaplayici.Girdi(2m, 150m, 0m, 20m), new FaturaHesaplayici.Girdi(1m, 99.9m, 10m, 10m)], 0m);
        var f = new Fatura
        {
            FaturaNo = "MYS" + DateTime.Now.Year + Random.Shared.NextInt64(100000000, 999999999), Tur = FaturaTuru.Satis, Durum = BelgeDurumu.Onaylandi,
            Tarih = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc),
            KdvToplam = sonuc.KdvToplam, GenelToplam = sonuc.GenelToplam, Aciklama = "Muhasebe Yönetim entegrasyon denemesi"
        };
        (string Ad, decimal Miktar, decimal Fiyat, decimal Isk, decimal Kdv)[] kalemler = [("Deneme ürünü", 2m, 150m, 0m, 20m), ("Deneme hizmeti", 1m, 99.9m, 10m, 10m)];
        for (var i = 0; i < kalemler.Length; i++)
        {
            var k = kalemler[i];
            var s = sonuc.Satirlar[i];
            f.Kalemler.Add(new FaturaKalemi
            {
                Id = i + 1, UrunKodu = "U" + i, UrunAd = k.Ad, Birim = "Adet", Miktar = k.Miktar, BirimFiyat = k.Fiyat, IskontoOrani = k.Isk,
                IskontoTutari = s.SatirIskontosu, KdvOrani = k.Kdv, KdvTutari = s.Kdv, SatirToplami = s.Toplam
            });
        }
        return f;
    }

    private static UblTaraf TestFirmasi() => new(TestFirmaVkn, "UYUMSOFT BİLGİ SİSTEMLERİ VE TEKNOLOJİLERİ TİCARET A.Ş.", VergiDairesi: "Esenler",
        Adres: "Uyumsoft", Ilce: "Esenler", Il: "İstanbul");

    private static EBelgePaketi Paket(EBelgeTuru tur, UblTaraf alici)
    {
        var f = DenemeFaturasi();
        var ettn = Guid.NewGuid();
        var girdi = new UblFaturaGirdisi(f, ettn, tur, EFaturaSenaryosu.Temel, TestFirmasi(), alici);
        Assert.Empty(UblTrFaturaUretici.Dogrula(girdi));
        return new EBelgePaketi(f.FaturaNo, ettn, tur, alici.VknTckn, null, UblTrFaturaUretici.UretMetin(girdi));
    }

    [Fact]
    public async Task Test_ortaminda_baglanti_alici_sorgusu_gonderim_ve_durum_sorgusu_calisir()
    {
        await CevrimiciGerekirAsync();
        var e = TestOrtami();

        var hesap = await e.BaglantiDeneAsync();
        Assert.Equal(TestFirmaVkn, hesap.VknTckn);
        Assert.True(hesap.EFatura);

        Assert.NotNull(await e.AliciEtiketiAsync(TestFirmaVkn));

        // e-Arşiv: nihai tüketiciye.
        var arsiv = Paket(EBelgeTuru.EArsiv, new UblTaraf(UblTrFaturaUretici.NihaiTuketiciTckn, "Ayşe Yılmaz", Il: "İstanbul", Ilce: "Kadıköy"));
        var s1 = await e.GonderAsync(arsiv);
        Assert.True(s1.Basarili, s1.Mesaj);
        Assert.Equal(EBelgeDurumu.Gonderildi, s1.Durum);
        var d1 = await e.DurumSorgulaAsync(arsiv.Ettn, EBelgeTuru.EArsiv);
        Assert.NotNull(d1);
        Assert.NotEqual(EBelgeDurumu.Hata, d1!.Durum);

        // e-Fatura: test firmasının kendi posta kutusuna.
        var efatura = Paket(EBelgeTuru.EFatura, TestFirmasi());
        var s2 = await e.GonderAsync(efatura);
        Assert.True(s2.Basarili, s2.Mesaj);
        Assert.NotNull(await e.DurumSorgulaAsync(efatura.Ettn, EBelgeTuru.EFatura));

        // Aynı ETTN ikinci kez gönderilemez (çift fatura oluşmaz).
        var tekrar = await e.GonderAsync(efatura);
        Assert.False(tekrar.Basarili);
    }

    [Fact]
    public async Task Test_ortaminda_gelen_faturalar_listelenir_ve_indirilen_xml_okunur()
    {
        await CevrimiciGerekirAsync();
        var e = TestOrtami();
        var liste = await e.GelenListeAsync(DateTime.Today.AddDays(-30), DateTime.Today);
        Assert.NotEmpty(liste);
        var ilk = liste.First(x => x.Ettn.Length > 0);
        var xml = await e.GelenXmlAsync(ilk.Ettn);
        var okunan = UblTrFaturaOkuyucu.Oku(xml);
        Assert.Equal(ilk.FaturaNo, okunan.FaturaNo);
    }

    // ---- Sahte sunucu ----

    private sealed class SahteSunucu(Func<string, string, string> yanitla) : HttpMessageHandler
    {
        public List<(string Islem, string Govde)> Istekler { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var islem = request.Headers.GetValues("SOAPAction").Single().Trim('"').Split('/').Last();
            var govde = await request.Content!.ReadAsStringAsync(cancellationToken);
            Istekler.Add((islem, govde));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(yanitla(islem, govde), Encoding.UTF8, "text/xml") };
        }
    }

    private static string Zarf(string icerik) =>
        $"<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\"><s:Body>{icerik}</s:Body></s:Envelope>";

    [Fact]
    public async Task Istek_ubl_belgesini_ve_aliciyi_tasir_yanit_dogru_yorumlanir()
    {
        var sunucu = new SahteSunucu((islem, _) => islem switch
        {
            "GetUserAliasses" => Zarf("<GetUserAliassesResponse xmlns=\"http://tempuri.org/\"><GetUserAliassesResult IsSucceded=\"true\"><Value>" +
                                      "<ReceiverboxAliases Alias=\"defaultpk\" Enabled=\"true\"/><ReceiverboxAliases Alias=\"urn:mail:defaultpk@firma.com\" Enabled=\"true\"/>" +
                                      "</Value></GetUserAliassesResult></GetUserAliassesResponse>"),
            "SendInvoice" => Zarf("<SendInvoiceResponse xmlns=\"http://tempuri.org/\"><SendInvoiceResult IsSucceded=\"true\"><Value Id=\"x\" Number=\"MYS2026000000001\" InvoiceScenario=\"eInvoice\"/></SendInvoiceResult></SendInvoiceResponse>"),
            "QueryOutboxInvoiceStatus" => Zarf("<QueryOutboxInvoiceStatusResponse xmlns=\"http://tempuri.org/\"><QueryOutboxInvoiceStatusResult IsSucceded=\"true\">" +
                                               "<Value Status=\"Declined\" StatusCode=\"1200\" InvoiceId=\"x\" Message=\"Fiyat hatalı\"/></QueryOutboxInvoiceStatusResult></QueryOutboxInvoiceStatusResponse>"),
            _ => throw new InvalidOperationException(islem)
        });
        var e = new UyumsoftEBelgeEntegratoru("kullanici", "gizli&<şifre>", testOrtami: false, new HttpClient(sunucu));
        Assert.Equal("Uyumsoft", e.Ad);

        var paket = Paket(EBelgeTuru.EFatura, new UblTaraf(EBelgeTests.GecerliVkn(67890), "Alıcı Ltd. Şti.", VergiDairesi: "Çankaya", Il: "Ankara", Ilce: "Çankaya"));
        var sonuc = await e.GonderAsync(paket);
        Assert.True(sonuc.Basarili);
        Assert.Contains("MYS2026000000001", sonuc.Mesaj);

        var gonderim = System.Xml.Linq.XDocument.Parse(sunucu.Istekler.Single(i => i.Islem == "SendInvoice").Govde);
        System.Xml.Linq.XNamespace t = "http://tempuri.org/";
        var bilgi = gonderim.Descendants(t + "InvoiceInfo").Single();
        Assert.Equal(paket.FaturaNo, bilgi.Attribute("LocalDocumentId")!.Value);
        Assert.Equal("eInvoice", bilgi.Element(t + "Scenario")!.Value);
        var hedef = bilgi.Element(t + "TargetCustomer")!;
        Assert.Equal("urn:mail:defaultpk@firma.com", hedef.Attribute("Alias")!.Value);
        Assert.Equal("Alıcı Ltd. Şti.", hedef.Attribute("Title")!.Value);
        Assert.Equal(paket.Ettn.ToString().ToUpperInvariant(), bilgi.Element(t + "Invoice")!.Element(UblTrFaturaUretici.Cbc + "UUID")!.Value);
        // Şifre XML'e kaçışlı yazılır, istek bozulmaz.
        Assert.Contains("gizli&amp;&lt;şifre&gt;", sunucu.Istekler[0].Govde);

        var durum = await e.DurumSorgulaAsync(paket.Ettn, EBelgeTuru.EFatura);
        Assert.Equal(EBelgeDurumu.Reddedildi, durum!.Durum);
        Assert.Contains("Fiyat hatalı", durum.Aciklama);
    }

    [Fact]
    public async Task Hatali_sifre_ve_kayitli_olmayan_alici_anlasilir_hata_verir()
    {
        var sunucu = new SahteSunucu((islem, _) => islem switch
        {
            "WhoAmI" => Zarf("<s:Fault><faultcode>s:Client</faultcode><faultstring>An error occurred when verifying security for the message.</faultstring></s:Fault>"),
            "GetUserAliasses" => Zarf("<GetUserAliassesResponse xmlns=\"http://tempuri.org/\"><GetUserAliassesResult IsSucceded=\"false\" Message=\"Kullanıcı bulunamadı\"/></GetUserAliassesResponse>"),
            _ => throw new InvalidOperationException(islem)
        });
        var e = new UyumsoftEBelgeEntegratoru("kullanici", "yanlis", testOrtami: true, new HttpClient(sunucu));

        var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => e.BaglantiDeneAsync());
        Assert.Contains("şifre hatalı", hata.Message);

        var paket = Paket(EBelgeTuru.EFatura, new UblTaraf(EBelgeTests.GecerliVkn(67890), "Alıcı Ltd. Şti.", VergiDairesi: "Çankaya", Il: "Ankara", Ilce: "Çankaya"));
        var sonuc = await e.GonderAsync(paket);
        Assert.False(sonuc.Basarili);
        Assert.Equal(EBelgeDurumu.Hata, sonuc.Durum);
        Assert.Contains("kayıtlı görünmüyor", sonuc.Mesaj);
        Assert.DoesNotContain(sunucu.Istekler, i => i.Islem == "SendInvoice");

        // Bilgi girilmemişse istek hiç gönderilmez.
        var bos = new UyumsoftEBelgeEntegratoru("", "", testOrtami: true, new HttpClient(sunucu));
        Assert.Contains("girilmemiş", (await Assert.ThrowsAsync<InvalidOperationException>(() => bos.BaglantiDeneAsync())).Message);
    }
}
