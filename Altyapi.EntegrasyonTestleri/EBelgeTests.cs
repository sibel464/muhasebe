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

    [Fact]
    public async Task Servis_belgeyi_hazirlar_dosyaya_yazar_gonderilmis_fatura_portal_iptali_olmadan_iptal_edilemez()
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

        await using var db = new MuhasebeBaglami(fixture.Options) { AktifSirketId = sirket.Id };
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
        var fatura = await faturaServisi.EkleAsync(new Fatura
        {
            CariKartId = cari.Id, Tarih = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc), Tur = FaturaTuru.Satis, FaturaNo = string.Empty, SubeId = sube.Id,
            Kalemler = [new FaturaKalemi { UrunId = urun.Id, Miktar = 2, BirimFiyat = 150, KdvOrani = 20, StokDagilimlari = [new FaturaKalemiStokDagilimi { DepoId = depo.Id, Miktar = 2 }] }]
        });
        await faturaServisi.OnaylaVeOdemeyiKaydetAsync(fatura.Id);

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
}
