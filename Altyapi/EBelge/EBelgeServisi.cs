using Altyapi.Servisler;
using Altyapi.Veri;
using EtkiAlani.Modeller.Cari;
using EtkiAlani.Modeller.Fatura;
using EtkiAlani.Modeller.Ortak;
using Microsoft.EntityFrameworkCore;
using Uygulama.Arayuzler;

namespace Altyapi.EBelge;

public class EBelgeServisi : IEBelgeServisi
{
    private readonly MuhasebeBaglami _baglam;
    private readonly IKullaniciServisi? _kullaniciServisi;
    private readonly IEBelgeEntegratoru _entegrator;

    public EBelgeServisi(MuhasebeBaglami baglam, IKullaniciServisi? kullaniciServisi = null, IEBelgeEntegratoru? entegrator = null)
    {
        _baglam = baglam;
        _kullaniciServisi = kullaniciServisi;
        _entegrator = entegrator ?? new DosyaEBelgeEntegratoru(() =>
            _baglam.SistemAyarlari.AsNoTracking().Where(a => a.Id == 1).Select(a => a.EBelgeKlasoru).FirstOrDefault() ?? string.Empty);
    }

    public string EntegratorAdi => _entegrator.Ad;
    public bool DogrudanGonderir => _entegrator.DogrudanGonderir;

