using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Finans;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Modeller.Tanimlar;
using EtkiAlani.Yardimcilar;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Servisler;

// Tanıtım veritabanı (yalnız "Örnek verilerle tanıyın" modunda, ayrı bir veritabanında): son 3 ayın alış/satış,
// tahsilat ve ödemeleri programın kendi servisleriyle (gerçek kullanımdaki gibi) oluşturulur; raporlar dolu görünür.
// Gerçek müşteri veritabanında asla çalıştırılmaz: boş olmayan veritabanına dokunmaz.
public static class OrnekVeriServisi
{
    public const string VeritabaniAdi = "Muhasebe_Ornek";

    public static async Task<bool> GerekirseDoldurAsync(Func<MuhasebeBaglami> baglamUret, IKullaniciServisi oturum, CancellationToken cancellationToken = default,
        bool adDenetimi = true)
    {
        await using (var c = baglamUret())
        {
            if (adDenetimi && c.Database.GetDbConnection().Database != VeritabaniAdi)
                throw new InvalidOperationException("Örnek veriler yalnız tanıtım veritabanına yazılır.");
            if (await c.CariKartlar.IgnoreQueryFilters().AnyAsync(cancellationToken) || await c.Faturalar.IgnoreQueryFilters().AnyAsync(cancellationToken))
                return false;
        }
        await DoldurAsync(baglamUret, oturum, cancellationToken);
        return true;
    }

    private static DateTime Gun(int geri, int saat) =>
        DateTime.SpecifyKind(DateTime.Today.AddDays(-geri).AddHours(saat), DateTimeKind.Utc);

    // Sağlama haneleri doğru, uydurma TR IBAN (banka kodu 00099).
    private static string Iban(string hesap)
    {
        var bban = "00099" + "0" + hesap.PadLeft(16, '0');
        var sayi = string.Concat((bban + "TR00").Select(ch => char.IsLetter(ch) ? (ch - 'A' + 10).ToString() : ch.ToString()));
        var kalan = sayi.Aggregate(0, (k, ch) => (k * 10 + (ch - '0')) % 97);
        return $"TR{98 - kalan:D2}{bban}";
    }

    private static string Vkn(int tohum)
    {
        var ilk = (100000000 + tohum % 800000000).ToString();
        for (var son = 0; son <= 9; son++)
            if (KimlikNoDogrulayici.VergiNoGecerliMi(ilk + son)) return ilk + son;
        return ilk + "0";
    }

