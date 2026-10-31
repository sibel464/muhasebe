using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Altyapi.EBelge;
using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Kurumsal;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Yardimcilar;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// e-Fatura / e-Arşiv: UBL-TR XML'in UBL 2.1 şemasına uygunluğu, tutar tutarlılığı ve e-belge durum kuralları.
[Collection("PostgreSQL integration")]
public sealed class EBelgeTests(IsolatedPostgreSqlFixture fixture)
{
    private static readonly XNamespace Cac = UblTrFaturaUretici.Cac;
    private static readonly XNamespace Cbc = UblTrFaturaUretici.Cbc;

    private static readonly Lazy<XmlSchemaSet> Semalar = new(() =>
    {
        var kok = Path.Combine(AppContext.BaseDirectory, "Sema", "UBL-2.1", "xsd");
        var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        // xmldsig şeması DTD içerdiğinden DTD işlemeye izin verilen okuyucuyla önceden eklenir.
        using (var dsig = XmlReader.Create(Path.Combine(kok, "common", "UBL-xmldsig-core-schema-2.1.xsd"), new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse }))
            set.Add(null, dsig);
        set.Add(UblTrFaturaUretici.Inv.NamespaceName, Path.Combine(kok, "maindoc", "UBL-Invoice-2.1.xsd"));
        set.Compile();
        return set;
    });

    // Sağlama hanesi doğru, rastgele vergi/T.C. kimlik numaraları.
    internal static string GecerliVkn(int tohum)
    {
        var ilk = (100000000 + tohum % 800000000).ToString();
        for (var son = 0; son <= 9; son++)
            if (KimlikNoDogrulayici.VergiNoGecerliMi(ilk + son)) return ilk + son;
        throw new InvalidOperationException();
    }

    internal static string GecerliTckn(int tohum)
    {
        var r = (100000000 + tohum % 800000000).ToString().Select(c => c - '0').ToArray();
        var tek = r[0] + r[2] + r[4] + r[6] + r[8];
        var cift = r[1] + r[3] + r[5] + r[7];
        var onuncu = ((tek * 7 - cift) % 10 + 10) % 10;
        return string.Concat(r) + onuncu + (tek + cift + onuncu) % 10;
    }

    private static UblTaraf Satici() => new(GecerliVkn(12345), "Deneme Ticaret A.Ş.", VergiDairesi: "Kadıköy", Adres: "Moda Cad. No:1",
        Ilce: "Kadıköy", Il: "İstanbul", PostaKodu: "34710", Telefon: "02160000000", Eposta: "info@deneme.com.tr", MersisNo: "0123456789000015");

    private static UblTaraf AliciFirma() => new(GecerliVkn(67890), "Alıcı Ltd. Şti.", VergiDairesi: "Çankaya", Adres: "Atatürk Blv. 10",
        Ilce: "Çankaya", Il: "Ankara");

    // Kalem tutarlarını uygulamanın kendi hesaplayıcısıyla doldurur (FaturaServisi ile aynı kurallar).
    private static Fatura Fatura(FaturaTuru tur, decimal genelIskonto, params (string Ad, string Birim, decimal Miktar, decimal Fiyat, decimal Isk, decimal Kdv)[] kalemler)
    {
        var sonuc = FaturaHesaplayici.Hesapla(kalemler.Select(k => new FaturaHesaplayici.Girdi(k.Miktar, k.Fiyat, k.Isk, k.Kdv)).ToList(), genelIskonto);
        var f = new Fatura
        {
            FaturaNo = (tur == FaturaTuru.Satis ? "SAT" : "ALI") + "2026000000123", Tur = tur, Durum = BelgeDurumu.Onaylandi,
            Tarih = DateTime.SpecifyKind(new DateTime(2026, 10, 22, 14, 30, 5), DateTimeKind.Utc),
            GenelIskontoOrani = genelIskonto, GenelIskontoTutari = sonuc.GenelIskontoTutari, KdvToplam = sonuc.KdvToplam, GenelToplam = sonuc.GenelToplam,
            Aciklama = "Deneme faturası"
        };
        for (var i = 0; i < kalemler.Length; i++)
        {
            var k = kalemler[i];
            var s = sonuc.Satirlar[i];
            f.Kalemler.Add(new FaturaKalemi
            {
                Id = i + 1, UrunKodu = "U" + i, UrunAd = k.Ad, Birim = k.Birim, Miktar = k.Miktar, BirimFiyat = k.Fiyat, IskontoOrani = k.Isk,
                IskontoTutari = s.SatirIskontosu, KdvOrani = k.Kdv, KdvTutari = s.Kdv, SatirToplami = s.Toplam
            });
        }
        return f;
    }

    private static void SemayaUygun(XDocument belge)
    {
        var hatalar = new List<string>();
        // Doğrulama metin üzerinden: elementlerin gerçekten yazıldığı biçim kontrol edilir.
        var metin = belge.Declaration + belge.Root!.ToString();
        var ayar = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = Semalar.Value };
        ayar.ValidationEventHandler += (_, e) => hatalar.Add($"{e.Severity}: {e.Message}");
        using (var okuyucu = XmlReader.Create(new StringReader(metin), ayar))
            while (okuyucu.Read()) { }
        Assert.True(hatalar.Count == 0, string.Join(Environment.NewLine, hatalar));
    }

    private static decimal Tutar(XElement e) => decimal.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void Tevkifatli_satis_e_faturasi_semaya_uygun_tevkifat_toplami_ve_odenecek_tutar_dogru()
    {
        var f = Fatura(FaturaTuru.Satis, 0m,
            ("Temizlik hizmeti", "Saat", 10m, 33.33m, 0m, 20m),
            ("Temizlik malzemesi", "Adet", 3m, 17.17m, 0m, 20m));
        f.TevkifatKodu = "612";
        f.TevkifatOrani = 0.9m;
        f.TevkifatTutari = TevkifatKodlari.Tutar(f.KdvToplam, f.TevkifatOrani);
        var girdi = new UblFaturaGirdisi(f, Guid.NewGuid(), EBelgeTuru.EFatura, EFaturaSenaryosu.Ticari, Satici(), AliciFirma());

        Assert.Empty(UblTrFaturaUretici.Dogrula(girdi));
        var xml = UblTrFaturaUretici.Uret(girdi);
        SemayaUygun(xml);

        var kok = xml.Root!;
        Assert.Equal("TEVKIFAT", kok.Element(Cbc + "InvoiceTypeCode")!.Value);
        var tevkifat = kok.Element(Cac + "WithholdingTaxTotal")!;
        Assert.Equal(f.TevkifatTutari, Tutar(tevkifat.Element(Cbc + "TaxAmount")!));
        Assert.Equal("612", tevkifat.Descendants(Cbc + "TaxTypeCode").Single().Value);
        Assert.Equal(90m, Tutar(tevkifat.Descendants(Cbc + "Percent").Single()));
        // Satır payları belge toplamına eşittir (kuruş farkı son satırda).
        Assert.Equal(f.TevkifatTutari, kok.Elements(Cac + "InvoiceLine").Sum(l => Tutar(l.Element(Cac + "WithholdingTaxTotal")!.Element(Cbc + "TaxAmount")!)));
        var toplam = kok.Element(Cac + "LegalMonetaryTotal")!;
        Assert.Equal(f.GenelToplam, Tutar(toplam.Element(Cbc + "TaxInclusiveAmount")!));
        Assert.Equal(f.GenelToplam - f.TevkifatTutari, Tutar(toplam.Element(Cbc + "PayableAmount")!));
    }

    [Fact]
    public void Satis_e_faturasi_semaya_uygun_ve_tutarlar_faturayla_ayni()
    {
        var f = Fatura(FaturaTuru.Satis, 5m,
            ("Kalem A", "Adet", 3m, 199.99m, 10m, 20m),
            ("Kalem B", "Kg", 2.5m, 40m, 0m, 10m),
            ("Hizmet", "Saat", 1m, 500m, 0m, 0m));
        var ettn = Guid.NewGuid();
        var girdi = new UblFaturaGirdisi(f, ettn, EBelgeTuru.EFatura, EFaturaSenaryosu.Ticari, Satici(), AliciFirma());

        Assert.Empty(UblTrFaturaUretici.Dogrula(girdi));
        var xml = UblTrFaturaUretici.Uret(girdi);
        SemayaUygun(xml);

        var kok = xml.Root!;
        Assert.Equal("TICARIFATURA", kok.Element(Cbc + "ProfileID")!.Value);
        Assert.Equal("SATIS", kok.Element(Cbc + "InvoiceTypeCode")!.Value);
        Assert.Equal(ettn.ToString().ToUpperInvariant(), kok.Element(Cbc + "UUID")!.Value);
        Assert.Equal("2026-10-22", kok.Element(Cbc + "IssueDate")!.Value);
        Assert.Equal("14:30:05", kok.Element(Cbc + "IssueTime")!.Value);
        Assert.Equal("3", kok.Element(Cbc + "LineCountNumeric")!.Value);

        var toplam = kok.Element(Cac + "LegalMonetaryTotal")!;
        Assert.Equal(f.GenelToplam, Tutar(toplam.Element(Cbc + "PayableAmount")!));
        Assert.Equal(f.GenelToplam, Tutar(toplam.Element(Cbc + "TaxInclusiveAmount")!));
        var vergi = kok.Element(Cac + "TaxTotal")!;
        Assert.Equal(f.KdvToplam, Tutar(vergi.Element(Cbc + "TaxAmount")!));
        Assert.Equal(3, vergi.Elements(Cac + "TaxSubtotal").Count());
        Assert.Equal(f.KdvToplam, vergi.Elements(Cac + "TaxSubtotal").Sum(s => Tutar(s.Element(Cbc + "TaxAmount")!)));
        // %0 KDV'de muafiyet kodu yazılır.
        Assert.Contains(vergi.Descendants(Cbc + "TaxExemptionReasonCode"), e => e.Value == "351");

        var satirlar = kok.Elements(Cac + "InvoiceLine").ToList();
        Assert.Equal(Tutar(toplam.Element(Cbc + "LineExtensionAmount")!), satirlar.Sum(s => Tutar(s.Element(Cbc + "LineExtensionAmount")!)));
        // Her satırda: brüt − indirim = satır tutarı.
        foreach (var s in satirlar)
        {
            var miktar = decimal.Parse(s.Element(Cbc + "InvoicedQuantity")!.Value, System.Globalization.CultureInfo.InvariantCulture);
            var fiyat = Tutar(s.Element(Cac + "Price")!.Element(Cbc + "PriceAmount")!);
            var indirim = s.Element(Cac + "AllowanceCharge") is { } a ? Tutar(a.Element(Cbc + "Amount")!) : 0m;
            Assert.Equal(decimal.Round(miktar * fiyat, 2, MidpointRounding.AwayFromZero) - indirim, Tutar(s.Element(Cbc + "LineExtensionAmount")!));
        }
        Assert.Equal("KGM", satirlar[1].Element(Cbc + "InvoicedQuantity")!.Attribute("unitCode")!.Value);
        Assert.Equal("HUR", satirlar[2].Element(Cbc + "InvoicedQuantity")!.Attribute("unitCode")!.Value);
    }

    [Fact]
    public void Kimliksiz_nihai_tuketiciye_e_arsiv_genel_tckn_ve_kisi_adiyla_kesilir()
    {
        var f = Fatura(FaturaTuru.Satis, 0m, ("Ürün", "Adet", 1m, 100m, 0m, 20m));
        var alici = new UblTaraf(UblTrFaturaUretici.NihaiTuketiciTckn, "Ayşe Yılmaz");
        var girdi = new UblFaturaGirdisi(f, Guid.NewGuid(), EBelgeTuru.EArsiv, EFaturaSenaryosu.Temel, Satici(), alici);

        Assert.Empty(UblTrFaturaUretici.Dogrula(girdi));
        var xml = UblTrFaturaUretici.Uret(girdi);
        SemayaUygun(xml);
        Assert.Equal("EARSIVFATURA", xml.Root!.Element(Cbc + "ProfileID")!.Value);
        var musteri = xml.Root.Element(Cac + "AccountingCustomerParty")!.Element(Cac + "Party")!;
        var kimlik = musteri.Element(Cac + "PartyIdentification")!.Element(Cbc + "ID")!;
        Assert.Equal("TCKN", kimlik.Attribute("schemeID")!.Value);
        Assert.Equal("11111111111", kimlik.Value);
        Assert.Equal("Ayşe", musteri.Element(Cac + "Person")!.Element(Cbc + "FirstName")!.Value);
        Assert.Equal("Yılmaz", musteri.Element(Cac + "Person")!.Element(Cbc + "FamilyName")!.Value);

        // Aynı alıcıya e-Fatura kesilemez (gerçek kimlik gerekir).
        Assert.Contains(UblTrFaturaUretici.Dogrula(girdi with { Tur = EBelgeTuru.EFatura }), h => h.Contains("e-Fatura alıcısının"));
    }

    [Fact]
    public void Alis_iadesi_iade_fatura_tipinde_ve_iade_edilen_faturaya_atifla_kesilir()
    {
        var f = Fatura(FaturaTuru.AlisIade, 0m, ("İade ürün", "Adet", 2m, 50m, 0m, 20m));
        var girdi = new UblFaturaGirdisi(f, Guid.NewGuid(), EBelgeTuru.EFatura, EFaturaSenaryosu.Temel, Satici(), AliciFirma());
        Assert.Contains(UblTrFaturaUretici.Dogrula(girdi), h => h.Contains("iade edilen faturanın"));

        f.IadeEdilenFaturaNo = "ABC2026000000077";
        f.IadeEdilenFaturaTarihi = new DateTime(2026, 10, 1);
        Assert.Empty(UblTrFaturaUretici.Dogrula(girdi));
        var xml = UblTrFaturaUretici.Uret(girdi);
        SemayaUygun(xml);
        Assert.Equal("IADE", xml.Root!.Element(Cbc + "InvoiceTypeCode")!.Value);
        var referans = xml.Root.Element(Cac + "BillingReference")!.Element(Cac + "InvoiceDocumentReference")!;
        Assert.Equal("ABC2026000000077", referans.Element(Cbc + "ID")!.Value);
        Assert.Equal("2026-10-01", referans.Element(Cbc + "IssueDate")!.Value);
    }

    [Fact]
    public void Eksik_veya_hatali_bilgide_anlasilir_uyarilar_verir()
    {
        var f = Fatura(FaturaTuru.Alis, 0m, ("Ürün", "Adet", 1m, 10m, 0m, 20m));
        f.FaturaNo = "FAT-2026-001";
        f.Durum = BelgeDurumu.Taslak;
        var gecerli = GecerliVkn(555);
        var satici = Satici() with { VknTckn = gecerli[..9] + (char)('0' + (gecerli[9] - '0' + 1) % 10), Il = null };
        var alici = new UblTaraf("", "");
        var hatalar = UblTrFaturaUretici.Dogrula(new UblFaturaGirdisi(f, Guid.NewGuid(), EBelgeTuru.EFatura, EFaturaSenaryosu.Temel, satici, alici));

        Assert.Contains(hatalar, h => h.Contains("Yalnız satış faturası"));
        Assert.Contains(hatalar, h => h.Contains("onaylı"));
        Assert.Contains(hatalar, h => h.Contains("GİB biçiminde"));
        Assert.Contains(hatalar, h => h.StartsWith("Firma (satıcı): vergi numarası geçersiz"));
        Assert.Contains(hatalar, h => h.StartsWith("Firma (satıcı): il ve ilçe"));
        Assert.Contains(hatalar, h => h.StartsWith("Alıcı (cari): vergi numarası (10 hane)"));
    }

    [Fact]
    public void Turkce_birim_adlari_UN_ECE_koduna_cevrilir()
    {
        Assert.Equal("C62", UblTrFaturaUretici.BirimKodu("Adet"));
        Assert.Equal("KGM", UblTrFaturaUretici.BirimKodu("KG"));
        Assert.Equal("LTR", UblTrFaturaUretici.BirimKodu("Lt."));
        Assert.Equal("MTK", UblTrFaturaUretici.BirimKodu("m²"));
        Assert.Equal("PA", UblTrFaturaUretici.BirimKodu("PAKET"));
        Assert.Equal("C62", UblTrFaturaUretici.BirimKodu("bilinmeyen"));
        Assert.Equal("C62", UblTrFaturaUretici.BirimKodu(null));
    }

    private sealed record Ortam(MuhasebeBaglami Db, Sirket Sirket, CariKart Cari, Urun Urun, Sube Sube, Depo Depo, Cari360TestKullanici Oturum, FaturaServisi FaturaServisi);

    private async Task<Ortam> OrtamHazirlaAsync()
    {
        fixture.RequireDatabase();
        var sirket = new Sirket
        {
            Id = Guid.NewGuid(), SirketKodu = $"EB-{Guid.NewGuid():N}"[..10], Unvan = "e-Belge Deneme A.Ş.", VergiNo = GecerliVkn(Random.Shared.Next()),
            VergiDairesi = "Konak", Adres = "Cumhuriyet Blv. 5", Il = "İzmir", Ilce = "Konak"
        };
        await using (var kurulum = new MuhasebeBaglami(fixture.Options))
        {
            kurulum.Sirketler.Add(sirket);
            await kurulum.SaveChangesAsync();
        }

        var db = new MuhasebeBaglami(fixture.Options) { AktifSirketId = sirket.Id };
        var cari = new CariKart
        {
            SirketId = sirket.Id, Kod = $"EB-{Guid.NewGuid():N}"[..12], Unvan = "Mükellef Alıcı A.Ş.", Aktif = true, Tur = CariTuru.Musteri,
            VergiNo = GecerliVkn(Random.Shared.Next()), VergiDairesi = "Bornova", Il = "İzmir", Ilce = "Bornova", EFaturaMukellefi = true,
            EFaturaEtiketi = "urn:mail:defaultpk@alici.com.tr"
        };
        var urun = new Urun { SirketId = sirket.Id, Kod = $"EB-{Guid.NewGuid():N}"[..12], Ad = "e-Belge Ürünü", Birim = "Adet", Aktif = true, KdvOrani = 20m, StokMiktari = 10 };
        db.AddRange(cari, urun);
        await db.SaveChangesAsync();
        var sube = new Sube { SirketId = sirket.Id, SubeKodu = $"S-{Guid.NewGuid():N}"[..8], SubeAdi = "Merkez" };
        db.Subeler.Add(sube);
        await db.SaveChangesAsync();
        var depo = new Depo { SubeId = sube.Id, DepoKodu = $"D-{Guid.NewGuid():N}"[..8], DepoAdi = "Ana Depo", AktifMi = true };
        db.Depolar.Add(depo);
        await db.SaveChangesAsync();
        db.UrunLokasyonStoklari.Add(new UrunLokasyonStogu { UrunId = urun.Id, DepoId = depo.Id, MevcutMiktar = 10 });
        await db.SaveChangesAsync();

        var oturum = (Cari360TestKullanici)StabilizasyonTestOturumu.Olustur();
        oturum.AktifYetkiler!.FaturaOlustur = true;
        oturum.AktifYetkiler.FaturaOnayla = true;
        oturum.AktifYetkiler.FaturaGoruntule = true;
        oturum.AktifYetkiler.FaturaIptal = true;
        var faturaServisi = new FaturaServisi(db, oturum);
        return new Ortam(db, sirket, cari, urun, sube, depo, oturum, faturaServisi);
    }

    private static async Task<Fatura> SatisFaturasiAsync(Ortam o, CariKart cari)
    {
        var fatura = await o.FaturaServisi.EkleAsync(new Fatura
        {
            CariKartId = cari.Id, Tarih = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc), Tur = FaturaTuru.Satis, FaturaNo = string.Empty, SubeId = o.Sube.Id,
            Kalemler = [new FaturaKalemi { UrunId = o.Urun.Id, Miktar = 1, BirimFiyat = 150, KdvOrani = 20, StokDagilimlari = [new FaturaKalemiStokDagilimi { DepoId = o.Depo.Id, Miktar = 1 }] }]
        });
        await o.FaturaServisi.OnaylaVeOdemeyiKaydetAsync(fatura.Id);
        return fatura;
    }


    [Fact]
    public async Task Servis_belgeyi_hazirlar_dosyaya_yazar_gonderilmis_fatura_portal_iptali_olmadan_iptal_edilemez()
    {
        var o = await OrtamHazirlaAsync();
        await using var db = o.Db;
        var (sirket, faturaServisi, oturum) = (o.Sirket, o.FaturaServisi, o.Oturum);
        var fatura = await SatisFaturasiAsync(o, o.Cari);

        var klasor = Path.Combine(Path.GetTempPath(), "ebelge-test-" + Guid.NewGuid().ToString("N"));
        var servis = new EBelgeServisi(db, oturum, new DosyaEBelgeEntegratoru(() => klasor));
        try
        {
            var kontrol = await servis.OnKontrolAsync(new EBelgeHazirlamaIstegi(fatura.Id));
            Assert.True(kontrol.Hazirlanabilir, string.Join("; ", kontrol.Hatalar));
            Assert.Equal(EBelgeTuru.EFatura, kontrol.Tur);

            var sonuc = await servis.HazirlaVeGonderAsync(new EBelgeHazirlamaIstegi(fatura.Id));
            Assert.True(sonuc.Basarili);
            Assert.Equal(EBelgeDurumu.Hazirlandi, sonuc.Durum);
            Assert.True(File.Exists(sonuc.DosyaYolu));
            var dosya = XDocument.Load(sonuc.DosyaYolu!);
            SemayaUygun(dosya);

            await using var oku = new MuhasebeBaglami(fixture.Options) { AktifSirketId = sirket.Id };
            var kayitli = await oku.Faturalar.AsNoTracking().SingleAsync(f => f.Id == fatura.Id);
            Assert.NotNull(kayitli.Ettn);
            Assert.Equal(EBelgeDurumu.Hazirlandi, kayitli.EBelgeDurumu);
            Assert.Equal(kayitli.Ettn!.Value.ToString().ToUpperInvariant(), dosya.Root!.Element(Cbc + "UUID")!.Value);
            Assert.Equal(sirket.VergiNo, dosya.Root.Element(Cac + "AccountingSupplierParty")!.Descendants(Cbc + "ID").First().Value);

            // Yeniden hazırlamada ETTN değişmez.
            await servis.HazirlaVeGonderAsync(new EBelgeHazirlamaIstegi(fatura.Id));
            Assert.Equal(kayitli.Ettn, (await oku.Faturalar.AsNoTracking().SingleAsync(f => f.Id == fatura.Id)).Ettn);
            Assert.Equal(2, await oku.EBelgeKayitlari.CountAsync(k => k.FaturaId == fatura.Id));
            Assert.Contains(kayitli.Ettn.Value.ToString().ToUpperInvariant(), await servis.SonXmlGetirAsync(fatura.Id));

            // Portala yüklendi → gönderildi: artık yeniden hazırlanamaz ve programdan iptal edilemez.
            await servis.DurumIsaretleAsync(fatura.Id, EBelgeDurumu.Gonderildi);
            await Assert.ThrowsAsync<InvalidOperationException>(() => servis.HazirlaVeGonderAsync(new EBelgeHazirlamaIstegi(fatura.Id)));
            var iptalHatasi = await Assert.ThrowsAsync<InvalidOperationException>(() => faturaServisi.IptalEtAsync(fatura.Id));
            Assert.Contains("e-Fatura olarak gönderildi", iptalHatasi.Message);
            // Temel senaryoda alıcı reddedemez.
            await Assert.ThrowsAsync<InvalidOperationException>(() => servis.DurumIsaretleAsync(fatura.Id, EBelgeDurumu.Reddedildi));

            // Portalda iptal edildi olarak işaretlenince fatura programda da iptal edilebilir.
            await servis.DurumIsaretleAsync(fatura.Id, EBelgeDurumu.IptalEdildi, "Portalda iptal edildi");
            var iptal = await faturaServisi.IptalEtAsync(fatura.Id);
            Assert.Equal(BelgeDurumu.Iptal, iptal.Durum);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public async Task Toplu_hazirlama_eksik_bilgili_faturayi_atlar_digerlerini_hazirlar()
    {
        var o = await OrtamHazirlaAsync();
        await using var db = o.Db;
        var eksikCari = new CariKart { SirketId = o.Sirket.Id, Kod = $"EB-{Guid.NewGuid():N}"[..12], Unvan = "Adressiz Ltd.", Aktif = true, Tur = CariTuru.Musteri, VergiNo = GecerliVkn(Random.Shared.Next()), VergiDairesi = "Konak" };
        db.CariKartlar.Add(eksikCari);
        await db.SaveChangesAsync();
        var iyi = await SatisFaturasiAsync(o, o.Cari);
        var eksik = await SatisFaturasiAsync(o, eksikCari);

        var klasor = Path.Combine(Path.GetTempPath(), "ebelge-toplu-" + Guid.NewGuid().ToString("N"));
        try
        {
            var servis = new EBelgeServisi(db, o.Oturum, new DosyaEBelgeEntegratoru(() => klasor));
            var sonuclar = await servis.TopluHazirlaAsync(DateTime.Today.AddDays(-1), DateTime.Today);

            Assert.True(sonuclar.Single(x => x.FaturaId == iyi.Id).Basarili);
            var atlanan = sonuclar.Single(x => x.FaturaId == eksik.Id);
            Assert.False(atlanan.Basarili);
            Assert.Contains("il ve ilçe", atlanan.Mesaj);

            await using var oku = new MuhasebeBaglami(fixture.Options) { AktifSirketId = o.Sirket.Id };
            Assert.Equal(EBelgeDurumu.Hazirlandi, (await oku.Faturalar.SingleAsync(f => f.Id == iyi.Id)).EBelgeDurumu);
            Assert.Equal(EBelgeDurumu.Yok, (await oku.Faturalar.SingleAsync(f => f.Id == eksik.Id)).EBelgeDurumu);
            Assert.Null((await oku.Faturalar.SingleAsync(f => f.Id == eksik.Id)).Ettn);

            // İkinci çalıştırmada hazırlanmış olan tekrar işlenmez.
            var ikinci = await servis.TopluHazirlaAsync(DateTime.Today.AddDays(-1), DateTime.Today);
            Assert.DoesNotContain(ikinci, x => x.FaturaId == iyi.Id);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public async Task Gelen_e_fatura_taslak_alis_faturasi_olarak_aktarilir_ikinci_kez_aktarilamaz()
    {
        var o = await OrtamHazirlaAsync();
        await using var db = o.Db;
        var servis = new EBelgeServisi(db, o.Oturum);

        // Tedarikçinin kestiği fatura: satıcı tedarikçi, alıcı bizim firmamız.
        var tedarikci = new UblTaraf(GecerliVkn(Random.Shared.Next()), "Toptancı Gıda A.Ş.", VergiDairesi: "Bayrampaşa", Adres: "Hal Yolu 3",
            Ilce: "Bayrampaşa", Il: "İstanbul", Eposta: "satis@toptanci.com.tr");
        var biz = new UblTaraf(o.Sirket.VergiNo!, o.Sirket.Unvan, VergiDairesi: o.Sirket.VergiDairesi, Il: o.Sirket.Il, Ilce: o.Sirket.Ilce);
        var gelen = Fatura(FaturaTuru.Satis, 0m, ("Zeytinyağı 5 Lt", "Adet", 4m, 750m, 10m, 10m), (o.Urun.Ad, "Adet", 3m, 100m, 0m, 20m));
        gelen.FaturaNo = "TPT2026" + Random.Shared.Next(100000000, 999999999);
        gelen.Kalemler[1].UrunKodu = o.Urun.Kod; // mevcut ürünle kodundan eşleşir
        var ettn = Guid.NewGuid();
        var xml = UblTrFaturaUretici.UretMetin(new UblFaturaGirdisi(gelen, ettn, EBelgeTuru.EFatura, EFaturaSenaryosu.Ticari, tedarikci, biz));

        var onizleme = await servis.GelenFaturaOnizleAsync(xml);
        Assert.True(onizleme.Aktarilabilir, string.Join("; ", onizleme.Hatalar));
        Assert.Null(onizleme.EslesenCari);
        Assert.Equal("Yeni ürün açılacak", onizleme.Satirlar[0].EslesenUrun);
        Assert.StartsWith(o.Urun.Kod, onizleme.Satirlar[1].EslesenUrun);
        Assert.Equal(10m, onizleme.Satirlar[0].IskontoOrani);

        var fatura = await servis.GelenFaturaAktarAsync(xml);
        Assert.Equal(FaturaTuru.Alis, fatura.Tur);
        Assert.Equal(BelgeDurumu.Taslak, fatura.Durum);
        Assert.Equal(gelen.FaturaNo, fatura.FaturaNo);
        Assert.Equal(ettn, fatura.Ettn);
        Assert.Equal(gelen.GenelToplam, fatura.GenelToplam);
        Assert.Equal(gelen.KdvToplam, fatura.KdvToplam);

        await using var oku = new MuhasebeBaglami(fixture.Options) { AktifSirketId = o.Sirket.Id };
        var cari = await oku.CariKartlar.SingleAsync(c => c.Id == fatura.CariKartId);
        Assert.Equal(tedarikci.VknTckn, cari.VergiNo);
        Assert.Equal(CariTuru.Tedarikci, cari.Tur);
        Assert.True(cari.EFaturaMukellefi);
        var kalemler = await oku.FaturaKalemleri.Where(k => k.FaturaId == fatura.Id).OrderBy(k => k.Id).ToListAsync();
        Assert.Equal(o.Urun.Id, kalemler[1].UrunId);
        var yeniUrun = await oku.Urunler.SingleAsync(u => u.Id == kalemler[0].UrunId);
        Assert.Equal("Zeytinyağı 5 Lt", yeniUrun.Ad);
        Assert.Equal(750m, yeniUrun.AlisFiyati);

        // Aynı ETTN ikinci kez aktarılamaz.
        var tekrar = await servis.GelenFaturaOnizleAsync(xml);
        Assert.Contains(tekrar.Hatalar, h => h.Contains("daha önce aktarılmış"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.GelenFaturaAktarAsync(xml));

        // Başka firmaya kesilmiş fatura reddedilir.
        var baskasi = biz with { VknTckn = GecerliVkn(Random.Shared.Next()) };
        gelen.FaturaNo = "TPT2026" + Random.Shared.Next(100000000, 999999999);
        var yanlis = UblTrFaturaUretici.UretMetin(new UblFaturaGirdisi(gelen, Guid.NewGuid(), EBelgeTuru.EFatura, EFaturaSenaryosu.Temel, tedarikci, baskasi));
        Assert.Contains((await servis.GelenFaturaOnizleAsync(yanlis)).Hatalar, h => h.Contains("firmanıza kesilmemiş"));

        // XML olmayan dosya anlaşılır hatayla reddedilir.
        await Assert.ThrowsAsync<InvalidOperationException>(() => servis.GelenFaturaOnizleAsync("bu bir xml değil"));
    }

    private static readonly Lazy<XmlSchemaSet> IrsaliyeSemasi = new(() =>
    {
        var kok = Path.Combine(AppContext.BaseDirectory, "Sema", "UBL-2.1", "xsd");
        var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        using (var dsig = XmlReader.Create(Path.Combine(kok, "common", "UBL-xmldsig-core-schema-2.1.xsd"), new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse }))
            set.Add(null, dsig);
        set.Add(UblTrIrsaliyeUretici.Da.NamespaceName, Path.Combine(kok, "maindoc", "UBL-DespatchAdvice-2.1.xsd"));
        set.Compile();
        return set;
    });

    [Fact]
    public async Task E_irsaliye_semaya_uygun_hazirlanir_gib_numarasi_bir_kez_verilir()
    {
        var o = await OrtamHazirlaAsync();
        await using var db = o.Db;
        o.Oturum.AktifYetkiler!.IrsaliyeGoruntule = true;
        o.Oturum.AktifYetkiler.IrsaliyeOnayla = true;
        var irsaliye = new EtkiAlani.Modeller.Irsaliye.Irsaliye
        {
            SirketId = o.Sirket.Id, CariKartId = o.Cari.Id, IrsaliyeNo = $"IRS-SAT-{Guid.NewGuid():N}"[..20], Tur = EtkiAlani.Modeller.Irsaliye.IrsaliyeTuru.Satis,
            Durum = EtkiAlani.Modeller.Irsaliye.IrsaliyeDurumu.Onaylandi, Tarih = DateTime.SpecifyKind(new DateTime(2026, 10, 26, 9, 15, 0), DateTimeKind.Utc),
            SevkTarihi = DateTime.SpecifyKind(new DateTime(2026, 10, 26, 10, 0, 0), DateTimeKind.Utc),
            TeslimatAdres = "Şantiye, Ankara Cad. 12", TeslimatIl = "İzmir", TeslimatIlce = "Bayraklı",
            Kalemler = [new EtkiAlani.Modeller.Irsaliye.IrsaliyeKalemi { UrunId = o.Urun.Id, UrunKodu = o.Urun.Kod, UrunAd = o.Urun.Ad, Birim = "Kg", Miktar = 12.5m }]
        };
        db.Irsaliyeler.Add(irsaliye);
        await db.SaveChangesAsync();

        var klasor = Path.Combine(Path.GetTempPath(), "eirsaliye-test-" + Guid.NewGuid().ToString("N"));
        var servis = new EBelgeServisi(db, o.Oturum, new DosyaEBelgeEntegratoru(() => klasor));
        try
        {
            // Plaka ve şoför bilgisi olmadan hazırlanmaz.
            var eksik = await servis.IrsaliyeOnKontrolAsync(irsaliye.Id);
            Assert.Contains(eksik, h => h.Contains("plaka"));
            Assert.Contains(eksik, h => h.Contains("T.C. kimlik"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => servis.IrsaliyeHazirlaAsync(irsaliye.Id));

            irsaliye.AracPlaka = "35 abc 123";
            irsaliye.SoforAdi = "Mehmet Ali Kaya";
            irsaliye.SoforTckn = GecerliTckn(Random.Shared.Next());
            await db.SaveChangesAsync();
            Assert.Empty(await servis.IrsaliyeOnKontrolAsync(irsaliye.Id));

            var sonuc = await servis.IrsaliyeHazirlaAsync(irsaliye.Id);
            Assert.True(sonuc.Basarili, sonuc.Mesaj);
            await using var oku = new MuhasebeBaglami(fixture.Options) { AktifSirketId = o.Sirket.Id };
            var kayit = await oku.Irsaliyeler.SingleAsync(i => i.Id == irsaliye.Id);
            Assert.Matches("^IRS2026[0-9]{9}$", kayit.EBelgeNo!);
            Assert.Equal(EBelgeDurumu.Hazirlandi, kayit.EBelgeDurumu);

            var xml = XDocument.Load(sonuc.DosyaYolu!);
            var hatalar = new List<string>();
            var ayar = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = IrsaliyeSemasi.Value };
            ayar.ValidationEventHandler += (_, e) => hatalar.Add($"{e.Severity}: {e.Message}");
            using (var okuyucu = XmlReader.Create(new StringReader(xml.Declaration + xml.Root!.ToString()), ayar))
                while (okuyucu.Read()) { }
            Assert.True(hatalar.Count == 0, string.Join(Environment.NewLine, hatalar));

            var kok = xml.Root!;
            Assert.Equal("TEMELIRSALIYE", kok.Element(Cbc + "ProfileID")!.Value);
            Assert.Equal("SEVK", kok.Element(Cbc + "DespatchAdviceTypeCode")!.Value);
            Assert.Equal(kayit.EBelgeNo, kok.Element(Cbc + "ID")!.Value);
            Assert.Equal("35ABC123", kok.Descendants(Cbc + "LicensePlateID").Single().Value);
            Assert.Equal("Mehmet Ali", kok.Descendants(Cac + "DriverPerson").Single().Element(Cbc + "FirstName")!.Value);
            Assert.Equal("Bayraklı", kok.Descendants(Cac + "DeliveryAddress").Single().Element(Cbc + "CitySubdivisionName")!.Value);
            Assert.Equal("KGM", kok.Descendants(Cbc + "DeliveredQuantity").Single().Attribute("unitCode")!.Value);
            Assert.Equal("10:00:00", kok.Descendants(Cbc + "ActualDespatchTime").Single().Value);

            // Yeniden hazırlama aynı numara ve ETTN'yi kullanır; gönderildi işaretlenince bir daha hazırlanmaz.
            var ilkNo = kayit.EBelgeNo;
            await servis.IrsaliyeHazirlaAsync(irsaliye.Id);
            await using (var oku2 = new MuhasebeBaglami(fixture.Options) { AktifSirketId = o.Sirket.Id })
                Assert.Equal(ilkNo, (await oku2.Irsaliyeler.SingleAsync(i => i.Id == irsaliye.Id)).EBelgeNo);
            await servis.IrsaliyeDurumIsaretleAsync(irsaliye.Id, EBelgeDurumu.Gonderildi);
            await Assert.ThrowsAsync<InvalidOperationException>(() => servis.IrsaliyeHazirlaAsync(irsaliye.Id));
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public async Task Gelen_tevkifatli_e_fatura_tevkifatli_alis_olarak_aktarilir()
    {
        var o = await OrtamHazirlaAsync();
        await using var db = o.Db;
        var servis = new EBelgeServisi(db, o.Oturum);
        var tedarikci = new UblTaraf(GecerliVkn(Random.Shared.Next()), "Parlak Temizlik Ltd.", VergiDairesi: "Şişli", Adres: "Büyükdere Cad. 5",
            Ilce: "Şişli", Il: "İstanbul");
        var biz = new UblTaraf(o.Sirket.VergiNo!, o.Sirket.Unvan, VergiDairesi: o.Sirket.VergiDairesi, Il: o.Sirket.Il, Ilce: o.Sirket.Ilce);
        var gelen = Fatura(FaturaTuru.Satis, 0m, ("Ofis temizliği (Ekim)", "Adet", 1m, 5000m, 0m, 20m));
        gelen.FaturaNo = "PRL2026" + Random.Shared.Next(100000000, 999999999);
        gelen.TevkifatKodu = "612";
        gelen.TevkifatOrani = 0.9m;
        gelen.TevkifatTutari = 900m;
        var xml = UblTrFaturaUretici.UretMetin(new UblFaturaGirdisi(gelen, Guid.NewGuid(), EBelgeTuru.EFatura, EFaturaSenaryosu.Ticari, tedarikci, biz));

        var onizleme = await servis.GelenFaturaOnizleAsync(xml);
        Assert.True(onizleme.Aktarilabilir, string.Join("; ", onizleme.Hatalar));
        Assert.Equal(5100m, onizleme.OdenecekTutar);
        Assert.DoesNotContain(onizleme.Uyarilar, u => u.Contains("farklı"));

        var fatura = await servis.GelenFaturaAktarAsync(xml);
        Assert.Equal(FaturaTuru.Alis, fatura.Tur);
        Assert.Equal("612", fatura.TevkifatKodu);
        Assert.Equal(900m, fatura.TevkifatTutari);
        Assert.Equal(5100m, fatura.OdenecekTutar);
    }

    // Uyumsoft TEST ortamıyla uçtan uca: alıcının mükellefiyeti GİB listesinden düzeltilir, belge doğrudan gönderilir,
    // durum sorgulanır; yanıtı kaybolmuş gönderim yeniden denendiğinde belge ikinci kez gönderilmez. Gelen kutusu listelenir.
    [Fact]
    public async Task Uyumsoft_test_ortaminda_servis_uzerinden_gonderim_durum_ve_gelen_kutusu()
    {
        await UyumsoftEntegratorTests.CevrimiciGerekirAsync();
        var o = await OrtamHazirlaAsync();
        await using var db = o.Db;
        // Test hesabı Uyumsoft'un kendi firmasıdır; gönderen ve (kendi posta kutusuna) alıcı bu VKN olur.
        var sirket = await db.Sirketler.SingleAsync(s => s.Id == o.Sirket.Id);
        sirket.VergiNo = "9000068418";
        var cari = await db.CariKartlar.SingleAsync(c => c.Id == o.Cari.Id);
        cari.VergiNo = "9000068418";
        cari.EFaturaMukellefi = false;   // yanlış işaretlenmiş: servis GİB listesinden düzeltmeli
        cari.EFaturaEtiketi = null;
        await db.SaveChangesAsync();

        var fatura = await SatisFaturasiAsync(o, cari);
        // Ortak test hesabında numara çakışmasın.
        var kayit = await db.Faturalar.SingleAsync(f => f.Id == fatura.Id);
        kayit.FaturaNo = "MYT" + DateTime.Now.Year + Random.Shared.NextInt64(100000000, 999999999);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var servis = new EBelgeServisi(db, o.Oturum, new UyumsoftEBelgeEntegratoru("Uyumsoft", "Uyumsoft", testOrtami: true));
        Assert.True(servis.CevrimiciMi);
        Assert.True(servis.DogrudanGonderir);

        var sonuc = await servis.HazirlaVeGonderAsync(new EBelgeHazirlamaIstegi(fatura.Id));
        Assert.True(sonuc.Basarili, sonuc.Mesaj);
        Assert.Equal(EBelgeDurumu.Gonderildi, sonuc.Durum);
        Assert.Contains("e-Fatura mükellefi", sonuc.Mesaj);

        await using (var oku = new MuhasebeBaglami(fixture.Options) { AktifSirketId = o.Sirket.Id })
        {
            var c = await oku.CariKartlar.AsNoTracking().SingleAsync(x => x.Id == cari.Id);
            Assert.True(c.EFaturaMukellefi);
            Assert.False(string.IsNullOrWhiteSpace(c.EFaturaEtiketi));
            var f = await oku.Faturalar.AsNoTracking().SingleAsync(x => x.Id == fatura.Id);
            Assert.Equal(EBelgeTuru.EFatura, f.EBelgeTuru);
            Assert.Equal(EBelgeDurumu.Gonderildi, f.EBelgeDurumu);
        }

        var durum = await servis.DurumSorgulaAsync(fatura.Id);
        Assert.NotEqual(EBelgeDurumu.Hata, durum.Durum);

        // Yanıtı kaybolmuş gönderim: program "Hata" sanıyor; yeniden denemede entegratör belgeyi tanıdığı için tekrar gönderilmez.
        db.ChangeTracker.Clear();
        var f2 = await db.Faturalar.SingleAsync(x => x.Id == fatura.Id);
        f2.EBelgeDurumu = EBelgeDurumu.Hata;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var tekrar = await servis.HazirlaVeGonderAsync(new EBelgeHazirlamaIstegi(fatura.Id));
        Assert.True(tekrar.Basarili, tekrar.Mesaj);
        Assert.Contains("yeniden gönderilmedi", tekrar.Mesaj);

        var gelen = await servis.GelenListeAsync(DateTime.Today.AddDays(-7), DateTime.Today);
        Assert.NotEmpty(gelen);
    }

    private sealed class SahteCevrimiciEntegrator : IEBelgeCevrimiciEntegrator
    {
        public List<EBelgePaketi> Gonderilen { get; } = [];
        public string Ad => "Sahte entegratör";
        public bool DogrudanGonderir => true;
        public Task<EBelgeGonderimSonucu> GonderAsync(EBelgePaketi paket, CancellationToken c = default)
        {
            Gonderilen.Add(paket);
            return Task.FromResult(new EBelgeGonderimSonucu(true, EBelgeDurumu.Gonderildi, "gönderildi"));
        }
        public Task<EntegratorHesapBilgisi> BaglantiDeneAsync(CancellationToken c = default) => Task.FromResult(new EntegratorHesapBilgisi("", "", true, true));
        public Task<string?> AliciEtiketiAsync(string vknTckn, CancellationToken c = default) => Task.FromResult<string?>("urn:mail:defaultpk@alici.com.tr");
        public Task<EBelgeDurumBilgisi?> DurumSorgulaAsync(Guid ettn, EBelgeTuru tur, CancellationToken c = default) => Task.FromResult<EBelgeDurumBilgisi?>(null);
        public Task<IReadOnlyList<GelenEBelgeOzeti>> GelenListeAsync(DateTime b, DateTime s, CancellationToken c = default) => Task.FromResult<IReadOnlyList<GelenEBelgeOzeti>>([]);
        public Task<string> GelenXmlAsync(string ettn, CancellationToken c = default) => throw new NotSupportedException();
        public Task<byte[]> PdfAsync(Guid ettn, CancellationToken c = default) => throw new NotSupportedException();
        public Task EArsivIptalAsync(Guid ettn, DateTime t, CancellationToken c = default) => Task.CompletedTask;
        public Task<FirmaSorguSonucu?> FirmaSorgulaAsync(string v, CancellationToken c = default) => Task.FromResult<FirmaSorguSonucu?>(null);
        public Task<EBelgeDurumBilgisi?> IrsaliyeDurumSorgulaAsync(Guid ettn, CancellationToken c = default) => Task.FromResult<EBelgeDurumBilgisi?>(null);
    }

    [Fact]
    public async Task Otomatik_gonderim_yalniz_ayar_acikken_ve_bir_kez_yapilir()
    {
        var o = await OrtamHazirlaAsync();
        await using var db = o.Db;
        var entegrator = new SahteCevrimiciEntegrator();
        var servis = new EBelgeServisi(db, o.Oturum, entegrator);
        var fatura = await SatisFaturasiAsync(o, o.Cari);

        var ayar = await db.SistemAyarlari.SingleAsync(a => a.Id == 1);
        var eski = ayar.EBelgeOtomatikGonder;
        try
        {
            ayar.EBelgeOtomatikGonder = false;
            await db.SaveChangesAsync();
            Assert.Null(await servis.OtomatikGonderAsync(fatura.Id));
            Assert.Empty(entegrator.Gonderilen);

            ayar.EBelgeOtomatikGonder = true;
            await db.SaveChangesAsync();
            var sonuc = await servis.OtomatikGonderAsync(fatura.Id);
            Assert.True(sonuc!.Basarili, sonuc.Mesaj);
            Assert.Single(entegrator.Gonderilen);

            // Gönderilmiş belge ikinci kez gönderilmez.
            Assert.Null(await servis.OtomatikGonderAsync(fatura.Id));
            Assert.Single(entegrator.Gonderilen);
        }
        finally
        {
            ayar.EBelgeOtomatikGonder = eski;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Ana_sayfa_uyarisi_hatali_ve_gonderilmemis_belgeleri_sayar()
    {
        var o = await OrtamHazirlaAsync();
        await using var db = o.Db;
        var klasor = Path.Combine(Path.GetTempPath(), "ebelge-uyari-" + Guid.NewGuid().ToString("N"));
        var servis = new EBelgeServisi(db, o.Oturum, new DosyaEBelgeEntegratoru(() => klasor));
        try
        {
            var f1 = await SatisFaturasiAsync(o, o.Cari);
            var f2 = await SatisFaturasiAsync(o, o.Cari);
            await SatisFaturasiAsync(o, o.Cari);

            // Hiç e-belge hazırlanmamış firmada "gönderilmedi" uyarısı çıkmaz (e-belge kullanmıyor olabilir).
            Assert.Equal(string.Empty, (await servis.UyariAsync()).Metin());

            await servis.HazirlaVeGonderAsync(new EBelgeHazirlamaIstegi(f1.Id));
            await servis.HazirlaVeGonderAsync(new EBelgeHazirlamaIstegi(f2.Id, EFaturaSenaryosu.Ticari));
            await servis.DurumIsaretleAsync(f2.Id, EBelgeDurumu.Gonderildi);
            await servis.DurumIsaretleAsync(f2.Id, EBelgeDurumu.Reddedildi);

            var u = await servis.UyariAsync();
            Assert.Equal(1, u.SorunluBelge);
            Assert.Equal(1, u.GonderilmemisSatis);
            Assert.Equal(0, u.YeniGelen);
            Assert.Contains("reddedildi", u.Metin());
            Assert.Contains("7 gün", u.Metin());
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public async Task Uyumsoft_test_ortaminda_e_irsaliye_gonderilir_ve_durumu_sorgulanir()
    {
        await UyumsoftEntegratorTests.CevrimiciGerekirAsync();
        var o = await OrtamHazirlaAsync();
        await using var db = o.Db;
        o.Oturum.AktifYetkiler!.IrsaliyeGoruntule = true;
        o.Oturum.AktifYetkiler.IrsaliyeOnayla = true;
        var sirket = await db.Sirketler.SingleAsync(s => s.Id == o.Sirket.Id);
        sirket.VergiNo = "9000068418";
        var cari = await db.CariKartlar.SingleAsync(c => c.Id == o.Cari.Id);
        cari.VergiNo = "9000068418";
        var simdi = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
        var irsaliye = new EtkiAlani.Modeller.Irsaliye.Irsaliye
        {
            SirketId = o.Sirket.Id, CariKartId = o.Cari.Id, IrsaliyeNo = $"IRS-SAT-{Guid.NewGuid():N}"[..20], Tur = EtkiAlani.Modeller.Irsaliye.IrsaliyeTuru.Satis,
            Durum = EtkiAlani.Modeller.Irsaliye.IrsaliyeDurumu.Onaylandi, Tarih = simdi, SevkTarihi = simdi,
            TeslimatAdres = "Şantiye, Ankara Cad. 12", TeslimatIl = "İstanbul", TeslimatIlce = "Esenler",
            AracPlaka = "34 ABC 123", SoforAdi = "Mehmet Ali Kaya", SoforTckn = GecerliTckn(Random.Shared.Next()),
            // Ortak test hesabında numara çakışmasın.
            EBelgeNo = "MYI" + DateTime.Now.Year + Random.Shared.NextInt64(100000000, 999999999),
            Kalemler = [new EtkiAlani.Modeller.Irsaliye.IrsaliyeKalemi { UrunId = o.Urun.Id, UrunKodu = o.Urun.Kod, UrunAd = o.Urun.Ad, Birim = "Adet", Miktar = 3m }]
        };
        db.Irsaliyeler.Add(irsaliye);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var servis = new EBelgeServisi(db, o.Oturum, new UyumsoftEBelgeEntegratoru("Uyumsoft", "Uyumsoft", testOrtami: true));
        Assert.Empty(await servis.IrsaliyeOnKontrolAsync(irsaliye.Id));
        var sonuc = await servis.IrsaliyeHazirlaAsync(irsaliye.Id);
        Assert.True(sonuc.Basarili, sonuc.Mesaj);
        Assert.Equal(EBelgeDurumu.Gonderildi, sonuc.Durum);

        var durum = await servis.IrsaliyeDurumSorgulaAsync(irsaliye.Id);
        Assert.NotEqual(EBelgeDurumu.Hata, durum.Durum);
    }
}