    public async Task<EBelgeOnKontrol> OnKontrolAsync(EBelgeHazirlamaIstegi istek, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.FaturaGoruntule, "e-Belge Ön Kontrol");
        var fatura = await FaturaGetirAsync(istek.FaturaId, izle: false, cancellationToken);
        IadeBilgisiUygula(fatura, istek);
        var girdi = await GirdiOlusturAsync(fatura, istek.Senaryo, cancellationToken);
        var hatalar = DurumHatalari(fatura);
        hatalar.AddRange(UblTrFaturaUretici.Dogrula(girdi));
        return new EBelgeOnKontrol(fatura.Id, fatura.FaturaNo, girdi.Tur, girdi.Senaryo, fatura.EBelgeDurumu,
            fatura.CariKart?.Unvan ?? string.Empty, girdi.Alici.VknTckn, fatura.Tur == FaturaTuru.AlisIade,
            fatura.IadeEdilenFaturaNo, fatura.IadeEdilenFaturaTarihi, fatura.EBelgeMesaji, hatalar);
    }

    public async Task<EBelgeGonderimSonucu> HazirlaVeGonderAsync(EBelgeHazirlamaIstegi istek, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.FaturaOnayla, "e-Belge Hazırlama/Gönderme");
        var fatura = await FaturaGetirAsync(istek.FaturaId, izle: true, cancellationToken);

        IadeBilgisiUygula(fatura, istek);

        var girdi = await GirdiOlusturAsync(fatura, istek.Senaryo, cancellationToken);
        var hatalar = DurumHatalari(fatura);
        hatalar.AddRange(UblTrFaturaUretici.Dogrula(girdi));
        if (hatalar.Count > 0)
            throw new InvalidOperationException("e-Belge hazırlanamadı:\n- " + string.Join("\n- ", hatalar));

        // ETTN belgenin kimliğidir: hata sonrası yeniden hazırlamada aynı ETTN kullanılır (aynı belge iki kez oluşmasın).
        fatura.Ettn ??= girdi.Ettn;
        fatura.EBelgeTuru = girdi.Tur;
        fatura.EFaturaSenaryosu = girdi.Senaryo;
        var xml = UblTrFaturaUretici.UretMetin(girdi with { Ettn = fatura.Ettn.Value });

        EBelgeGonderimSonucu sonuc;
        try
        {
            sonuc = await _entegrator.GonderAsync(new EBelgePaketi(fatura.FaturaNo, fatura.Ettn.Value, girdi.Tur, girdi.Alici.VknTckn,
                fatura.CariKart?.EFaturaEtiketi, xml), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sonuc = new EBelgeGonderimSonucu(false, EBelgeDurumu.Hata, ex.Message);
        }

        fatura.EBelgeDurumu = sonuc.Durum;
        fatura.EBelgeTarihi = DateTime.UtcNow;
        fatura.EBelgeMesaji = sonuc.Mesaj;
        _baglam.EBelgeKayitlari.Add(new EBelgeKaydi
        {
            FaturaId = fatura.Id, Ettn = fatura.Ettn.Value, Tur = girdi.Tur, Durum = sonuc.Durum, Entegrator = _entegrator.Ad,
            Xml = xml, DosyaYolu = sonuc.DosyaYolu, Mesaj = sonuc.Mesaj, KullaniciId = _kullaniciServisi?.AktifKullanici?.Id
        });
        await _baglam.SaveChangesAsync(cancellationToken);
        return sonuc;
    }

    public async Task DurumIsaretleAsync(int faturaId, EBelgeDurumu durum, string? aciklama = null, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.FaturaOnayla, "e-Belge Durum İşaretleme");
        var fatura = await FaturaGetirAsync(faturaId, izle: true, cancellationToken);
        if (fatura.EBelgeDurumu == EBelgeDurumu.Yok || fatura.Ettn == null)
            throw new InvalidOperationException("Bu fatura için henüz e-belge hazırlanmadı.");
        if (durum is EBelgeDurumu.Yok or EBelgeDurumu.Hazirlandi)
            throw new InvalidOperationException("Bu durum elle işaretlenemez.");
        if (fatura.EBelgeDurumu == EBelgeDurumu.IptalEdildi)
            throw new InvalidOperationException("İptal edilmiş e-belgenin durumu değiştirilemez.");
        if (durum == EBelgeDurumu.Reddedildi && !(fatura.EBelgeTuru == EBelgeTuru.EFatura && fatura.EFaturaSenaryosu == EFaturaSenaryosu.Ticari))
            throw new InvalidOperationException("Yalnız ticari senaryodaki e-Fatura alıcı tarafından reddedilebilir.");

        fatura.EBelgeDurumu = durum;
        fatura.EBelgeTarihi = DateTime.UtcNow;
        fatura.EBelgeMesaji = string.IsNullOrWhiteSpace(aciklama) ? $"Kullanıcı tarafından '{durum}' olarak işaretlendi." : aciklama.Trim();
        _baglam.EBelgeKayitlari.Add(new EBelgeKaydi
        {
            FaturaId = fatura.Id, Ettn = fatura.Ettn.Value, Tur = fatura.EBelgeTuru, Durum = durum, Entegrator = _entegrator.Ad,
            Mesaj = fatura.EBelgeMesaji, KullaniciId = _kullaniciServisi?.AktifKullanici?.Id
        });
        await _baglam.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> SonXmlGetirAsync(int faturaId, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.FaturaGoruntule, "e-Belge Görüntüleme");
        // Şirket ayrımı fatura üzerinden: başka şirketin faturası bulunamaz.
        await FaturaGetirAsync(faturaId, izle: false, cancellationToken);
        return await _baglam.EBelgeKayitlari.AsNoTracking()
            .Where(k => k.FaturaId == faturaId && k.Xml != "")
            .OrderByDescending(k => k.Id).Select(k => k.Xml).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EBelgeTopluSonuc>> TopluHazirlaAsync(DateTime baslangic, DateTime bitis, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.FaturaOnayla, "Toplu e-Belge Hazırlama");
        var aralik = RaporTarihAraligi.Olustur(baslangic, bitis);
        var adaylar = await _baglam.Faturalar.AsNoTracking()
            .Where(f => f.Durum == BelgeDurumu.Onaylandi && (f.Tur == FaturaTuru.Satis || f.Tur == FaturaTuru.AlisIade) &&
                        (f.EBelgeDurumu == EBelgeDurumu.Yok || f.EBelgeDurumu == EBelgeDurumu.Hata) &&
                        f.Tarih >= aralik.Baslangic && f.Tarih < aralik.BitisSonrasi)
            .OrderBy(f => f.Tarih).ThenBy(f => f.FaturaNo)
            .Select(f => new { f.Id, f.FaturaNo, Unvan = f.CariKart != null ? f.CariKart.Unvan : "" })
            .ToListAsync(cancellationToken);

        var sonuclar = new List<EBelgeTopluSonuc>();
        foreach (var a in adaylar)
        {
            try
            {
                var s = await HazirlaVeGonderAsync(new EBelgeHazirlamaIstegi(a.Id), cancellationToken);
                sonuclar.Add(new EBelgeTopluSonuc(a.Id, a.FaturaNo, a.Unvan, s.Basarili, s.Mesaj ?? string.Empty));
            }
            catch (InvalidOperationException ex)
            {
                // Doğrulama hatası: bu fatura atlanır, yarım kalan değişiklik sonraki kayda karışmasın.
                foreach (var giris in _baglam.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
                {
                    if (giris.State == EntityState.Added) { giris.State = EntityState.Detached; continue; }
                    giris.CurrentValues.SetValues(giris.OriginalValues);
                    giris.State = EntityState.Unchanged;
                }
                sonuclar.Add(new EBelgeTopluSonuc(a.Id, a.FaturaNo, a.Unvan, false, ex.Message));
            }
        }
        return sonuclar;
    }

    // ---- Gelen e-Fatura ----

    private sealed record GelenHazirlik(
        GelenFatura Belge, FaturaTuru Tur, CariKart? Cari,
        List<(GelenFaturaSatiri Satir, EtkiAlani.Modeller.Stok.Urun? Urun, decimal IskontoOrani)> Satirlar,
        List<string> Uyarilar, List<string> Hatalar);

    private static readonly string[] AlisOlarakAlinanTipler = ["SATIS", "ISTISNA", "IHRACKAYITLI", "OZELMATRAH", "TEVKIFAT"];

    private async Task<GelenHazirlik> GelenHazirlaAsync(string xml, CancellationToken cancellationToken)
    {
        var belge = UblTrFaturaOkuyucu.Oku(xml);
        var hatalar = new List<string>();
        var uyarilar = new List<string>();

        var tur = belge.Tip == "IADE" ? FaturaTuru.SatisIade : FaturaTuru.Alis;
        if (belge.Tip != "IADE" && !AlisOlarakAlinanTipler.Contains(belge.Tip))
            hatalar.Add($"'{belge.Tip}' tipindeki fatura otomatik aktarılamıyor; lütfen elle girin.");
        // Kısmi tevkifat (6xx) alışta tevkifat olarak işlenir; tam tevkifat ya da birden çok kod elle girilmelidir.
        var tevkifatKodlari = belge.TevkifatKodlari ?? [];
        if (belge.TevkifatTutari > 0)
        {
            if (tur != FaturaTuru.Alis)
                hatalar.Add("Tevkifatlı iade faturası otomatik aktarılamıyor; lütfen elle girin.");
            else if (tevkifatKodlari.Count != 1 || EtkiAlani.Yardimcilar.TevkifatKodlari.Bul(tevkifatKodlari[0]) == null)
                hatalar.Add($"Faturadaki tevkifat kodu ({string.Join(", ", tevkifatKodlari)}) otomatik aktarılamıyor; lütfen elle girin.");
            else if (belge.TevkifatYuzdesi % 10 != 0 || belge.TevkifatYuzdesi is <= 0 or > 100)
                hatalar.Add($"Faturadaki tevkifat oranı (%{belge.TevkifatYuzdesi}) onda bir katı değil; lütfen elle girin.");
        }
        if (belge.Tip is "IHRACKAYITLI" or "OZELMATRAH")
            uyarilar.Add($"Fatura tipi {belge.Tip}: KDV tutarlarını kontrol edin.");
        if (!string.Equals(belge.ParaBirimi, "TRY", StringComparison.OrdinalIgnoreCase))
            hatalar.Add($"Dövizli ({belge.ParaBirimi}) fatura henüz otomatik aktarılamıyor; lütfen elle girin.");

        // Fatura bu firmaya kesilmiş olmalı.
        var ayar = await _baglam.SistemAyarlari.AsNoTracking().FirstOrDefaultAsync(a => a.Id == 1, cancellationToken);
        var sirketId = _baglam.SirketIdCoz(Guid.Empty);
        var sirket = await _baglam.Sirketler.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sirketId, cancellationToken);
        var firma = BelgeYazdirmaServisi.FirmaBilgisiOlustur(ayar, sirket, sirketId == MuhasebeBaglami.VarsayilanSirketId);
        var firmaVkn = Rakamlar(firma.VergiNo);
        if (firmaVkn.Length > 0 && Rakamlar(belge.Alici.VknTckn) != firmaVkn)
            hatalar.Add($"Bu fatura firmanıza kesilmemiş: alıcı {belge.Alici.Unvan} ({belge.Alici.VknTckn}), firmanızın vergi no'su {firmaVkn}.");
        else if (firmaVkn.Length == 0)
            uyarilar.Add("Firmanızın vergi numarası Ayarlar'da girilmemiş; faturanın size kesildiği doğrulanamadı.");

        var mevcut = await _baglam.Faturalar.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.Ettn == belge.Ettn).Select(f => f.FaturaNo).FirstOrDefaultAsync(cancellationToken);
        if (mevcut != null)
            hatalar.Add($"Bu e-Fatura daha önce aktarılmış ({mevcut}).");
        else if (await _baglam.Faturalar.IgnoreQueryFilters().AnyAsync(f => f.FaturaNo == belge.FaturaNo, cancellationToken))
            hatalar.Add($"'{belge.FaturaNo}' numaralı başka bir fatura kayıtlı; aynı numara ikinci kez kullanılamaz.");

        var vkn = Rakamlar(belge.Satici.VknTckn);
        var cari = await _baglam.CariKartlar.FirstOrDefaultAsync(c => c.VergiNo == vkn || c.TcNo == vkn, cancellationToken);

        var urunler = await _baglam.Urunler.AsNoTracking().Where(u => u.Aktif).ToListAsync(cancellationToken);
        var satirlar = belge.Satirlar.Select(s =>
        {
            var urun = (s.SaticiUrunKodu is { Length: > 0 } kod
                    ? urunler.FirstOrDefault(u => string.Equals(u.Kod, kod, StringComparison.OrdinalIgnoreCase) || u.Barkod == kod)
                    : null)
                ?? urunler.FirstOrDefault(u => string.Equals(u.Ad.Trim(), s.UrunAdi, StringComparison.CurrentCultureIgnoreCase));
            var brut = s.Miktar * s.BirimFiyat;
            var iskonto = brut > 0 && s.Indirim > 0 ? decimal.Round(s.Indirim / brut * 100m, 4) : 0m;
            return (Satir: s, Urun: urun, IskontoOrani: iskonto);
        }).ToList();
        if (belge.Satirlar.Any(s => s.Miktar <= 0))
            hatalar.Add("Faturada miktarı sıfır olan satır var.");

        // Program toplamı kendi kurallarıyla (satır bazında kuruş yuvarlaması) hesaplar; fark varsa kullanıcı görsün.
        var hesap = EtkiAlani.Yardimcilar.FaturaHesaplayici.Hesapla(
            satirlar.Select(x => new EtkiAlani.Yardimcilar.FaturaHesaplayici.Girdi(x.Satir.Miktar, x.Satir.BirimFiyat, x.IskontoOrani, x.Satir.KdvOrani)).ToList(), 0);
        var hesapTevkifat = belge.TevkifatTutari > 0 ? EtkiAlani.Yardimcilar.TevkifatKodlari.Tutar(hesap.KdvToplam, belge.TevkifatYuzdesi / 100m) : 0;
        if (Math.Abs(hesap.GenelToplam - hesapTevkifat - belge.OdenecekTutar) > 0.05m)
            uyarilar.Add($"Programın hesapladığı ödenecek tutar ({hesap.GenelToplam - hesapTevkifat:N2} ₺) faturadakinden ({belge.OdenecekTutar:N2} ₺) farklı; taslağı onaylamadan önce kontrol edin.");
        if (belge.TevkifatTutari > 0)
            uyarilar.Add($"KDV tevkifatlı fatura ({tevkifatKodlari.FirstOrDefault()}, {belge.TevkifatYuzdesi / 10:0}/10): tevkif edilen {belge.TevkifatTutari:N2} ₺ KDV'yi siz beyan edip ödersiniz.");
        if (cari == null)
            uyarilar.Add($"'{belge.Satici.Unvan}' için yeni cari kart açılacak.");
        var yeniUrun = satirlar.Count(x => x.Urun == null);
        if (yeniUrun > 0)
            uyarilar.Add($"{yeniUrun} satır için yeni ürün kartı açılacak.");

        return new GelenHazirlik(belge, tur, cari, satirlar, uyarilar, hatalar);
    }

    public async Task<GelenFaturaOnizleme> GelenFaturaOnizleAsync(string xml, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.FaturaGoruntule, "Gelen e-Fatura Önizleme");
        var h = await GelenHazirlaAsync(xml, cancellationToken);
        var b = h.Belge;
        var turAciklama = (b.Profil == "EARSIVFATURA" ? "e-Arşiv" : "e-Fatura") + " • " +
                          (h.Tur == FaturaTuru.SatisIade ? "müşteri iadesi → Satış İade faturası" : "alış → Alış faturası");
        return new GelenFaturaOnizleme(b.FaturaNo, b.Ettn, b.Tarih, turAciklama, b.Satici.Unvan, b.Satici.VknTckn,
            h.Cari is { } c ? $"{c.Kod} - {c.Unvan}" : null, b.OdenecekTutar,
            h.Satirlar.Select(x => new GelenFaturaSatirOnizleme(x.Satir.SiraNo, x.Satir.UrunAdi, x.Satir.SaticiUrunKodu, x.Satir.Miktar,
                UblTrFaturaOkuyucu.BirimAdi(x.Satir.BirimKodu), x.Satir.BirimFiyat, x.IskontoOrani, x.Satir.KdvOrani, x.Satir.SatirTutari,
                x.Urun is { } u ? $"{u.Kod} - {u.Ad}" : "Yeni ürün açılacak")).ToList(),
            h.Uyarilar, h.Hatalar);
    }

    public async Task<Fatura> GelenFaturaAktarAsync(string xml, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.FaturaOlustur, "Gelen e-Fatura Aktarma");
        var h = await GelenHazirlaAsync(xml, cancellationToken);
        if (h.Hatalar.Count > 0)
            throw new InvalidOperationException("e-Fatura aktarılamadı:\n- " + string.Join("\n- ", h.Hatalar));
        var b = h.Belge;

        var cari = h.Cari;
        if (cari == null)
        {
            var s = b.Satici;
            var vkn = Rakamlar(s.VknTckn);
            var gercekKisi = vkn.Length == 11;
            var parcalar = s.Unvan.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            cari = await new CariServisi(_baglam, _kullaniciServisi).OtomatikKodlaEkleAsync(new CariKart
            {
                Tur = h.Tur == FaturaTuru.SatisIade ? CariTuru.Musteri : CariTuru.Tedarikci,
                CariTipiId = gercekKisi ? CariTipiTanimi.BireyselId : CariTipiTanimi.KurumsalId,
                Unvan = s.Unvan,
                Ad = gercekKisi && parcalar.Length > 1 ? string.Join(' ', parcalar[..^1]) : null,
                Soyad = gercekKisi && parcalar.Length > 1 ? parcalar[^1] : null,
                VergiNo = gercekKisi ? null : vkn, TcNo = gercekKisi ? vkn : null, VergiDairesi = s.VergiDairesi,
                Adres = s.Adres, Il = s.Il, Ilce = s.Ilce, PostaKodu = s.PostaKodu, Telefon = s.Telefon, Eposta = s.Eposta,
                EFaturaMukellefi = b.Profil != "EARSIVFATURA", Aktif = true,
                Aciklama = $"{b.FaturaNo} numaralı e-Faturadan otomatik açıldı."
            });
        }

        var urunServisi = new UrunServisi(_baglam, _kullaniciServisi);
        var kalemler = new List<FaturaKalemi>();
        foreach (var (satir, bulunan, iskonto) in h.Satirlar)
        {
            var urun = bulunan;
            if (urun == null)
            {
                urun = await urunServisi.GuvenliEkleAsync(new EtkiAlani.Modeller.Stok.Urun
                {
                    Ad = satir.UrunAdi.Length > 200 ? satir.UrunAdi[..200] : satir.UrunAdi,
                    Birim = UblTrFaturaOkuyucu.BirimAdi(satir.BirimKodu),
                    KdvOrani = satir.KdvOrani, AlisFiyati = satir.BirimFiyat, Aktif = true,
                    Aciklama = $"{b.Satici.Unvan} e-Faturasından ({b.FaturaNo}) otomatik açıldı." + (satir.SaticiUrunKodu is { } k ? $" Tedarikçi kodu: {k}" : "")
                }, otomatikKod: true, otomatikBarkod: false);
            }
            kalemler.Add(new FaturaKalemi
            {
                UrunId = urun.Id, UrunKodu = urun.Kod, UrunAd = urun.Ad, Birim = urun.Birim,
                Miktar = satir.Miktar, BirimFiyat = satir.BirimFiyat, IskontoOrani = iskonto, KdvOrani = satir.KdvOrani
            });
        }

        var fatura = new Fatura
        {
            Tur = h.Tur, FaturaNo = b.FaturaNo, Tarih = DateTime.SpecifyKind(b.Tarih, DateTimeKind.Utc), CariKartId = cari.Id,
            Ettn = b.Ettn, EBelgeTuru = b.Profil == "EARSIVFATURA" ? EBelgeTuru.EArsiv : EBelgeTuru.EFatura,
            IadeEdilenFaturaNo = b.IadeEdilenFaturaNo,
            IadeEdilenFaturaTarihi = b.IadeEdilenFaturaTarihi is DateTime t ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : null,
            Aciklama = $"Gelen {(b.Profil == "EARSIVFATURA" ? "e-Arşiv" : "e-Fatura")}: {b.FaturaNo} (ETTN {b.Ettn.ToString().ToUpperInvariant()})",
            TevkifatKodu = b.TevkifatTutari > 0 ? b.TevkifatKodlari![0] : null,
            TevkifatOrani = b.TevkifatTutari > 0 ? b.TevkifatYuzdesi / 100m : 0,
            Kalemler = kalemler
        };
        return await new FaturaServisi(_baglam, _kullaniciServisi).EkleAsync(fatura);
    }


    private static void IadeBilgisiUygula(Fatura fatura, EBelgeHazirlamaIstegi istek)
    {
        if (fatura.Tur != FaturaTuru.AlisIade)
            return;
        if (!string.IsNullOrWhiteSpace(istek.IadeEdilenFaturaNo))
            fatura.IadeEdilenFaturaNo = istek.IadeEdilenFaturaNo.Trim();
        if (istek.IadeEdilenFaturaTarihi.HasValue)
            fatura.IadeEdilenFaturaTarihi = DateTime.SpecifyKind(istek.IadeEdilenFaturaTarihi.Value.Date, DateTimeKind.Utc);
    }

    private static List<string> DurumHatalari(Fatura fatura)
    {
        var hatalar = new List<string>();
        if (fatura.EBelgeDurumu is EBelgeDurumu.Gonderildi or EBelgeDurumu.KabulEdildi or EBelgeDurumu.Reddedildi or EBelgeDurumu.IptalEdildi)
            hatalar.Add("Bu faturanın e-belgesi zaten gönderildi; yeniden hazırlanamaz.");
        return hatalar;
    }

    private async Task<Fatura> FaturaGetirAsync(int faturaId, bool izle, CancellationToken cancellationToken)
    {
        var sorgu = _baglam.Faturalar.Include(f => f.Kalemler).Include(f => f.CariKart).AsQueryable();
        if (!izle)
            sorgu = sorgu.AsNoTracking();
        return await sorgu.FirstOrDefaultAsync(f => f.Id == faturaId, cancellationToken)
               ?? throw new KeyNotFoundException("Fatura bulunamadı.");
    }

    private async Task<UblFaturaGirdisi> GirdiOlusturAsync(Fatura fatura, EFaturaSenaryosu? senaryo, CancellationToken cancellationToken)
    {
        var ayar = await _baglam.SistemAyarlari.AsNoTracking().FirstOrDefaultAsync(a => a.Id == 1, cancellationToken);
        var sirketId = fatura.SirketId ?? MuhasebeBaglami.VarsayilanSirketId;
        var sirket = await _baglam.Sirketler.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sirketId, cancellationToken);
        var firma = BelgeYazdirmaServisi.FirmaBilgisiOlustur(ayar, sirket, sirketId == MuhasebeBaglami.VarsayilanSirketId);

        var cari = fatura.CariKart ?? new CariKart();
        var tur = cari.EFaturaMukellefi ? EBelgeTuru.EFatura : EBelgeTuru.EArsiv;
        var secilenSenaryo = senaryo
            ?? (fatura.EBelgeDurumu != EBelgeDurumu.Yok ? fatura.EFaturaSenaryosu : ayar?.EFaturaVarsayilanSenaryo ?? EFaturaSenaryosu.Temel);
        return new UblFaturaGirdisi(fatura, fatura.Ettn ?? Guid.NewGuid(), tur, secilenSenaryo, Satici(firma), Alici(cari, tur));
    }

    private static UblTaraf Satici(FirmaBilgisi f) => new(
        Rakamlar(f.VergiNo), f.FirmaUnvani, VergiDairesi: f.VergiDairesi, Adres: f.Adres, Ilce: f.Ilce, Il: f.Il, PostaKodu: f.PostaKodu,
        Telefon: f.Telefon, Eposta: f.Eposta, MersisNo: f.MersisNo, WebSitesi: f.WebSitesi);

    // Alıcı kimliği: 10 haneli vergi no, yoksa 11 haneli T.C. kimlik no; e-Arşivde ikisi de yoksa nihai tüketici.
    private static UblTaraf Alici(CariKart c, EBelgeTuru tur)
    {
        var vkn = Rakamlar(c.VergiNo);
        var tckn = Rakamlar(c.TcNo);
        var kimlik = vkn.Length is 10 or 11 ? vkn : tckn.Length == 11 ? tckn : string.Empty;
        if (kimlik.Length == 0 && tur == EBelgeTuru.EArsiv)
            kimlik = UblTrFaturaUretici.NihaiTuketiciTckn;
        return new UblTaraf(kimlik, c.Unvan, c.Ad, c.Soyad, c.VergiDairesi, c.Adres, c.Ilce, c.Il, c.PostaKodu,
            c.CepTelefonu ?? c.Telefon, c.Eposta);
    }

    // ---- e-İrsaliye ----

    private async Task<(EtkiAlani.Modeller.Irsaliye.Irsaliye Irsaliye, UblIrsaliyeGirdisi Girdi)> IrsaliyeGirdisiAsync(int irsaliyeId, bool izle, CancellationToken cancellationToken)
    {
        var sorgu = _baglam.Irsaliyeler.Include(i => i.Kalemler).Include(i => i.CariKart).AsQueryable();
        if (!izle) sorgu = sorgu.AsNoTracking();
        var irsaliye = await sorgu.FirstOrDefaultAsync(i => i.Id == irsaliyeId, cancellationToken)
                       ?? throw new KeyNotFoundException("İrsaliye bulunamadı.");
        var ayar = await _baglam.SistemAyarlari.AsNoTracking().FirstOrDefaultAsync(a => a.Id == 1, cancellationToken);
        var sirketId = irsaliye.SirketId ?? MuhasebeBaglami.VarsayilanSirketId;
        var sirket = await _baglam.Sirketler.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sirketId, cancellationToken);
        var firma = BelgeYazdirmaServisi.FirmaBilgisiOlustur(ayar, sirket, sirketId == MuhasebeBaglami.VarsayilanSirketId);
        // Numara henüz verilmediyse ön kontrol örnek numarayla yapılır (biçim kuralı numara verilince sağlanır).
        var girdi = new UblIrsaliyeGirdisi(irsaliye, irsaliye.EBelgeNo ?? $"IRS{irsaliye.Tarih.Year}000000001", irsaliye.Ettn ?? Guid.NewGuid(),
            Satici(firma), Alici(irsaliye.CariKart ?? new CariKart(), EBelgeTuru.EFatura));
        return (irsaliye, girdi);
    }

    private static List<string> IrsaliyeDurumHatalari(EtkiAlani.Modeller.Irsaliye.Irsaliye i) => i.EBelgeDurumu switch
    {
        EBelgeDurumu.Gonderildi or EBelgeDurumu.KabulEdildi => ["Bu irsaliye e-İrsaliye olarak gönderilmiş; yeniden hazırlanamaz."],
        EBelgeDurumu.IptalEdildi => ["Bu e-İrsaliye iptal edilmiş."],
        _ => []
    };

    public async Task<IReadOnlyList<string>> IrsaliyeOnKontrolAsync(int irsaliyeId, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.IrsaliyeGoruntule, "e-İrsaliye Ön Kontrol");
        var (irsaliye, girdi) = await IrsaliyeGirdisiAsync(irsaliyeId, izle: false, cancellationToken);
        var hatalar = IrsaliyeDurumHatalari(irsaliye);
        hatalar.AddRange(UblTrIrsaliyeUretici.Dogrula(girdi));
        return hatalar;
    }

    public async Task IrsaliyeTasimaBilgisiKaydetAsync(int irsaliyeId, string aracPlaka, string soforAdi, string soforTckn, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.IrsaliyeOnayla, "e-İrsaliye Taşıma Bilgisi");
        var irsaliye = await _baglam.Irsaliyeler.FirstOrDefaultAsync(i => i.Id == irsaliyeId, cancellationToken)
                       ?? throw new KeyNotFoundException("İrsaliye bulunamadı.");
        if (IrsaliyeDurumHatalari(irsaliye) is { Count: > 0 } h)
            throw new InvalidOperationException(h[0]);
        string Kisa(string s, int n) => (s ?? string.Empty).Trim() is var t && t.Length > n ? t[..n] : t;
        irsaliye.AracPlaka = Kisa(UblTrIrsaliyeUretici.Plaka(aracPlaka ?? string.Empty), 20);
        irsaliye.SoforAdi = Kisa(string.Join(' ', (soforAdi ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)), 100);
        irsaliye.SoforTckn = Kisa(Rakamlar(soforTckn), 11);
        await _baglam.SaveChangesAsync(cancellationToken);
    }

    public async Task<EBelgeGonderimSonucu> IrsaliyeHazirlaAsync(int irsaliyeId, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.IrsaliyeOnayla, "e-İrsaliye Hazırlama");
        await using var tx = await _baglam.Database.BeginTransactionAsync(cancellationToken);
        var (irsaliye, girdi) = await IrsaliyeGirdisiAsync(irsaliyeId, izle: true, cancellationToken);
        var hatalar = IrsaliyeDurumHatalari(irsaliye);
        hatalar.AddRange(UblTrIrsaliyeUretici.Dogrula(girdi));
        if (hatalar.Count > 0)
            throw new InvalidOperationException("e-İrsaliye hazırlanamadı:\n- " + string.Join("\n- ", hatalar));

        // Numara ve ETTN bir kez verilir: hata sonrası yeniden hazırlamada aynı belge numarası/ETTN kullanılır.
        if (irsaliye.EBelgeNo == null)
        {
            var yil = irsaliye.Tarih.Year;
            var seriKodu = $"IRS{yil}";
            await _baglam.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1111836500, hashtext({_baglam.SirketIdCoz(irsaliye.SirketId).ToString() + seriKodu}))", cancellationToken);
            var sirketId = _baglam.SirketIdCoz(irsaliye.SirketId);
            var seri = await _baglam.BelgeSerileri.FirstOrDefaultAsync(s => s.SirketId == sirketId && s.BelgeTipi == "EIrsaliye" && s.Seri == seriKodu, cancellationToken);
            if (seri == null)
            {
                seri = new EtkiAlani.Modeller.Kurumsal.BelgeSerisi { SirketId = sirketId, BelgeTipi = "EIrsaliye", Seri = seriKodu, Prefix = "IRS" };
                _baglam.BelgeSerileri.Add(seri);
            }
            seri.SonNumara++;
            irsaliye.EBelgeNo = $"IRS{yil}{seri.SonNumara:D9}";
        }
        irsaliye.Ettn ??= girdi.Ettn;
        var xml = UblTrIrsaliyeUretici.UretMetin(girdi with { BelgeNo = irsaliye.EBelgeNo, Ettn = irsaliye.Ettn.Value });

        EBelgeGonderimSonucu sonuc;
        try
        {
            sonuc = await _entegrator.GonderAsync(new EBelgePaketi(irsaliye.EBelgeNo, irsaliye.Ettn.Value, EBelgeTuru.EIrsaliye, girdi.Alici.VknTckn,
                irsaliye.CariKart?.EFaturaEtiketi, xml), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sonuc = new EBelgeGonderimSonucu(false, EBelgeDurumu.Hata, ex.Message);
        }
        irsaliye.EBelgeDurumu = sonuc.Durum;
        var mesaj = (sonuc.Mesaj + (sonuc.DosyaYolu is { } yol ? $" Dosya: {yol}" : "")).Trim();
        irsaliye.EBelgeMesaji = mesaj.Length > 1000 ? mesaj[..1000] : mesaj;
        await _baglam.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return sonuc;
    }

    public async Task IrsaliyeDurumIsaretleAsync(int irsaliyeId, EBelgeDurumu durum, string? aciklama = null, CancellationToken cancellationToken = default)
    {
        Yetki(y => y.IrsaliyeOnayla, "e-İrsaliye Durum İşaretleme");
        var irsaliye = await _baglam.Irsaliyeler.FirstOrDefaultAsync(i => i.Id == irsaliyeId, cancellationToken)
                       ?? throw new KeyNotFoundException("İrsaliye bulunamadı.");
        if (irsaliye.EBelgeDurumu == EBelgeDurumu.Yok || irsaliye.Ettn == null)
            throw new InvalidOperationException("Bu irsaliye için henüz e-İrsaliye hazırlanmadı.");
        if (durum is EBelgeDurumu.Yok or EBelgeDurumu.Hazirlandi)
            throw new InvalidOperationException("Bu durum elle işaretlenemez.");
        if (irsaliye.EBelgeDurumu == EBelgeDurumu.IptalEdildi)
            throw new InvalidOperationException("İptal edilmiş e-İrsaliyenin durumu değiştirilemez.");
        irsaliye.EBelgeDurumu = durum;
        irsaliye.EBelgeMesaji = string.IsNullOrWhiteSpace(aciklama) ? $"Kullanıcı tarafından '{durum}' olarak işaretlendi." : aciklama.Trim();
        await _baglam.SaveChangesAsync(cancellationToken);
    }

    private static string Rakamlar(string? s) => new((s ?? string.Empty).Where(char.IsDigit).ToArray());

    private void Yetki(Func<EtkiAlani.Modeller.Kullanici.KullaniciYetkileri, bool> sorgu, string islem) =>
        (_kullaniciServisi ?? throw new YetkisizErisimException("Oturum doğrulama")).YetkiKontrolEt(sorgu, islem);
}