    private static async Task DoldurAsync(Func<MuhasebeBaglami> baglamUret, IKullaniciServisi oturum, CancellationToken ct)
    {
        var rastgele = new Random(2026);

        await using (var c = baglamUret())
        {
            var ayar = await c.SistemAyarlari.FirstAsync(a => a.Id == 1, ct);
            ayar.FirmaUnvani = "Örnek Ticaret Ltd. Şti. (TANITIM)";
            ayar.VergiDairesi = "Kadıköy";
            ayar.VergiNo = Vkn(424242);
            ayar.Adres = "Caferağa Mah. Moda Cad. No:1";
            ayar.Il = "İstanbul";
            ayar.Ilce = "Kadıköy";
            ayar.PostaKodu = "34710";
            ayar.Telefon = "0216 000 00 00";
            ayar.Eposta = "info@ornekticaret.example";
            await c.SaveChangesAsync(ct);
        }

        var musteriAdlari = new[]
        {
            ("Anadolu Market A.Ş.", "Ankara", "Çankaya"), ("Ege Kırtasiye Ltd. Şti.", "İzmir", "Konak"), ("Marmara Ofis Çözümleri", "Bursa", "Nilüfer"),
            ("Akdeniz Otelcilik A.Ş.", "Antalya", "Muratpaşa"), ("Boğaziçi Kafe", "İstanbul", "Beşiktaş"), ("Karadeniz Yapı Ltd. Şti.", "Trabzon", "Ortahisar"),
            ("Kapadokya Turizm", "Nevşehir", "Ürgüp"), ("Toros Lojistik A.Ş.", "Mersin", "Yenişehir")
        };
        var tedarikciAdlari = new[] { ("Yıldız Toptan Gıda A.Ş.", "İstanbul", "Esenyurt"), ("Atlas Kâğıt Sanayi Ltd. Şti.", "Kocaeli", "Gebze"), ("Deniz Temizlik Ürünleri", "İzmir", "Bornova") };

        var musteriler = new List<int>();
        var tedarikciler = new List<int>();
        await using (var c = baglamUret())
        {
            var cariler = new CariServisi(c, oturum);
            var i = 0;
            foreach (var (ad, il, ilce) in musteriAdlari)
                musteriler.Add((await cariler.OtomatikKodlaEkleAsync(new CariKart
                {
                    Unvan = ad, Tur = CariTuru.Musteri, Aktif = true, CariTipiId = CariTipiTanimi.KurumsalId, VergiNo = Vkn(1000 + i), VergiDairesi = ilce,
                    Il = il, Ilce = ilce, Adres = "Örnek Mah. Deneme Sok. No:" + (++i), Telefon = "0500 000 00 0" + i, Eposta = $"muhasebe{i}@ornek.example",
                    VadeGunu = 30, RiskLimiti = 100000
                })).Id);
            foreach (var (ad, il, ilce) in tedarikciAdlari)
                tedarikciler.Add((await cariler.OtomatikKodlaEkleAsync(new CariKart
                {
                    Unvan = ad, Tur = CariTuru.Tedarikci, Aktif = true, CariTipiId = CariTipiTanimi.KurumsalId, VergiNo = Vkn(2000 + ++i), VergiDairesi = ilce,
                    Il = il, Ilce = ilce, Adres = "Sanayi Sitesi " + i + ". Blok", Eposta = $"satis{i}@tedarik.example"
                })).Id);
        }

        var urunTanimlari = new (string Ad, decimal Alis, decimal Satis, decimal Kdv, int Tedarikci)[]
        {
            ("A4 Fotokopi Kâğıdı (500'lü)", 95, 145, 20, 1), ("Tükenmez Kalem Mavi (50'li)", 120, 189, 20, 1), ("Dosya Klasörü Geniş", 18, 32, 20, 1),
            ("Zımba Makinesi", 85, 139, 20, 1), ("Yapışkan Not Kâğıdı", 12, 24, 20, 1), ("Türk Kahvesi 500 g", 160, 245, 1, 0),
            ("Çay 1 kg", 210, 299, 1, 0), ("Kesme Şeker 1 kg", 38, 55, 1, 0), ("Su 0,5 L (24'lü)", 55, 89, 1, 0),
            ("Bulaşık Deterjanı 4 L", 110, 175, 20, 2), ("Kâğıt Havlu (12'li)", 140, 215, 20, 2), ("Çöp Torbası Büyük (10'lu)", 30, 52, 20, 2)
        };
        var urunler = new List<(int Id, decimal Satis, decimal Kdv, decimal Alis, int Tedarikci)>();
        await using (var c = baglamUret())
        {
            var adet = await c.MerkeziTanimlar.FirstOrDefaultAsync(x => x.Grup == TanimGruplari.Birim && x.Kod == "ADET", ct);
            var servis = new UrunServisi(c, oturum);
            foreach (var u in urunTanimlari)
            {
                var urun = await servis.GuvenliEkleAsync(new Urun
                {
                    Ad = u.Ad, Birim = adet?.KisaAd ?? adet?.Ad ?? "Adet", BirimTanimiId = adet?.Id, KdvOrani = u.Kdv,
                    AlisFiyati = u.Alis, PerakendeSatisFiyati = u.Satis, MinimumStok = 10, Aktif = true
                }, otomatikKod: true, otomatikBarkod: true);
                urunler.Add((urun.Id, u.Satis, u.Kdv, u.Alis, u.Tedarikci));
            }
        }

        int posId, vadesizId;
        await using (var c = baglamUret())
        {
            var banka = new BankaServisi(c, oturum);
            vadesizId = (await banka.BankaHesabiEkleAsync(new BankaHesabi { Kod = "BNK-01", Ad = "Örnek Bankası Vadesiz TL", Iban = Iban("1234567"), HesapTuru = BankaHesapTuru.VadesizTL, AktifMi = true })).Id;
            posId = (await banka.BankaHesabiEkleAsync(new BankaHesabi { Kod = "POS-01", Ad = "Örnek Bankası POS", HesapTuru = BankaHesapTuru.POSHesabi, AktifMi = true })).Id;
        }

        // Stok girişi: her tedarikçiden 85, 55 ve 25 gün önce alış.
        foreach (var geri in new[] { 85, 55, 25 })
            for (var t = 0; t < tedarikciler.Count; t++)
            {
                await using var c = baglamUret();
                var faturalar = new FaturaServisi(c, oturum);
                var alis = await faturalar.EkleAsync(new Fatura
                {
                    FaturaNo = string.Empty, Tur = FaturaTuru.Alis, CariKartId = tedarikciler[t], Tarih = Gun(geri, 10), VadeTarihi = Gun(geri - 30, 10),
                    Kalemler = urunler.Where(u => u.Tedarikci == t)
                        .Select(u => new FaturaKalemi { UrunId = u.Id, Miktar = 40 + rastgele.Next(0, 40), BirimFiyat = u.Alis, KdvOrani = u.Kdv }).ToList()
                });
                await faturalar.OnaylaAsync(alis.Id);
            }

        // Satışlar: son 80 gün, günde 0-2 fatura; nakit, kart ve açık hesap karışık.
        var odemeler = new[] { "Nakit", "Kredi Kartı", "Açık Hesap", "Açık Hesap" };
        var acikSatislar = new List<(int CariId, decimal Tutar, int Geri)>();
        for (var geri = 80; geri >= 0; geri--)
        {
            var adetSatis = rastgele.Next(0, 3);
            for (var s = 0; s < adetSatis; s++)
            {
                var cari = musteriler[rastgele.Next(musteriler.Count)];
                var odeme = odemeler[rastgele.Next(odemeler.Length)];
                var kalemler = urunler.OrderBy(_ => rastgele.Next()).Take(rastgele.Next(1, 4))
                    .Select(u => new FaturaKalemi { UrunId = u.Id, Miktar = rastgele.Next(1, 6), BirimFiyat = u.Satis, KdvOrani = u.Kdv }).ToList();
                await using var c = baglamUret();
                var fatura = await new FaturaServisi(c, oturum).PosSatisiniTamamlaAsync(new Fatura
                {
                    FaturaNo = string.Empty, Tur = FaturaTuru.Satis, CariKartId = cari, Tarih = Gun(geri, 9 + s * 3),
                    VadeTarihi = odeme == "Açık Hesap" ? Gun(geri - 30, 9) : null, Kalemler = kalemler
                }, odeme);
                if (odeme == "Açık Hesap")
                    acikSatislar.Add((cari, fatura.OdenecekTutar, geri));
            }
        }

        // Tahsilatlar: 35 günden eski açık satışların çoğu tahsil edilmiş (bir kısmı bankaya havale), yenileri açık; birkaçı gecikmiş kalır.
        foreach (var (cariId, tutar, geri) in acikSatislar.Where(x => x.Geri > 35))
        {
            if (rastgele.Next(0, 5) == 0) continue; // vadesi geçmiş alacak örneği
            await using var c = baglamUret();
            if (rastgele.Next(0, 2) == 0)
                await new KasaServisi(c, oturum).TahsilatEkleAsync(cariId, tutar, "Fatura tahsilatı", tarih: Gun(geri - 25, 14));
            else
                await new BankaServisi(c, oturum).HesabaTahsilatEkleAsync(vadesizId, cariId, tutar, "Havale ile tahsilat", tarih: Gun(geri - 28, 11));
        }

        // Kartlı satışların POS'ta biriken tutarı vadesiz hesaba geçer (%2 komisyon).
        await using (var c = baglamUret())
        {
            var banka = new BankaServisi(c, oturum);
            var pos = await banka.HesapBakiyeHesaplaAsync(posId);
            if (pos > 0)
                await banka.PosBlokeHesabaGecirAsync(posId, vadesizId, pos, decimal.Round(pos * 0.02m, 2), "POS hesabından aktarım", Gun(5, 16));
        }

        // Tedarikçi ödemeleri: eski alışlar bankadaki para yettiği kadar ödenir; kalanı borç olarak görünür.
        await using (var c = baglamUret())
        {
            var alislar = await c.Faturalar.AsNoTracking().Where(f => f.Tur == FaturaTuru.Alis && f.Tarih < Gun(40, 0))
                .OrderBy(f => f.Tarih).Select(f => new { f.CariKartId, f.OdenecekTutar, f.Tarih }).ToListAsync(ct);
            var banka = new BankaServisi(c, oturum);
            foreach (var a in alislar)
            {
                var tutar = Math.Min(a.OdenecekTutar, decimal.Floor(await banka.HesapBakiyeHesaplaAsync(vadesizId) - 1000));
                if (tutar <= 0) break;
                await banka.HesaptanOdemeEkleAsync(vadesizId, a.CariKartId, tutar, "Tedarikçi ödemesi", tarih: Gun(3, 15));
            }
        }
    }
}
