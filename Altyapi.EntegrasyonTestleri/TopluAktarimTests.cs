using System.IO.Compression;
using System.Text;
using Altyapi.Aktarim;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Kurumsal;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;
using Xunit;

namespace Altyapi.EntegrasyonTestleri;

// Başka programdan geçiş: Excel/CSV'den cari ve ürün aktarımı.
[Collection("PostgreSQL integration")]
public sealed class TopluAktarimTests(IsolatedPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData("1.250,50", 1250.50)]
    [InlineData("1250,5", 1250.5)]
    [InlineData("1.250", 1250)]
    [InlineData("1.250.000", 1250000)]
    [InlineData("12.5", 12.5)]
    [InlineData("1234.567", 1234.567)]
    [InlineData("-300", -300)]
    [InlineData("(300,25)", -300.25)]
    [InlineData("0,125", 0.125)]
    [InlineData("1,250.75", 1250.75)]
    public void Sayilar_Turkce_ve_Excel_bicimlerinde_okunur(string metin, double beklenen)
    {
        Assert.True(TopluAktarimServisi.Sayi(metin, out var d));
        Assert.Equal((decimal)beklenen, d);
    }

    [Fact]
    public void Csv_noktali_virgul_tirnak_ve_Turkce_kodlama_okunur()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var metin = "Ünvan;Açıklama;Tutar\r\n\"Şeker; Çay A.Ş.\";\"Satır \"\"tırnaklı\"\"\";1.250,50\r\nİğne Ltd;;5\r\n";
        var tablo = TabloOkuyucu.CsvOku(Encoding.GetEncoding(1254).GetBytes(metin));
        Assert.Equal(3, tablo.Count);
        Assert.Equal(["Ünvan", "Açıklama", "Tutar"], tablo[0].Hucreler);
        Assert.Equal("Şeker; Çay A.Ş.", tablo[1].Hucreler[0]);
        Assert.Equal("Satır \"tırnaklı\"", tablo[1].Hucreler[1]);
        Assert.Equal("İğne Ltd", tablo[2].Hucreler[0]);

        // UTF-8 (BOM'lu) ve virgül ayırıcı
        var utf8 = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Kod,Ad\nA1,Öğrenci Defteri\n")).ToArray();
        var t2 = TabloOkuyucu.CsvOku(utf8);
        Assert.Equal("Öğrenci Defteri", t2[1].Hucreler[1]);
    }

    // Excel'in yazdığı biçimde küçük bir .xlsx: paylaşılan metin, satır içi metin, sayı, tarih ve atlanan sütun.
    internal static byte[] OrnekXlsx(params string[][] satirlar)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Yaz(string ad, string icerik)
            {
                using var w = new StreamWriter(zip.CreateEntry(ad).Open(), new UTF8Encoding(false));
                w.Write(icerik);
            }
            var paylasilan = new List<string>();
            var sb = new StringBuilder();
            for (var r = 0; r < satirlar.Length; r++)
            {
                sb.Append($"<row r=\"{r + 1}\">");
                for (var c = 0; c < satirlar[r].Length; c++)
                {
                    var deger = satirlar[r][c];
                    if (deger.Length == 0) continue; // boş hücre yazılmaz (Excel gibi)
                    var hucre = $"{(char)('A' + c)}{r + 1}";
                    if (deger.StartsWith("#"))       // sayı
                        sb.Append($"<c r=\"{hucre}\"><v>{deger[1..]}</v></c>");
                    else if (deger.StartsWith("@"))  // tarih (seri no, stil 1 = tarih)
                        sb.Append($"<c r=\"{hucre}\" s=\"1\"><v>{deger[1..]}</v></c>");
                    else
                    {
                        paylasilan.Add(deger);
                        sb.Append($"<c r=\"{hucre}\" t=\"s\"><v>{paylasilan.Count - 1}</v></c>");
                    }
                }
                sb.Append("</row>");
            }
            const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            Yaz("[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>");
            Yaz("xl/workbook.xml", $"<workbook xmlns=\"{ns}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Liste\" sheetId=\"1\" r:id=\"rId7\"/></sheets></workbook>");
            Yaz("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId7\" Type=\"worksheet\" Target=\"worksheets/liste.xml\"/></Relationships>");
            Yaz("xl/styles.xml", $"<styleSheet xmlns=\"{ns}\"><cellXfs count=\"2\"><xf numFmtId=\"0\"/><xf numFmtId=\"14\"/></cellXfs></styleSheet>");
            Yaz("xl/sharedStrings.xml", $"<sst xmlns=\"{ns}\">" + string.Concat(paylasilan.Select(p => $"<si><t>{System.Security.SecurityElement.Escape(p)}</t></si>")) + "</sst>");
            Yaz("xl/worksheets/liste.xml", $"<worksheet xmlns=\"{ns}\"><sheetData>{sb}</sheetData></worksheet>");
        }
        return ms.ToArray();
    }

    [Fact]
    public void Xlsx_ilk_sayfa_metin_sayi_tarih_ve_bos_hucreleri_okunur()
    {
        var veri = OrnekXlsx(
            ["Ürün Adı", "Barkod", "Fiyat", "Tarih", "Not"],
            ["Çay 1 Kg", "#8690000000000", "#0.125", "@46317", ""],
            ["Şeker", "", "#1250.5", "", "x"]);
        var tablo = TabloOkuyucu.XlsxOku(veri);
        Assert.Equal("Çay 1 Kg", tablo[1].Hucreler[0]);
        Assert.Equal("8690000000000", tablo[1].Hucreler[1]);
        Assert.Equal("0,125", tablo[1].Hucreler[2]);
        Assert.Equal("22.10.2026", tablo[1].Hucreler[3]);
        Assert.Equal(string.Empty, tablo[2].Hucreler[1]);
        Assert.Equal("1250,5", tablo[2].Hucreler[2]);
        Assert.Equal("x", tablo[2].Hucreler[4]);
        Assert.True(TopluAktarimServisi.Sayi(tablo[1].Hucreler[2], out var fiyat));
        Assert.Equal(0.125m, fiyat);
    }

    private async Task<(MuhasebeBaglami Db, Sirket Sirket, Cari360TestKullanici Oturum, Depo Depo)> OrtamAsync()
    {
        fixture.RequireDatabase();
        var sirket = new Sirket { Id = Guid.NewGuid(), SirketKodu = $"TA-{Guid.NewGuid():N}"[..10], Unvan = "Aktarım Deneme" };
        await using (var k = new MuhasebeBaglami(fixture.Options)) { k.Sirketler.Add(sirket); await k.SaveChangesAsync(); }
        var db = new MuhasebeBaglami(fixture.Options) { AktifSirketId = sirket.Id };
        var sube = new Sube { SirketId = sirket.Id, SubeKodu = $"S-{Guid.NewGuid():N}"[..8], SubeAdi = "Merkez" };
        db.Subeler.Add(sube);
        await db.SaveChangesAsync();
        var depo = new Depo { SubeId = sube.Id, DepoKodu = $"D-{Guid.NewGuid():N}"[..8], DepoAdi = "Ana Mağaza", DepoTuru = DepoTuru.Magaza, AktifMi = true };
        db.Depolar.Add(depo);
        await db.SaveChangesAsync();
        return (db, sirket, (Cari360TestKullanici)StabilizasyonTestOturumu.Olustur(), depo);
    }

    [Fact]
    public async Task Urun_sablonu_hizmet_ve_masraf_kartlariyla_aktarilir()
    {
        var (db, sirket, oturum, _) = await OrtamAsync();
        await using var _ = db;
        var servis = new TopluAktarimServisi(db, oturum);
        var ek = Random.Shared.Next(100000, 999999);
        var csv = servis.SablonCsv(AktarimTuru.Urun)
            .Replace("Örnek Ürün 1 Kg", $"Şablon Ürün {ek}").Replace("Montaj İşçiliği", $"Montaj {ek}").Replace("Elektrik Faturası", $"Elektrik {ek}")
            .Replace("8690000000000", $"869{Random.Shared.NextInt64(1000000000, 9999999999)}")
            + $";;Hatalı Hizmet {ek};Adet;;20;0;0;;;;5;Hizmet;\r\n";
        var dosya = Path.Combine(Path.GetTempPath(), $"urun-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(dosya, csv, new UTF8Encoding(false));
        try
        {
            var onizleme = servis.Onizle(AktarimTuru.Urun, dosya);
            Assert.Equal(4, onizleme.Count);
            Assert.All(onizleme.Take(3), x => Assert.True(x.Gecerli, x.Hata));
            Assert.Contains("açılış stoğu girilmez", onizleme[3].Hata);

            await servis.AktarAsync(AktarimTuru.Urun, dosya, DateTime.Today);
            await using var oku = new MuhasebeBaglami(fixture.Options) { AktifSirketId = sirket.Id };
            var sablonUrun = await oku.Urunler.SingleAsync(u => u.Ad == $"Şablon Ürün {ek}");
            Assert.True(sablonUrun.StokTakibi);
            Assert.Equal(59.901m, decimal.Round(sablonUrun.PerakendeSatisFiyati, 3)); // 60,50 KDV dahil / 1,01
            Assert.Equal(750m, (await oku.Urunler.SingleAsync(u => u.Ad == $"Montaj {ek}")).PerakendeSatisFiyati);
            var montaj = await oku.Urunler.SingleAsync(u => u.Ad == $"Montaj {ek}");
            Assert.False(montaj.StokTakibi);
            Assert.Null(montaj.GiderHesapKodu);
            var elektrik = await oku.Urunler.SingleAsync(u => u.Ad == $"Elektrik {ek}");
            Assert.False(elektrik.StokTakibi);
            Assert.Equal("770", elektrik.GiderHesapKodu);
        }
        finally { File.Delete(dosya); }
    }

    [Fact]
    public async Task Cari_listesi_acilis_bakiyesiyle_aktarilir_hatali_satirlar_atlanir()
    {
        var (db, sirket, oturum, _) = await OrtamAsync();
        await using var _ = db;
        var vkn = EBelgeTests.GecerliVkn(Random.Shared.Next());
        var tckn = EBelgeTests.GecerliTckn(Random.Shared.Next());
        var kod = $"AKT{Random.Shared.Next(100000, 999999)}";
        var csv = "Cari Kodu;Ünvanı;Cari Türü;Vergi Dairesi;VKN;T.C. Kimlik No;İl;e-Fatura;Açılış Bakiyesi\r\n" +
                  $"{kod};Kurumsal Alıcı A.Ş.;Müşteri;Kadıköy;{vkn};;İstanbul;Evet;1.250,50\r\n" +
                  $";Ahmet Yılmaz;Tedarikçi;;;{tckn};Ankara;;-300\r\n" +
                  $";Hatalı Vkn Ltd;;;1111111111;;;;\r\n" +
                  $"{kod};Aynı Kodlu;;;;;;;\r\n" +
                  ";;;;;;;;100\r\n";
        var dosya = Path.Combine(Path.GetTempPath(), $"cari-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(dosya, csv, new UTF8Encoding(true));
        try
        {
            var servis = new TopluAktarimServisi(db, oturum);
            var onizleme = servis.Onizle(AktarimTuru.Cari, dosya);
            Assert.Equal(5, onizleme.Count);
            Assert.True(onizleme[0].Gecerli);
            Assert.True(onizleme[1].Gecerli);
            Assert.Contains("Vergi no geçersiz", onizleme[2].Hata);
            Assert.Contains("iki kez", onizleme[3].Hata);
            Assert.Contains("Ünvan boş", onizleme[4].Hata);

            var acilis = new DateTime(DateTime.Today.Year, 1, 1);
            var sonuc = await servis.AktarAsync(AktarimTuru.Cari, dosya, acilis);
            Assert.Equal(2, sonuc.Count(x => x.Basarili));

            await using var oku = new MuhasebeBaglami(fixture.Options) { AktifSirketId = sirket.Id };
            var kurumsal = await oku.CariKartlar.SingleAsync(c => c.Kod == kod);
            Assert.Equal(vkn, kurumsal.VergiNo);
            Assert.True(kurumsal.EFaturaMukellefi);
            Assert.Equal(CariTuru.Musteri, kurumsal.Tur);
            Assert.Equal(1250.50m, kurumsal.Bakiye);
            var sahis = await oku.CariKartlar.SingleAsync(c => c.TcNo == tckn);
            Assert.Equal(CariTipiTanimi.BireyselId, sahis.CariTipiId);
            Assert.Equal(CariTuru.Tedarikci, sahis.Tur);
            Assert.Equal(-300m, sahis.Bakiye);
            var devir = await oku.CariHareketler.SingleAsync(h => h.CariKartId == kurumsal.Id);
            Assert.Equal(CariHareketTuru.Devir, devir.HareketTuru);
            Assert.Equal(acilis, devir.Tarih.Date);
            // Açılış muhasebeye 120 / 500 olarak işlenir.
            var fisSatirlari = await oku.YevmiyeSatirlari.Where(s => s.Fis!.BelgeId == devir.Id && s.Fis.BelgeTipi == "CariFis" && s.Fis.SirketId == sirket.Id)
                .Select(s => new { s.Hesap!.HesapKodu, s.Borc, s.Alacak }).ToListAsync();
            Assert.Contains(fisSatirlari, s => s.HesapKodu == "120" && s.Borc == 1250.50m);
            Assert.Contains(fisSatirlari, s => s.HesapKodu == "500" && s.Alacak == 1250.50m);

            // Aynı dosya ikinci kez aktarılırsa kayıtlı cariler tekrarlanmaz.
            var tekrar = servis.Onizle(AktarimTuru.Cari, dosya);
            Assert.Contains("zaten kayıtlı", tekrar[0].Hata);
            Assert.Contains("kayıtlı cari var", tekrar[1].Hata);
        }
        finally { File.Delete(dosya); }
    }

    [Fact]
    public async Task Urun_listesi_excelden_acilis_stoguyla_aktarilir()
    {
        var (db, sirket, oturum, depo) = await OrtamAsync();
        await using var _ = db;
        var barkod = "869" + Random.Shared.NextInt64(1000000000, 9999999999);
        var veri = OrnekXlsx(
            ["Ürün Adı", "Barkod", "Birim", "KDV Oranı", "Alış Fiyatı", "Satış Fiyatı", "Açılış Stoğu"],
            ["Zeytin 1 Kg", "#" + barkod, "Kg", "#1", "#180.5", "#249.9", "#12.5"],
            ["Peçete", "", "", "", "", "#15", ""],
            ["Aynı Barkod", "#" + barkod, "", "", "", "", ""]);
        var dosya = Path.Combine(Path.GetTempPath(), $"urun-{Guid.NewGuid():N}.xlsx");
        await File.WriteAllBytesAsync(dosya, veri);
        try
        {
            var servis = new TopluAktarimServisi(db, oturum);
            var onizleme = servis.Onizle(AktarimTuru.Urun, dosya);
            Assert.True(onizleme[0].Gecerli);
            Assert.Contains("KDV boş", onizleme[1].Uyari);
            Assert.Contains("iki kez", onizleme[2].Hata);

            var sonuc = await servis.AktarAsync(AktarimTuru.Urun, dosya, DateTime.Today);
            Assert.Equal(2, sonuc.Count(x => x.Basarili));

            await using var oku = new MuhasebeBaglami(fixture.Options) { AktifSirketId = sirket.Id };
            var zeytin = await oku.Urunler.SingleAsync(u => u.Barkod == barkod);
            Assert.Equal("Kg", zeytin.Birim);
            Assert.Equal(1m, zeytin.KdvOrani);
            Assert.Equal(180.5m, zeytin.AlisFiyati);
            Assert.Equal(249.9m, zeytin.PerakendeSatisFiyati);
            Assert.Equal(12.5m, zeytin.StokMiktari);
            Assert.Equal(12.5m, await oku.UrunLokasyonStoklari.Where(l => l.UrunId == zeytin.Id && l.DepoId == depo.Id).SumAsync(l => l.MevcutMiktar));
            Assert.False(string.IsNullOrWhiteSpace(zeytin.Kod)); // kod otomatik verildi
            var pecete = await oku.Urunler.SingleAsync(u => u.Ad == "Peçete" && u.SirketId == sirket.Id);
            Assert.Equal(0m, pecete.StokMiktari);
        }
        finally { File.Delete(dosya); }
    }
}
