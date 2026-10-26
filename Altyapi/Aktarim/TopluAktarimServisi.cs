using System.Globalization;
using System.Text;
using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Stok;
using EtkiAlani.Yardimcilar;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.Aktarim;

// Başka programdan geçişte cari ve ürün listelerinin Excel/CSV'den toplu aktarımı.
// Sütun başlıkları esnek tanınır (büyük/küçük harf, Türkçe karakter, boşluk fark etmez; "VKN" = "Vergi No").
public class TopluAktarimServisi : ITopluAktarimServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;

    public TopluAktarimServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
    }

    private static readonly Dictionary<string, string[]> CariSutunlari = new()
    {
        ["Kod"] = ["kod", "carikod", "carikodu", "hesapkodu", "musterikodu"],
        ["Unvan"] = ["unvan", "unvani", "cariunvani", "firmaunvani", "firmaadi", "cariadi", "musteriadi", "adsoyad", "adisoyadi"],
        ["Ad"] = ["ad", "adi"],
        ["Soyad"] = ["soyad", "soyadi"],
        ["Tur"] = ["tur", "turu", "carituru", "hesapturu"],
        ["Tip"] = ["tip", "tipi", "caritipi", "kisitipi"],
        ["VergiDairesi"] = ["vergidairesi", "vd"],
        ["VergiNo"] = ["vergino", "vkn", "verginumarasi", "vergikimlikno"],
        ["TcNo"] = ["tcno", "tckn", "tckimlikno", "tckimliknumarasi", "kimlikno"],
        ["Telefon"] = ["telefon", "tel", "istelefonu", "sabittelefon"],
        ["Cep"] = ["cep", "ceptelefonu", "gsm", "cepno"],
        ["Eposta"] = ["eposta", "email", "mail", "epostaadresi"],
        ["Adres"] = ["adres", "acikadres"],
        ["Il"] = ["il", "sehir"],
        ["Ilce"] = ["ilce"],
        ["PostaKodu"] = ["postakodu"],
        ["EFatura"] = ["efaturamukellefi", "efatura", "efaturakullanicisi"],
        ["RiskLimiti"] = ["risklimiti", "limit"],
        ["VadeGunu"] = ["vadegunu", "vade"],
        ["Bakiye"] = ["acilisbakiyesi", "bakiye", "devirbakiyesi", "devir"],
        ["Borc"] = ["borc", "borcbakiyesi"],
        ["Alacak"] = ["alacak", "alacakbakiyesi"],
    };

    private static readonly Dictionary<string, string[]> UrunSutunlari = new()
    {
        ["Kod"] = ["kod", "urunkodu", "stokkodu", "malzemekodu"],
        ["Barkod"] = ["barkod", "barkodno"],
        ["Ad"] = ["ad", "adi", "urunadi", "stokadi", "malzemeadi", "urun"],
        ["Birim"] = ["birim", "olcubirimi"],
        ["Kategori"] = ["kategori", "grup", "urungrubu"],
        ["Kdv"] = ["kdv", "kdvorani", "kdvyuzde"],
        ["Alis"] = ["alisfiyati", "alis", "maliyet", "birimmaliyet"],
        ["Satis"] = ["satisfiyati", "satis", "perakendesatisfiyati", "perakendefiyat", "fiyat"],
        ["Toptan"] = ["toptansatisfiyati", "toptanfiyat", "toptan"],
        ["MinStok"] = ["minimumstok", "kritikstok", "minstok"],
        ["Stok"] = ["acilisstogu", "stok", "stokmiktari", "miktar", "mevcut"],
        // "Hizmet" / "Masraf": stok tutulmayan kart; boş ya da "Ürün"/"Stok"/"Mal": stoklu ürün.
        ["Tur"] = ["tur", "turu", "kartturu", "stokturu", "urunturu"],
        ["GiderHesabi"] = ["giderhesabi", "giderhesapkodu", "masrafhesabi"],
    };

    public string SablonCsv(AktarimTuru tur) => tur == AktarimTuru.Cari
        ? "﻿Kod;Ünvan;Tür;Tip;Vergi Dairesi;Vergi No;TC Kimlik No;Telefon;Cep Telefonu;E-posta;Adres;İl;İlçe;Posta Kodu;e-Fatura Mükellefi;Risk Limiti;Vade Günü;Açılış Bakiyesi\r\n" +
          ";Örnek Ticaret Ltd. Şti.;Müşteri;Kurumsal;Kadıköy;1234567890;;02160000000;05320000000;info@ornek.com;Moda Cad. No:1;İstanbul;Kadıköy;34710;Evet;50000;30;1250,50\r\n"
        : "﻿Kod;Barkod;Ürün Adı;Birim;Kategori;KDV Oranı;Alış Fiyatı;Satış Fiyatı;Toptan Fiyatı;Minimum Stok;Açılış Stoğu;Kart Türü;Gider Hesabı\r\n" +
          ";8690000000000;Örnek Ürün 1 Kg;Adet;Gıda;1;45,00;59,90;55,00;10;120;Ürün;\r\n" +
          ";;Montaj İşçiliği;Saat;Hizmetler;20;0;750,00;;;;Hizmet;\r\n" +
          ";;Elektrik Faturası;Adet;Giderler;20;0;0;;;;Masraf;770\r\n";

    public IReadOnlyList<AktarimSatiri> Onizle(AktarimTuru tur, string dosyaYolu)
    {
        Yetki(tur);
        var satirlar = TabloOkuyucu.Oku(dosyaYolu);
        return tur == AktarimTuru.Cari ? CariHazirla(satirlar).Select(x => x.Satir).ToList() : UrunHazirla(satirlar).Select(x => x.Satir).ToList();
    }

    public async Task<IReadOnlyList<AktarimSonucu>> AktarAsync(AktarimTuru tur, string dosyaYolu, DateTime acilisTarihi, CancellationToken cancellationToken = default)
    {
        Yetki(tur);
        var satirlar = TabloOkuyucu.Oku(dosyaYolu);
        var sonuclar = new List<AktarimSonucu>();
        if (tur == AktarimTuru.Cari)
        {
            var cariServisi = new CariServisi(_baglam, _kullaniciServisi);
            var hareketServisi = new CariHareketServisi(_baglam, _kullaniciServisi);
            foreach (var (satir, cari, bakiye) in CariHazirla(satirlar))
            {
                if (!satir.Gecerli || cari == null) { sonuclar.Add(new AktarimSonucu(satir.SatirNo, satir.Ad, false, satir.Hata ?? "Geçersiz satır.")); continue; }
                CariKart kayit;
                try
                {
                    kayit = string.IsNullOrWhiteSpace(cari.Kod)
                        ? await cariServisi.OtomatikKodlaEkleAsync(cari)
                        : await cariServisi.EkleAsync(cari);
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    Temizle();
                    sonuclar.Add(new AktarimSonucu(satir.SatirNo, satir.Ad, false, ex.Message));
                    continue;
                }
                if (bakiye == 0) { sonuclar.Add(new AktarimSonucu(satir.SatirNo, satir.Ad, true, $"Cari açıldı ({kayit.Kod}).")); continue; }
                try
                {
                    await hareketServisi.EkleAsync(new CariHareket
                    {
                        CariKartId = kayit.Id, SirketId = kayit.SirketId, Tarih = DateTime.SpecifyKind(acilisTarihi.Date, DateTimeKind.Utc), HareketTuru = CariHareketTuru.Devir,
                        Borc = bakiye > 0 ? bakiye : 0, Alacak = bakiye < 0 ? -bakiye : 0, BelgeNo = "ACILIS", Aciklama = "Açılış bakiyesi (toplu aktarım)"
                    }, "500");
                    sonuclar.Add(new AktarimSonucu(satir.SatirNo, satir.Ad, true, $"Cari açıldı ({kayit.Kod}), açılış bakiyesi {Math.Abs(bakiye):N2} ₺ {(bakiye > 0 ? "borç" : "alacak")} işlendi."));
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    Temizle();
                    sonuclar.Add(new AktarimSonucu(satir.SatirNo, satir.Ad, false, $"Cari açıldı ({kayit.Kod}) ama açılış bakiyesi işlenemedi: {ex.Message} Bakiyeyi cari kartından 'Cari Fiş → Açılış / Devir' ile girin."));
                }
            }
        }
        else
        {
            var urunServisi = new UrunServisi(_baglam, _kullaniciServisi);
            var depo = await DepoKurallari.VarsayilanDepoAsync(_baglam, cancellationToken);
            foreach (var (satir, urun, stok) in UrunHazirla(satirlar))
            {
                if (!satir.Gecerli || urun == null) { sonuclar.Add(new AktarimSonucu(satir.SatirNo, satir.Ad, false, satir.Hata ?? "Geçersiz satır.")); continue; }
                if (stok > 0)
                    urun.LokasyonStoklari = [new UrunLokasyonStogu { DepoId = depo.Id, MevcutMiktar = stok }];
                try
                {
                    var kayit = string.IsNullOrWhiteSpace(urun.Kod)
                        ? await urunServisi.GuvenliEkleAsync(urun, otomatikKod: true, otomatikBarkod: false)
                        : await urunServisi.EkleAsync(urun);
                    sonuclar.Add(new AktarimSonucu(satir.SatirNo, satir.Ad, true,
                        $"Ürün açıldı ({kayit.Kod})" + (stok > 0 ? $", açılış stoğu {stok:0.####} {kayit.Birim} ({depo.DepoAdi})." : ".")));
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    Temizle();
                    sonuclar.Add(new AktarimSonucu(satir.SatirNo, satir.Ad, false, ex.Message));
                }
            }
        }
        return sonuclar;
    }

    // Başarısız satırın bağlamda kalan yarım kaydı sonraki satırın kaydına karışmasın.
    private void Temizle()
    {
        foreach (var giris in _baglam.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
            giris.State = EntityState.Detached;
    }

    // ---- Cari ----

    private List<(AktarimSatiri Satir, CariKart? Cari, decimal Bakiye)> CariHazirla(List<TabloOkuyucu.Satir> satirlar)
    {
        var harita = SutunHaritasi(satirlar, CariSutunlari);
        if (!harita.ContainsKey("Unvan") && !(harita.ContainsKey("Ad") && harita.ContainsKey("Soyad")))
            throw new InvalidOperationException("Dosyada 'Ünvan' sütunu bulunamadı. Şablonu indirip sütun başlıklarını kontrol edin.");

        var mevcutKodlar = _baglam.CariKartlar.IgnoreQueryFilters([MuhasebeBaglami.SilinmisFiltresi]).Select(c => c.Kod).ToList().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mevcutKimlikler = _baglam.CariKartlar.Select(c => new { c.Kod, c.VergiNo, c.TcNo }).ToList()
            .SelectMany(c => new[] { (No: Rakam(c.VergiNo), c.Kod), (No: Rakam(c.TcNo), c.Kod) }).Where(x => x.No.Length > 0)
            .GroupBy(x => x.No).ToDictionary(g => g.Key, g => g.First().Kod);
        var dosyaKodlari = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dosyaKimlikleri = new HashSet<string>();

        var sonuc = new List<(AktarimSatiri, CariKart?, decimal)>();
        foreach (var s in satirlar)
        {
            string D(string alan) => harita.TryGetValue(alan, out var b) && s.Hucreler.TryGetValue(b, out var v) ? v.Trim() : string.Empty;
            var hatalar = new List<string>();
            var uyarilar = new List<string>();

            var kod = D("Kod").ToUpperInvariant();
            var ad = D("Ad");
            var soyad = D("Soyad");
            var unvan = D("Unvan");
            if (unvan.Length == 0) unvan = $"{ad} {soyad}".Trim();
            var vkn = Rakam(D("VergiNo"));
            var tckn = Rakam(D("TcNo"));
            // Vergi no sütununa 11 haneli T.C. kimlik no yazılmışsa (şahıs) T.C. no sayılır.
            if (vkn.Length == 11 && tckn.Length == 0) { tckn = vkn; vkn = string.Empty; }

            if (unvan.Length == 0) hatalar.Add("Ünvan boş");
            if (vkn.Length > 0 && !KimlikNoDogrulayici.VergiNoGecerliMi(vkn)) hatalar.Add($"Vergi no geçersiz ({vkn})");
            if (tckn.Length > 0 && !KimlikNoDogrulayici.TcKimlikNoGecerliMi(tckn)) hatalar.Add($"T.C. kimlik no geçersiz ({tckn})");
            if (kod.Length > 0 && mevcutKodlar.Contains(kod)) hatalar.Add($"'{kod}' kodlu cari zaten kayıtlı");
            else if (kod.Length > 0 && !dosyaKodlari.Add(kod)) hatalar.Add($"'{kod}' kodu dosyada iki kez geçiyor");
            foreach (var no in new[] { vkn, tckn }.Where(n => n.Length > 0))
            {
                if (mevcutKimlikler.TryGetValue(no, out var mevcutKod)) hatalar.Add($"{no} numarasıyla kayıtlı cari var ({mevcutKod})");
                else if (!dosyaKimlikleri.Add(no)) hatalar.Add($"{no} numarası dosyada iki kez geçiyor");
            }

            var turMetni = Normal(D("Tur"));
            var cariTuru = turMetni.Contains("tedarik") || turMetni.Contains("satici")
                ? (turMetni.Contains("musteri") || turMetni.Contains("alici") ? CariTuru.MusteriTedarikci : CariTuru.Tedarikci)
                : turMetni.Contains("ikisi") || turMetni.Contains("her") ? CariTuru.MusteriTedarikci : CariTuru.Musteri;
            var tipMetni = Normal(D("Tip"));
            var bireysel = tipMetni.Contains("bireysel") || tipMetni.Contains("sahis") || (tipMetni.Length == 0 && vkn.Length == 0 && tckn.Length == 11);

            decimal bakiye = 0;
            if (harita.ContainsKey("Bakiye") && D("Bakiye").Length > 0)
            {
                if (!Sayi(D("Bakiye"), out bakiye)) hatalar.Add($"Açılış bakiyesi sayı değil ('{D("Bakiye")}')");
            }
            else if (D("Borc").Length > 0 || D("Alacak").Length > 0)
            {
                var borcOk = Sayi(D("Borc"), out var borc) || D("Borc").Length == 0;
                var alacakOk = Sayi(D("Alacak"), out var alacak) || D("Alacak").Length == 0;
                if (!borcOk || !alacakOk) hatalar.Add("Borç/alacak sayı değil");
                bakiye = borc - alacak;
            }
            decimal? risk = null;
            if (D("RiskLimiti").Length > 0) { if (Sayi(D("RiskLimiti"), out var r)) risk = r; else uyarilar.Add("Risk limiti okunamadı, boş bırakıldı"); }
            int? vade = null;
            if (D("VadeGunu").Length > 0) { if (int.TryParse(D("VadeGunu"), out var v) && v >= 0) vade = v; else uyarilar.Add("Vade günü okunamadı, boş bırakıldı"); }
            if (bakiye != 0) uyarilar.Add($"Açılış bakiyesi: {Math.Abs(bakiye):N2} ₺ {(bakiye > 0 ? "borç" : "alacak")}");

            var cari = hatalar.Count > 0 ? null : new CariKart
            {
                Kod = kod, Unvan = unvan, Ad = ad.Length > 0 ? ad : null, Soyad = soyad.Length > 0 ? soyad : null, Tur = cariTuru,
                CariTipiId = bireysel ? CariTipiTanimi.BireyselId : CariTipiTanimi.KurumsalId,
                VergiDairesi = Bos(D("VergiDairesi")), VergiNo = Bos(vkn), TcNo = Bos(tckn),
                Telefon = Bos(D("Telefon")), CepTelefonu = Bos(D("Cep")), Eposta = Bos(D("Eposta")),
                Adres = Bos(D("Adres")), Il = Bos(D("Il")), Ilce = Bos(D("Ilce")), PostaKodu = Bos(D("PostaKodu")),
                EFaturaMukellefi = Evet(D("EFatura")), RiskLimiti = risk, VadeGunu = vade, Aktif = true
            };
            var ozet = string.Join(" • ", new[] { cariTuru == CariTuru.Tedarikci ? "Tedarikçi" : cariTuru == CariTuru.MusteriTedarikci ? "Müşteri+Tedarikçi" : "Müşteri",
                vkn.Length > 0 ? "VKN " + vkn : tckn.Length > 0 ? "TCKN " + tckn : "", D("Il") }.Where(x => x.Length > 0));
            sonuc.Add((new AktarimSatiri(s.SatirNo, kod.Length > 0 ? kod : "(otomatik)", unvan, ozet,
                hatalar.Count > 0 ? string.Join("; ", hatalar) : null, uyarilar.Count > 0 ? string.Join("; ", uyarilar) : null), cari, bakiye));
        }
        return sonuc;
    }

    // ---- Ürün ----

    private List<(AktarimSatiri Satir, Urun? Urun, decimal Stok)> UrunHazirla(List<TabloOkuyucu.Satir> satirlar)
    {
        var harita = SutunHaritasi(satirlar, UrunSutunlari);
        if (!harita.ContainsKey("Ad"))
            throw new InvalidOperationException("Dosyada 'Ürün Adı' sütunu bulunamadı. Şablonu indirip sütun başlıklarını kontrol edin.");

        var varsayilanKdv = _baglam.SistemAyarlari.AsNoTracking().Where(a => a.Id == 1).Select(a => (decimal?)a.VarsayilanKdv).FirstOrDefault() ?? 20m;
        var mevcutKodlar = _baglam.Urunler.IgnoreQueryFilters([MuhasebeBaglami.SilinmisUrunFiltresi]).Where(u => !u.Silindi).Select(u => u.Kod).ToList().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mevcutBarkodlar = _baglam.Urunler.Where(u => u.Barkod != null).Select(u => u.Barkod!).ToList().ToHashSet();
        var dosyaKodlari = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dosyaBarkodlari = new HashSet<string>();

        var sonuc = new List<(AktarimSatiri, Urun?, decimal)>();
        foreach (var s in satirlar)
        {
            string D(string alan) => harita.TryGetValue(alan, out var b) && s.Hucreler.TryGetValue(b, out var v) ? v.Trim() : string.Empty;
            var hatalar = new List<string>();
            var uyarilar = new List<string>();
            var kod = D("Kod").ToUpperInvariant();
            var ad = D("Ad");
            var barkod = D("Barkod");
            if (ad.Length == 0) hatalar.Add("Ürün adı boş");
            if (kod.Length > 0 && mevcutKodlar.Contains(kod)) hatalar.Add($"'{kod}' kodlu ürün zaten kayıtlı");
            else if (kod.Length > 0 && !dosyaKodlari.Add(kod)) hatalar.Add($"'{kod}' kodu dosyada iki kez geçiyor");
            if (barkod.Length > 0 && mevcutBarkodlar.Contains(barkod)) hatalar.Add($"'{barkod}' barkodu zaten kayıtlı");
            else if (barkod.Length > 0 && !dosyaBarkodlari.Add(barkod)) hatalar.Add($"'{barkod}' barkodu dosyada iki kez geçiyor");

            decimal Tutar(string alan, string ad2, decimal varsayilan)
            {
                var m = D(alan);
                if (m.Length == 0) return varsayilan;
                if (Sayi(m.Replace("%", "").Replace("₺", "").Replace("TL", ""), out var d) && d >= 0) return d;
                hatalar.Add($"{ad2} okunamadı ('{m}')");
                return varsayilan;
            }
            var kdv = Tutar("Kdv", "KDV oranı", varsayilanKdv);
            // "0,20" gibi oran yazılmışsa yüzdeye çevrilir.
            if (kdv > 0 && kdv < 1) kdv *= 100;
            if (kdv > 100) hatalar.Add("KDV oranı 100'den büyük olamaz");
            var alis = Tutar("Alis", "Alış fiyatı", 0);
            var satis = Tutar("Satis", "Satış fiyatı", 0);
            var toptan = Tutar("Toptan", "Toptan fiyatı", 0);
            var minStok = Tutar("MinStok", "Minimum stok", 0);
            var stok = Tutar("Stok", "Açılış stoğu", 0);
            if (D("Kdv").Length == 0) uyarilar.Add($"KDV boş, %{varsayilanKdv:0.##} kabul edildi");
            var turMetni = Normal(D("Tur"));
            var hizmet = turMetni.StartsWith("hizmet") || turMetni.StartsWith("masraf") || turMetni.StartsWith("gider");
            if (turMetni.Length > 0 && !hizmet && !(turMetni.StartsWith("urun") || turMetni.StartsWith("stok") || turMetni.StartsWith("mal") || turMetni.StartsWith("emtia")))
                hatalar.Add($"Kart türü anlaşılamadı ('{D("Tur")}'): Ürün ya da Hizmet yazın");
            if (hizmet && stok > 0) hatalar.Add("Hizmet / masraf kartına açılış stoğu girilmez");
            var giderKodu = D("GiderHesabi");
            if (giderKodu.Length > 0 && !giderKodu.All(c => char.IsAsciiDigit(c) || c == '.')) hatalar.Add($"Gider hesap kodu rakam olmalı ('{giderKodu}')");
            if (stok > 0 && alis == 0) uyarilar.Add("Alış fiyatı boş: açılış stoğu sıfır maliyetle girer");

            var urun = hatalar.Count > 0 ? null : new Urun
            {
                Kod = kod, Ad = ad.Length > 200 ? ad[..200] : ad, Barkod = Bos(barkod), Birim = D("Birim").Length > 0 ? D("Birim") : "Adet",
                Kategori = Bos(D("Kategori")), KdvOrani = kdv, AlisFiyati = alis, PerakendeSatisFiyati = satis, ToptanSatisFiyati = toptan,
                MinimumStok = hizmet ? 0 : minStok, Aktif = true,
                StokTakibi = !hizmet, GiderHesapKodu = hizmet && giderKodu.Length > 0 ? giderKodu : null
            };
            var ozet = (hizmet ? "Hizmet/masraf • " : "") + $"KDV %{kdv:0.##} • Alış {alis:N2} • Satış {satis:N2}" + (stok > 0 ? $" • Stok {stok:0.####}" : "");
            sonuc.Add((new AktarimSatiri(s.SatirNo, kod.Length > 0 ? kod : "(otomatik)", ad, ozet,
                hatalar.Count > 0 ? string.Join("; ", hatalar) : null, uyarilar.Count > 0 ? string.Join("; ", uyarilar) : null), urun, stok));
        }
        return sonuc;
    }

    // ---- Yardımcılar ----

    private static Dictionary<string, string> SutunHaritasi(List<TabloOkuyucu.Satir> satirlar, Dictionary<string, string[]> tanimlar)
    {
        var basliklar = satirlar.FirstOrDefault()?.Hucreler.Keys.ToList() ?? new List<string>();
        var harita = new Dictionary<string, string>();
        foreach (var baslik in basliklar)
        {
            var n = Normal(baslik);
            var alan = tanimlar.FirstOrDefault(t => t.Value.Contains(n)).Key;
            if (alan != null && !harita.ContainsKey(alan))
                harita[alan] = baslik;
        }
        return harita;
    }

    // "Vergi Dairesi" → "vergidairesi", "Ünvanı" → "unvani", "E-posta" → "eposta".
    public static string Normal(string metin)
    {
        var sb = new StringBuilder();
        foreach (var ch in metin.Trim().ToLower(new CultureInfo("tr-TR")))
        {
            var c = ch switch { 'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', 'â' => 'a', 'î' => 'i', 'û' => 'u', _ => ch };
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    // "1.250,50", "1250,5", "1250.5", "-300" ve "(300)" okunur (Excel hücreleri okuyucudan ondalık virgülle gelir). Yalnız nokta varsa ve her noktadan sonra
    // tam 3 rakam geliyorsa (ör. "1.250") binlik ayırıcı sayılır.
    public static bool Sayi(string metin, out decimal sonuc)
    {
        sonuc = 0;
        var m = metin.Trim().Replace(" ", "").Replace(" ", "");
        if (m.Length == 0) return false;
        var eksi = m.StartsWith('(') && m.EndsWith(')');
        if (eksi) m = m[1..^1];
        var virgul = m.LastIndexOf(',');
        var nokta = m.LastIndexOf('.');
        if (virgul >= 0 && nokta >= 0)
            m = virgul > nokta ? m.Replace(".", "").Replace(',', '.') : m.Replace(",", "");
        else if (virgul >= 0)
            m = m.Replace(',', '.');
        else if (nokta >= 0 && m.Split('.').Skip(1).All(p => p.Length == 3) && m.Count(c => c == '.') >= 1 && m.Split('.')[0].TrimStart('-').Length is > 0 and <= 3)
            m = m.Replace(".", "");
        if (!decimal.TryParse(m, NumberStyles.Number, CultureInfo.InvariantCulture, out sonuc)) return false;
        if (eksi) sonuc = -sonuc;
        return true;
    }

    private static bool Evet(string m) => Normal(m) is "evet" or "e" or "1" or "x" or "var" or "true" or "yes";
    private static string? Bos(string m) => string.IsNullOrWhiteSpace(m) ? null : m.Trim();
    private static string Rakam(string? s) => new((s ?? string.Empty).Where(char.IsDigit).ToArray());

    private void Yetki(AktarimTuru tur) =>
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama"))
            .YetkiKontrolEt(y => tur == AktarimTuru.Cari ? y.CariEkle : y.StokEkle, tur == AktarimTuru.Cari ? "Toplu Cari Aktarımı" : "Toplu Ürün Aktarımı");
}
